using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AnagramSolver.Contracts;
using AnagramSolver.EF.CodeFirst.Data;
using Microsoft.EntityFrameworkCore;

namespace AnagramSolver.EF.CodeFirst.Repositories;

public class EfWordRepository : IWordRepository
{
    private readonly AnagramDbContext _db;

    public EfWordRepository(AnagramDbContext db) => _db = db;

    public async Task<IEnumerable<string>> GetAllWordsAsync(CancellationToken ct = default)
        => await _db.Words.AsNoTracking().Select(w => w.Value).ToListAsync(ct);

    public async Task<AddWordResult> AddWordAsync(string word, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(word))
        {
            return AddWordResult.Invalid;
        }

        var normalized = word.Trim().ToLowerInvariant();

        var exists = await _db.Words.AnyAsync(w => w.Value == normalized, ct);
        if (exists)
        {
            return AddWordResult.AlreadyExists;
        }

        _db.Words.Add(new Models.Word
        {
            Value = normalized,
            IsApproved = true,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);
        return AddWordResult.Added;
    }
}