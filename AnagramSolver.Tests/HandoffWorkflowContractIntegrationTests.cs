using System.Text.Json;
using AnagramMsAgentFramework.Console;
using AnagramMsAgentFramework.Console.Workflows.Handoff;
using AnagramMsAgentFramework.Console.Workflows.Handoff.Models;
using AnagramMsAgentFramework.Console.Workflows.Handoff.Streaming;
using AnagramSolver.BusinessLogic;
using AnagramSolver.Contracts;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Moq;

namespace AnagramSolver.Tests;

public class HandoffWorkflowContractIntegrationTests
{
	[Fact]
	public async Task StageRuns_ShouldUseConfiguredPerRoleModelOverrides()
	{
		var factory = CreateFactory();
		var workflow = new HandoffWorkflow(
			factory,
			new NoOpWriter(),
			new HandoffWorkflowOptions
			{
				StreamingEnabled = false,
				TriageModel = "triage-model",
				AnagramSpecialistModel = "anagram-model",
				WordAnalysisSpecialistModel = "analysis-model"
			});

		_ = await workflow.RunTriageAsync(new TriageContext("find anagrams for listen", new HandoffConversationState()), CancellationToken.None);
		_ = await workflow.RunAnagramSpecialistAsync(new SpecialistRequest("find anagrams for listen", HandoffIntent.AnagramSearch, new HandoffConversationState()), CancellationToken.None);
		_ = await workflow.RunWordAnalysisSpecialistAsync(new SpecialistRequest("show top words", HandoffIntent.WordAnalysis, new HandoffConversationState()), CancellationToken.None);

		factory.LastTriageModelRequested.Should().Be("triage-model");
		factory.LastAnagramSpecialistModelRequested.Should().Be("anagram-model");
		factory.LastWordAnalysisSpecialistModelRequested.Should().Be("analysis-model");
	}

	[Fact]
	public async Task RunAnagramSpecialistAsync_ShouldLimitStructuredPayloadToMaxPresentedItems()
	{
		var factory = CreateFactory(new[] { "silent", "enlist", "tinsel", "inlets" });
		var workflow = new HandoffWorkflow(
			factory,
			new NoOpWriter(),
			new HandoffWorkflowOptions
			{
				StreamingEnabled = false,
				MaxPresentedItems = 2
			});

		var response = await workflow.RunAnagramSpecialistAsync(
			new SpecialistRequest("listen", HandoffIntent.AnagramSearch, new HandoffConversationState()),
			CancellationToken.None);

		var payload = JsonSerializer.Deserialize<AnagramSpecialistPayload>(
			response.StructuredPayloadJson!,
			new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

		payload.Should().NotBeNull();
		payload!.TopAnagrams.Count.Should().Be(2);
		response.HandBackToTriage.Should().BeTrue();
	}

	[Fact]
	public async Task ExecuteAsync_ShouldHandlePlannedThreeTurnScenario()
	{
		var workflow = CreateWorkflow();

		var turn1 = await workflow.ExecuteAsync("find anagrams for listen");
		var turn2 = await workflow.ExecuteAsync("show word frequency statistics for this text");
		var turn3 = await workflow.ExecuteAsync("can you help me with this");

		turn1.RoutedRole.Should().Be(HandoffAgentRole.AnagramSpecialist);
		turn2.RoutedRole.Should().Be(HandoffAgentRole.WordAnalysisSpecialist);
		turn3.RoutedRole.Should().Be(HandoffAgentRole.Triage);
		turn3.State.PendingClarification.Should().NotBeNullOrWhiteSpace();
		turn1.State.TurnNumber.Should().Be(1);
		turn2.State.TurnNumber.Should().Be(2);
		turn3.State.TurnNumber.Should().Be(3);
	}

	[Fact]
	public async Task ExecuteAsync_WhenCanceledDuringSpecialist_ShouldNotCorruptConversationState()
	{
		var hooks = new HandoffWorkflowExecutionHooks
		{
			TriageRunner = (_, _) => Task.FromResult(new TriageDecision
			{
				Intent = HandoffIntent.AnagramSearch,
				NextRole = HandoffAgentRole.AnagramSpecialist,
				Confidence = 0.99,
				RouteReason = "forced"
			}),
			AnagramSpecialistRunner = async (_, ct) =>
			{
				await Task.Delay(TimeSpan.FromMilliseconds(100), ct);
				return new SpecialistResponse
				{
					CurrentRole = HandoffAgentRole.AnagramSpecialist,
					FinalMessage = "ok",
					HandBackToTriage = true
				};
			}
		};

		var workflow = CreateWorkflow(hooks: hooks);
		using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(10));

		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => workflow.ExecuteAsync("find anagrams for listen", cts.Token));

		var recovered = await workflow.ExecuteAsync("find anagrams for listen");
		recovered.State.TurnNumber.Should().Be(1);
		recovered.State.ActiveRole.Should().Be(HandoffAgentRole.Triage);
	}

	private static HandoffWorkflow CreateWorkflow(
		HandoffWorkflowOptions? options = null,
		HandoffWorkflowExecutionHooks? hooks = null)
	{
		var factory = CreateFactory();
		return new HandoffWorkflow(
			factory,
			new NoOpWriter(),
			options ?? new HandoffWorkflowOptions { StreamingEnabled = false },
			hooks ?? new HandoffWorkflowExecutionHooks(),
			streamStageRunner: null);
	}

	private static HandoffWorkflowAgentFactory CreateFactory(IReadOnlyList<string>? anagrams = null)
	{
		var chatClient = new Mock<IChatClient>().Object;
		var anagramSolver = new Mock<IAnagramSolver>();
		anagramSolver
			.Setup(x => x.GetAnagramsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((anagrams ?? new[] { "silent", "enlist" }).ToList());

		var wordRepository = new Mock<IWordRepository>().Object;
		var frequencyAnalyzer = new Mock<IWordFrequencyAnalyzer>();
		frequencyAnalyzer
			.Setup(x => x.Analyze(It.IsAny<string>(), It.IsAny<int>()))
			.Returns(new WordFrequencyAnalysisResult
			{
				TopWords = new[] { new FrequentWordResult { Word = "alpha", Count = 2 } },
				TotalWordCount = 2,
				UniqueWordCount = 1,
				LongestWord = "alpha"
			});

		var tools = new AnagramTools(
			anagramSolver.Object,
			wordRepository,
			new UserProcessor(2),
			new WordNormalizer(),
			frequencyAnalyzer.Object);

		return new HandoffWorkflowAgentFactory(chatClient, tools);
	}

	private sealed class NoOpWriter : IHandoffStreamWriter
	{
		public Task WriteUpdateAsync(HandoffStreamEvent streamEvent, string text, CancellationToken ct = default)
		{
			return Task.CompletedTask;
		}

		public Task WriteCompletedAsync(HandoffStreamEvent streamEvent, CancellationToken ct = default)
		{
			return Task.CompletedTask;
		}
	}
}
