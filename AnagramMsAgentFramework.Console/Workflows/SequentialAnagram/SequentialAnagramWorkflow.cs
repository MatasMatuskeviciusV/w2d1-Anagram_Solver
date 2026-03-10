using System.Text.Json;
using System.Text.RegularExpressions;
using AnagramMsAgentFramework.Console.Workflows.SequentialAnagram.Models;
using AnagramMsAgentFramework.Console.Workflows.SequentialAnagram.Streaming;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AnagramMsAgentFramework.Console.Workflows.SequentialAnagram;

public sealed class SequentialAnagramWorkflow : ISequentialAnagramWorkflow
{
	private const string RankingPolicy = "WordLengthDesc_LexicalAsc";
	private static readonly Regex QuotedPhraseRegex = new("['\"](?<phrase>[^'\"]+)['\"]", RegexOptions.Compiled | RegexOptions.CultureInvariant);
	private static readonly Regex FindCommandRegex = new("\\bfind\\b", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
	private static readonly Regex CountCommandRegex = new("\\b(count|how many)\\b", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
	private static readonly Regex NumberRegex = new("\\b(?<num>\\d{1,6})\\b", RegexOptions.Compiled | RegexOptions.CultureInvariant);
	private static readonly JsonSerializerOptions HandoffJsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase
	};

	private readonly SequentialWorkflowAgentFactory _agentFactory;
	private readonly IWorkflowStreamWriter _streamWriter;
	private readonly SequentialAnagramWorkflowOptions _options;
	private readonly SequentialAnagramWorkflowExecutionHooks _executionHooks;
	private readonly Func<AIAgent, AgentSession, string, WorkflowStage, CancellationToken, Task<string>>? _streamStageRunner;

	public SequentialAnagramWorkflow(
		SequentialWorkflowAgentFactory agentFactory,
		IWorkflowStreamWriter streamWriter)
		: this(agentFactory, streamWriter, new SequentialAnagramWorkflowOptions())
	{
	}

	public SequentialAnagramWorkflow(
		SequentialWorkflowAgentFactory agentFactory,
		IWorkflowStreamWriter streamWriter,
		SequentialAnagramWorkflowOptions options)
		: this(agentFactory, streamWriter, options, new SequentialAnagramWorkflowExecutionHooks())
	{
	}

	internal SequentialAnagramWorkflow(
		SequentialWorkflowAgentFactory agentFactory,
		IWorkflowStreamWriter streamWriter,
		SequentialAnagramWorkflowOptions options,
		SequentialAnagramWorkflowExecutionHooks executionHooks)
		: this(agentFactory, streamWriter, options, executionHooks, null)
	{
	}

	internal SequentialAnagramWorkflow(
		SequentialWorkflowAgentFactory agentFactory,
		IWorkflowStreamWriter streamWriter,
		SequentialAnagramWorkflowOptions options,
		SequentialAnagramWorkflowExecutionHooks executionHooks,
		Func<AIAgent, AgentSession, string, WorkflowStage, CancellationToken, Task<string>>? streamStageRunner)
	{
		_agentFactory = agentFactory ?? throw new ArgumentNullException(nameof(agentFactory));
		_streamWriter = streamWriter ?? throw new ArgumentNullException(nameof(streamWriter));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_executionHooks = executionHooks ?? throw new ArgumentNullException(nameof(executionHooks));
		_streamStageRunner = streamStageRunner;
	}

	public async Task<PresenterResult> ExecuteAsync(string userInput, CancellationToken ct = default)
	{
		if (string.IsNullOrWhiteSpace(userInput))
		{
			return new PresenterResult("Input cannot be empty. Please provide a word or phrase.");
		}

		var parsedRequest = ParseUserRequest(userInput);

		var finderInput = new FinderInput(parsedRequest.LookupInput);
		var finderResult = _executionHooks.FinderStageRunner is null
			? await RunFinderStageAsync(finderInput, ct)
			: await _executionHooks.FinderStageRunner(finderInput, ct);

		if (!finderResult.IsValid)
		{
			var invalidPresenterInput = new PresenterInput(
				OriginalInput: parsedRequest.LookupInput,
				Finder: finderResult,
				Analyzer: new AnalyzerResult(0, new Dictionary<int, int>(), Array.Empty<string>(), RankingPolicy));

			return new PresenterResult(BuildFallbackPresentation(invalidPresenterInput, parsedRequest.RequestedLimit ?? _options.MaxPresentedItems));
		}

		var analyzerInput = new AnalyzerInput(finderResult);
		var analyzerResult = _executionHooks.AnalyzerStageRunner is null
			? await RunAnalyzerStageAsync(analyzerInput, ct)
			: await _executionHooks.AnalyzerStageRunner(analyzerInput, ct);

		var presenterInput = new PresenterInput(parsedRequest.LookupInput, finderResult, analyzerResult);
		if (_executionHooks.PresenterStageRunner is not null)
		{
			return await _executionHooks.PresenterStageRunner(presenterInput, ct);
		}

		return await RunPresenterStageAsync(presenterInput, parsedRequest.RequestedLimit, ct);
	}

	private async Task<FinderResult> RunFinderStageAsync(FinderInput input, CancellationToken ct)
	{
		ct.ThrowIfCancellationRequested();
		var finderAgent = _agentFactory.CreateFinderAgent(_options.FinderModel);
		var session = await finderAgent.CreateSessionAsync();

		if (_options.StreamingEnabled)
		{
			await _streamWriter.WriteUpdateAsync(
				WorkflowStage.Finder,
				"Progress updates only. Final handoff payload is produced by deterministic host tools.",
				ct);

			await StreamStageWithGuardAsync(
				finderAgent,
				session,
				$"Find anagrams for user input: {input.UserInput}",
				WorkflowStage.Finder,
				ct);
		}

		var finderResult = await _agentFactory.FindStructuredAsync(input.UserInput, ct);
		await _streamWriter.WriteStageCompletedAsync(WorkflowStage.Finder, ct);
		return finderResult;
	}

	private async Task<AnalyzerResult> RunAnalyzerStageAsync(AnalyzerInput input, CancellationToken ct)
	{
		ct.ThrowIfCancellationRequested();
		var analyzerAgent = _agentFactory.CreateAnalyzerAgent(_options.AnalyzerModel);
		var session = await analyzerAgent.CreateSessionAsync();

		if (_options.StreamingEnabled)
		{
			var cappedFinderForStreaming = LimitFinderForStreaming(input.FinderResult);
			var finderPayload = JsonSerializer.Serialize(cappedFinderForStreaming, HandoffJsonOptions);
			await StreamStageWithGuardAsync(
				analyzerAgent,
				session,
				$"Analyze finder payload using deterministic policy: {finderPayload}",
				WorkflowStage.Analyzer,
				ct);
		}

		var result = AnalyzeFinderResult(input.FinderResult, _options.MaxPresentedItems);
		await _streamWriter.WriteStageCompletedAsync(WorkflowStage.Analyzer, ct);
		return result;
	}

	private async Task<PresenterResult> RunPresenterStageAsync(PresenterInput input, int? requestedLimit, CancellationToken ct)
	{
		ct.ThrowIfCancellationRequested();
		var presenterAgent = _agentFactory.CreatePresenterAgent(_options.PresenterModel);
		var session = await presenterAgent.CreateSessionAsync();

		var presenterPayload = SerializePresenterInput(LimitPresenterInputForStreaming(input, requestedLimit));
		var prompt = BuildPresenterPrompt(presenterPayload);
		var finalMessage = _options.StreamingEnabled
			? await StreamStageWithGuardAsync(presenterAgent, session, prompt, WorkflowStage.Presenter, ct)
			: (await presenterAgent.RunAsync(prompt, session))?.ToString();

		if (string.IsNullOrWhiteSpace(finalMessage))
		{
			finalMessage = BuildFallbackPresentation(input, requestedLimit ?? _options.MaxPresentedItems);
		}

		await _streamWriter.WriteStageCompletedAsync(WorkflowStage.Presenter, ct);
		return new PresenterResult(finalMessage);
	}

	private FinderResult LimitFinderForStreaming(FinderResult input)
	{
		var safeLimit = Math.Max(1, _options.MaxStreamingPayloadAnagrams);
		if (input.Anagrams.Count <= safeLimit)
		{
			return input;
		}

		return input with
		{
			Anagrams = input.Anagrams.Take(safeLimit).ToList()
		};
	}

	private PresenterInput LimitPresenterInputForStreaming(PresenterInput input, int? requestedLimit)
	{
		var safeRequested = requestedLimit.GetValueOrDefault(_options.MaxPresentedItems);
		safeRequested = Math.Max(1, safeRequested);
		var safePayloadLimit = Math.Max(1, _options.MaxStreamingPayloadAnagrams);
		var effectiveLimit = Math.Min(safeRequested, safePayloadLimit);

		var limitedFinder = input.Finder with
		{
			Anagrams = input.Finder.Anagrams.Take(effectiveLimit).ToList()
		};

		var limitedAnalyzer = input.Analyzer with
		{
			TopRanked = input.Analyzer.TopRanked.Take(effectiveLimit).ToList()
		};

		return input with
		{
			Finder = limitedFinder,
			Analyzer = limitedAnalyzer
		};
	}

	internal static string SerializePresenterInput(PresenterInput input)
	{
		return JsonSerializer.Serialize(input, HandoffJsonOptions);
	}

	internal static AnalyzerResult AnalyzeFinderResult(FinderResult finderResult, int topRankedLimit)
	{
		var safeTopRankedLimit = Math.Max(1, topRankedLimit);

		if (!finderResult.IsValid || finderResult.Anagrams.Count == 0)
		{
			return new AnalyzerResult(0, new Dictionary<int, int>(), Array.Empty<string>(), RankingPolicy);
		}

		var grouped = finderResult.Anagrams
			.Where(x => !string.IsNullOrWhiteSpace(x))
			.Select(x => x.Trim())
			.GroupBy(x => x.Length)
			.OrderBy(g => g.Key)
			.ToDictionary(g => g.Key, g => g.Count());

		var topRanked = finderResult.Anagrams
			.Where(x => !string.IsNullOrWhiteSpace(x))
			.Select(x => x.Trim())
			.Distinct(StringComparer.Ordinal)
			.OrderByDescending(x => x.Length)
			.ThenBy(x => x, StringComparer.Ordinal)
			.Take(safeTopRankedLimit)
			.ToList();

		return new AnalyzerResult(
			TotalCount: finderResult.Anagrams.Count,
			CountByWordLength: grouped,
			TopRanked: topRanked,
			RankingPolicy: RankingPolicy);
	}

	private async Task<string> StreamStageWithGuardAsync(
		AIAgent agent,
		AgentSession session,
		string prompt,
		WorkflowStage stage,
		CancellationToken ct)
	{
		var timeoutSeconds = Math.Max(1, _options.StreamingStageTimeoutSeconds);
		using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
		timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

		try
		{
			return _streamStageRunner is null
				? await StreamStageAsync(agent, session, prompt, stage, timeoutCts.Token)
				: await _streamStageRunner(agent, session, prompt, stage, timeoutCts.Token);
		}
		catch (OperationCanceledException) when (!ct.IsCancellationRequested)
		{
			await _streamWriter.WriteUpdateAsync(
				stage,
				$"Streaming timed out after {timeoutSeconds}s. Continuing with deterministic workflow output.",
				ct);
			return string.Empty;
		}
	}

	private async Task<string> StreamStageAsync(
		AIAgent agent,
		AgentSession session,
		string prompt,
		WorkflowStage stage,
		CancellationToken ct)
	{
		var chunks = new List<string>();
		var messages = new[] { new ChatMessage(ChatRole.User, prompt) };

		await foreach (var update in agent.RunStreamingAsync(messages, session, cancellationToken: ct))
		{
			var text = update?.ToString();
			if (!string.IsNullOrWhiteSpace(text))
			{
				chunks.Add(text);
				await _streamWriter.WriteUpdateAsync(stage, text, ct);
			}
		}

		return string.Concat(chunks).Trim();
	}

	internal static ParsedAnagramUserRequest ParseUserRequest(string userInput)
	{
		var trimmed = userInput.Trim();
		if (trimmed.Length == 0)
		{
			return new ParsedAnagramUserRequest(string.Empty, null);
		}

		int? requestedLimit = null;
		var numberMatch = NumberRegex.Match(trimmed);
		if (numberMatch.Success && int.TryParse(numberMatch.Groups["num"].Value, out var parsedNum) && parsedNum > 0)
		{
			requestedLimit = parsedNum;
		}

		var quotedMatch = QuotedPhraseRegex.Match(trimmed);
		if (quotedMatch.Success)
		{
			var phrase = quotedMatch.Groups["phrase"].Value.Trim();
			if (phrase.Length > 0)
			{
				return new ParsedAnagramUserRequest(phrase, requestedLimit);
			}
		}

		if (FindCommandRegex.IsMatch(trimmed) || CountCommandRegex.IsMatch(trimmed))
		{
			var lowered = trimmed;
			var forIndex = lowered.IndexOf(" for ", StringComparison.OrdinalIgnoreCase);
			if (forIndex >= 0)
			{
				var maybePhrase = lowered[(forIndex + 5)..].Trim();
				if (maybePhrase.Length > 0)
				{
					return new ParsedAnagramUserRequest(maybePhrase.Trim('\'', '"'), requestedLimit);
				}
			}
		}

		return new ParsedAnagramUserRequest(trimmed, requestedLimit);
	}

	internal static string BuildPresenterPrompt(string presenterPayloadJson)
	{
		return
			"You are Presenter. Produce one concise user-facing response using ONLY the JSON payload below. " +
			"Do not invent fields and do not rely on other context.\n" +
			"JSON payload:\n" +
			presenterPayloadJson;
	}

	internal static string BuildFallbackPresentation(PresenterInput input, int maxPresentedItems)
	{
		var safeMaxPresentedItems = Math.Max(1, maxPresentedItems);

		if (!input.Finder.IsValid)
		{
			return input.Finder.Error ?? "Input is invalid.";
		}

		if (input.Analyzer.TotalCount == 0)
		{
			return $"No anagrams found for '{input.OriginalInput}'.";
		}

		var top = input.Analyzer.TopRanked.Count == 0
			? "none"
			: string.Join(", ", input.Analyzer.TopRanked.Take(safeMaxPresentedItems));

		return $"Found {input.Analyzer.TotalCount} anagram(s) for '{input.OriginalInput}'. Top ranked: {top}.";
	}
}

internal sealed record ParsedAnagramUserRequest(string LookupInput, int? RequestedLimit);

internal sealed class SequentialAnagramWorkflowExecutionHooks
{
	public Func<FinderInput, CancellationToken, Task<FinderResult>>? FinderStageRunner { get; init; }
	public Func<AnalyzerInput, CancellationToken, Task<AnalyzerResult>>? AnalyzerStageRunner { get; init; }
	public Func<PresenterInput, CancellationToken, Task<PresenterResult>>? PresenterStageRunner { get; init; }
}
