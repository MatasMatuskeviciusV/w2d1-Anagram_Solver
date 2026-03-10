namespace AnagramMsAgentFramework.Console.Workflows.GroupChat.Models;

public sealed record OrchestratorDecision
{
	public GroupChatAgentRole NextRole { get; init; }
	public string Reason { get; init; } = string.Empty;
	public double Confidence { get; init; }
}
