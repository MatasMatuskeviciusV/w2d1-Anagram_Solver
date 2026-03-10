namespace AnagramMsAgentFramework.Console.Workflows.Handoff.Models;

public sealed record SpecialistRequest(
	string UserInput,
	HandoffIntent Intent,
	HandoffConversationState ConversationState);
