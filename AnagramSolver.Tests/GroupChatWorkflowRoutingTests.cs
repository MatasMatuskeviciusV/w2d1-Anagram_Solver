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

public class GroupChatWorkflowRoutingTests
{
	[Fact]
	public async Task ExecuteAsync_WhenOrchestratorReturnsInvalidRole_ShouldUseSafeFallback()
	{
		var hooks = new GroupChatWorkflowExecutionHooks
		{
			OrchestratorRunner = (_, _) => Task.FromResult(new OrchestratorDecision
			{
				NextRole = GroupChatAgentRole.Completed,
				Reason = "invalid",
				Confidence = 0.8
			})
		};
		var workflow = CreateWorkflow(hooks: hooks);

		var result = await workflow.ExecuteAsync("play");

		result.UsedFallback.Should().BeTrue();
		result.RoutedRole.Should().Be(GroupChatAgentRole.FirstPlayer);
		result.FinalMessage.Should().Contain("Reviewer verdict:");
	}

	[Fact]
	public async Task ExecuteAsync_ShouldAdvanceRolesToCompletion()
	{
		var workflow = CreateWorkflow();

		var result = await workflow.ExecuteAsync("play");

		result.RoutedRole.Should().Be(GroupChatAgentRole.FirstPlayer);
		result.State.ActiveRole.Should().Be(GroupChatAgentRole.Completed);
		result.State.TurnNumber.Should().Be(1);
		result.State.SecretWord.Should().NotBeNullOrWhiteSpace();
		result.State.ProducedAnagram.Should().NotBeNullOrWhiteSpace();
		result.State.LatestGuess.Should().NotBeNullOrWhiteSpace();
	}

	[Fact]
	public async Task ExecuteAsync_WhenOrchestratorReroutesAfterFirstStage_ShouldHonorReroute()
	{
		var secondPlayerCalled = false;
		var hooks = new GroupChatWorkflowExecutionHooks
		{
			OrchestratorRunner = (context, _) =>
			{
				var nextRole = string.IsNullOrWhiteSpace(context.State.ProducedAnagram)
					? GroupChatAgentRole.FirstPlayer
					: GroupChatAgentRole.Reviewer;

				return Task.FromResult(new OrchestratorDecision
				{
					NextRole = nextRole,
					Reason = "test reroute",
					Confidence = 0.9
				});
			},
			FirstPlayerRunner = (_, _) => Task.FromResult(new FirstPlayerTurnResult("listen", "silent", "ok", false)),
			SecondPlayerRunner = (_, _) =>
			{
				secondPlayerCalled = true;
				return Task.FromResult(new SecondPlayerTurnResult("listen", "ok", false));
			}
		};
		var workflow = CreateWorkflow(hooks: hooks);

		var result = await workflow.ExecuteAsync("play");

		result.UsedFallback.Should().BeTrue();
		result.RoutedRole.Should().Be(GroupChatAgentRole.FirstPlayer);
		result.FinalMessage.Should().Contain("Reviewer verdict:");
		secondPlayerCalled.Should().BeTrue();
	}

	[Fact]
	public async Task ExecuteAsync_WhenOrchestratorChoosesFirstPlayerAfterAnagramExists_ShouldRerouteToSecondPlayer()
	{
		var secondPlayerCalled = false;
		var hooks = new GroupChatWorkflowExecutionHooks
		{
			OrchestratorRunner = (_, _) => Task.FromResult(new OrchestratorDecision
			{
				NextRole = GroupChatAgentRole.FirstPlayer,
				Reason = "invalid loop choice",
				Confidence = 0.9
			}),
			FirstPlayerRunner = (_, _) => Task.FromResult(new FirstPlayerTurnResult("listen", "silent", "ok", false)),
			SecondPlayerRunner = (_, _) =>
			{
				secondPlayerCalled = true;
				return Task.FromResult(new SecondPlayerTurnResult("listen", "guess", false));
			}
		};
		var workflow = CreateWorkflow(hooks: hooks);

		var result = await workflow.ExecuteAsync("play");

		secondPlayerCalled.Should().BeTrue();
		result.FinalMessage.Should().Contain("Reviewer verdict:");
		result.UsedFallback.Should().BeTrue();
	}

	[Fact]
	public async Task ExecuteAsync_WhenOrchestratorChoosesReviewerBeforeGuess_ShouldRerouteDeterministically()
	{
		var firstPlayerCalled = false;
		var hooks = new GroupChatWorkflowExecutionHooks
		{
			OrchestratorRunner = (_, _) => Task.FromResult(new OrchestratorDecision
			{
				NextRole = GroupChatAgentRole.Reviewer,
				Reason = "invalid early reviewer",
				Confidence = 0.9
			}),
			FirstPlayerRunner = (_, _) =>
			{
				firstPlayerCalled = true;
				return Task.FromResult(new FirstPlayerTurnResult("listen", "silent", "ok", false));
			}
		};
		var workflow = CreateWorkflow(hooks: hooks);

		var result = await workflow.ExecuteAsync("play");

		firstPlayerCalled.Should().BeTrue();
		result.FinalMessage.Should().NotContain("Reviewer stage requires both");
		result.UsedFallback.Should().BeTrue();
	}

	[Fact]
	public void OrchestratorJsonParse_AcceptsValidPayload()
	{
		const string payload = "{\"nextRole\":\"FirstPlayer\",\"reason\":\"start\",\"confidence\":0.9}";

		var parsed = GroupChatWorkflow.TryParseOrchestratorDecision(payload, out var decision);

		parsed.Should().BeTrue();
		decision.NextRole.Should().Be(GroupChatAgentRole.FirstPlayer);
		decision.Reason.Should().Be("start");
	}

	[Fact]
	public void OrchestratorJsonParse_RejectsMalformedPayload()
	{
		const string payload = "{\"nextRole\":\"BadRole\",\"reason\":\"x\",\"confidence\":2}";

		var parsed = GroupChatWorkflow.TryParseOrchestratorDecision(payload, out _);

		parsed.Should().BeFalse();
	}

	private static GroupChatWorkflow CreateWorkflow(GroupChatWorkflowExecutionHooks? hooks = null)
	{
		var factory = CreateFactory();
		return new GroupChatWorkflow(
			factory,
			new NoOpWriter(),
			new GroupChatWorkflowOptions
			{
				StreamingEnabled = false,
				MaxRoleHopsPerTurn = 4,
				MaxRoundsPerGame = 1,
				MinWordLength = 4
			},
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
