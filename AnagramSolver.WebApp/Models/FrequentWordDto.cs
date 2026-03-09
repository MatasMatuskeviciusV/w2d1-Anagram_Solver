namespace AnagramSolver.WebApp.Models;

public sealed class FrequentWordDto
{
    public required string Word { get; init; }
    public int Count { get; init; }
}
