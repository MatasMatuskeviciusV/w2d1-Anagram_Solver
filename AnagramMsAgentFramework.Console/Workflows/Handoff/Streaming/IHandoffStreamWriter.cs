namespace AnagramMsAgentFramework.Console.Workflows.Handoff.Streaming;

public interface IHandoffStreamWriter
{
	Task WriteUpdateAsync(HandoffStreamEvent streamEvent, string text, CancellationToken ct = default);
	Task WriteCompletedAsync(HandoffStreamEvent streamEvent, CancellationToken ct = default);
}
