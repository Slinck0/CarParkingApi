using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ParkingImporter.Data;
using ParkingImporter.Models;

namespace ParkingImporter.Import;

public static class PaymentsImporter
{
    public static async Task ImportAsync(AppDbContext db, string jsonPath)
    {
        if (!File.Exists(jsonPath))
            throw new FileNotFoundException($"Bestand niet gevonden: {jsonPath}");

        await using var stream = File.OpenRead(jsonPath);
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        var dateFormat = "yyyy-MM-dd HH:mm:ss";

        var batch = new List<Payment>();
        int counter = 0;

        await foreach (var raw in JsonSerializer.DeserializeAsyncEnumerable<PaymentRaw>(stream, opts))
        {
            if (raw == null) continue;

            var entity = new Payment
            {
                Transaction = raw.transaction,
                Amount      = raw.amount,
                Initiator   = raw.initiator,
                Hash        = raw.hash,
                CreatedAt   = DateTimeOffset.Now,
                CompletedAt = DateTimeOffset.Now,
                TAmount     = raw.t_data.amount,
                TDate       = DateTimeOffset.ParseExact(raw.t_data.date, dateFormat, CultureInfo.InvariantCulture),
                Method      = raw.t_data.method,
                Issuer      = raw.t_data.issuer,
                Bank        = raw.t_data.bank
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
