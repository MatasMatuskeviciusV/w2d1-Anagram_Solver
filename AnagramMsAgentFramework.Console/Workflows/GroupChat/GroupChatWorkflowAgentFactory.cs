using AnagramMsAgentFramework.Console.Workflows.SequentialAnagram.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AnagramMsAgentFramework.Console.Workflows.GroupChat;

public sealed class GroupChatWorkflowAgentFactory
{
	private readonly IChatClient _chatClient;
	private readonly AnagramTools _tools;

	internal string? LastOrchestratorModelRequested { get; private set; }
	internal string? LastFirstPlayerModelRequested { get; private set; }
	internal string? LastSecondPlayerModelRequested { get; private set; }
	internal string? LastReviewerModelRequested { get; private set; }

	public GroupChatWorkflowAgentFactory(IChatClient chatClient, AnagramTools tools)
	{
		_chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
		_tools = tools ?? throw new ArgumentNullException(nameof(tools));
	}

	public AIAgent CreateOrchestratorAgent(string? model = null)
	{
		LastOrchestratorModelRequested = model;
		var stageClient = CreateStageClient(model);
		return stageClient.AsAIAgent(
			instructions:
				"You are the Orchestrator. Return ONLY JSON with nextRole, reason, confidence. Choose first-player, second-player, or reviewer.",
			name: "Orchestrator");
	}

	public AIAgent CreateFirstPlayerAgent(string? model = null)
	{
		LastFirstPlayerModelRequested = model;
		var stageClient = CreateStageClient(model);
		return stageClient.AsAIAgent(
			instructions:
				"You are FirstPlayer. Return ONLY JSON with a single property anagram containing one word that is an anagram of the provided secret word.",
			name: "FirstPlayer",
			tools:
			[
				AIFunctionFactory.Create(_tools.FindAnagramsStructuredAsync)
			]);
	}

	public AIAgent CreateSecondPlayerAgent(string? model = null)
	{
		LastSecondPlayerModelRequested = model;
		var stageClient = CreateStageClient(model);
		return stageClient.AsAIAgent(
			instructions:
				"You are SecondPlayer. Return ONLY JSON with a single property guess containing one dictionary word guessed from the anagram.",
			name: "SecondPlayer");
	}

	public AIAgent CreateReviewerAgent(string? model = null)
	{
		LastReviewerModelRequested = model;
		var stageClient = CreateStageClient(model);
		return stageClient.AsAIAgent(
			instructions:
				"You are Reviewer. Return ONLY JSON with isCorrect and finalMessage based on provided secret word and guess.",
			name: "Reviewer");
	}

	public Task<FinderResult> FindAnagramsStructuredAsync(string userInput, CancellationToken ct = default)
	{
		return _tools.FindAnagramsStructuredAsync(userInput, ct);
	}

	public Task<IReadOnlyList<string>> GetDictionaryWordsAsync(int minLength, int maxItems, CancellationToken ct = default)
	{
		return _tools.GetDictionaryWordsAsync(minLength, maxItems, ct);
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
