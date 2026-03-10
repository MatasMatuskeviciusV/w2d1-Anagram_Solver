using AnagramMsAgentFramework.Console.Workflows.SequentialAnagram.Models;

namespace AnagramMsAgentFramework.Console.Workflows.SequentialAnagram;

public interface ISequentialAnagramWorkflow
{
	Task<PresenterResult> ExecuteAsync(string userInput, CancellationToken ct = default);
}
