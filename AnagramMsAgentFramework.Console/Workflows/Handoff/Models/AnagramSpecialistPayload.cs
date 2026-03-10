namespace AnagramMsAgentFramework.Console.Workflows.Handoff.Models;

public sealed record AnagramSpecialistPayload
{
	public bool IsValid { get; init; }
	public string NormalizedInput { get; init; } = string.Empty;
	public int TotalCount { get; init; }
	public IReadOnlyList<string> TopAnagrams { get; init; } = Array.Empty<string>();
	public string? Error { get; init; }
}
