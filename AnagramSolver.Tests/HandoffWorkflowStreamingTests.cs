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

public class HandoffWorkflowStreamingTests
{
	[Fact]
	public async Task ExecuteAsync_ShouldEmitLabeledStreamEventsForEachRole()
	{
		var writer = new RecordingWriter();
		var workflow = CreateWorkflow(writer);

		await workflow.ExecuteAsync("find anagrams for listen");

		writer.Updates.Should().Contain(x => x.StartsWith("Triage:", StringComparison.Ordinal));
		writer.Updates.Should().Contain(x => x.StartsWith("AnagramSpecialist:", StringComparison.Ordinal));
	}

	[Fact]
	public async Task ExecuteAsync_ShouldEmitExactlyOneCompletionEventPerExecutedRole()
	{
		var writer = new RecordingWriter();
		var workflow = CreateWorkflow(writer);

		await workflow.ExecuteAsync("show top word frequency statistics");

		writer.Completions[HandoffStreamEvent.Triage].Should().Be(1);
		writer.Completions[HandoffStreamEvent.WordAnalysisSpecialist].Should().Be(1);
	}

	[Fact]
	public async Task ExecuteAsync_WhenStageTimeoutOccurs_ShouldReturnDeterministicFallback()
	{
		var writer = new RecordingWriter();
		var hooks = new HandoffWorkflowExecutionHooks
		{
			AnagramSpecialistRunner = (_, _) => Task.FromResult(new SpecialistResponse
			{
				CurrentRole = HandoffAgentRole.AnagramSpecialist,
				FinalMessage = string.Empty,
				HandBackToTriage = true
			})
		};

		var workflow = CreateWorkflow(
			writer,
			hooks,
			stageRunner: (_, _, timeoutCt) => Task.Delay(TimeSpan.FromMinutes(1), timeoutCt).ContinueWith(_ => string.Empty, timeoutCt));

		var result = await workflow.ExecuteAsync("find anagrams for listen");

		result.UsedFallback.Should().BeTrue();
		result.FinalMessage.Should().Contain("reliable specialist response");
		writer.Updates.Should().Contain(x => x.Contains("Streaming timed out", StringComparison.Ordinal));
	}

	[Fact]
	public async Task ExecuteAsync_WhenTriageThrows_ShouldStillEmitTriageCompletion()
	{
		var writer = new RecordingWriter();
		var hooks = new HandoffWorkflowExecutionHooks
		{
			TriageRunner = (_, _) => throw new InvalidOperationException("triage failed")
		};

		var workflow = CreateWorkflow(writer, hooks);

		await Assert.ThrowsAsync<InvalidOperationException>(() => workflow.ExecuteAsync("find anagrams for listen"));
		writer.Completions[HandoffStreamEvent.Triage].Should().Be(1);
	}

	[Fact]
	public async Task ExecuteAsync_WhenSpecialistThrows_ShouldStillEmitSpecialistCompletion()
	{
		var writer = new RecordingWriter();
		var hooks = new HandoffWorkflowExecutionHooks
		{
			AnagramSpecialistRunner = (_, _) => throw new InvalidOperationException("specialist failed")
		};

		var workflow = CreateWorkflow(writer, hooks);

		await Assert.ThrowsAsync<InvalidOperationException>(() => workflow.ExecuteAsync("find anagrams for listen"));
		writer.Completions[HandoffStreamEvent.Triage].Should().Be(1);
		writer.Completions[HandoffStreamEvent.AnagramSpecialist].Should().Be(1);
	}

	private static HandoffWorkflow CreateWorkflow(
		RecordingWriter writer,
		HandoffWorkflowExecutionHooks? hooks = null,
		Func<HandoffStreamEvent, string, CancellationToken, Task<string>>? stageRunner = null)
	{
		var factory = CreateFactory();
		return new HandoffWorkflow(
			factory,
			writer,
			new HandoffWorkflowOptions
			{
				StreamingEnabled = true,
				StreamingStageTimeoutSeconds = 1,
				MaxHandoffDepthPerTurn = 2,
				MaxPresentedItems = 10,
				RouteConfidenceThreshold = 0.7
			},
			hooks ?? new HandoffWorkflowExecutionHooks(),
			stageRunner);
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

	private sealed class RecordingWriter : IHandoffStreamWriter
	{
		public List<string> Updates { get; } = new();

		public Dictionary<HandoffStreamEvent, int> Completions { get; } = new()
		{
			{ HandoffStreamEvent.Triage, 0 },
			{ HandoffStreamEvent.AnagramSpecialist, 0 },
			{ HandoffStreamEvent.WordAnalysisSpecialist, 0 }
		};

		public Task WriteUpdateAsync(HandoffStreamEvent streamEvent, string text, CancellationToken ct = default)
		{
			Updates.Add($"{streamEvent}:{text}");
			return Task.CompletedTask;
		}

		public Task WriteCompletedAsync(HandoffStreamEvent streamEvent, CancellationToken ct = default)
		{
			Completions[streamEvent]++;
			return Task.CompletedTask;
		}
	}
}
