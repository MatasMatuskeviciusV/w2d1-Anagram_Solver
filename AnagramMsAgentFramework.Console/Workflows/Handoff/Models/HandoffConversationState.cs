namespace AnagramMsAgentFramework.Console.Workflows.Handoff.Models;

public sealed record HandoffConversationState
{
	public int TurnNumber { get; init; }
	public HandoffAgentRole ActiveRole { get; init; } = HandoffAgentRole.Triage;
	public HandoffIntent LastIntent { get; init; } = HandoffIntent.Unknown;
	public string? LastNormalizedInput { get; init; }
	public string? PendingClarification { get; init; }
}
