using System.Text.Json;
using AnagramMsAgentFramework.Console.Workflows.GroupChat.Models;
using AnagramMsAgentFramework.Console.Workflows.GroupChat.Streaming;
using Microsoft.Agents.AI;

namespace AnagramMsAgentFramework.Console.Workflows.GroupChat;

public sealed class GroupChatWorkflow : IGroupChatWorkflow
{
	private readonly GroupChatWorkflowAgentFactory _agentFactory;
	private readonly IGroupChatStreamWriter _streamWriter;
	private readonly GroupChatWorkflowOptions _options;
	private readonly GroupChatWorkflowExecutionHooks _executionHooks;
	private readonly Func<GroupChatStreamEvent, string, CancellationToken, Task<string>>? _streamStageRunner;
	private readonly object _stateGate = new();

	private GroupChatConversationState _state = new()
	{
		ActiveRole = GroupChatAgentRole.Orchestrator
	};

	public GroupChatWorkflow(
		GroupChatWorkflowAgentFactory agentFactory,
		IGroupChatStreamWriter streamWriter,
		GroupChatWorkflowOptions options)
		: this(agentFactory, streamWriter, options, new GroupChatWorkflowExecutionHooks(), null)
	{
	}

	internal GroupChatWorkflow(
		GroupChatWorkflowAgentFactory agentFactory,
		IGroupChatStreamWriter streamWriter,
		GroupChatWorkflowOptions options,
		GroupChatWorkflowExecutionHooks executionHooks,
		Func<GroupChatStreamEvent, string, CancellationToken, Task<string>>? streamStageRunner)
	{
		_agentFactory = agentFactory ?? throw new ArgumentNullException(nameof(agentFactory));
		_streamWriter = streamWriter ?? throw new ArgumentNullException(nameof(streamWriter));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_executionHooks = executionHooks ?? throw new ArgumentNullException(nameof(executionHooks));
		_streamStageRunner = streamStageRunner;
	}

