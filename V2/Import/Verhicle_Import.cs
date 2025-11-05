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

        if (root.ValueKind == JsonValueKind.Array)
        {
            return JsonSerializer.Deserialize<List<T>>(json, opts) ?? new();
        }

        if (root.ValueKind == JsonValueKind.Object)
        {
            // 1) wrapper "vehicles" die zelf een dictionary bevat
            if (root.TryGetProperty("vehicles", out var vehiclesProp) && vehiclesProp.ValueKind == JsonValueKind.Object)
            {
                var list = new List<T>();
                foreach (var prop in vehiclesProp.EnumerateObject())
                {
                    var item = prop.Value.Deserialize<T>(opts);
                    if (item is not null) list.Add(item);
                }
                return list;
            }

            // 2) wrapper "vehicles" als array
            if (root.TryGetProperty("vehicles", out var vehiclesArr) && vehiclesArr.ValueKind == JsonValueKind.Array)
            {
                return JsonSerializer.Deserialize<List<T>>(vehiclesArr.GetRawText(), opts) ?? new();
            }

            // 3) root zelf als dictionary
            var list2 = new List<T>();
            foreach (var prop in root.EnumerateObject())
            {
                var item = prop.Value.Deserialize<T>(opts);
                if (item is not null) list2.Add(item);
            }
            return list2;
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
        var bad   = new List<VehicleRaw>();

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
            // Als kenteken verplicht is in jouw schema, gooi zonder kenteken in bad:
            // if (plate is null) { bad.Add(r); continue; }

            // Binnen import niet twee keer hetzelfde kenteken (optioneel)
            if (plate is not null && !seenPlates.Add(plate))
            { bad.Add(r); continue; }

            // --- Jaar ---
            int? year = null;
            if (!string.IsNullOrWhiteSpace(r.year) &&
                int.TryParse(r.year, NumberStyles.Integer, CultureInfo.InvariantCulture, out var y))
            {
                year = y;
            }

            // --- Active ---


            // --- Merk/Model/… (pas aan naar jouw velden) ---
            var make  = r.make?.Trim();
            var model = r.model?.Trim();
            var color = r.color?.Trim();

            valid.Add(new Vehicle
            {
                Id           = id,
                UserId       = userId ?? 0, // of als je property nullable is 
                LicensePlate = plate,        
                Make         = make,
                Model        = model,
                Color        = color,
                Year         = year ?? 0,     // of null als je property nullable is
            });
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
                existingByPlate.TryGetValue(v.LicensePlate, out var exPlate) &&
                exPlate.Id != v.Id)
            {
                // Zelfde kenteken bestaat bij andere Id → sla over naar bad
                // (of kies merge-strategie)
                var raw = rawList.FirstOrDefault(r =>
                    int.TryParse(r.id, out var parsed) && parsed == v.Id);
                if (raw != null) bad.Add(raw!);
                continue;
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
