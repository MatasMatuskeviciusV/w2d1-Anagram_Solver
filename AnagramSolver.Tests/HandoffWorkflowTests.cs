using AnagramMsAgentFramework.Console;
using AnagramMsAgentFramework.Console.Workflows.Handoff;
using AnagramMsAgentFramework.Console.Workflows.Handoff.Streaming;
using AnagramSolver.BusinessLogic;
using AnagramSolver.Contracts;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Moq;

namespace AnagramSolver.Tests;

public class HandoffWorkflowTests
{
	[Fact]
	public async Task ExecuteAsync_ShouldReturnToTriageAfterSpecialistResponse()
	{
		var workflow = CreateWorkflow();

		var result = await workflow.ExecuteAsync("find anagrams for listen");

		result.RoutedRole.Should().Be(HandoffAgentRole.AnagramSpecialist);
		result.State.ActiveRole.Should().Be(HandoffAgentRole.Triage);
		result.State.TurnNumber.Should().Be(1);
	}

	[Fact]
	public async Task ExecuteAsync_ShouldIncrementTurnCountAndUpdateLastIntent()
	{
		var workflow = CreateWorkflow();

		await workflow.ExecuteAsync("find anagrams for listen");
		var second = await workflow.ExecuteAsync("show word frequency statistics for this text");

		second.State.TurnNumber.Should().Be(2);
		second.State.LastIntent.Should().Be(HandoffIntent.WordAnalysis);
	}

	[Fact]
	public async Task ExecuteAsync_WhenMaxDepthIsTooLow_ShouldReturnSafeFallback()
	{
		var workflow = CreateWorkflow(options: new HandoffWorkflowOptions
		{
			MaxHandoffDepthPerTurn = 1,
			StreamingEnabled = false,
			MaxPresentedItems = 10,
			RouteConfidenceThreshold = 0.7
		});

		var result = await workflow.ExecuteAsync("find anagrams for listen");

		result.UsedFallback.Should().BeTrue();
		result.FinalMessage.Should().Contain("Max handoff depth");
	}

	[Fact]
	public async Task ResetAsync_ShouldClearPendingClarificationAndState()
	{
		var workflow = CreateWorkflow();

		var first = await workflow.ExecuteAsync("hello there");
		first.State.PendingClarification.Should().NotBeNullOrWhiteSpace();

		await workflow.ResetAsync();
		var second = await workflow.ExecuteAsync("find anagrams for listen");

		second.State.PendingClarification.Should().BeNull();
		second.State.TurnNumber.Should().Be(1);
	}

	[Fact]
	public async Task ExecuteAsync_AnagramCommandWithForClause_ShouldExtractLookupPhrase()
	{
		var workflow = CreateWorkflow();

		var result = await workflow.ExecuteAsync("find anagrams for 'visma praktika'");

		result.RoutedRole.Should().Be(HandoffAgentRole.AnagramSpecialist);
		result.FinalMessage.ToLowerInvariant().Should().NotContain("invalid or too short");
	}

	private static HandoffWorkflow CreateWorkflow(HandoffWorkflowOptions? options = null)
	{
		var factory = CreateFactory();
		return new HandoffWorkflow(
			factory,
			new NoOpWriter(),
			options ?? new HandoffWorkflowOptions { StreamingEnabled = false });
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
			new UserProcessor(4),
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
