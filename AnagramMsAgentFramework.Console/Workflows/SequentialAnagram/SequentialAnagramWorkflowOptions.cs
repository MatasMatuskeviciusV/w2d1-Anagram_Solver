namespace AnagramMsAgentFramework.Console.Workflows.SequentialAnagram;

public sealed class SequentialAnagramWorkflowOptions
{
	public const string SectionName = "Workflows:SequentialAnagram";
	public bool StreamingEnabled { get; set; } = true;
	public int MaxPresentedItems { get; set; } = 10;
	public int StreamingStageTimeoutSeconds { get; set; } = 30;
	public int MaxStreamingPayloadAnagrams { get; set; } = 200;
	public string? FinderModel { get; set; }
	public string? AnalyzerModel { get; set; }
	public string? PresenterModel { get; set; }
}
