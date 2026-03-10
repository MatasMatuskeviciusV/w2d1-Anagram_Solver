namespace AnagramMsAgentFramework.Console.Workflows.GroupChat;

public sealed class GroupChatWorkflowOptions
{
	public const string SectionName = "Workflows:GroupChat";

	public bool StreamingEnabled { get; set; } = true;
	public int StreamingStageTimeoutSeconds { get; set; } = 30;
	public int MaxRoleHopsPerTurn { get; set; } = 4;
	public int MaxRoundsPerGame { get; set; } = 1;
	public int MinWordLength { get; set; } = 4;
	public string? OrchestratorModel { get; set; }
	public string? FirstPlayerModel { get; set; }
	public string? SecondPlayerModel { get; set; }
	public string? ReviewerModel { get; set; }
}
