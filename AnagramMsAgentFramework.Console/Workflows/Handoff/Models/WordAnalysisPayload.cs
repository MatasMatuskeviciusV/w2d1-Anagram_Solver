namespace AnagramMsAgentFramework.Console.Workflows.Handoff.Models;

public sealed record WordAnalysisPayload
{
	public IReadOnlyList<WordFrequencyItem> TopWords { get; init; } = Array.Empty<WordFrequencyItem>();
	public int TotalWordCount { get; init; }
	public int UniqueWordCount { get; init; }
	public string LongestWord { get; init; } = string.Empty;

	public sealed record WordFrequencyItem(string Word, int Count);
}
