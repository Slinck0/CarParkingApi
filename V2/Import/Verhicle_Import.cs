using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using ParkingImporter.Data;
using ParkingImporter.Models;

namespace ParkingImporter.Import;

public static class VehiclesImporter
{
    // ===== Helpers =====

    private static string? NormalizePlate(string? plate)
    {
        if (string.IsNullOrWhiteSpace(plate)) return null;
        var p = new string(plate.Where(char.IsLetterOrDigit).ToArray()); // strip spaties/tekens
        return p.ToUpperInvariant();
    }

    private static List<T> DeserializeFromDictOrArray<T>(string json, JsonSerializerOptions opts)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // 1) Plain array at root
        if (root.ValueKind == JsonValueKind.Array)
            return JsonSerializer.Deserialize<List<T>>(json, opts) ?? new();

        // 2) Root object
        if (root.ValueKind == JsonValueKind.Object)
        {
            // 2a) wrapper: { "vehicles": [...] }
            if (root.TryGetProperty("vehicles", out var vehicles))
            {
                if (vehicles.ValueKind == JsonValueKind.Array)
                    return JsonSerializer.Deserialize<List<T>>(vehicles.GetRawText(), opts) ?? new();

                // 2b) wrapper: { "vehicles": { "<id>": {..}, ... } }
                if (vehicles.ValueKind == JsonValueKind.Object)
                {
                    var list = new List<T>();
                    foreach (var p in vehicles.EnumerateObject())
                    {
                        var item = p.Value.Deserialize<T>(opts);
                        if (item is not null) list.Add(item);
                    }
                    return list;
                }
            }

            // 2c) Nested “per user” dictionary:
            // { "user1": { "<plate>": {..vehicle..}, ... }, "user2": { ... } }
            {
                var list = new List<T>();
                foreach (var userBucket in root.EnumerateObject())
                {
                    if (userBucket.Value.ValueKind != JsonValueKind.Object)
                    {
                        // fall back to direct attempt
                        var maybe = userBucket.Value.Deserialize<T>(opts);
                        if (maybe is not null) list.Add(maybe);
                        continue;
                    }

                    // Is this bucket itself a dictionary of vehicles?
                    bool looksLikeDictOfVehicles = false;
                    foreach (var inner in userBucket.Value.EnumerateObject())
                    {
                        if (inner.Value.ValueKind == JsonValueKind.Object &&
                            (inner.Value.TryGetProperty("license_plate", out _) ||
                             inner.Value.TryGetProperty("id", out _)))
                        {
                            looksLikeDictOfVehicles = true;
                            break;
                        }
                    }

                    if (looksLikeDictOfVehicles)
                    {
                        foreach (var inner in userBucket.Value.EnumerateObject())
                        {
                            var v = inner.Value.Deserialize<T>(opts);
                            if (v is not null) list.Add(v);
                        }
                    }
                    else
                    {
                        // maybe it’s already one vehicle object
                        var v = userBucket.Value.Deserialize<T>(opts);
                        if (v is not null) list.Add(v);
                    }
                }
                return list;
            }
        }

