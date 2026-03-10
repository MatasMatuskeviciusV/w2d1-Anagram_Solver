namespace AnagramMsAgentFramework.Console.Workflows.SequentialAnagram.Models;

public sealed record FinderResult(
	bool IsValid,
	string NormalizedInput,
	IReadOnlyList<string> Anagrams,
	string? Error);
