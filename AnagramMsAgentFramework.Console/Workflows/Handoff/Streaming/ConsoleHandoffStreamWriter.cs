namespace AnagramMsAgentFramework.Console.Workflows.Handoff.Streaming;

public sealed class ConsoleHandoffStreamWriter : IHandoffStreamWriter
{
	public Task WriteUpdateAsync(HandoffStreamEvent streamEvent, string text, CancellationToken ct = default)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return Task.CompletedTask;
		}

		ct.ThrowIfCancellationRequested();
		System.Console.WriteLine($"[{streamEvent}] {text}");
		return Task.CompletedTask;
	}

	public Task WriteCompletedAsync(HandoffStreamEvent streamEvent, CancellationToken ct = default)
	{
		ct.ThrowIfCancellationRequested();
		System.Console.WriteLine($"[{streamEvent}] Completed.");
		return Task.CompletedTask;
	}
}
