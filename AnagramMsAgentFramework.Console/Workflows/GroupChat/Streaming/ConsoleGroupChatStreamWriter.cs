namespace AnagramMsAgentFramework.Console.Workflows.GroupChat.Streaming;

public sealed class ConsoleGroupChatStreamWriter : IGroupChatStreamWriter
{
	public Task WriteUpdateAsync(GroupChatStreamEvent streamEvent, string text, CancellationToken ct = default)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return Task.CompletedTask;
		}

		ct.ThrowIfCancellationRequested();
		System.Console.WriteLine($"[{streamEvent}] {text}");
		return Task.CompletedTask;
	}

	public Task WriteCompletedAsync(GroupChatStreamEvent streamEvent, CancellationToken ct = default)
	{
		ct.ThrowIfCancellationRequested();
		System.Console.WriteLine($"[{streamEvent}] Completed.");
		return Task.CompletedTask;
	}
}
