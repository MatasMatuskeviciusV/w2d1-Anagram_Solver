using AnagramSolver.Contracts;
using AnagramSolver.EF.CodeFirst.Data;
using AnagramSolver.EF.CodeFirst.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

public class LoggingAnagramSolver : IAnagramSolver
{
    private readonly IAnagramSolver _inner;
    private readonly ISearchLogRepository _logRepo;
    public LoggingAnagramSolver(IAnagramSolver inner, ISearchLogRepository logRepo)
    {
        _inner = inner;
        _logRepo = logRepo;
    }

    public async Task<IList<string>> GetAnagramsAsync(string myWords, CancellationToken ct = default)
    {
        var results = await _inner.GetAnagramsAsync(myWords, ct);

        await _logRepo.AddAsync(myWords, results, ct);

        return results;
    }
}