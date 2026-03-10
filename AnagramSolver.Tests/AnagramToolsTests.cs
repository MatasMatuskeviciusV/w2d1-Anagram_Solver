using AnagramMsAgentFramework.Console;
using AnagramSolver.BusinessLogic;
using AnagramSolver.Contracts;
using FluentAssertions;
using Moq;

namespace AnagramSolver.Tests;

public class AnagramToolsTests
{
	[Fact]
	public async Task FindAnagramsStructuredAsync_ShouldFilterOutShortWordsAndShortTokensInPhrases()
	{
		var tools = CreateTools(new[]
		{
			"abcd",
			"abc",
			"ab cd",
			"abcd efgh",
			"  efgh  ",
			"abcd"
		});

		var result = await tools.FindAnagramsStructuredAsync("listen");

		result.IsValid.Should().BeTrue();
		result.Anagrams.Should().BeEquivalentTo(new[] { "abcd", "abcd efgh", "efgh" });
	}

	[Fact]
	public async Task CountAnagramsAsync_ShouldCountOnlyFilteredOutputs()
	{
		var tools = CreateTools(new[]
		{
			"abcd",
			"abc",
			"ab cd",
			"abcd efgh",
			"efgh"
		});

		var result = await tools.CountAnagramsAsync("listen");

		result.Should().Be("Found 3 anagram(s) for 'listen'.");
	}

	private static AnagramTools CreateTools(IList<string> solverOutputs)
	{
		var solverMock = new Mock<IAnagramSolver>();
		solverMock
			.Setup(x => x.GetAnagramsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(solverOutputs);

		var wordRepositoryMock = new Mock<IWordRepository>();
		var analyzerMock = new Mock<IWordFrequencyAnalyzer>();

		return new AnagramTools(
			solverMock.Object,
			wordRepositoryMock.Object,
			new UserProcessor(2),
			new WordNormalizer(),
			analyzerMock.Object);
	}
}
