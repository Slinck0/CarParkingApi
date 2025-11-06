using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ParkingImporter.Data;
using ParkingImporter.Models;

namespace ParkingImporter.Import;

public static class PaymentsImporter
{
    private static bool TryParseOffset(string? s, out DateTimeOffset dto)
    {
        dto = default;
        if (string.IsNullOrWhiteSpace(s)) return false;

        return DateTimeOffset.TryParseExact(
            s,
            new[] { "yyyy-MM-ddTHH:mm:sszzz", "yyyy-MM-ddTHH:mm:ss.fffzzz", "O" },
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal,
            out dto
        );
    }

    public static async Task ImportAsync(AppDbContext db, string jsonPath)
    {
        if (!File.Exists(jsonPath))
            throw new FileNotFoundException($"Bestand niet gevonden: {jsonPath}");

        await using var stream = File.OpenRead(jsonPath);
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        var dateFormat = "yyyy-MM-dd HH:mm:ss";

        var batch = new List<Payment>();
        int counter = 0;

        var bad = new List<PaymentRaw>();
        await foreach (var raw in JsonSerializer.DeserializeAsyncEnumerable<PaymentRaw>(stream, opts))
        {
            if (raw == null) continue;

            if (!TryParseOffset(raw.t_data.date, out var tDate))
            {
                bad.Add(raw);
                continue;
            }

            var entity = new Payment
            {
                Transaction = raw.transaction,
                Amount = raw.amount,
                Initiator = raw.initiator,
                Hash = raw.hash,

                CreatedAt = DateTimeOffset.Now,
                CompletedAt = DateTimeOffset.Now,

                TAmount = raw.t_data.amount,
                TDate = tDate,
                Method = raw.t_data.method,
                Issuer = raw.t_data.issuer,
                Bank = raw.t_data.bank
            };

            batch.Add(entity);
            counter++;

            if (counter % 1000 == 0) // elke 1000 records wegschrijven
            {
                await db.Payments.AddRangeAsync(batch);
                await db.SaveChangesAsync();
                Console.WriteLine($"Imported {counter} payments...");
                batch.Clear();
            }

            if (bad.Count > 0)
            {
                await File.WriteAllTextAsync(
                    "bad-payments.json",
                    JsonSerializer.Serialize(bad, new JsonSerializerOptions { WriteIndented = true })
                );
                Console.WriteLine($"→ Probleemrecords geschreven naar bad-payments.json ({bad.Count})");
            }
        }

        // Restje flushen
        if (batch.Count > 0)
        {
            await db.Payments.AddRangeAsync(batch);
            await db.SaveChangesAsync();
            Console.WriteLine($"Imported {counter} payments (final save).");
        }
    }

}