        return new();
    }

    public static async Task ImportAsync(AppDbContext db, string jsonPath)
    {
        // ---- Lees JSON ----
        if (!File.Exists(jsonPath))
        {
            Console.WriteLine($"[Vehicles] JSON-bestand niet gevonden: {jsonPath}");
            return;
        }

        var json = await File.ReadAllTextAsync(jsonPath);

        var opts = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };

        // ---- Deserialize flexibel (dict, wrapper of array) ----
        List<VehicleRaw> rawList;
        try
        {
            rawList = DeserializeFromDictOrArray<VehicleRaw>(json, opts);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Vehicles] Fout bij deserializen: {ex.Message}");
            // kleine hint: eerste 200 chars laten zien om root te herkennen
            Console.WriteLine($"[Vehicles] JSON start: {json.Substring(0, Math.Min(200, json.Length))}");
            throw;
        }

        Console.WriteLine($"[Vehicles] Ruwe records ingelezen: {rawList.Count}");

        if (rawList.Count == 0)
        {
            // extra diagnostiek: toon root type
            using var doc = JsonDocument.Parse(json);
            Console.WriteLine($"[Vehicles] Root type: {doc.RootElement.ValueKind}");
            // Schrijf een sample weg zodat je de structuur kan zien
            await File.WriteAllTextAsync("vehicles-sample.json", json);
            Console.WriteLine("[Vehicles] JSON weggeschreven naar vehicles-sample.json (controleer de structuur).");
        }

        // ===== Mapping & validatie =====
        var valid = new List<Vehicle>();
        var bad = new List<VehicleRaw>();

        // Voor deduplicatie binnen deze import
        var seenIds = new HashSet<int>();
        var seenPlates = new HashSet<string>();

        foreach (var r in rawList)
        {
            // --- Id ---
            if (!int.TryParse(r.id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
            { bad.Add(r); continue; }
            if (!seenIds.Add(id))
            { bad.Add(r); continue; }

            // --- UserId (optioneel) ---
            int? userId = null;
            if (!string.IsNullOrWhiteSpace(r.user_id) &&
                int.TryParse(r.user_id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var uid))
            {
                userId = uid;
            }

            // --- Kenteken ---
            var plate = NormalizePlate(r.license_plate);
            // Require a plate
            if (string.IsNullOrWhiteSpace(plate)) { bad.Add(r); continue; }
            // basic sanity check
            if (plate.Length > 16) { bad.Add(r); continue; }
            // Als kenteken verplicht is in jouw schema, gooi zonder kenteken in bad:
            // if (plate is null) { bad.Add(r); continue; }

            // Binnen import niet twee keer hetzelfde kenteken (optioneel)
            if (plate is not null && !seenPlates.Add(plate))
            { bad.Add(r); continue; }

            // --- Jaar ---
            int? year = r.year;   // DTO is now int?

            if (year.HasValue)
            {
                var thisYear = DateTime.UtcNow.Year + 1;
                if (year < 1950 || year > thisYear) { bad.Add(r); continue; }
            }

            // --- Merk/Model/… (pas aan naar jouw velden) ---
            var make = r.make?.Trim();
            var model = r.model?.Trim();
            var color = r.color?.Trim();

            if (userId.HasValue)
            {
                var exists = await db.Users.AsNoTracking().AnyAsync(u => u.Id == userId.Value);
                if (!exists) userId = null;
            }

            var v = new Vehicle
            {
                Id = id,
                LicensePlate = plate,
                Make = make,
                Model = model,
                Color = color,
            };
            if (userId.HasValue) v.UserId = userId.Value;
            if (year.HasValue) v.Year = year.Value;

            valid.Add(v);

        }

        // ===== Conflicten met DB oplossen =====
        // Als je LicensePlate UNIQUE is, check conflicts vooraf:
        var plates = valid.Select(v => v.LicensePlate)
                          .Where(p => p != null)
                          .Distinct()!
                          .ToList();

        var existingByPlate = await db.Vehicles
            .Where(v => plates.Contains(v.LicensePlate))
            .ToDictionaryAsync(v => v.LicensePlate!);

        var final = new List<Vehicle>();
        foreach (var v in valid)
        {
            if (v.LicensePlate != null &&
            existingByPlate.TryGetValue(v.LicensePlate, out var exPlate))
            {
                // Treat plate as key: update the existing vehicle
                v.Id = exPlate.Id;
            }
            final.Add(v);
        }

        // ===== Upsert op Id =====
        var ids = final.Select(v => v.Id).ToList();
        var existingById = await db.Vehicles
                                   .Where(v => ids.Contains(v.Id))
                                   .ToDictionaryAsync(v => v.Id);

        foreach (var v in final)
        {
            if (existingById.TryGetValue(v.Id, out var ex))
            {
                db.Entry(ex).CurrentValues.SetValues(v);
            }
            else
            {
                await db.Vehicles.AddAsync(v);
            }
        }

        // ===== Persist =====
        var saved = await db.SaveChangesAsync();
        Console.WriteLine($"Vehicles import: ok={final.Count}, overgeslagen={bad.Count}, savedChanges={saved}");

        if (bad.Count > 0)
        {
            await File.WriteAllTextAsync("bad-vehicles.json",
                JsonSerializer.Serialize(bad, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"→ Probleemrecords geschreven naar bad-vehicles.json");
        }
    }
}
