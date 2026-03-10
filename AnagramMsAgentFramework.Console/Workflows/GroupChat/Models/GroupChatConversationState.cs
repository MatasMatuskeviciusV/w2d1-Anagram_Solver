namespace AnagramMsAgentFramework.Console.Workflows.GroupChat.Models;

public sealed record GroupChatConversationState
{
	public int TurnNumber { get; init; }
	public int CurrentRound { get; init; }
	public GroupChatAgentRole ActiveRole { get; init; }
	public string? SecretWord { get; init; }
	public string? ProducedAnagram { get; init; }
	public string? LatestGuess { get; init; }
	public bool? IsGuessCorrect { get; init; }
}