	public async Task<GroupChatTurnResult> ExecuteAsync(string userInput, CancellationToken ct = default)
	{
		if (string.IsNullOrWhiteSpace(userInput))
		{
			var unchanged = GetStateSnapshot();
			return new GroupChatTurnResult(
				FinalMessage: "Input cannot be empty. Please provide a request.",
				RoutedRole: GroupChatAgentRole.Orchestrator,
				State: unchanged,
				UsedFallback: true);
		}

		ct.ThrowIfCancellationRequested();
		var snapshot = GetStateSnapshot();
		var maxRounds = Math.Max(1, _options.MaxRoundsPerGame);
		if (snapshot.CurrentRound >= maxRounds)
		{
			return new GroupChatTurnResult(
				FinalMessage: "Max rounds reached for this game. Type 'reset' to start a new round.",
				RoutedRole: GroupChatAgentRole.Completed,
				State: snapshot with { ActiveRole = GroupChatAgentRole.Completed },
				UsedFallback: true);
		}

		var turnState = snapshot with
		{
			ActiveRole = GroupChatAgentRole.Orchestrator,
			SecretWord = null,
			ProducedAnagram = null,
			LatestGuess = null,
			IsGuessCorrect = null
		};

		var routedRole = GroupChatAgentRole.Orchestrator;
		var activeRole = GroupChatAgentRole.Orchestrator;
		var workingState = turnState with { ActiveRole = activeRole };
		var usedFallback = false;
		string finalMessage = "The turn completed without a final message.";
		var maxHops = Math.Max(1, _options.MaxRoleHopsPerTurn);

		for (var hop = 1; hop <= maxHops; hop++)
		{
			ct.ThrowIfCancellationRequested();

			if (activeRole == GroupChatAgentRole.Orchestrator)
			{
				var route = await SelectNextRoleAsync(userInput.Trim(), workingState, ct);
				usedFallback |= route.UsedFallback;
				if (hop == 1)
				{
					routedRole = route.NextRole;
				}

				if (route.NextRole is GroupChatAgentRole.Orchestrator or GroupChatAgentRole.Completed)
				{
					return CompleteTurn(
						state: workingState with { ActiveRole = GroupChatAgentRole.Completed },
						message: "I could not determine a safe role route for this turn.",
						routedRole: routedRole,
						usedFallback: true);
				}

				activeRole = route.NextRole;
				workingState = workingState with { ActiveRole = activeRole };
				hop--;
				continue;
			}

			switch (activeRole)
			{
				case GroupChatAgentRole.FirstPlayer:
				{
					var first = await RunFirstPlayerAsync(new FirstPlayerRequest(userInput.Trim(), workingState), ct);
					usedFallback |= first.UsedFallback;
					if (!ValidateFirstPlayerOutput(first.SecretWord, first.ProducedAnagram, out _))
					{
						return CompleteTurn(
							state: workingState with { ActiveRole = GroupChatAgentRole.Completed },
							message: "First player could not produce a valid one-word anagram.",
							routedRole: routedRole,
							usedFallback: true);
					}

					workingState = workingState with
					{
						ActiveRole = GroupChatAgentRole.Orchestrator,
						SecretWord = first.SecretWord,
						ProducedAnagram = first.ProducedAnagram
					};
					finalMessage = first.Message;
					activeRole = GroupChatAgentRole.Orchestrator;
					break;
				}

				case GroupChatAgentRole.SecondPlayer:
				{
					if (string.IsNullOrWhiteSpace(workingState.ProducedAnagram))
					{
						return CompleteTurn(
							state: workingState with { ActiveRole = GroupChatAgentRole.Completed },
							message: "Second player stage requires a generated anagram token.",
							routedRole: routedRole,
							usedFallback: true);
					}

					var second = await RunSecondPlayerAsync(new SecondPlayerRequest(workingState.ProducedAnagram, userInput.Trim(), workingState), ct);
					usedFallback |= second.UsedFallback;
					if (string.IsNullOrWhiteSpace(second.Guess))
					{
						return CompleteTurn(
							state: workingState with { ActiveRole = GroupChatAgentRole.Completed },
							message: "Second player did not provide a valid guess.",
							routedRole: routedRole,
							usedFallback: true);
					}

					workingState = workingState with
					{
						ActiveRole = GroupChatAgentRole.Orchestrator,
						LatestGuess = second.Guess
					};
					finalMessage = second.Message;
					activeRole = GroupChatAgentRole.Orchestrator;
					break;
				}

				case GroupChatAgentRole.Reviewer:
				{
					if (string.IsNullOrWhiteSpace(workingState.SecretWord) || string.IsNullOrWhiteSpace(workingState.LatestGuess))
					{
						return CompleteTurn(
							state: workingState with { ActiveRole = GroupChatAgentRole.Completed },
							message: "Reviewer stage requires both a secret word and a guess.",
							routedRole: routedRole,
							usedFallback: true);
					}

					var reviewer = await RunReviewerAsync(
						new ReviewerRequest(workingState.SecretWord, workingState.LatestGuess, userInput.Trim(), workingState),
						ct);
					usedFallback |= reviewer.UsedFallback;
					workingState = workingState with
					{
						ActiveRole = GroupChatAgentRole.Completed,
						IsGuessCorrect = reviewer.IsGuessCorrect,
						CurrentRound = Math.Min(maxRounds, snapshot.CurrentRound + 1)
					};
					finalMessage = reviewer.FinalMessage;
					activeRole = GroupChatAgentRole.Completed;
					break;
				}

				case GroupChatAgentRole.Completed:
					return CompleteTurn(workingState, finalMessage, routedRole, usedFallback);

				default:
					return CompleteTurn(
						state: workingState with { ActiveRole = GroupChatAgentRole.Completed },
						message: "An unsupported role was selected for this turn.",
						routedRole: routedRole,
						usedFallback: true);
			}
		}

		return CompleteTurn(
			state: workingState with { ActiveRole = GroupChatAgentRole.Completed },
			message: "Max role hops exceeded for this turn.",
			routedRole: routedRole,
			usedFallback: true);
	}

	private async Task<(GroupChatAgentRole NextRole, bool UsedFallback)> SelectNextRoleAsync(
		string userInput,
		GroupChatConversationState state,
		CancellationToken ct)
	{
		var orchestratorDecision = await RunOrchestratorAsync(new OrchestratorContext(userInput, state), ct);
		if (!IsMalformedOrchestratorDecision(orchestratorDecision) &&
			IsContextValidRoleChoice(orchestratorDecision.NextRole, state))
		{
			return (orchestratorDecision.NextRole, false);
		}

		var fallbackDecision = DetermineOrchestratorDecision(state);
		if (!IsMalformedOrchestratorDecision(fallbackDecision))
		{
			return (fallbackDecision.NextRole, true);
		}

		return (GroupChatAgentRole.Orchestrator, true);
	}

	private static bool IsContextValidRoleChoice(GroupChatAgentRole role, GroupChatConversationState state)
	{
		return role switch
		{
			GroupChatAgentRole.FirstPlayer => string.IsNullOrWhiteSpace(state.ProducedAnagram),
			GroupChatAgentRole.SecondPlayer =>
				!string.IsNullOrWhiteSpace(state.ProducedAnagram) &&
				string.IsNullOrWhiteSpace(state.LatestGuess),
			GroupChatAgentRole.Reviewer =>
				!string.IsNullOrWhiteSpace(state.SecretWord) &&
				!string.IsNullOrWhiteSpace(state.LatestGuess) &&
				!state.IsGuessCorrect.HasValue,
			_ => false
		};
	}

