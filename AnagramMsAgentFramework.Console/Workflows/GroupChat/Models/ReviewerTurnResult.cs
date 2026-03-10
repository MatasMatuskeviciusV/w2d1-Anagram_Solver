namespace AnagramMsAgentFramework.Console.Workflows.GroupChat.Models;

public sealed record ReviewerTurnResult(
	bool IsGuessCorrect,
	string FinalMessage,
	bool UsedFallback);
