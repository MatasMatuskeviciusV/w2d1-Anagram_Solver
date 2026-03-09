namespace AnagramSolver.WebApp.Models;

public sealed class FrequencyAnalysisResponse
{
    public required IReadOnlyList<FrequentWordDto> TopWords { get; init; }
    public int TotalWordCount { get; init; }
    public int UniqueWordCount { get; init; }
    public string LongestWord { get; init; } = string.Empty;
}