	public Task ResetAsync(CancellationToken ct = default)
	{
		ct.ThrowIfCancellationRequested();
		lock (_stateGate)
		{
			_state = new GroupChatConversationState
			{
				ActiveRole = GroupChatAgentRole.Orchestrator
			};
		}

		return Task.CompletedTask;
	}

	internal async Task<OrchestratorDecision> RunOrchestratorAsync(OrchestratorContext context, CancellationToken ct)
	{
		ct.ThrowIfCancellationRequested();
		await _streamWriter.WriteUpdateAsync(GroupChatStreamEvent.Orchestrator, "Selecting next role.", ct);
		try
		{
			await StreamStageWithGuardAsync(GroupChatStreamEvent.Orchestrator, context.UserInput, ct);
			if (_executionHooks.OrchestratorRunner is not null)
			{
				return await _executionHooks.OrchestratorRunner(context, ct);
			}

			return await RunOrchestratorByAgentWithFallbackAsync(context, ct);
		}
		finally
		{
			await _streamWriter.WriteCompletedAsync(GroupChatStreamEvent.Orchestrator, ct);
		}
	}

	internal async Task<FirstPlayerTurnResult> RunFirstPlayerAsync(FirstPlayerRequest request, CancellationToken ct)
	{
		ct.ThrowIfCancellationRequested();
		await _streamWriter.WriteUpdateAsync(GroupChatStreamEvent.FirstPlayer, "Generating one-word anagram.", ct);
		try
		{
			await StreamStageWithGuardAsync(GroupChatStreamEvent.FirstPlayer, request.UserInput, ct);
			if (_executionHooks.FirstPlayerRunner is not null)
			{
				return await _executionHooks.FirstPlayerRunner(request, ct);
			}

			return await RunFirstPlayerByAgentWithFallbackAsync(request, ct);
		}
		finally
		{
			await _streamWriter.WriteCompletedAsync(GroupChatStreamEvent.FirstPlayer, ct);
		}
	}

	internal async Task<SecondPlayerTurnResult> RunSecondPlayerAsync(SecondPlayerRequest request, CancellationToken ct)
	{
		ct.ThrowIfCancellationRequested();
		await _streamWriter.WriteUpdateAsync(GroupChatStreamEvent.SecondPlayer, "Guessing the original word.", ct);
		try
		{
			await StreamStageWithGuardAsync(GroupChatStreamEvent.SecondPlayer, request.AnagramToken, ct);
			if (_executionHooks.SecondPlayerRunner is not null)
			{
				return await _executionHooks.SecondPlayerRunner(request, ct);
			}

			return await RunSecondPlayerByAgentWithFallbackAsync(request, ct);
		}
		finally
		{
			await _streamWriter.WriteCompletedAsync(GroupChatStreamEvent.SecondPlayer, ct);
		}
	}

	internal async Task<ReviewerTurnResult> RunReviewerAsync(ReviewerRequest request, CancellationToken ct)
	{
		ct.ThrowIfCancellationRequested();
		await _streamWriter.WriteUpdateAsync(GroupChatStreamEvent.Reviewer, "Verifying guess correctness.", ct);
		try
		{
			await StreamStageWithGuardAsync(GroupChatStreamEvent.Reviewer, request.Guess, ct);
			if (_executionHooks.ReviewerRunner is not null)
			{
				return await _executionHooks.ReviewerRunner(request, ct);
			}

			return await RunReviewerByAgentWithFallbackAsync(request, ct);
		}
		finally
		{
			await _streamWriter.WriteCompletedAsync(GroupChatStreamEvent.Reviewer, ct);
		}
	}

	private async Task<OrchestratorDecision> RunOrchestratorByAgentWithFallbackAsync(OrchestratorContext context, CancellationToken ct)
	{
		ct.ThrowIfCancellationRequested();
		var fallback = DetermineOrchestratorDecision(context.State);
		try
		{
			var orchestrator = _agentFactory.CreateOrchestratorAgent(_options.OrchestratorModel);
			var responseText = await RunAgentStageWithTimeoutAsync(
				GroupChatStreamEvent.Orchestrator,
				async timeoutCt =>
				{
					timeoutCt.ThrowIfCancellationRequested();
					var session = await orchestrator.CreateSessionAsync();
					var response = await orchestrator.RunAsync(BuildOrchestratorPrompt(context), session);
					timeoutCt.ThrowIfCancellationRequested();
					return response?.ToString();
				},
				ct);

			if (string.IsNullOrWhiteSpace(responseText))
			{
				return fallback with
				{
					Reason = $"{fallback.Reason} (fallback: timed out or unavailable orchestrator output)"
				};
			}

			if (TryParseOrchestratorDecision(responseText, out var parsed) && !IsMalformedOrchestratorDecision(parsed))
			{
				return parsed;
			}
		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception)
		{
			// Deterministic fallback keeps role routing safe.
		}

		return fallback with
		{
			Reason = $"{fallback.Reason} (fallback: malformed or unavailable orchestrator output)"
		};
	}

