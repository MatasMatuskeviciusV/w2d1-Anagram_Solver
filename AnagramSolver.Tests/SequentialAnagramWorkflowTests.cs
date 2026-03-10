using AnagramMsAgentFramework.Console.Workflows.SequentialAnagram;
using AnagramMsAgentFramework.Console.Workflows.SequentialAnagram.Models;
using AnagramMsAgentFramework.Console.Workflows.SequentialAnagram.Streaming;
using AnagramMsAgentFramework.Console;
using AnagramSolver.BusinessLogic;
using AnagramSolver.Contracts;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Moq;

namespace AnagramSolver.Tests;

public class SequentialAnagramWorkflowTests
{
	[Fact]
	public async Task ExecuteAsync_WhenInputIsEmpty_ShouldReturnValidationMessage()
	{
		var chatClient = new Mock<IChatClient>().Object;
		var anagramSolver = new Mock<IAnagramSolver>().Object;
		var wordRepository = new Mock<IWordRepository>().Object;
		var wordFrequencyAnalyzer = new Mock<IWordFrequencyAnalyzer>().Object;
		var tools = new AnagramTools(
			anagramSolver,
			wordRepository,
			new UserProcessor(2),
			new WordNormalizer(),
			wordFrequencyAnalyzer);
		var factory = new SequentialWorkflowAgentFactory(chatClient, tools);

		var workflow = new SequentialAnagramWorkflow(
			agentFactory: factory,
			streamWriter: new NoOpWorkflowStreamWriter());

		var result = await workflow.ExecuteAsync("   ");

		result.FinalMessage.Should().Be("Input cannot be empty. Please provide a word or phrase.");
	}

	[Fact]
	public async Task ExecuteAsync_ShouldRunStagesInStrictOrder()
	{
		var writer = new RecordingWorkflowStreamWriter();
		var hooks = new SequentialAnagramWorkflowExecutionHooks
		{
			FinderStageRunner = async (_, _) =>
			{
				await writer.WriteUpdateAsync(WorkflowStage.Finder, "finder");
				await writer.WriteStageCompletedAsync(WorkflowStage.Finder);
				return new FinderResult(true, "listen", new[] { "silent", "enlist" }, null);
			},
			AnalyzerStageRunner = async (_, _) =>
			{
				await writer.WriteUpdateAsync(WorkflowStage.Analyzer, "analyzer");
				await writer.WriteStageCompletedAsync(WorkflowStage.Analyzer);
				return new AnalyzerResult(
					TotalCount: 2,
					CountByWordLength: new Dictionary<int, int> { { 6, 2 } },
					TopRanked: new[] { "enlist", "silent" },
					RankingPolicy: "WordLengthDesc_LexicalAsc");
			},
			PresenterStageRunner = async (_, _) =>
			{
				await writer.WriteUpdateAsync(WorkflowStage.Presenter, "presenter");
				await writer.WriteStageCompletedAsync(WorkflowStage.Presenter);
				return new PresenterResult("ok");
			}
		};

		var workflow = CreateWorkflow(writer, hooks);

		var result = await workflow.ExecuteAsync("listen");

		result.FinalMessage.Should().Be("ok");
		writer.Events.Should().ContainInOrder(
			"update:Finder:finder",
			"completed:Finder",
			"update:Analyzer:analyzer",
			"completed:Analyzer",
			"update:Presenter:presenter",
			"completed:Presenter");
	}

	[Fact]
	public async Task ExecuteAsync_WhenCancelledInFinder_ShouldNotRunLaterStages()
	{
		var writer = new RecordingWorkflowStreamWriter();
		var hooks = new SequentialAnagramWorkflowExecutionHooks
		{
			FinderStageRunner = async (_, ct) =>
			{
				await writer.WriteUpdateAsync(WorkflowStage.Finder, "finder", ct);
				ct.ThrowIfCancellationRequested();
				await writer.WriteStageCompletedAsync(WorkflowStage.Finder, ct);
				return new FinderResult(true, "listen", new[] { "silent" }, null);
			},
			AnalyzerStageRunner = (_, _) =>
			{
				writer.Events.Add("analyzer-ran");
				return Task.FromResult(new AnalyzerResult(0, new Dictionary<int, int>(), Array.Empty<string>(), "WordLengthDesc_LexicalAsc"));
			},
			PresenterStageRunner = (_, _) =>
			{
				writer.Events.Add("presenter-ran");
				return Task.FromResult(new PresenterResult("should not run"));
			}
		};

		var workflow = CreateWorkflow(writer, hooks);
		using var cts = new CancellationTokenSource();
		cts.Cancel();

		var act = () => workflow.ExecuteAsync("listen", cts.Token);

		await act.Should().ThrowAsync<OperationCanceledException>();
		writer.Events.Should().NotContain("completed:Finder");
		writer.Events.Should().NotContain("analyzer-ran");
		writer.Events.Should().NotContain("presenter-ran");
	}

