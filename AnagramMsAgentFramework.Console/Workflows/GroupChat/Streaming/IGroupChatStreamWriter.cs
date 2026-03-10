namespace AnagramMsAgentFramework.Console.Workflows.GroupChat.Streaming;

public interface IGroupChatStreamWriter
{
	Task WriteUpdateAsync(GroupChatStreamEvent streamEvent, string text, CancellationToken ct = default);
	Task WriteCompletedAsync(GroupChatStreamEvent streamEvent, CancellationToken ct = default);
}
