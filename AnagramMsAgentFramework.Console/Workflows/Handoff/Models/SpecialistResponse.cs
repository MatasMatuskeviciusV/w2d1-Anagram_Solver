namespace AnagramMsAgentFramework.Console.Workflows.Handoff.Models;

public sealed record SpecialistResponse
{
	public HandoffAgentRole CurrentRole { get; init; }
	public string FinalMessage { get; init; } = string.Empty;
	public bool HandBackToTriage { get; init; }
	public string? StructuredPayloadJson { get; init; }
}
