namespace AnagramMsAgentFramework.Console.Workflows.Handoff.Models;

public sealed record TriageDecision
{
	public HandoffIntent Intent { get; init; } = HandoffIntent.Unknown;
	public HandoffAgentRole NextRole { get; init; } = HandoffAgentRole.Triage;
	public double Confidence { get; init; }
	public string? ClarificationQuestion { get; init; }
	public string RouteReason { get; init; } = string.Empty;
}