	private async Task<FirstPlayerTurnResult> RunFirstPlayerByAgentWithFallbackAsync(FirstPlayerRequest request, CancellationToken ct)
	{
		ct.ThrowIfCancellationRequested();
		var fallback = await BuildDeterministicFirstPlayerResultAsync(request.UserInput, ct);
		try
		{
			var firstPlayer = _agentFactory.CreateFirstPlayerAgent(_options.FirstPlayerModel);
			var responseText = await RunAgentStageWithTimeoutAsync(
				GroupChatStreamEvent.FirstPlayer,
				async timeoutCt =>
				{
					timeoutCt.ThrowIfCancellationRequested();
					var session = await firstPlayer.CreateSessionAsync();
					var response = await firstPlayer.RunAsync(
						BuildFirstPlayerPrompt(request.UserInput, fallback.SecretWord ?? string.Empty),
						session);
					timeoutCt.ThrowIfCancellationRequested();
					return response?.ToString();
				},
				ct);

			if (string.IsNullOrWhiteSpace(responseText))
			{
				return fallback;
			}

			if (TryParseFirstPlayerAnagram(responseText, out var candidateAnagram) &&
				ValidateFirstPlayerOutput(fallback.SecretWord, candidateAnagram, out _))
			{
				return fallback with
				{
					ProducedAnagram = candidateAnagram,
					Message = $"First player produced an anagram token: {candidateAnagram}.",
					UsedFallback = false
				};
			}
		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception)
		{
			// Deterministic fallback keeps the game consistent.
		}

		return fallback;
	}

	private async Task<SecondPlayerTurnResult> RunSecondPlayerByAgentWithFallbackAsync(SecondPlayerRequest request, CancellationToken ct)
	{
		ct.ThrowIfCancellationRequested();
		var fallback = await BuildDeterministicSecondPlayerResultAsync(request.AnagramToken, ct);
		try
		{
			var secondPlayer = _agentFactory.CreateSecondPlayerAgent(_options.SecondPlayerModel);
			var responseText = await RunAgentStageWithTimeoutAsync(
				GroupChatStreamEvent.SecondPlayer,
				async timeoutCt =>
				{
					timeoutCt.ThrowIfCancellationRequested();
					var session = await secondPlayer.CreateSessionAsync();
					var response = await secondPlayer.RunAsync(BuildSecondPlayerPrompt(request.AnagramToken), session);
					timeoutCt.ThrowIfCancellationRequested();
					return response?.ToString();
				},
				ct);

			if (string.IsNullOrWhiteSpace(responseText))
			{
				return fallback;
			}

			if (TryParseSecondPlayerGuess(responseText, out var guess) && IsSingleWordToken(guess))
			{
				return fallback with
				{
					Guess = guess,
					Message = $"Second player guess: {guess}.",
					UsedFallback = false
				};
			}
		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception)
		{
			// Deterministic fallback keeps the game consistent.
		}

		return fallback;
	}

	private async Task<ReviewerTurnResult> RunReviewerByAgentWithFallbackAsync(ReviewerRequest request, CancellationToken ct)
	{
		ct.ThrowIfCancellationRequested();
		var expected = IsDeterministicMatch(request.SecretWord, request.Guess);
		var fallback = BuildDeterministicReviewerResult(request.SecretWord, request.Guess, expected, usedFallback: true);
		try
		{
			string? responseText;
			if (_executionHooks.ReviewerRawResponseRunner is not null)
			{
				responseText = await _executionHooks.ReviewerRawResponseRunner(request, ct);
			}
			else
			{
				var reviewer = _agentFactory.CreateReviewerAgent(_options.ReviewerModel);
				responseText = await RunAgentStageWithTimeoutAsync(
					GroupChatStreamEvent.Reviewer,
					async timeoutCt =>
					{
						timeoutCt.ThrowIfCancellationRequested();
						var session = await reviewer.CreateSessionAsync();
						var response = await reviewer.RunAsync(BuildReviewerPrompt(request.SecretWord, request.Guess), session);
						timeoutCt.ThrowIfCancellationRequested();
						return response?.ToString();
					},
					ct);
			}

			if (string.IsNullOrWhiteSpace(responseText))
			{
				return fallback;
			}

			if (!TryParseReviewerResult(responseText, out var parsed))
			{
				return fallback;
			}

			if (parsed.IsGuessCorrect != expected)
			{
				return BuildDeterministicReviewerResult(request.SecretWord, request.Guess, expected, usedFallback: true);
			}

			return BuildDeterministicReviewerResult(
				request.SecretWord,
				request.Guess,
				expected,
				usedFallback: string.IsNullOrWhiteSpace(parsed.FinalMessage));
		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception)
		{
			return fallback;
		}
	}

