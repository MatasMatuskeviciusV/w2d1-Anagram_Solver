namespace AnagramMsAgentFramework.Console.Workflows.SequentialAnagram.Streaming;

public sealed class ConsoleWorkflowStreamWriter : IWorkflowStreamWriter
{
	public Task WriteUpdateAsync(WorkflowStage stage, string text, CancellationToken ct = default)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return Task.CompletedTask;
		}

		ct.ThrowIfCancellationRequested();
		System.Console.WriteLine($"[{stage}] {text}");
		return Task.CompletedTask;
	}

	public Task WriteStageCompletedAsync(WorkflowStage stage, CancellationToken ct = default)
	{
		ct.ThrowIfCancellationRequested();
		System.Console.WriteLine($"[{stage}] Completed.");
		return Task.CompletedTask;
	}
}
