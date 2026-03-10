namespace AnagramMsAgentFramework.Console.Workflows.SequentialAnagram.Models;

public sealed record AnalyzerResult(
	int TotalCount,
	IReadOnlyDictionary<int, int> CountByWordLength,
	IReadOnlyList<string> TopRanked,
	string RankingPolicy);
