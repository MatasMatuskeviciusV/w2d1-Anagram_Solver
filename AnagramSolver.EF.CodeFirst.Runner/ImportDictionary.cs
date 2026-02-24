using AnagramSolver.EF.CodeFirst.Data;
using AnagramSolver.EF.CodeFirst.Models;
using Microsoft.EntityFrameworkCore;
using System.IO.Pipelines;
using System.Text;

public static class ImportDictionary
{
    private static readonly string[] CategoryNames =
    [
        "daiktavardis",
        "veiksmažodis",
        "būdvardis",
        "prieveiksmis",
        "skaitvardis"
    ];

    public static async Task RunAsync(string filePath, int batchSize = 5000, CancellationToken ct = default)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Dictionary file not found", filePath);
        }

        var options = new DbContextOptionsBuilder<AnagramDbContext>().UseSqlServer("Server=localhost;Database=AnagramSolver_CF;Trusted_Connection=True;TrustServerCertificate=True").Options;

        using var db = new AnagramDbContext(options);

        await db.Database.MigrateAsync(ct);

        var categoryIds = await EnsureCategoriesAndGetIdsAsync(db, ct);

        var existing = new HashSet<string>(
            await db.Words.AsNoTracking().Select(w => w.Value).ToListAsync(ct),
            StringComparer.Ordinal);

        db.ChangeTracker.AutoDetectChangesEnabled = false;

        int read = 0, inserted = 0, skipped = 0, bad = 0;

        var batch = new List<Word>(batchSize);

        using var sr = new StreamReader(filePath, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        string? line;
        while ((line = await sr.ReadLineAsync()) != null)
        {
            ct.ThrowIfCancellationRequested();
            read++;

            if (string.IsNullOrEmpty(line))
            {
                skipped++;
                continue;
            }

            var comma = line.IndexOf(',');
            if (comma <= 0 || comma >= line.Length - 1)
            {
                bad++;
                continue;
            }

            var wordRaw = line[..comma].Trim();
            var catRaw = line[(comma + 1)..].Trim();

            if(wordRaw.Length == 0)
            {
                bad++;
                continue;
            }
            int? categoryIndex = null;
            if (catRaw.Length > 0)
            {
                if (!int.TryParse(catRaw, out var parsed))
                {
                    bad++;
                    continue;
                }
                categoryIndex = parsed;
            }

            var normalized = NormalizeWord(wordRaw);
            if(normalized.Length == 0)
            {
                bad++;
                continue;
            }

            if (existing.Contains(normalized))
            {
                skipped++;
                continue;
            }

            int? categoryId = null;
            if(categoryIndex.HasValue && categoryIndex.Value >= 1 && categoryIndex.Value <= 5)
            {
                categoryId = categoryIds[categoryIndex.Value - 1];
            }

            batch.Add(new Word
            {
                Value = normalized,
                CategoryId = categoryId,
                IsApproved = true,
                CreatedAt = DateTime.UtcNow
            });

            existing.Add(normalized);

            if(batch.Count >= batchSize)
            {
                inserted += await FlushBatchAsync(db, batch, ct);
                batch.Clear();

                Console.WriteLine($"Read: {read:n0} | Inserted: {inserted:n0} | Skipped: {skipped:n0} | Bad: {bad:n0}");
            }
        }

        if(batch.Count > 0)
        {
            inserted += await FlushBatchAsync(db, batch, ct);
            batch.Clear();
        }

        Console.WriteLine($"DONE | Read: {read:n0} | Inserted: {inserted:n0} | Skipped: {skipped:n0} | Bad: {bad:n0}");
    }

    private static string NormalizeWord(string word) => word.Trim().ToLowerInvariant();

    private static async Task<int[]> EnsureCategoriesAndGetIdsAsync(AnagramDbContext db, CancellationToken ct)
    {
        foreach(var name in CategoryNames)
        {
            var exists = await db.Categories.AnyAsync(c => c.Name == name, ct);
            if (!exists)
            {
                db.Categories.Add(new Category { Name = name });
            }
        }   
        
        await db.SaveChangesAsync(ct);

        var ids = new int[CategoryNames.Length];
        for(int i = 0; i < CategoryNames.Length; i++)
        {
            ids[i] = await db.Categories.Where(c => c.Name == CategoryNames[i]).Select(c => c.Id).FirstAsync(ct);
        }
        return ids;
    }

    private static async Task<int> FlushBatchAsync(AnagramDbContext db, List<Word> batch, CancellationToken ct)
    {
        db.Words.AddRange(batch);
        var changes = await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
        return changes;
    }
}