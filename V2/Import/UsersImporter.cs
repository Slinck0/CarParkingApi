using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using V2.Data;
using V2.Models;

namespace V2.Import;

public static class UsersImporter
{
    private static string? NormalizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        return email.Trim().ToLowerInvariant();
    }

    public static async Task ImportAsync(AppDbContext db, string jsonPath)
    {
        var json = await File.ReadAllTextAsync(jsonPath);

        var opts = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            // Optioneel: alle numerieke properties tolerant voor strings:
            // NumberHandling = JsonNumberHandling.AllowReadingFromString
        };

        var rawList = JsonSerializer.Deserialize<List<UserRaw>>(json, opts) ?? new();

        // We bewaren zowel de gemapte User als de oorspronkelijke raw voor logging in bad-users.json
        var valid = new List<(UserModel user, UserRaw raw)>();
        var bad   = new List<UserRaw>();

        // 1) Voor de-duplicatie binnen de import
        var seenEmails = new HashSet<string>(); // normalized emails
        var seenById   = new HashSet<int>();

        foreach (var r in rawList)
        {
            if (!int.TryParse(r.id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
            { bad.Add(r); continue; }

            if (!seenById.Add(id))
            { bad.Add(r); continue; }

            // created_at → DateOnly (probeer meerdere formaten)
            DateOnly created = default;
            if (!string.IsNullOrWhiteSpace(r.created_at))
            {
                if (!DateOnly.TryParse(r.created_at, CultureInfo.InvariantCulture, DateTimeStyles.None, out created) &&
                    !DateOnly.TryParseExact(r.created_at, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out created))
                { bad.Add(r); continue; }
            }

            int birthYear = r.birth_year ?? 0;
            bool active   = r.active ?? false;

            var normalizedEmail = NormalizeEmail(r.email);

            // Binnen dezelfde import: dubbele e-mail → sla de latere over
            if (normalizedEmail is not null)
            {
                if (!seenEmails.Add(normalizedEmail))
                {
                    bad.Add(r);
                    continue;
                }
            }

            var user = new UserModel
            {
                Id        = id,
                Username  = r.username?.Trim(),
                Password  = r.password, // indien nodig: hashing/validatie elders
                Name      = r.name?.Trim(),
                Email     = normalizedEmail,
                Phone     = r.phone?.Trim(),
                Role      = string.IsNullOrWhiteSpace(r.role) ? "USER" : r.role!.Trim(),
                CreatedAt = created,
                BirthYear = birthYear,
                Active    = active
            };

            valid.Add((user, r));
        }

        // 2) Conflicten met bestaande DB (zelfde email, andere Id) → naar bad
        var emails = valid.Select(v => v.user.Email)
                          .Where(e => e != null)
                          .Distinct()!
                          .ToList();

        var existingByEmail = await db.Users
            .Where(u => emails.Contains(u.Email))
            .ToDictionaryAsync(u => u.Email!); // alleen niet-null keys zitten in 'emails'

        var final = new List<UserModel>();
        foreach (var (user, raw) in valid)
        {
            if (user.Email != null &&
                existingByEmail.TryGetValue(user.Email, out var exByEmail) &&
                exByEmail.Id != user.Id)
            {
                // e-mail al in DB, maar aan andere gebruiker gekoppeld → conflict
                bad.Add(raw);
                continue;
            }

            final.Add(user);
        }

        // 3) Upsert op basis van Id (bestond al in jouw code)
        var ids = final.Select(u => u.Id).ToList();

        var existingById = await db.Users
                                   .Where(u => ids.Contains(u.Id))
                                   .ToDictionaryAsync(u => u.Id);

        foreach (var u in final)
        {
            if (existingById.TryGetValue(u.Id, out var ex))
            {
                // Als je e-mail uniek is en je update hem, is dit oké zolang geen conflict (boven gecheckt)
                db.Entry(ex).CurrentValues.SetValues(u);
            }
            else
            {
                await db.Users.AddAsync(u);
            }
        }

        await db.SaveChangesAsync();

        if (bad.Count > 0)
        {
            await File.WriteAllTextAsync("bad-users.json",
                JsonSerializer.Serialize(bad, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"Users import: ok={final.Count}, overgeslagen={bad.Count} → bad-users.json");
        }
        else
        {
            Console.WriteLine($"Users import: ok={final.Count}, overgeslagen=0");
        }
    }
}
