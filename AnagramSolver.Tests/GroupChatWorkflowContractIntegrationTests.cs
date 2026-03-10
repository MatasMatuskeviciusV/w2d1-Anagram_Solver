using AnagramMsAgentFramework.Console;
using AnagramMsAgentFramework.Console.Workflows.GroupChat;
using AnagramMsAgentFramework.Console.Workflows.GroupChat.Models;
using AnagramMsAgentFramework.Console.Workflows.GroupChat.Streaming;
using AnagramSolver.BusinessLogic;
using AnagramSolver.Contracts;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Moq;
using System.Text.Json;

namespace AnagramSolver.Tests;

public class GroupChatWorkflowContractIntegrationTests
{
	[Fact]
	public async Task StageRuns_ShouldUseConfiguredPerRoleModelOverrides()
	{
		var factory = CreateFactory();
		var workflow = new GroupChatWorkflow(
			factory,
			new NoOpWriter(),
			new GroupChatWorkflowOptions
			{
				StreamingEnabled = false,
				OrchestratorModel = "orchestrator-model",
				FirstPlayerModel = "first-model",
				SecondPlayerModel = "second-model",
				ReviewerModel = "reviewer-model"
			});

		_ = await workflow.RunOrchestratorAsync(new OrchestratorContext("play", new GroupChatConversationState()), CancellationToken.None);
		_ = await workflow.RunFirstPlayerAsync(new FirstPlayerRequest("play", new GroupChatConversationState()), CancellationToken.None);
		_ = await workflow.RunSecondPlayerAsync(new SecondPlayerRequest("silent", "play", new GroupChatConversationState()), CancellationToken.None);
		_ = await workflow.RunReviewerAsync(new ReviewerRequest("listen", "listen", "play", new GroupChatConversationState()), CancellationToken.None);

		factory.LastOrchestratorModelRequested.Should().Be("orchestrator-model");
		factory.LastFirstPlayerModelRequested.Should().Be("first-model");
		factory.LastSecondPlayerModelRequested.Should().Be("second-model");
		factory.LastReviewerModelRequested.Should().Be("reviewer-model");
	}

	[Fact]
	public void ReviewerPayloadParse_AcceptsObjectAndArrayVariants()
	{
		const string objectPayload = "{\"isCorrect\":true,\"finalMessage\":\"ok\"}";
		const string arrayPayload = "[{\"verdict\":\"incorrect\",\"finalMessage\":\"no\"}]";

		var parsedObject = GroupChatWorkflow.TryParseReviewerResult(objectPayload, out var objectResult);
		var parsedArray = GroupChatWorkflow.TryParseReviewerResult(arrayPayload, out var arrayResult);

		parsedObject.Should().BeTrue();
		objectResult.IsGuessCorrect.Should().BeTrue();
		parsedArray.Should().BeTrue();
		arrayResult.IsGuessCorrect.Should().BeFalse();
	}

	[Fact]
	public async Task ExecuteAsync_HappyPath_ShouldCompleteAllStages()
	{
		var workflow = CreateWorkflow();

		var result = await workflow.ExecuteAsync("play");

		result.RoutedRole.Should().Be(GroupChatAgentRole.FirstPlayer);
		result.State.ActiveRole.Should().Be(GroupChatAgentRole.Completed);
		result.State.IsGuessCorrect.Should().NotBeNull();
	}

	[Fact]
	public async Task ExecuteAsync_IncorrectGuessFlow_ShouldReportIncorrectVerdict()
	{
		var hooks = new GroupChatWorkflowExecutionHooks
		{
			FirstPlayerRunner = (_, _) => Task.FromResult(new FirstPlayerTurnResult("listen", "silent", false.ToString(), false)),
			SecondPlayerRunner = (_, _) => Task.FromResult(new SecondPlayerTurnResult("enlist", "guess", false)),
			ReviewerRunner = (request, _) => Task.FromResult(new ReviewerTurnResult(
				request.SecretWord.Equals(request.Guess, StringComparison.OrdinalIgnoreCase),
				"wrong",
				false))
		};
		var workflow = CreateWorkflow(hooks: hooks);

		var result = await workflow.ExecuteAsync("play");

		result.State.IsGuessCorrect.Should().BeFalse();
		result.FinalMessage.Should().Contain("wrong");
	}

	[Fact]
	public async Task ExecuteAsync_WhenCanceledDuringRoleExecution_ShouldPropagateAndPreserveState()
	{
		var hooks = new GroupChatWorkflowExecutionHooks
		{
			FirstPlayerRunner = async (_, ct) =>
			{
				await Task.Delay(TimeSpan.FromMilliseconds(100), ct);
				return new FirstPlayerTurnResult("listen", "silent", "ok", false);
			}
		};
		var workflow = CreateWorkflow(hooks: hooks);
		using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(10));

		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => workflow.ExecuteAsync("play", cts.Token));

		var recovered = await workflow.ExecuteAsync("play");
		recovered.State.TurnNumber.Should().Be(1);
		recovered.State.ActiveRole.Should().Be(GroupChatAgentRole.Completed);
	}

	[Fact]
	public void SecondPlayerPayloadParse_ShouldRequireStrictJsonObject()
	{
		const string validPayload = "{\"guess\":\"listen\"}";

		var parsedValid = GroupChatWorkflow.TryParseSecondPlayerGuess(validPayload, out var validGuess);
		var parsedPlainToken = GroupChatWorkflow.TryParseSecondPlayerGuess("listen", out _);
		var parsedArrayPayload = GroupChatWorkflow.TryParseSecondPlayerGuess("[{\"guess\":\"listen\"}]", out _);

		parsedValid.Should().BeTrue();
		validGuess.Should().Be("listen");
		parsedPlainToken.Should().BeFalse();
		parsedArrayPayload.Should().BeFalse();
	}

	[Fact]
	public async Task ExecuteAsync_WhenReviewerOutputConflictsWithDeterministicCheck_ShouldUseFallbackVerdict()
	{
		var hooks = new GroupChatWorkflowExecutionHooks
		{
			FirstPlayerRunner = (_, _) => Task.FromResult(new FirstPlayerTurnResult("listen", "silent", "ok", false)),
			SecondPlayerRunner = (_, _) => Task.FromResult(new SecondPlayerTurnResult("listen", "ok", false)),
			ReviewerRawResponseRunner = (_, _) => Task.FromResult<string?>("{\"notValid\":true}")
		};
		var workflow = CreateWorkflow(hooks: hooks);

		var result = await workflow.ExecuteAsync("play");

		result.UsedFallback.Should().BeTrue();
		result.State.IsGuessCorrect.Should().BeTrue();
		result.FinalMessage.Should().Contain("Reviewer verdict: correct");
	}

	[Fact]
	public void BuildReviewerPayloadJson_ShouldEscapeSpecialCharacters()
	{
		const string secretWord = "li\"sten\nword";
		const string guess = "en\\list\tguess";

		var payload = GroupChatWorkflow.BuildReviewerPayloadJson(secretWord, guess);

		using var doc = JsonDocument.Parse(payload);
		doc.RootElement.GetProperty("secretWord").GetString().Should().Be(secretWord);
		doc.RootElement.GetProperty("guess").GetString().Should().Be(guess);
	}

	[Fact]
	public void ComputeDeterministicStartIndex_WhenHashIsIntMinValue_ShouldReturnNonNegativeIndex()
	{
		var index = GroupChatWorkflow.ComputeDeterministicStartIndex("play", 3, _ => int.MinValue);

		index.Should().Be(2);
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