	private async Task<FirstPlayerTurnResult> BuildDeterministicFirstPlayerResultAsync(string userInput, CancellationToken ct)
	{
		var minLength = Math.Max(2, _options.MinWordLength);
		var candidates = await _agentFactory.GetDictionaryWordsAsync(minLength, maxItems: 1000, ct);
		if (candidates.Count == 0)
		{
			return new FirstPlayerTurnResult(
				SecretWord: null,
				ProducedAnagram: null,
				Message: "No dictionary candidates were available for the first player.",
				UsedFallback: true);
		}

		var startIndex = ComputeDeterministicStartIndex(userInput, candidates.Count);
		for (var i = 0; i < candidates.Count; i++)
		{
			var secretWord = candidates[(startIndex + i) % candidates.Count];
			var finderResult = await _agentFactory.FindAnagramsStructuredAsync(secretWord, ct);
			if (!finderResult.IsValid || finderResult.Anagrams.Count == 0)
			{
				continue;
			}

			var anagram = finderResult.Anagrams.FirstOrDefault(x => ValidateFirstPlayerOutput(secretWord, x, out _));
			if (string.IsNullOrWhiteSpace(anagram))
			{
				continue;
			}

			return new FirstPlayerTurnResult(
				SecretWord: secretWord,
				ProducedAnagram: anagram,
				Message: $"First player produced an anagram token: {anagram}.",
				UsedFallback: true);
		}

		return new FirstPlayerTurnResult(
			SecretWord: null,
			ProducedAnagram: null,
			Message: "No valid one-word anagram could be produced from dictionary candidates.",
			UsedFallback: true);
	}

	private async Task<SecondPlayerTurnResult> BuildDeterministicSecondPlayerResultAsync(string anagramToken, CancellationToken ct)
	{
		var finder = await _agentFactory.FindAnagramsStructuredAsync(anagramToken, ct);
		var guess = finder.Anagrams.FirstOrDefault(IsSingleWordToken);
		if (string.IsNullOrWhiteSpace(guess))
		{
			guess = anagramToken;
		}

		return new SecondPlayerTurnResult(
			Guess: guess,
			Message: $"Second player guess: {guess}.",
			UsedFallback: true);
	}

	private static ReviewerTurnResult BuildDeterministicReviewerResult(string secretWord, string guess, bool isCorrect, bool usedFallback)
	{
		var finalMessage = isCorrect
			? $"Reviewer verdict: correct. Guess '{guess}' matches secret word '{secretWord}'."
			: $"Reviewer verdict: incorrect. Guess '{guess}' does not match secret word '{secretWord}'.";

		return new ReviewerTurnResult(
			IsGuessCorrect: isCorrect,
			FinalMessage: finalMessage,
			UsedFallback: usedFallback);
	}

	private GroupChatTurnResult CompleteTurn(
		GroupChatConversationState state,
		string message,
		GroupChatAgentRole routedRole,
		bool usedFallback)
	{
		GroupChatConversationState updated;
		lock (_stateGate)
		{
			updated = state with
			{
				TurnNumber = _state.TurnNumber + 1,
				CurrentRound = state.CurrentRound == 0 ? _state.CurrentRound : state.CurrentRound
			};
			_state = updated;
		}

		return new GroupChatTurnResult(message, routedRole, updated, usedFallback);
	}

	private GroupChatConversationState GetStateSnapshot()
	{
		lock (_stateGate)
		{
			return _state;
		}
	}

	private static OrchestratorDecision DetermineOrchestratorDecision(GroupChatConversationState state)
	{
		if (string.IsNullOrWhiteSpace(state.ProducedAnagram))
		{
			return new OrchestratorDecision
			{
				NextRole = GroupChatAgentRole.FirstPlayer,
				Reason = "No anagram has been produced yet.",
				Confidence = 0.95
			};
		}

		if (string.IsNullOrWhiteSpace(state.LatestGuess))
		{
			return new OrchestratorDecision
			{
				NextRole = GroupChatAgentRole.SecondPlayer,
				Reason = "Anagram exists but no guess is present.",
				Confidence = 0.95
			};
		}

		if (!state.IsGuessCorrect.HasValue)
		{
			return new OrchestratorDecision
			{
				NextRole = GroupChatAgentRole.Reviewer,
				Reason = "Guess exists and requires deterministic review.",
				Confidence = 0.95
			};
		}

		return new OrchestratorDecision
		{
			NextRole = GroupChatAgentRole.Completed,
			Reason = "Turn state already has a final verdict.",
			Confidence = 1.0
		};
	}