	[Fact]
	public async Task ExecuteAsync_WhenSuccessful_ShouldEmitSingleCompletionMarkerPerStage()
	{
		var writer = new RecordingWorkflowStreamWriter();
		var hooks = new SequentialAnagramWorkflowExecutionHooks
		{
			FinderStageRunner = async (_, _) =>
			{
				await writer.WriteStageCompletedAsync(WorkflowStage.Finder);
				return new FinderResult(true, "listen", new[] { "silent" }, null);
			},
			AnalyzerStageRunner = async (_, _) =>
			{
				await writer.WriteStageCompletedAsync(WorkflowStage.Analyzer);
				return new AnalyzerResult(
					TotalCount: 1,
					CountByWordLength: new Dictionary<int, int> { { 6, 1 } },
					TopRanked: new[] { "silent" },
					RankingPolicy: "WordLengthDesc_LexicalAsc");
			},
			PresenterStageRunner = async (_, _) =>
			{
				await writer.WriteStageCompletedAsync(WorkflowStage.Presenter);
				return new PresenterResult("ok");
			}
		};

		var workflow = CreateWorkflow(writer, hooks);

		await workflow.ExecuteAsync("listen");

		writer.CompletionCount[WorkflowStage.Finder].Should().Be(1);
		writer.CompletionCount[WorkflowStage.Analyzer].Should().Be(1);
		writer.CompletionCount[WorkflowStage.Presenter].Should().Be(1);
	}

	[Fact]
	public async Task ExecuteAsync_WhenFinderResultInvalid_ShouldSkipDownstreamStages()
	{
		var writer = new RecordingWorkflowStreamWriter();
		var hooks = new SequentialAnagramWorkflowExecutionHooks
		{
			FinderStageRunner = (_, _) =>
			{
				return Task.FromResult(new FinderResult(false, string.Empty, Array.Empty<string>(), "Input is invalid or too short."));
			},
			AnalyzerStageRunner = (_, _) =>
			{
				writer.Events.Add("analyzer-ran");
				return Task.FromResult(new AnalyzerResult(0, new Dictionary<int, int>(), Array.Empty<string>(), "WordLengthDesc_LexicalAsc"));
			},
			PresenterStageRunner = (_, _) =>
			{
				writer.Events.Add("presenter-ran");
				return Task.FromResult(new PresenterResult("should not run"));
			}
		};

		var workflow = CreateWorkflow(writer, hooks);

		var result = await workflow.ExecuteAsync("x");

		result.FinalMessage.Should().Be("Input is invalid or too short.");
		writer.Events.Should().NotContain("analyzer-ran");
		writer.Events.Should().NotContain("presenter-ran");
	}

	[Fact]
	public void ParseUserRequest_WhenInputContainsQuotedPhraseAndLimit_ShouldExtractBoth()
	{
		var parsed = SequentialAnagramWorkflow.ParseUserRequest("find 10 anagrams for 'visma praktika'");

		parsed.LookupInput.Should().Be("visma praktika");
		parsed.RequestedLimit.Should().Be(10);
	}

	[Fact]
	public async Task ExecuteAsync_WhenNaturalLanguageContainsShortTokens_ShouldUseParsedPhraseForLookup()
	{
		var writer = new RecordingWorkflowStreamWriter();
		FinderInput? capturedFinderInput = null;

		var hooks = new SequentialAnagramWorkflowExecutionHooks
		{
			FinderStageRunner = (finderInput, _) =>
			{
				capturedFinderInput = finderInput;
				return Task.FromResult(new FinderResult(true, "visma praktika", new[] { "praktika visma", "visma praktika" }, null));
			},
			AnalyzerStageRunner = (_, _) => Task.FromResult(new AnalyzerResult(
				TotalCount: 2,
				CountByWordLength: new Dictionary<int, int> { { 14, 2 } },
				TopRanked: new[] { "praktika visma", "visma praktika" },
				RankingPolicy: "WordLengthDesc_LexicalAsc")),
			PresenterStageRunner = (_, _) => Task.FromResult(new PresenterResult("ok"))
		};

		var workflow = CreateWorkflow(writer, hooks);

		var result = await workflow.ExecuteAsync("find 10 anagrams for 'visma praktika'");

		result.FinalMessage.Should().Be("ok");
		capturedFinderInput.Should().NotBeNull();
		capturedFinderInput!.UserInput.Should().Be("visma praktika");
	}

