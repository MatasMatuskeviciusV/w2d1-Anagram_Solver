namespace AnagramMsAgentFramework.Console.Workflows.GroupChat.Models;

public sealed record SecondPlayerTurnResult(
	string? Guess,
	string Message,
	bool UsedFallback);
