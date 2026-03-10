using AnagramMsAgentFramework.Console;
using AnagramMsAgentFramework.Console.Workflows.GroupChat;
using AnagramMsAgentFramework.Console.Workflows.GroupChat.Streaming;
using AnagramSolver.BusinessLogic;
using AnagramSolver.Contracts;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Moq;

namespace AnagramSolver.Tests;

public class GroupChatWorkflowStreamingTests
{
	[Fact]
	public async Task ExecuteAsync_ShouldEmitLabeledUpdatesForEachExecutedRole()
	{
		var writer = new RecordingWriter();
		var workflow = CreateWorkflow(writer);

		await workflow.ExecuteAsync("play");

		writer.Updates.Should().Contain(x => x.StartsWith("Orchestrator:", StringComparison.Ordinal));
		writer.Updates.Should().Contain(x => x.StartsWith("FirstPlayer:", StringComparison.Ordinal));
		writer.Updates.Should().Contain(x => x.StartsWith("SecondPlayer:", StringComparison.Ordinal));
		writer.Updates.Should().Contain(x => x.StartsWith("Reviewer:", StringComparison.Ordinal));
	}

	[Fact]
	public async Task ExecuteAsync_ShouldEmitExactlyOneCompletionPerExecutedRole()
	{
		var writer = new RecordingWriter();
		var workflow = CreateWorkflow(writer);

		await workflow.ExecuteAsync("play");

		writer.Completions[GroupChatStreamEvent.Orchestrator].Should().BeGreaterThanOrEqualTo(1);
		writer.Completions[GroupChatStreamEvent.FirstPlayer].Should().Be(1);
		writer.Completions[GroupChatStreamEvent.SecondPlayer].Should().Be(1);
		writer.Completions[GroupChatStreamEvent.Reviewer].Should().Be(1);
	}

	[Fact]
	public async Task ExecuteAsync_WhenStageTimeoutOccurs_ShouldEmitWarningAndReturnDeterministicResult()
	{
		var writer = new RecordingWriter();
		var workflow = CreateWorkflow(
			writer,
			hooks: new GroupChatWorkflowExecutionHooks(),
			stageRunner: (_, _, timeoutCt) => Task.Delay(TimeSpan.FromMinutes(1), timeoutCt).ContinueWith(_ => string.Empty, timeoutCt));

		var result = await workflow.ExecuteAsync("play");

		result.UsedFallback.Should().BeTrue();
		writer.Updates.Should().Contain(x => x.Contains("Streaming timed out", StringComparison.Ordinal));
	}

	private static GroupChatWorkflow CreateWorkflow(
		RecordingWriter writer,
		GroupChatWorkflowExecutionHooks? hooks = null,
		Func<GroupChatStreamEvent, string, CancellationToken, Task<string>>? stageRunner = null)
	{
		var factory = CreateFactory();
		return new GroupChatWorkflow(
			factory,
			writer,
			new GroupChatWorkflowOptions
			{
				StreamingEnabled = true,
				StreamingStageTimeoutSeconds = 1,
				MaxRoleHopsPerTurn = 4,
				MaxRoundsPerGame = 1,
				MinWordLength = 4
			},
			hooks ?? new GroupChatWorkflowExecutionHooks(),
			stageRunner);
	}

	private static GroupChatWorkflowAgentFactory CreateFactory()
	{
		var chatClient = new Mock<IChatClient>().Object;
		var anagramSolver = new Mock<IAnagramSolver>();
		anagramSolver
			.Setup(x => x.GetAnagramsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((string key, CancellationToken _) => key switch
			{
				"eilnst" => new List<string> { "silent", "enlist", "listen" },
				_ => new List<string>()
			});

		var repository = new Mock<IWordRepository>();
		repository
			.Setup(x => x.GetAllWordsAsync(It.IsAny<CancellationToken>()))
			.ReturnsAsync(new[] { "listen", "stone", "tones" }.AsEnumerable());

		var frequencyAnalyzer = new Mock<IWordFrequencyAnalyzer>();
		frequencyAnalyzer
			.Setup(x => x.Analyze(It.IsAny<string>(), It.IsAny<int>()))
			.Returns(new WordFrequencyAnalysisResult { TopWords = Array.Empty<FrequentWordResult>() });

		var tools = new AnagramTools(
			anagramSolver.Object,
			repository.Object,
			new UserProcessor(2),
			new WordNormalizer(),
			frequencyAnalyzer.Object);

		return new GroupChatWorkflowAgentFactory(chatClient, tools);
	}

	private sealed class RecordingWriter : IGroupChatStreamWriter
	{
		public List<string> Updates { get; } = new();

		public Dictionary<GroupChatStreamEvent, int> Completions { get; } = new()
		{
			{ GroupChatStreamEvent.Orchestrator, 0 },
			{ GroupChatStreamEvent.FirstPlayer, 0 },
			{ GroupChatStreamEvent.SecondPlayer, 0 },
			{ GroupChatStreamEvent.Reviewer, 0 }
		};

		public Task WriteUpdateAsync(GroupChatStreamEvent streamEvent, string text, CancellationToken ct = default)
		{
			Updates.Add($"{streamEvent}:{text}");
			return Task.CompletedTask;
		}

		public Task WriteCompletedAsync(GroupChatStreamEvent streamEvent, CancellationToken ct = default)
		{
			Completions[streamEvent]++;
			return Task.CompletedTask;
		}
	}
}
