namespace AnagramMsAgentFramework.Console.Workflows.Handoff;

public sealed class HandoffWorkflowOptions
{
	public const string SectionName = "Workflows:Handoff";

	public bool StreamingEnabled { get; set; } = true;
	public int StreamingStageTimeoutSeconds { get; set; } = 30;
	public int MaxHandoffDepthPerTurn { get; set; } = 2;
	public int MaxPresentedItems { get; set; } = 10;
	public string? TriageModel { get; set; }
	public string? AnagramSpecialistModel { get; set; }
	public string? WordAnalysisSpecialistModel { get; set; }
	public double RouteConfidenceThreshold { get; set; } = 0.7;
}
