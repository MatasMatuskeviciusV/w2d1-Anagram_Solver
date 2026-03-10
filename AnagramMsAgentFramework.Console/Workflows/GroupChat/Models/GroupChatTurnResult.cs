namespace AnagramMsAgentFramework.Console.Workflows.GroupChat.Models;

public sealed record GroupChatTurnResult(
	string FinalMessage,
	GroupChatAgentRole RoutedRole,
	GroupChatConversationState State,
	bool UsedFallback);
