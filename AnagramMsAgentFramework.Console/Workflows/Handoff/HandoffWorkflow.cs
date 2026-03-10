using System.Text.Json;
using System.Text.RegularExpressions;
using AnagramMsAgentFramework.Console.Workflows.Handoff.Models;
using AnagramMsAgentFramework.Console.Workflows.Handoff.Streaming;
using Microsoft.Agents.AI;

namespace AnagramMsAgentFramework.Console.Workflows.Handoff;

public sealed class HandoffWorkflow : IHandoffWorkflow
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
	};

	private static readonly Regex QuotedPhraseRegex = new("['\"](?<phrase>[^'\"]+)['\"]", RegexOptions.Compiled | RegexOptions.CultureInvariant);
	private static readonly Regex FindCommandRegex = new("\\bfind\\b", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
	private static readonly Regex CountCommandRegex = new("\\b(count|how many)\\b", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	private static readonly string[] AnagramKeywords =
	[
		"anagram", "anagrams", "unscramble", "rearrange", "letters", "find"
	];

	private static readonly string[] WordAnalysisKeywords =
	[
		"frequency", "frequencies", "statistics", "stats", "top words", "most common", "analyze"
	];

	private readonly HandoffWorkflowAgentFactory _agentFactory;
	private readonly IHandoffStreamWriter _streamWriter;
	private readonly HandoffWorkflowOptions _options;
	private readonly HandoffWorkflowExecutionHooks _executionHooks;
	private readonly Func<HandoffStreamEvent, string, CancellationToken, Task<string>>? _streamStageRunner;
	private readonly object _stateGate = new();

	private HandoffConversationState _state = new();

	public HandoffWorkflow(
		HandoffWorkflowAgentFactory agentFactory,
		IHandoffStreamWriter streamWriter,
		HandoffWorkflowOptions options)
		: this(agentFactory, streamWriter, options, new HandoffWorkflowExecutionHooks(), null)
	{
	}

	internal HandoffWorkflow(
		HandoffWorkflowAgentFactory agentFactory,
		IHandoffStreamWriter streamWriter,
		HandoffWorkflowOptions options,
		HandoffWorkflowExecutionHooks executionHooks,
		Func<HandoffStreamEvent, string, CancellationToken, Task<string>>? streamStageRunner)
	{
		_agentFactory = agentFactory ?? throw new ArgumentNullException(nameof(agentFactory));
		_streamWriter = streamWriter ?? throw new ArgumentNullException(nameof(streamWriter));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_executionHooks = executionHooks ?? throw new ArgumentNullException(nameof(executionHooks));
		_streamStageRunner = streamStageRunner;
	}

	public async Task<HandoffTurnResult> ExecuteAsync(string userInput, CancellationToken ct = default)
	{
		if (string.IsNullOrWhiteSpace(userInput))
		{
			var unchanged = GetStateSnapshot();
			return new HandoffTurnResult(
				FinalMessage: "Input cannot be empty. Please provide a request.",
				Intent: HandoffIntent.Unknown,
				RoutedRole: HandoffAgentRole.Triage,
				State: unchanged,
				UsedFallback: true);
		}

		ct.ThrowIfCancellationRequested();
		var stateSnapshot = GetStateSnapshot();
		var triageContext = new TriageContext(userInput.Trim(), stateSnapshot);
		var triageDecision = await RunTriageAsync(triageContext, ct);

		if (IsMalformedDecision(triageDecision))
		{
			return CompleteTurn(
				message: "I could not determine a safe route for your request. Please rephrase it.",
				intent: HandoffIntent.Unknown,
				routedRole: HandoffAgentRole.Triage,
				pendingClarification: "Would you like an anagram search or word analysis?",
				lastNormalizedInput: null,
				usedFallback: true);
		}

		if (triageDecision.Confidence < _options.RouteConfidenceThreshold || triageDecision.NextRole == HandoffAgentRole.Triage)
		{
			var clarification = string.IsNullOrWhiteSpace(triageDecision.ClarificationQuestion)
				? "Do you want an anagram search or a word-frequency analysis?"
				: triageDecision.ClarificationQuestion!;

			return CompleteTurn(
				message: clarification,
				intent: triageDecision.Intent,
				routedRole: HandoffAgentRole.Triage,
				pendingClarification: clarification,
				lastNormalizedInput: stateSnapshot.LastNormalizedInput,
				usedFallback: false);
		}

		if (_options.MaxHandoffDepthPerTurn < 2)
		{
			return CompleteTurn(
				message: "Max handoff depth reached for this turn. Please try again.",
				intent: triageDecision.Intent,
				routedRole: triageDecision.NextRole,
				pendingClarification: null,
				lastNormalizedInput: stateSnapshot.LastNormalizedInput,
				usedFallback: true);
		}

		var specialistInput = triageDecision.NextRole == HandoffAgentRole.AnagramSpecialist
			? ExtractAnagramLookupInput(userInput)
			: userInput.Trim();
		var specialistRequest = new SpecialistRequest(specialistInput, triageDecision.Intent, stateSnapshot);
		var specialistResponse = triageDecision.NextRole switch
		{
			HandoffAgentRole.AnagramSpecialist => await RunAnagramSpecialistAsync(specialistRequest, ct),
			HandoffAgentRole.WordAnalysisSpecialist => await RunWordAnalysisSpecialistAsync(specialistRequest, ct),
			_ => new SpecialistResponse
			{
				CurrentRole = HandoffAgentRole.Triage,
				FinalMessage = string.Empty,
				HandBackToTriage = true
			}
		};

		var fallbackUsed = false;
		var finalMessage = specialistResponse.FinalMessage;
		if (string.IsNullOrWhiteSpace(finalMessage))
		{
			finalMessage = "I could not produce a reliable specialist response.";
			fallbackUsed = true;
		}

		if (!specialistResponse.HandBackToTriage)
		{
			finalMessage = "Specialist handback marker was missing, so control returned to triage.";
			fallbackUsed = true;
		}

		var lastNormalized = ExtractLastNormalizedInput(
			specialistResponse.StructuredPayloadJson,
			triageDecision.Intent,
			stateSnapshot.LastNormalizedInput);

		return CompleteTurn(
			message: finalMessage,
			intent: triageDecision.Intent,
			routedRole: triageDecision.NextRole,
			pendingClarification: null,
			lastNormalizedInput: lastNormalized,
			usedFallback: fallbackUsed);
	}

	public Task ResetAsync(CancellationToken ct = default)
	{
		ct.ThrowIfCancellationRequested();
		lock (_stateGate)
		{
			_state = new HandoffConversationState();
		}

		return Task.CompletedTask;
	}

	public async Task<TriageDecision> RunTriageAsync(TriageContext context, CancellationToken ct)
	{
		ct.ThrowIfCancellationRequested();
		await _streamWriter.WriteUpdateAsync(HandoffStreamEvent.Triage, "Classifying request intent.", ct);
		try
		{
			await StreamStageWithGuardAsync(HandoffStreamEvent.Triage, context.UserInput, ct);

			if (_executionHooks.TriageRunner is not null)
			{
				return await _executionHooks.TriageRunner(context, ct);
			}

			return await RunTriageByAgentWithFallbackAsync(context, ct);
		}
		finally
		{
			await _streamWriter.WriteCompletedAsync(HandoffStreamEvent.Triage, ct);
		}
	}

	public async Task<SpecialistResponse> RunAnagramSpecialistAsync(SpecialistRequest request, CancellationToken ct)
	{
		ct.ThrowIfCancellationRequested();
		await _streamWriter.WriteUpdateAsync(HandoffStreamEvent.AnagramSpecialist, "Running anagram lookup.", ct);
		try
		{
			await StreamStageWithGuardAsync(HandoffStreamEvent.AnagramSpecialist, request.UserInput, ct);

			if (_executionHooks.AnagramSpecialistRunner is not null)
			{
				return await _executionHooks.AnagramSpecialistRunner(request, ct);
			}

			return await RunAnagramSpecialistByAgentWithFallbackAsync(request, ct);
		}
		finally
		{
			await _streamWriter.WriteCompletedAsync(HandoffStreamEvent.AnagramSpecialist, ct);
		}
	}

	public async Task<SpecialistResponse> RunWordAnalysisSpecialistAsync(SpecialistRequest request, CancellationToken ct)
	{
		ct.ThrowIfCancellationRequested();
		await _streamWriter.WriteUpdateAsync(HandoffStreamEvent.WordAnalysisSpecialist, "Running word analysis.", ct);
		try
		{
			await StreamStageWithGuardAsync(HandoffStreamEvent.WordAnalysisSpecialist, request.UserInput, ct);

			if (_executionHooks.WordAnalysisSpecialistRunner is not null)
			{
				return await _executionHooks.WordAnalysisSpecialistRunner(request, ct);
			}

			return await RunWordAnalysisSpecialistByAgentWithFallbackAsync(request, ct);
		}
		finally
		{
			await _streamWriter.WriteCompletedAsync(HandoffStreamEvent.WordAnalysisSpecialist, ct);
		}
	}

	private async Task<TriageDecision> RunTriageByAgentWithFallbackAsync(TriageContext context, CancellationToken ct)
	{
		var fallbackDecision = DetermineTriageDecision(context.UserInput);
		try
		{
			var triageAgent = _agentFactory.CreateTriageAgent(_options.TriageModel);
			var session = await triageAgent.CreateSessionAsync();
			var responseText = (await triageAgent.RunAsync(BuildTriagePrompt(context), session))?.ToString();

			if (TryParseTriageDecision(responseText, out var decision) && !IsMalformedDecision(decision))
			{
				return decision;
			}
		}
		catch (Exception)
		{
			// Deterministic fallback keeps routing safe when model output is unavailable.
		}

		return fallbackDecision with
		{
			RouteReason = $"{fallbackDecision.RouteReason} (fallback: malformed or unavailable triage output)"
		};
	}

	private async Task<SpecialistResponse> RunAnagramSpecialistByAgentWithFallbackAsync(SpecialistRequest request, CancellationToken ct)
	{
		var fallback = await BuildDeterministicAnagramResponseAsync(request, ct);
		try
		{
			var specialistAgent = _agentFactory.CreateAnagramSpecialistAgent(_options.AnagramSpecialistModel);
			var session = await specialistAgent.CreateSessionAsync();
			var responseText = (await specialistAgent.RunAsync(BuildSpecialistPrompt(request, HandoffAgentRole.AnagramSpecialist), session))?.ToString();

			if (TryParseSpecialistResponse(responseText, HandoffAgentRole.AnagramSpecialist, out var parsedResponse))
			{
				return parsedResponse;
			}
		}
		catch (Exception)
		{
			// Deterministic fallback keeps specialist responses stable when model output is unavailable.
		}

		return fallback;
	}

	private async Task<SpecialistResponse> RunWordAnalysisSpecialistByAgentWithFallbackAsync(SpecialistRequest request, CancellationToken ct)
	{
		var fallback = BuildDeterministicWordAnalysisResponse(request);
		try
		{
			var specialistAgent = _agentFactory.CreateWordAnalysisSpecialistAgent(_options.WordAnalysisSpecialistModel);
			var session = await specialistAgent.CreateSessionAsync();
			var responseText = (await specialistAgent.RunAsync(BuildSpecialistPrompt(request, HandoffAgentRole.WordAnalysisSpecialist), session))?.ToString();

			if (TryParseSpecialistResponse(responseText, HandoffAgentRole.WordAnalysisSpecialist, out var parsedResponse))
			{
				return parsedResponse;
			}
		}
		catch (Exception)
		{
			// Deterministic fallback keeps specialist responses stable when model output is unavailable.
		}

		return fallback;
	}

	private async Task<SpecialistResponse> BuildDeterministicAnagramResponseAsync(SpecialistRequest request, CancellationToken ct)
	{
		var finderResult = await _agentFactory.FindAnagramsStructuredAsync(request.UserInput, ct);
		var payload = new AnagramSpecialistPayload
		{
			IsValid = finderResult.IsValid,
			NormalizedInput = finderResult.NormalizedInput,
			TotalCount = finderResult.Anagrams.Count,
			TopAnagrams = finderResult.Anagrams.Take(Math.Max(1, _options.MaxPresentedItems)).ToArray(),
			Error = finderResult.Error
		};

		return new SpecialistResponse
		{
			CurrentRole = HandoffAgentRole.AnagramSpecialist,
			FinalMessage = BuildAnagramMessage(request.UserInput, payload),
			HandBackToTriage = true,
			StructuredPayloadJson = JsonSerializer.Serialize(payload, JsonOptions)
		};
	}

	private SpecialistResponse BuildDeterministicWordAnalysisResponse(SpecialistRequest request)
	{
		var payload = _agentFactory.AnalyzeWordFrequencyStructured(request.UserInput, _options.MaxPresentedItems);
		return new SpecialistResponse
		{
			CurrentRole = HandoffAgentRole.WordAnalysisSpecialist,
			FinalMessage = BuildWordAnalysisMessage(payload),
			HandBackToTriage = true,
			StructuredPayloadJson = JsonSerializer.Serialize(payload, JsonOptions)
		};
	}

	private HandoffTurnResult CompleteTurn(
		string message,
		HandoffIntent intent,
		HandoffAgentRole routedRole,
		string? pendingClarification,
		string? lastNormalizedInput,
		bool usedFallback)
	{
		HandoffConversationState updated;
		lock (_stateGate)
		{
			updated = _state with
			{
				TurnNumber = _state.TurnNumber + 1,
				ActiveRole = HandoffAgentRole.Triage,
				LastIntent = intent,
				LastNormalizedInput = lastNormalizedInput,
				PendingClarification = pendingClarification
			};
			_state = updated;
		}

		return new HandoffTurnResult(message, intent, routedRole, updated, usedFallback);
	}

	private HandoffConversationState GetStateSnapshot()
	{
		lock (_stateGate)
		{
			return _state;
		}
	}

	private static TriageDecision DetermineTriageDecision(string input)
	{
		var normalized = input.Trim().ToLowerInvariant();
		var anagramHits = AnagramKeywords.Count(normalized.Contains);
		var analysisHits = WordAnalysisKeywords.Count(normalized.Contains);

		if (anagramHits == 0 && analysisHits == 0)
		{
			return new TriageDecision
			{
				Intent = HandoffIntent.Unknown,
				NextRole = HandoffAgentRole.Triage,
				Confidence = 0.45,
				ClarificationQuestion = "Do you want an anagram search or a word-frequency analysis?",
				RouteReason = "No explicit intent keywords were detected."
			};
		}

		if (anagramHits == analysisHits)
		{
			return new TriageDecision
			{
				Intent = HandoffIntent.Unknown,
				NextRole = HandoffAgentRole.Triage,
				Confidence = 0.6,
				ClarificationQuestion = "I can help with anagrams or word statistics. Which one do you need?",
				RouteReason = "Input contained mixed intent keywords."
			};
		}

		if (anagramHits > analysisHits)
		{
			return new TriageDecision
			{
				Intent = HandoffIntent.AnagramSearch,
				NextRole = HandoffAgentRole.AnagramSpecialist,
				Confidence = 0.92,
				RouteReason = "Detected anagram-specific keywords."
			};
		}

		return new TriageDecision
		{
			Intent = HandoffIntent.WordAnalysis,
			NextRole = HandoffAgentRole.WordAnalysisSpecialist,
			Confidence = 0.92,
			RouteReason = "Detected word-analysis keywords."
		};
	}

	private static bool IsMalformedDecision(TriageDecision decision)
	{
		if (decision is null)
		{
			return true;
		}

		if (string.IsNullOrWhiteSpace(decision.RouteReason))
		{
			return true;
		}

		if (decision.Confidence is < 0 or > 1)
		{
			return true;
		}

		if (decision.Intent == HandoffIntent.AnagramSearch && decision.NextRole != HandoffAgentRole.AnagramSpecialist)
		{
			return true;
		}

		if (decision.Intent == HandoffIntent.WordAnalysis && decision.NextRole != HandoffAgentRole.WordAnalysisSpecialist)
		{
			return true;
		}

		if (decision.Intent == HandoffIntent.Unknown && decision.NextRole != HandoffAgentRole.Triage)
		{
			return true;
		}

		return false;
	}

	private static string BuildTriagePrompt(TriageContext context)
	{
		return
			"You are Triage. Classify the request intent and output ONLY JSON with properties: " +
			"intent, nextRole, confidence, clarificationQuestion, routeReason. " +
			"intent values: Unknown, AnagramSearch, WordAnalysis. " +
			"nextRole values: Triage, AnagramSpecialist, WordAnalysisSpecialist. " +
			"For Unknown intent, nextRole must be Triage. confidence must be 0..1.\n" +
			$"User input: {context.UserInput}";
	}

	private static string BuildSpecialistPrompt(SpecialistRequest request, HandoffAgentRole specialistRole)
	{
		return
			$"You are {specialistRole}. Return ONLY JSON with properties: " +
			"currentRole, finalMessage, handBackToTriage, structuredPayloadJson. " +
			"handBackToTriage must be true and currentRole must match your role.\n" +
			$"Intent: {request.Intent}\n" +
			$"User input: {request.UserInput}";
	}

	private static bool TryParseTriageDecision(string? rawResponse, out TriageDecision decision)
	{
		decision = new TriageDecision();
		if (string.IsNullOrWhiteSpace(rawResponse))
		{
			return false;
		}

		if (!TryParseJsonObject(rawResponse, out var document))
		{
			return false;
		}

		using (document)
		{
			var root = document.RootElement;
			if (!TryReadEnum(root, "intent", TryParseIntent, out var intent) ||
				!TryReadEnum(root, "nextRole", TryParseRole, out var nextRole) ||
				!TryReadDouble(root, "confidence", out var confidence) ||
				!TryReadString(root, "routeReason", out var routeReason))
			{
				return false;
			}

			root.TryGetProperty("clarificationQuestion", out var clarificationElement);
			var clarification = clarificationElement.ValueKind == JsonValueKind.String
				? clarificationElement.GetString()
				: null;

			decision = new TriageDecision
			{
				Intent = intent,
				NextRole = nextRole,
				Confidence = confidence,
				ClarificationQuestion = clarification,
				RouteReason = routeReason!
			};

			return true;
		}
	}

	private static bool TryParseSpecialistResponse(string? rawResponse, HandoffAgentRole expectedRole, out SpecialistResponse response)
	{
		response = new SpecialistResponse();
		if (string.IsNullOrWhiteSpace(rawResponse))
		{
			return false;
		}

		if (!TryParseJsonObject(rawResponse, out var document))
		{
			return false;
		}

		using (document)
		{
			var root = document.RootElement;
			if (!TryReadEnum(root, "currentRole", TryParseRole, out var currentRole) ||
				!TryReadString(root, "finalMessage", out var finalMessage) ||
				!TryReadBoolean(root, "handBackToTriage", out var handBackToTriage))
			{
				return false;
			}

			if (currentRole != expectedRole || !handBackToTriage || string.IsNullOrWhiteSpace(finalMessage))
			{
				return false;
			}

			string? structuredPayloadJson = null;
			if (root.TryGetProperty("structuredPayloadJson", out var payloadElement))
			{
				if (payloadElement.ValueKind == JsonValueKind.String)
				{
					structuredPayloadJson = payloadElement.GetString();
				}
				else if (payloadElement.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
				{
					structuredPayloadJson = payloadElement.GetRawText();
				}
			}

			response = new SpecialistResponse
			{
				CurrentRole = currentRole,
				FinalMessage = finalMessage!,
				HandBackToTriage = handBackToTriage,
				StructuredPayloadJson = structuredPayloadJson
			};

			return true;
		}
	}

	private static bool TryParseJsonObject(string rawResponse, out JsonDocument? document)
	{
		document = null;
		var json = ExtractJsonObject(rawResponse);
		if (json is null)
		{
			return false;
		}

		try
		{
			document = JsonDocument.Parse(json);
			return document.RootElement.ValueKind == JsonValueKind.Object;
		}
		catch (JsonException)
		{
			return false;
		}
	}

	private static string? ExtractJsonObject(string rawResponse)
	{
		var start = rawResponse.IndexOf('{');
		var end = rawResponse.LastIndexOf('}');
		if (start < 0 || end <= start)
		{
			return null;
		}

		return rawResponse[start..(end + 1)];
	}

	private static bool TryReadString(JsonElement root, string propertyName, out string? value)
	{
		value = null;
		if (!root.TryGetProperty(propertyName, out var element) || element.ValueKind != JsonValueKind.String)
		{
			return false;
		}

		value = element.GetString();
		return !string.IsNullOrWhiteSpace(value);
	}

	private static bool TryReadBoolean(JsonElement root, string propertyName, out bool value)
	{
		value = false;
		if (!root.TryGetProperty(propertyName, out var element))
		{
			return false;
		}

		if (element.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
		{
			return false;
		}

		value = element.GetBoolean();
		return true;
	}

	private static bool TryReadDouble(JsonElement root, string propertyName, out double value)
	{
		value = 0;
		if (!root.TryGetProperty(propertyName, out var element) || element.ValueKind != JsonValueKind.Number)
		{
			return false;
		}

		return element.TryGetDouble(out value);
	}

	private static bool TryReadEnum<TEnum>(
		JsonElement root,
		string propertyName,
		Func<string, TEnum?> parser,
		out TEnum parsedValue)
		where TEnum : struct
	{
		parsedValue = default;
		if (!TryReadString(root, propertyName, out var raw) || raw is null)
		{
			return false;
		}

		var parsed = parser(raw);
		if (!parsed.HasValue)
		{
			return false;
		}

		parsedValue = parsed.Value;
		return true;
	}

	private static HandoffIntent? TryParseIntent(string raw)
	{
		return raw.Trim().ToLowerInvariant() switch
		{
			"anagramsearch" or "anagram-search" => HandoffIntent.AnagramSearch,
			"wordanalysis" or "word-analysis" => HandoffIntent.WordAnalysis,
			"unknown" or "clarificationneeded" or "clarification-needed" => HandoffIntent.Unknown,
			_ => null
		};
	}

	private static HandoffAgentRole? TryParseRole(string raw)
	{
		return raw.Trim().ToLowerInvariant() switch
		{
			"triage" => HandoffAgentRole.Triage,
			"anagramspecialist" or "anagram-specialist" => HandoffAgentRole.AnagramSpecialist,
			"wordanalysisspecialist" or "word-analysis-specialist" => HandoffAgentRole.WordAnalysisSpecialist,
			_ => null
		};
	}

	private async Task<string> StreamStageWithGuardAsync(HandoffStreamEvent stage, string prompt, CancellationToken ct)
	{
		if (!_options.StreamingEnabled || _streamStageRunner is null)
		{
			return string.Empty;
		}

		var timeoutSeconds = Math.Max(1, _options.StreamingStageTimeoutSeconds);
		using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
		timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

		try
		{
			return await _streamStageRunner(stage, prompt, timeoutCts.Token);
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

	private static string BuildAnagramMessage(string originalInput, AnagramSpecialistPayload payload)
	{
		if (!payload.IsValid)
		{
			return payload.Error ?? "Input is invalid for anagram search.";
		}

		if (payload.TotalCount == 0)
		{
			return $"No anagrams found for '{originalInput}'.";
		}

		var preview = payload.TopAnagrams.Count == 0
			? "none"
			: string.Join(", ", payload.TopAnagrams);
		return $"Found {payload.TotalCount} anagram(s) for '{originalInput}'. Top results: {preview}.";
	}

	private static string BuildWordAnalysisMessage(WordAnalysisPayload payload)
	{
		if (payload.TopWords.Count == 0)
		{
			return "No analyzable words found in the text.";
		}

		var topWords = string.Join(", ", payload.TopWords.Select(x => $"{x.Word}:{x.Count}"));
		return $"Top words: {topWords}. Total words: {payload.TotalWordCount}. Unique words: {payload.UniqueWordCount}. Longest word: {payload.LongestWord}.";
	}

	private static string? ExtractLastNormalizedInput(string? payloadJson, HandoffIntent intent, string? fallback)
	{
		if (string.IsNullOrWhiteSpace(payloadJson))
		{
			return fallback;
		}

		if (intent != HandoffIntent.AnagramSearch)
		{
			return fallback;
		}

		try
		{
			using var document = JsonDocument.Parse(payloadJson);
			if (document.RootElement.TryGetProperty("normalizedInput", out var normalizedElement))
			{
				return normalizedElement.GetString();
			}
		}
		catch (JsonException)
		{
			return fallback;
		}

		return fallback;
	}

	private static string ExtractAnagramLookupInput(string userInput)
	{
		var trimmed = userInput.Trim();
		if (trimmed.Length == 0)
		{
			return trimmed;
		}

		var quotedMatch = QuotedPhraseRegex.Match(trimmed);
		if (quotedMatch.Success)
		{
			var phrase = quotedMatch.Groups["phrase"].Value.Trim();
			if (phrase.Length > 0)
			{
				return phrase;
			}
		}

		if (FindCommandRegex.IsMatch(trimmed) || CountCommandRegex.IsMatch(trimmed))
		{
			var forIndex = trimmed.IndexOf(" for ", StringComparison.OrdinalIgnoreCase);
			if (forIndex >= 0)
			{
				var maybePhrase = trimmed[(forIndex + 5)..].Trim();
				if (maybePhrase.Length > 0)
				{
					return maybePhrase.Trim('"', '\'');
				}
			}
		}

		return trimmed;
	}
}

internal sealed class HandoffWorkflowExecutionHooks
{
	public Func<TriageContext, CancellationToken, Task<TriageDecision>>? TriageRunner { get; init; }
	public Func<SpecialistRequest, CancellationToken, Task<SpecialistResponse>>? AnagramSpecialistRunner { get; init; }
	public Func<SpecialistRequest, CancellationToken, Task<SpecialistResponse>>? WordAnalysisSpecialistRunner { get; init; }
}
