using AnagramMsAgentFramework.Console.Workflows.Handoff.Models;

namespace AnagramMsAgentFramework.Console.Workflows.Handoff;

public interface IHandoffWorkflow
{
	Task<HandoffTurnResult> ExecuteAsync(string userInput, CancellationToken ct = default);
	Task ResetAsync(CancellationToken ct = default);
}