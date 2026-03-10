using AnagramMsAgentFramework.Console.Workflows.Handoff.Models;
using AnagramMsAgentFramework.Console.Workflows.SequentialAnagram.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AnagramMsAgentFramework.Console.Workflows.Handoff;

public sealed class HandoffWorkflowAgentFactory
{
	private readonly IChatClient _chatClient;
	private readonly AnagramTools _tools;

	internal string? LastTriageModelRequested { get; private set; }
	internal string? LastAnagramSpecialistModelRequested { get; private set; }
	internal string? LastWordAnalysisSpecialistModelRequested { get; private set; }

	public HandoffWorkflowAgentFactory(IChatClient chatClient, AnagramTools tools)
	{
		_chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
		_tools = tools ?? throw new ArgumentNullException(nameof(tools));
	}

	public AIAgent CreateTriageAgent(string? model = null)
	{
		LastTriageModelRequested = model;
		var stageClient = CreateStageClient(model);
		return stageClient.AsAIAgent(
			instructions:
				"You are Triage. Classify each user turn as anagram-search, word-analysis, or clarification-needed. Return concise status only.",
			name: "Triage");
	}

	public AIAgent CreateAnagramSpecialistAgent(string? model = null)
	{
		LastAnagramSpecialistModelRequested = model;
		var stageClient = CreateStageClient(model);
		return stageClient.AsAIAgent(
			instructions:
				"You are the Anagram specialist. Use structured anagram tools and return concise user-facing responses with handback intent.",
			name: "AnagramSpecialist",
			tools:
			[
				AIFunctionFactory.Create(_tools.FindAnagramsStructuredAsync),
				AIFunctionFactory.Create(_tools.CountAnagramsAsync)
			]);
	}

	public AIAgent CreateWordAnalysisSpecialistAgent(string? model = null)
	{
		LastWordAnalysisSpecialistModelRequested = model;
		var stageClient = CreateStageClient(model);
		return stageClient.AsAIAgent(
			instructions:
				"You are the Word analysis specialist. Use frequency analysis tools and return concise user-facing responses with handback intent.",
			name: "WordAnalysisSpecialist",
			tools:
			[
				AIFunctionFactory.Create(_tools.AnalyzeWordFrequency),
				AIFunctionFactory.Create(_tools.AnalyzeWordFrequencyStructured)
			]);
	}

	public Task<FinderResult> FindAnagramsStructuredAsync(string userInput, CancellationToken ct = default)
	{
		return _tools.FindAnagramsStructuredAsync(userInput, ct);
	}

	public WordAnalysisPayload AnalyzeWordFrequencyStructured(string text, int topN = 10)
	{
		return _tools.AnalyzeWordFrequencyStructured(text, topN);
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
