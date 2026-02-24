using AnagramSolver.Contracts;
using AnagramSolver.EF.CodeFirst.Data;
using AnagramSolver.EF.CodeFirst.Models;
using System.Text.Json;

namespace AnagramSolver.EF.CodeFirst.Repositories
{
    public class EfSearchLogRepository : ISearchLogRepository
    {
        private readonly AnagramDbContext _db;

        public EfSearchLogRepository(AnagramDbContext db)
        {
            _db = db;
        }

        public async Task AddAsync(string input, IList<string> results, CancellationToken ct = default)
        {
            _db.SearchLogs.Add(new SearchLog
            {
                Input = input,
                ResultCount = results.Count,
                ResultsJson = JsonSerializer.Serialize(results),
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync(ct);
        }
    }
}
