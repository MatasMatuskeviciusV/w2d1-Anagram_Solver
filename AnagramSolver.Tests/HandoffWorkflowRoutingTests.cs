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

public class HandoffWorkflowRoutingTests
{
	[Fact]
	public async Task RunTriageAsync_ShouldRouteAnagramIntentToAnagramSpecialist()
	{
		var workflow = CreateWorkflow();

		var decision = await workflow.RunTriageAsync(new TriageContext("find anagrams for listen", new HandoffConversationState()), CancellationToken.None);

		decision.Intent.Should().Be(HandoffIntent.AnagramSearch);
		decision.NextRole.Should().Be(HandoffAgentRole.AnagramSpecialist);
		decision.Confidence.Should().BeGreaterThan(0.8);
	}

	[Fact]
	public async Task RunTriageAsync_ShouldRouteStatisticsIntentToWordAnalysisSpecialist()
	{
		var workflow = CreateWorkflow();

		var decision = await workflow.RunTriageAsync(new TriageContext("show top word frequency statistics", new HandoffConversationState()), CancellationToken.None);

		decision.Intent.Should().Be(HandoffIntent.WordAnalysis);
		decision.NextRole.Should().Be(HandoffAgentRole.WordAnalysisSpecialist);
	}

	[Fact]
	public async Task ExecuteAsync_WhenConfidenceBelowThreshold_ShouldRequestClarification()
	{
		var workflow = CreateWorkflow(options: new HandoffWorkflowOptions
		{
			RouteConfidenceThreshold = 0.7,
			StreamingEnabled = false
		});

		var result = await workflow.ExecuteAsync("help me maybe");

		result.RoutedRole.Should().Be(HandoffAgentRole.Triage);
		result.State.PendingClarification.Should().NotBeNullOrWhiteSpace();
		result.FinalMessage.Should().Contain("anagram");
	}

	[Fact]
	public async Task ExecuteAsync_ShouldPreventSpecialistToSpecialistDirectRouting()
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
			AnagramSpecialistRunner = (_, _) => Task.FromResult(new SpecialistResponse
			{
				CurrentRole = HandoffAgentRole.AnagramSpecialist,
				FinalMessage = "rerouting to analysis",
				HandBackToTriage = false
			})
		};

		var workflow = CreateWorkflow(hooks: hooks);
		var result = await workflow.ExecuteAsync("find anagram and then analyze");

		result.FinalMessage.Should().Contain("handback marker was missing");
		result.State.ActiveRole.Should().Be(HandoffAgentRole.Triage);
		result.UsedFallback.Should().BeTrue();
	}

	[Fact]
	public async Task ExecuteAsync_WhenUnknownIntentRoutesToSpecialist_ShouldFailSafeToTriage()
	{
		var hooks = new HandoffWorkflowExecutionHooks
		{
			TriageRunner = (_, _) => Task.FromResult(new TriageDecision
			{
				Intent = HandoffIntent.Unknown,
				NextRole = HandoffAgentRole.AnagramSpecialist,
				Confidence = 0.99,
				RouteReason = "forced malformed routing"
			})
		};

		var workflow = CreateWorkflow(hooks: hooks);
		var result = await workflow.ExecuteAsync("anything");

		result.RoutedRole.Should().Be(HandoffAgentRole.Triage);
		result.Intent.Should().Be(HandoffIntent.Unknown);
		result.UsedFallback.Should().BeTrue();
		result.FinalMessage.Should().Contain("safe route");
	}

	private static HandoffWorkflow CreateWorkflow(
		HandoffWorkflowOptions? options = null,
		HandoffWorkflowExecutionHooks? hooks = null)
	{
		var factory = CreateFactory();
		return new HandoffWorkflow(
			factory,
			new RecordingStreamWriter(),
			options ?? new HandoffWorkflowOptions { StreamingEnabled = false },
			hooks ?? new HandoffWorkflowExecutionHooks(),
			streamStageRunner: null);
	}

	private static HandoffWorkflowAgentFactory CreateFactory()
	{
		var chatClient = new Mock<IChatClient>().Object;
		var anagramSolver = new Mock<IAnagramSolver>();
		anagramSolver
			.Setup(x => x.GetAnagramsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(new List<string> { "silent", "enlist" });

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

	private sealed class RecordingStreamWriter : IHandoffStreamWriter
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
