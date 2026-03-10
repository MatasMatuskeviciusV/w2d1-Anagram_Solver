namespace AnagramMsAgentFramework.Console.Workflows.GroupChat.Models;

public sealed record FirstPlayerTurnResult(
	string? SecretWord,
	string? ProducedAnagram,
	string Message,
	bool UsedFallback);
