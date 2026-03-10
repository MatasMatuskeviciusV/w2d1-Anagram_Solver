using AnagramMsAgentFramework.Console.Workflows.SequentialAnagram;
using AnagramMsAgentFramework.Console.Workflows.SequentialAnagram.Models;
using FluentAssertions;

namespace AnagramSolver.Tests;

public class SequentialAnagramAnalyzerPolicyTests
{
	[Fact]
	public void AnalyzeFinderResult_WhenInputHasTies_ShouldRankDeterministically()
	{
		var finder = new FinderResult(
			IsValid: true,
			NormalizedInput: "abc",
			Anagrams: new[] { "cab", "abc", "bac", "foo", "food" },
			Error: null);

		var result = SequentialAnagramWorkflow.AnalyzeFinderResult(finder, 10);

		result.TopRanked.Should().ContainInOrder("food", "abc", "bac", "cab", "foo");
		result.RankingPolicy.Should().Be("WordLengthDesc_LexicalAsc");
	}

	[Fact]
	public void AnalyzeFinderResult_WhenNoAnagrams_ShouldReturnZeroSafeResult()
	{
		var finder = new FinderResult(
			IsValid: true,
			NormalizedInput: "abc",
			Anagrams: Array.Empty<string>(),
			Error: null);

		var result = SequentialAnagramWorkflow.AnalyzeFinderResult(finder, 10);

		result.TotalCount.Should().Be(0);
		result.TopRanked.Should().BeEmpty();
		result.CountByWordLength.Should().BeEmpty();
	}

	[Fact]
	public void AnalyzeFinderResult_ShouldGroupByWordLengthCorrectly()
	{
		var finder = new FinderResult(
			IsValid: true,
			NormalizedInput: "abcdef",
			Anagrams: new[] { "ab", "cd", "ef", "abc", "defg" },
			Error: null);

		var result = SequentialAnagramWorkflow.AnalyzeFinderResult(finder, 10);

		result.CountByWordLength[2].Should().Be(3);
		result.CountByWordLength[3].Should().Be(1);
		result.CountByWordLength[4].Should().Be(1);
	}
}
