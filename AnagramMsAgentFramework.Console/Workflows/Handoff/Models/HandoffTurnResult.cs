namespace AnagramMsAgentFramework.Console.Workflows.Handoff.Models;

public sealed record HandoffTurnResult(
	string FinalMessage,
	HandoffIntent Intent,
	HandoffAgentRole RoutedRole,
	HandoffConversationState State,
	bool UsedFallback);
