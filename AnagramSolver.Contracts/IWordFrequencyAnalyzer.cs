namespace AnagramSolver.Contracts;

public interface IWordFrequencyAnalyzer
{
    WordFrequencyAnalysisResult Analyze(string text, int topN = 10);
}

public sealed class WordFrequencyAnalysisResult
{
    public required IReadOnlyList<FrequentWordResult> TopWords { get; init; }
    public int TotalWordCount { get; init; }
    public int UniqueWordCount { get; init; }
    public string LongestWord { get; init; } = string.Empty;
}

public sealed class FrequentWordResult
{
    public required string Word { get; init; }
    public int Count { get; init; }
}
