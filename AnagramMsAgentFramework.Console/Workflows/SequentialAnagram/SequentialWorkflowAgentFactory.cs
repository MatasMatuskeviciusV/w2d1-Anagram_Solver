using AnagramMsAgentFramework.Console.Workflows.SequentialAnagram.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AnagramMsAgentFramework.Console.Workflows.SequentialAnagram;

public sealed class SequentialWorkflowAgentFactory
{
	private readonly IChatClient _chatClient;
	private readonly AnagramTools _tools;

	public SequentialWorkflowAgentFactory(IChatClient chatClient, AnagramTools tools)
	{
		_chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
		_tools = tools ?? throw new ArgumentNullException(nameof(tools));
	}

	public AIAgent CreateFinderAgent(string? model = null)
	{
		var stageClient = CreateStageClient(model);
		return stageClient.AsAIAgent(
			instructions:
				"You are Finder. Accept a user input string, call tools to locate anagrams, and respond with short status updates only.",
			name: "Finder",
			tools:
			[
				AIFunctionFactory.Create(_tools.FindAnagramsAsync),
				AIFunctionFactory.Create(_tools.CountAnagramsAsync),
				AIFunctionFactory.Create(_tools.FindAnagramsStructuredAsync)
			]);
	}

	public AIAgent CreateAnalyzerAgent(string? model = null)
	{
		var stageClient = CreateStageClient(model);
		return stageClient.AsAIAgent(
			instructions:
				"You are Analyzer. Analyze provided structured finder results and share concise analytical progress updates only.",
			name: "Analyzer");
	}

	public AIAgent CreatePresenterAgent(string? model = null)
	{
		var stageClient = CreateStageClient(model);
		return stageClient.AsAIAgent(
			instructions:
				"You are Presenter. Read only the provided structured JSON payload and produce a clear, friendly, concise user response.",
			name: "Presenter");
	}

	public Task<FinderResult> FindStructuredAsync(string userInput, CancellationToken ct = default)
	{
		return _tools.FindAnagramsStructuredAsync(userInput, ct);
	}

	private IChatClient CreateStageClient(string? model)
	{
		if (string.IsNullOrWhiteSpace(model))
		{
			return _chatClient;
		}

		return new ConfigureOptionsChatClient(
			innerClient: _chatClient,
			configure: options => options.ModelId = model);
	}
}