	internal static bool IsMalformedOrchestratorDecision(OrchestratorDecision decision)
	{
		if (decision is null)
		{
			return true;
		}

		if (string.IsNullOrWhiteSpace(decision.Reason))
		{
			return true;
		}

		if (decision.Confidence is < 0 or > 1)
		{
			return true;
		}

		return decision.NextRole is GroupChatAgentRole.Orchestrator or GroupChatAgentRole.Completed;
	}

	private static bool ValidateFirstPlayerOutput(string? secretWord, string? producedAnagram, out string error)
	{
		error = string.Empty;
		if (string.IsNullOrWhiteSpace(secretWord) || string.IsNullOrWhiteSpace(producedAnagram))
		{
			error = "Secret word and produced anagram are required.";
			return false;
		}

		if (!IsSingleWordToken(producedAnagram))
		{
			error = "Produced anagram must be a single word token.";
			return false;
		}

		if (string.Equals(secretWord, producedAnagram, StringComparison.OrdinalIgnoreCase))
		{
			error = "Produced anagram cannot equal the secret word.";
			return false;
		}

		if (!HaveSameCharacterSignature(secretWord, producedAnagram))
		{
			error = "Produced token is not a true anagram of the secret word.";
			return false;
		}

		return true;
	}

	private static bool IsDeterministicMatch(string secretWord, string guess)
	{
		return string.Equals(
			NormalizeToken(secretWord),
			NormalizeToken(guess),
			StringComparison.Ordinal);
	}

	private static bool HaveSameCharacterSignature(string left, string right)
	{
		var l = NormalizeToken(left).ToCharArray();
		var r = NormalizeToken(right).ToCharArray();
		if (l.Length == 0 || l.Length != r.Length)
		{
			return false;
		}

		Array.Sort(l);
		Array.Sort(r);
		return l.SequenceEqual(r);
	}

	private static string NormalizeToken(string value)
	{
		return new string(value
			.Where(char.IsLetterOrDigit)
			.Select(char.ToLowerInvariant)
			.ToArray());
	}

	private static bool IsSingleWordToken(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return false;
		}

		var token = value.Trim();
		if (token.Contains(' ') || token.Contains('\t'))
		{
			return false;
		}