	[Fact]
	public async Task ExecuteAsync_WhenRequestedLimitProvided_ShouldUseItInFallbackPresentation()
	{
		var writer = new RecordingWorkflowStreamWriter();
		var hooks = new SequentialAnagramWorkflowExecutionHooks
		{
			FinderStageRunner = (_, _) => Task.FromResult(new FinderResult(
				true,
				"visma praktika",
				new[] { "z y", "a b", "c d", "e f", "g h" },
				null)),
			AnalyzerStageRunner = (_, _) => Task.FromResult(new AnalyzerResult(
				TotalCount: 5,
				CountByWordLength: new Dictionary<int, int> { { 3, 5 } },
				TopRanked: new[] { "a b", "c d", "e f", "g h", "z y" },
				RankingPolicy: "WordLengthDesc_LexicalAsc"))
		};

		var workflow = CreateWorkflow(
			streamWriter: writer,
			executionHooks: hooks,
			options: new SequentialAnagramWorkflowOptions
			{
				StreamingEnabled = true,
				MaxPresentedItems = 10,
				StreamingStageTimeoutSeconds = 1,
				MaxStreamingPayloadAnagrams = 20
			},
			streamStageRunner: (_, _, _, _, timeoutCt) => Task.Delay(TimeSpan.FromMinutes(1), timeoutCt).ContinueWith(_ => string.Empty, timeoutCt));
		var result = await workflow.ExecuteAsync("find 2 anagrams for 'visma praktika'");

		result.FinalMessage.Should().Contain("Top ranked: a b, c d");
		result.FinalMessage.Should().NotContain("e f");
	}

	[Fact]
	public async Task ExecuteAsync_WhenStageStreamingTimesOut_ShouldContinueToFallback()
	{
		var writer = new RecordingWorkflowStreamWriter();
		var hooks = new SequentialAnagramWorkflowExecutionHooks
		{
			FinderStageRunner = (_, _) => Task.FromResult(new FinderResult(
				true,
				"visma praktika",
				new[] { "a b", "b a", "ab" },
				null)),
			AnalyzerStageRunner = (_, _) => Task.FromResult(new AnalyzerResult(
				TotalCount: 3,
				CountByWordLength: new Dictionary<int, int> { { 3, 2 }, { 2, 1 } },
				TopRanked: new[] { "a b", "b a", "ab" },
				RankingPolicy: "WordLengthDesc_LexicalAsc"))
		};

		var workflow = CreateWorkflow(
			streamWriter: writer,
			executionHooks: hooks,
			options: new SequentialAnagramWorkflowOptions
			{
				StreamingEnabled = true,
				MaxPresentedItems = 2,
				StreamingStageTimeoutSeconds = 1,
				MaxStreamingPayloadAnagrams = 2
			},
			streamStageRunner: (_, _, _, _, timeoutCt) => Task.Delay(TimeSpan.FromMinutes(1), timeoutCt).ContinueWith(_ => string.Empty, timeoutCt));

		var result = await workflow.ExecuteAsync("find 2 anagrams for 'visma praktika'");

		result.FinalMessage.Should().Contain("Found 3 anagram(s)");
		writer.Events.Should().Contain(e => e.StartsWith("update:Presenter:Streaming timed out", StringComparison.Ordinal));
	}

	[Fact]
	public void SerializePresenterInput_ShouldUseStructuredCamelCasePayload()
	{
		var input = new PresenterInput(
			OriginalInput: "listen",
			Finder: new FinderResult(true, "listen", new[] { "silent" }, null),
			Analyzer: new AnalyzerResult(
				TotalCount: 1,
				CountByWordLength: new Dictionary<int, int> { { 6, 1 } },
				TopRanked: new[] { "silent" },
				RankingPolicy: "WordLengthDesc_LexicalAsc"));

		var json = SequentialAnagramWorkflow.SerializePresenterInput(input);

		json.Should().Contain("\"originalInput\"");
		json.Should().Contain("\"finder\"");
		json.Should().Contain("\"analyzer\"");
		json.Should().NotContain("\"OriginalInput\"");
	}

	[Fact]
	public void BuildPresenterPrompt_ShouldRequireJsonOnlyConsumption()
	{
		const string payload = "{\"originalInput\":\"listen\"}";

		var prompt = SequentialAnagramWorkflow.BuildPresenterPrompt(payload);

		prompt.Should().Contain("using ONLY the JSON payload");
		prompt.Should().Contain(payload);
	}

	[Fact]
	public void AnalyzeFinderResult_ShouldRespectConfiguredTopLimit()
	{
		var finder = new FinderResult(
			IsValid: true,
			NormalizedInput: "abc",
			Anagrams: new[] { "cba", "bac", "acb", "bca" },
			Error: null);

		var result = SequentialAnagramWorkflow.AnalyzeFinderResult(finder, 2);

		result.TopRanked.Should().HaveCount(2);
	}

