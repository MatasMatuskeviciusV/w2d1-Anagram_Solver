namespace AnagramMsAgentFramework.Console.Workflows.SequentialAnagram.Streaming;

public interface IWorkflowStreamWriter
{
	Task WriteUpdateAsync(WorkflowStage stage, string text, CancellationToken ct = default);
	Task WriteStageCompletedAsync(WorkflowStage stage, CancellationToken ct = default);
}