		return token.All(char.IsLetterOrDigit);
	}

	private static string BuildOrchestratorPrompt(OrchestratorContext context)
	{
		var state = context.State;
		var hasSecretWord = !string.IsNullOrWhiteSpace(state.SecretWord);
		var hasProducedAnagram = !string.IsNullOrWhiteSpace(state.ProducedAnagram);
		var hasLatestGuess = !string.IsNullOrWhiteSpace(state.LatestGuess);
		var hasFinalVerdict = state.IsGuessCorrect.HasValue;

		return
			"You are the Group Chat orchestrator. Return ONLY JSON with properties: nextRole, reason, confidence. " +
			"nextRole values: FirstPlayer, SecondPlayer, Reviewer. confidence must be a number between 0 and 1. " +
			"Choose a role that is valid for the provided turn state.\n" +
			$"User input: {context.UserInput}\n" +
			$"State: hasSecretWord={hasSecretWord}, hasProducedAnagram={hasProducedAnagram}, hasLatestGuess={hasLatestGuess}, hasFinalVerdict={hasFinalVerdict}.\n" +
			"Role prerequisites: FirstPlayer when no anagram exists; SecondPlayer when anagram exists and no guess exists; Reviewer when both secret word and guess exist without a final verdict.";
	}

	private static string BuildFirstPlayerPrompt(string userInput, string secretWord)
	{
		return
			"You are FirstPlayer. Return ONLY JSON with property anagram. " +
			"It must be exactly one token, must be a true anagram of the secret word, and must not equal the secret word.\n" +
			$"User input: {userInput}\n" +
			$"Secret word: {secretWord}";
	}

	private static string BuildSecondPlayerPrompt(string anagramToken)
	{
		return
			"You are SecondPlayer. Return ONLY JSON with property guess containing one word. " +
			"Do not output the secret word because it is unknown to you.\n" +
			$"Anagram token: {anagramToken}";
	}

	private static string BuildReviewerPrompt(string secretWord, string guess)
	{
		var payloadJson = BuildReviewerPayloadJson(secretWord, guess);
		return
			"You are Reviewer. Return ONLY JSON with properties isCorrect and finalMessage.\n" +
			$"Payload: {payloadJson}";
	}

	internal static string BuildReviewerPayloadJson(string secretWord, string guess)
	{
		return JsonSerializer.Serialize(new
		{
			secretWord,
			guess
		});
	}

	internal static int ComputeDeterministicStartIndex(string userInput, int candidateCount, Func<string, int>? hashProvider = null)
	{
		if (candidateCount <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(candidateCount), "Candidate count must be greater than zero.");
		}

		var provider = hashProvider ?? StringComparer.Ordinal.GetHashCode;
		var hash = unchecked((uint)provider(userInput));
		return (int)(hash % (uint)candidateCount);
	}

	internal static bool TryParseOrchestratorDecision(string? rawResponse, out OrchestratorDecision decision)
	{
		decision = new OrchestratorDecision();
		if (string.IsNullOrWhiteSpace(rawResponse))
		{
			return false;
		}

		if (!TryParseJson(rawResponse, out var document) || document is null)
		{
			return false;
		}

		using (document)
		{
			if (document.RootElement.ValueKind != JsonValueKind.Object)
			{
				return false;
			}

			var root = document.RootElement;
			if (!TryReadString(root, "reason", out var reason) ||
				!TryReadDouble(root, "confidence", out var confidence) ||
				!TryReadRole(root, "nextRole", out var role))
			{
				return false;
			}

			decision = new OrchestratorDecision
			{
				NextRole = role,
				Reason = reason!,
				Confidence = confidence
			};
			return true;
		}
	}

	private static bool TryParseFirstPlayerAnagram(string? rawResponse, out string anagram)
	{
		anagram = string.Empty;
		if (string.IsNullOrWhiteSpace(rawResponse))
		{
			return false;
		}

		if (TryParseJson(rawResponse, out var document) && document is not null)
		{
			using (document)
			{
				if (document.RootElement.ValueKind == JsonValueKind.Object &&
					TryReadString(document.RootElement, "anagram", out var jsonAnagram) &&
					!string.IsNullOrWhiteSpace(jsonAnagram))
				{
					anagram = jsonAnagram;
					return true;
				}
			}
		}

		var token = rawResponse.Trim();
		if (!IsSingleWordToken(token))
		{
			return false;
		}

		anagram = token;
		return true;
	}

	internal static bool TryParseSecondPlayerGuess(string? rawResponse, out string guess)
	{
		guess = string.Empty;
		if (string.IsNullOrWhiteSpace(rawResponse))
		{
			return false;
		}

		if (!TryParseJson(rawResponse, out var document) || document is null)
		{
			return false;
		}

		using (document)
		{
			if (document.RootElement.ValueKind == JsonValueKind.Object &&
				TryReadString(document.RootElement, "guess", out var jsonGuess) &&
				!string.IsNullOrWhiteSpace(jsonGuess) &&
				IsSingleWordToken(jsonGuess))
			{
				guess = jsonGuess;
				return true;
			}
		}

		return false;
	}

	internal static bool TryParseReviewerResult(string? rawResponse, out ReviewerTurnResult result)
	{
		result = new ReviewerTurnResult(false, string.Empty, true);
		if (string.IsNullOrWhiteSpace(rawResponse))
		{
			return false;
		}

		if (!TryParseJson(rawResponse, out var document) || document is null)
		{
			return false;
		}

		using (document)
		{
			if (TryReadReviewerPayload(document.RootElement, out var parsed))
			{
				result = parsed;
				return true;
			}
		}

		return false;
	}

	private static bool TryReadReviewerPayload(JsonElement element, out ReviewerTurnResult result)
	{
		result = new ReviewerTurnResult(false, string.Empty, true);

		if (element.ValueKind == JsonValueKind.Object)
		{
			if (!TryReadReviewerObject(element, out result))
			{
				return false;
			}

			return true;
		}

		if (element.ValueKind == JsonValueKind.Array)
		{
			foreach (var item in element.EnumerateArray())
			{
				if (item.ValueKind == JsonValueKind.Object && TryReadReviewerObject(item, out result))
				{
					return true;
				}
			}
		}

		return false;
	}

	private static bool TryReadReviewerObject(JsonElement root, out ReviewerTurnResult result)
	{
		result = new ReviewerTurnResult(false, string.Empty, true);
		if (!TryReadBoolean(root, "isCorrect", out var isCorrect))
		{
			if (!TryReadString(root, "verdict", out var verdict))
			{
				return false;
			}

			isCorrect = verdict!.Trim().Equals("correct", StringComparison.OrdinalIgnoreCase);
		}

		if (!TryReadString(root, "finalMessage", out var finalMessage))
		{
			finalMessage = string.Empty;
		}

		result = new ReviewerTurnResult(isCorrect, finalMessage!, false);
		return true;
	}

	private static bool TryParseJson(string rawResponse, out JsonDocument? document)
	{
		document = null;
		var json = ExtractJsonPayload(rawResponse);
		if (json is null)
		{
			return false;
		}

		try
		{
			document = JsonDocument.Parse(json);
			return true;
		}
		catch (JsonException)
		{
			return false;
		}
	}

	private static string? ExtractJsonPayload(string rawResponse)
	{
		var objectStart = rawResponse.IndexOf('{');
		var objectEnd = rawResponse.LastIndexOf('}');
		var arrayStart = rawResponse.IndexOf('[');
		var arrayEnd = rawResponse.LastIndexOf(']');

		var objectCandidate = objectStart >= 0 && objectEnd > objectStart
			? rawResponse[objectStart..(objectEnd + 1)]
			: null;
		var arrayCandidate = arrayStart >= 0 && arrayEnd > arrayStart
			? rawResponse[arrayStart..(arrayEnd + 1)]
			: null;

		if (objectCandidate is null)
		{
			return arrayCandidate;
		}

		if (arrayCandidate is null)
		{
			return objectCandidate;
		}

		return objectCandidate.Length >= arrayCandidate.Length ? objectCandidate : arrayCandidate;
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

	private static bool TryReadRole(JsonElement root, string propertyName, out GroupChatAgentRole role)
	{
		role = GroupChatAgentRole.Orchestrator;
		if (!TryReadString(root, propertyName, out var rawRole) || rawRole is null)
		{
			return false;
		}

		var parsed = rawRole.Trim().ToLowerInvariant() switch
		{
			"firstplayer" or "first-player" => GroupChatAgentRole.FirstPlayer,
			"secondplayer" or "second-player" => GroupChatAgentRole.SecondPlayer,
			"reviewer" => GroupChatAgentRole.Reviewer,
			"completed" => GroupChatAgentRole.Completed,
			_ => GroupChatAgentRole.Orchestrator
		};

		if (parsed is GroupChatAgentRole.Orchestrator)
		{
			return false;
		}

		role = parsed;
		return true;
	}

	private async Task<string> StreamStageWithGuardAsync(GroupChatStreamEvent stage, string prompt, CancellationToken ct)
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

	private async Task<string?> RunAgentStageWithTimeoutAsync(
		GroupChatStreamEvent stage,
		Func<CancellationToken, Task<string?>> stageCall,
		CancellationToken ct)
	{
		var timeoutSeconds = Math.Max(1, _options.StreamingStageTimeoutSeconds);
		using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
		timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

		var stageTask = stageCall(timeoutCts.Token);
		var timeoutTask = Task.Delay(Timeout.InfiniteTimeSpan, timeoutCts.Token);
		var completedTask = await Task.WhenAny(stageTask, timeoutTask);
		if (completedTask == stageTask)
		{
			return await stageTask;
		}

		ct.ThrowIfCancellationRequested();
		ObserveTimedOutStageTask(stageTask);
		await _streamWriter.WriteUpdateAsync(
			stage,
			$"Stage timed out after {timeoutSeconds}s. Continuing with deterministic workflow output.",
			ct);
		return null;
	}

	private static void ObserveTimedOutStageTask(Task<string?> stageTask)
	{
		_ = stageTask.ContinueWith(
			static task =>
			{
				_ = task.Exception;
			},
			CancellationToken.None,
			TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
			TaskScheduler.Default);
	}
}

internal sealed class GroupChatWorkflowExecutionHooks
{
	public Func<OrchestratorContext, CancellationToken, Task<OrchestratorDecision>>? OrchestratorRunner { get; init; }
	public Func<FirstPlayerRequest, CancellationToken, Task<FirstPlayerTurnResult>>? FirstPlayerRunner { get; init; }
	public Func<SecondPlayerRequest, CancellationToken, Task<SecondPlayerTurnResult>>? SecondPlayerRunner { get; init; }
	public Func<ReviewerRequest, CancellationToken, Task<ReviewerTurnResult>>? ReviewerRunner { get; init; }
	public Func<ReviewerRequest, CancellationToken, Task<string?>>? ReviewerRawResponseRunner { get; init; }
}

internal sealed record OrchestratorContext(string UserInput, GroupChatConversationState State);
internal sealed record FirstPlayerRequest(string UserInput, GroupChatConversationState State);
internal sealed record SecondPlayerRequest(string AnagramToken, string UserInput, GroupChatConversationState State);
internal sealed record ReviewerRequest(string SecretWord, string Guess, string UserInput, GroupChatConversationState State);