	[Fact]
	public void BuildFallbackPresentation_ShouldRespectConfiguredTopLimit()
	{
		var input = new PresenterInput(
			OriginalInput: "listen",
			Finder: new FinderResult(true, "listen", new[] { "silent", "enlist", "tinsel" }, null),
			Analyzer: new AnalyzerResult(
				TotalCount: 3,
				CountByWordLength: new Dictionary<int, int> { { 6, 3 } },
				TopRanked: new[] { "silent", "enlist", "tinsel" },
				RankingPolicy: "WordLengthDesc_LexicalAsc"));

		var fallback = SequentialAnagramWorkflow.BuildFallbackPresentation(input, 2);

		fallback.Should().Contain("silent, enlist");
		fallback.Should().NotContain("tinsel");
	}

	[Fact]
	public void BuildFallbackPresentation_WhenFinderInvalid_ShouldReturnFinderError()
	{
		var input = new PresenterInput(
			OriginalInput: "x",
			Finder: new FinderResult(false, string.Empty, Array.Empty<string>(), "Input is invalid or too short."),
			Analyzer: new AnalyzerResult(0, new Dictionary<int, int>(), Array.Empty<string>(), "WordLengthDesc_LexicalAsc"));

		var fallback = SequentialAnagramWorkflow.BuildFallbackPresentation(input, 10);

		fallback.Should().Be("Input is invalid or too short.");
	}

	[Fact]
	public async Task ConsoleWorkflowStreamWriter_ShouldWriteLabeledUpdateAndSingleCompletionMarker()
	{
		using var textWriter = new StringWriter();
		var originalOut = System.Console.Out;

		try
		{
			System.Console.SetOut(textWriter);
			var writer = new ConsoleWorkflowStreamWriter();

			await writer.WriteUpdateAsync(WorkflowStage.Analyzer, "Progress");
			await writer.WriteStageCompletedAsync(WorkflowStage.Analyzer);

			var output = textWriter.ToString();
			output.Should().Contain("[Analyzer] Progress");
			output.Should().Contain("[Analyzer] Completed.");
			output.Split("[Analyzer] Completed.", StringSplitOptions.None).Length.Should().Be(2);
		}
		finally
		{
			System.Console.SetOut(originalOut);
		}
	}

	private sealed class NoOpWorkflowStreamWriter : IWorkflowStreamWriter
	{
		public Task WriteUpdateAsync(WorkflowStage stage, string text, CancellationToken ct = default)
		{
			return Task.CompletedTask;
		}

		public Task WriteStageCompletedAsync(WorkflowStage stage, CancellationToken ct = default)
		{
			return Task.CompletedTask;
		}
	}

	private static SequentialAnagramWorkflow CreateWorkflow(
		IWorkflowStreamWriter streamWriter,
		SequentialAnagramWorkflowExecutionHooks executionHooks,
		SequentialAnagramWorkflowOptions? options = null,
		Func<Microsoft.Agents.AI.AIAgent, Microsoft.Agents.AI.AgentSession, string, WorkflowStage, CancellationToken, Task<string>>? streamStageRunner = null)
	{
		var chatClient = new Mock<IChatClient>().Object;
		var anagramSolver = new Mock<IAnagramSolver>().Object;
		var wordRepository = new Mock<IWordRepository>().Object;
		var wordFrequencyAnalyzer = new Mock<IWordFrequencyAnalyzer>().Object;
		var tools = new AnagramTools(
			anagramSolver,
			wordRepository,
			new UserProcessor(2),
			new WordNormalizer(),
			wordFrequencyAnalyzer);
		var factory = new SequentialWorkflowAgentFactory(chatClient, tools);

		return new SequentialAnagramWorkflow(
			agentFactory: factory,
			streamWriter: streamWriter,
			options: options ?? new SequentialAnagramWorkflowOptions
			{
				StreamingEnabled = false,
				MaxPresentedItems = 10
			},
			executionHooks: executionHooks,
			streamStageRunner: streamStageRunner);
	}

	private sealed class RecordingWorkflowStreamWriter : IWorkflowStreamWriter
	{
		public List<string> Events { get; } = new();

		public Dictionary<WorkflowStage, int> CompletionCount { get; } = new()
		{
			{ WorkflowStage.Finder, 0 },
			{ WorkflowStage.Analyzer, 0 },
			{ WorkflowStage.Presenter, 0 }
		};

		public Task WriteUpdateAsync(WorkflowStage stage, string text, CancellationToken ct = default)
		{
			Events.Add($"update:{stage}:{text}");
			return Task.CompletedTask;
		}

		public Task WriteStageCompletedAsync(WorkflowStage stage, CancellationToken ct = default)
		{
			Events.Add($"completed:{stage}");
			CompletionCount[stage]++;
			return Task.CompletedTask;
		}
	}
}
