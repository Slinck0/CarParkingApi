using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using V2.Data;
using V2.Models;

namespace V2.Import;

public static class PaymentsImporter
{
    private const int BatchSize = 1000;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
    private const string DateFormat = "yyyy-MM-dd HH:mm:ss";

    public static async Task ImportAsync(AppDbContext db, string jsonPath)
    {
        if (!File.Exists(jsonPath))
            throw new FileNotFoundException($"Bestand niet gevonden: {jsonPath}");

        await using var stream = File.OpenRead(jsonPath);

        var batch = new List<Payment>(BatchSize);
        var badRecords = new List<object>(); // log ongeldige/dupe records
        int total = 0;

        await foreach (var raw in JsonSerializer.DeserializeAsyncEnumerable<PaymentsRaw>(stream, JsonOpts))
        {
            if (raw is null)
            {
                badRecords.Add(new { reason = "null item", raw });
                continue;
            }

            // --- Validatie & normalisatie ---
            var tx = NormalizeKey(raw.transaction);
            if (string.IsNullOrEmpty(tx))
            {
                badRecords.Add(new { reason = "missing transaction", raw });
                continue;
            }

            if (!TryParseDate(raw.t_data?.date, out var tDate))
            {
                badRecords.Add(new { reason = "invalid date", rawDate = raw.t_data?.date, raw });
                continue;
            }

            // NB: normaliseer alle stringvelden die uniqueness kunnen raken
            var entity = new Payment
            {
                Transaction = tx, // genormaliseerd!
                Amount      = raw.amount,
                Initiator   = raw.initiator?.Trim(),
                Hash        = raw.hash?.Trim(),
                CreatedAt   = DateTimeOffset.Now,
                CompletedAt = DateTimeOffset.Now,
                TAmount     = raw.t_data?.amount ?? 0m,
                TDate       = tDate,
                Method      = raw.t_data?.method?.Trim(),
                Issuer      = raw.t_data?.issuer?.Trim(),
                Bank        = raw.t_data?.bank?.Trim()
            };

            batch.Add(entity);
            total++;

            if (batch.Count >= BatchSize)
            {
                await FlushChunkAsync(db, batch, badRecords);
                batch.Clear();
                Console.WriteLine($"Imported {total} payments...");
            }
        }

        if (batch.Count > 0)
        {
            await FlushChunkAsync(db, batch, badRecords);
            Console.WriteLine($"Imported {total} payments (final save).");
        }

        // Schrijf slechte records weg voor analyse
        if (badRecords.Count > 0)
        {
            var badPath = Path.Combine(AppContext.BaseDirectory, "bad-payments.json");
            await File.WriteAllTextAsync(badPath, JsonSerializer.Serialize(badRecords, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"⚠️ Probleemrecords geschreven naar {badPath} (count={badRecords.Count}).");
        }
    }

    private static async Task FlushChunkAsync(AppDbContext db, List<Payment> batch, List<object> badRecords)
    {
        // 1) Dedup binnen de batch zelf (op Transaction)
        var deduped = batch
            .GroupBy(p => p.Transaction)
            .Select(g => g.First())
            .ToList();

        // 2) Haal bestaande keys in één keer op
        var keys = deduped.Select(p => p.Transaction).Distinct().ToList();
        var existingKeys = new HashSet<string>(
            await db.Payments
                .Where(x => keys.Contains(x.Transaction))
                .Select(x => x.Transaction)
                .ToListAsync()
        );

        // 3) Split nieuw vs bestaand
        var toInsert = new List<Payment>(deduped.Count);
        foreach (var p in deduped)
        {
            if (!existingKeys.Contains(p.Transaction))
                toInsert.Add(p);
            else
                badRecords.Add(new { reason = "duplicate (db)", transaction = p.Transaction });
        }

        if (toInsert.Count == 0)
            return;

        // 4) Insert alleen nieuw; laat DB-unique constraint de rest bewaken
        await db.Payments.AddRangeAsync(toInsert);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            // Fallback: log en ga verder – we proberen precieze dups eruit te filteren
            badRecords.Add(new { reason = "db update exception", message = ex.Message });
            // Je kunt hier eventueel nog fijnmazig per record checken, maar meestal is bovenstaande filtering voldoende.
        }
    }

    private static string NormalizeKey(string? s)
        => string.IsNullOrWhiteSpace(s) ? string.Empty : s.Trim().ToUpperInvariant();

    private static bool TryParseDate(string? s, out DateTimeOffset dto)
    {
        if (!string.IsNullOrWhiteSpace(s) &&
            DateTimeOffset.TryParseExact(s.Trim(), DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out dto))
            return true;

        dto = default;
        return false;
    }
}
