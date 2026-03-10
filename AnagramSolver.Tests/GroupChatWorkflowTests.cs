using AnagramMsAgentFramework.Console;
using AnagramMsAgentFramework.Console.Workflows.GroupChat;
using AnagramMsAgentFramework.Console.Workflows.GroupChat.Models;
using AnagramMsAgentFramework.Console.Workflows.GroupChat.Streaming;
using AnagramSolver.BusinessLogic;
using AnagramSolver.Contracts;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Moq;

namespace AnagramSolver.Tests;

public class GroupChatWorkflowTests
{
	[Fact]
	public async Task ExecuteAsync_WhenInputIsEmpty_ShouldReturnValidationMessage()
	{
		var workflow = CreateWorkflow();

		var result = await workflow.ExecuteAsync("   ");

		result.UsedFallback.Should().BeTrue();
		result.RoutedRole.Should().Be(GroupChatAgentRole.Orchestrator);
		result.FinalMessage.Should().Contain("Input cannot be empty");
	}

	[Fact]
	public async Task FirstPlayer_WhenOutputIsNotSingleWord_ShouldFailSafe()
	{
		var hooks = new GroupChatWorkflowExecutionHooks
		{
			FirstPlayerRunner = (_, _) => Task.FromResult(new FirstPlayerTurnResult("listen", "two words", "invalid", false))
		};
		var workflow = CreateWorkflow(hooks: hooks);

		var result = await workflow.ExecuteAsync("start game");

		result.UsedFallback.Should().BeTrue();
		result.FinalMessage.Should().Contain("First player could not produce");
	}

	[Fact]
	public async Task FirstPlayer_WhenOutputIsNotAnagram_ShouldFailSafe()
	{
		var hooks = new GroupChatWorkflowExecutionHooks
		{
			FirstPlayerRunner = (_, _) => Task.FromResult(new FirstPlayerTurnResult("listen", "banana", "invalid", false))
		};
		var workflow = CreateWorkflow(hooks: hooks);

		var result = await workflow.ExecuteAsync("start game");

		result.UsedFallback.Should().BeTrue();
		result.FinalMessage.Should().Contain("First player could not produce");
	}

	[Fact]
	public async Task Reviewer_WhenGuessMatchesSecret_ShouldReturnCorrect()
	{
		var hooks = new GroupChatWorkflowExecutionHooks
		{
			FirstPlayerRunner = (_, _) => Task.FromResult(new FirstPlayerTurnResult("listen", "silent", "ok", false)),
			SecondPlayerRunner = (_, _) => Task.FromResult(new SecondPlayerTurnResult("listen", "ok", false)),
			ReviewerRunner = (request, _) => Task.FromResult(new ReviewerTurnResult(
				string.Equals(request.SecretWord, request.Guess, StringComparison.OrdinalIgnoreCase),
				"correct",
				false))
		};
		var workflow = CreateWorkflow(hooks: hooks);

		var result = await workflow.ExecuteAsync("play");

		result.State.IsGuessCorrect.Should().BeTrue();
		result.FinalMessage.Should().Contain("correct");
	}

	[Fact]
	public async Task Reviewer_WhenGuessDoesNotMatchSecret_ShouldReturnIncorrect()
	{
		var hooks = new GroupChatWorkflowExecutionHooks
		{
			FirstPlayerRunner = (_, _) => Task.FromResult(new FirstPlayerTurnResult("listen", "silent", "ok", false)),
			SecondPlayerRunner = (_, _) => Task.FromResult(new SecondPlayerTurnResult("enlist", "ok", false)),
			ReviewerRunner = (request, _) => Task.FromResult(new ReviewerTurnResult(
				string.Equals(request.SecretWord, request.Guess, StringComparison.OrdinalIgnoreCase),
				"incorrect",
				false))
		};
		var workflow = CreateWorkflow(hooks: hooks);

		var result = await workflow.ExecuteAsync("play");

		result.State.IsGuessCorrect.Should().BeFalse();
		result.FinalMessage.Should().Contain("incorrect");
	}

	[Fact]
	public async Task ExecuteAsync_WhenMaxRoleHopsExceeded_ShouldStopWithFallback()
	{
		var workflow = CreateWorkflow(options: new GroupChatWorkflowOptions
		{
			StreamingEnabled = false,
			MaxRoleHopsPerTurn = 1,
			MaxRoundsPerGame = 1,
			MinWordLength = 4
		});

		var result = await workflow.ExecuteAsync("play");

		result.UsedFallback.Should().BeTrue();
		result.FinalMessage.Should().Contain("Max role hops exceeded");
	}

	[Fact]
	public async Task ResetAsync_ShouldClearGameState()
	{
		var workflow = CreateWorkflow();

		var beforeReset = await workflow.ExecuteAsync("play");
		beforeReset.State.TurnNumber.Should().Be(1);

		await workflow.ResetAsync();
		var afterReset = await workflow.ExecuteAsync("play again");

		afterReset.State.TurnNumber.Should().Be(1);
		afterReset.State.CurrentRound.Should().Be(1);
	}

	[Fact]
	public async Task ExecuteAsync_WhenMaxRoundsReached_ShouldStopWithDeterministicMessage()
	{
		var workflow = CreateWorkflow(options: new GroupChatWorkflowOptions
		{
			StreamingEnabled = false,
			MaxRoleHopsPerTurn = 4,
			MaxRoundsPerGame = 1,
			MinWordLength = 4
		});

		var firstTurn = await workflow.ExecuteAsync("play");
		var secondTurn = await workflow.ExecuteAsync("play again");

		firstTurn.State.CurrentRound.Should().Be(1);
		secondTurn.UsedFallback.Should().BeTrue();
		secondTurn.RoutedRole.Should().Be(GroupChatAgentRole.Completed);
		secondTurn.FinalMessage.Should().Contain("Max rounds reached");
		secondTurn.State.TurnNumber.Should().Be(firstTurn.State.TurnNumber);
	}

	private static GroupChatWorkflow CreateWorkflow(
		GroupChatWorkflowOptions? options = null,
		GroupChatWorkflowExecutionHooks? hooks = null)
	{
		var factory = CreateFactory();
		return new GroupChatWorkflow(
			factory,
			new NoOpWriter(),
			options ?? new GroupChatWorkflowOptions { StreamingEnabled = false, MaxRoleHopsPerTurn = 4, MaxRoundsPerGame = 1, MinWordLength = 4 },
			hooks ?? new GroupChatWorkflowExecutionHooks(),
			streamStageRunner: null);
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

	private sealed class NoOpWriter : IGroupChatStreamWriter
	{
		public Task WriteUpdateAsync(GroupChatStreamEvent streamEvent, string text, CancellationToken ct = default)
		{
			return Task.CompletedTask;
		}

		public Task WriteCompletedAsync(GroupChatStreamEvent streamEvent, CancellationToken ct = default)
		{
			return Task.CompletedTask;
		}
	}
}
