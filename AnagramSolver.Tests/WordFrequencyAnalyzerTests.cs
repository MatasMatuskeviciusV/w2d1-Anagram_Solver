using AnagramSolver.BusinessLogic;
using FluentAssertions;

namespace AnagramSolver.Tests;

public class WordFrequencyAnalyzerTests
{
    [Fact]
    public void Analyze_WhenTextContainsMixedCase_ShouldCountCaseInsensitively()
    {
        // Arrange
        var analyzer = new WordFrequencyAnalyzer(Array.Empty<string>());

        // Act
        var result = analyzer.Analyze("Apple apple APPLE banana", topN: 10);

        // Assert
        result.TopWords.Select(x => new { x.Word, x.Count }).Should().Equal(
            new[]
            {
                new { Word = "apple", Count = 3 },
                new { Word = "banana", Count = 1 }
            });
        result.TotalWordCount.Should().Be(4);
        result.UniqueWordCount.Should().Be(2);
        result.LongestWord.Should().Be("banana");
    }

    [Fact]
    public void Analyze_WhenTextContainsStopWords_ShouldExcludeStopWords()
    {
        // Arrange
        var analyzer = new WordFrequencyAnalyzer(new[] { "the", "and" });

        // Act
        var result = analyzer.Analyze("The apple and banana the", topN: 10);

        // Assert
        result.TopWords.Select(x => x.Word).Should().Equal("apple", "banana");
        result.TotalWordCount.Should().Be(2);
        result.UniqueWordCount.Should().Be(2);
        result.LongestWord.Should().Be("banana");
    }

    [Fact]
    public void Analyze_WhenTextContainsPunctuation_ShouldTokenizeCorrectly()
    {
        // Arrange
        var analyzer = new WordFrequencyAnalyzer(Array.Empty<string>());

        // Act
        var result = analyzer.Analyze("Hello, hello... world-world!!! #@$", topN: 10);

        // Assert
        result.TopWords.Select(x => new { x.Word, x.Count }).Should().Equal(
            new[]
            {
                new { Word = "hello", Count = 2 },
                new { Word = "world", Count = 2 }
            });
        result.TotalWordCount.Should().Be(4);
        result.UniqueWordCount.Should().Be(2);
        result.LongestWord.Should().Be("hello");
    }

    [Fact]
    public void Analyze_WhenTextIsWhitespaceOrSymbolsOnly_ShouldReturnZeroedMetrics()
    {
        // Arrange
        var analyzer = new WordFrequencyAnalyzer(Array.Empty<string>());

        // Act
        var result = analyzer.Analyze("   !!! ###   ", topN: 10);

        // Assert
        result.TopWords.Should().BeEmpty();
        result.TotalWordCount.Should().Be(0);
        result.UniqueWordCount.Should().Be(0);
        result.LongestWord.Should().BeEmpty();
    }

    [Fact]
    public void Analyze_WhenAllTokensAreStopWords_ShouldReturnZeroedMetrics()
    {
        // Arrange
        var analyzer = new WordFrequencyAnalyzer(new[] { "the", "a", "an" });

        // Act
        var result = analyzer.Analyze("The a an THE", topN: 10);

        // Assert
        result.TopWords.Should().BeEmpty();
        result.TotalWordCount.Should().Be(0);
        result.UniqueWordCount.Should().Be(0);
        result.LongestWord.Should().BeEmpty();
    }

    [Fact]
    public void Analyze_WhenFrequenciesTie_ShouldSortAlphabetically()
    {
        // Arrange
        var analyzer = new WordFrequencyAnalyzer(Array.Empty<string>());

        // Act
        var result = analyzer.Analyze("beta alpha gamma", topN: 10);

        // Assert
        result.TopWords.Select(x => x.Word).Should().Equal("alpha", "beta", "gamma");
        result.TotalWordCount.Should().Be(3);
        result.UniqueWordCount.Should().Be(3);
        result.LongestWord.Should().Be("alpha");
    }

    [Fact]
    public void Analyze_WhenLongestWordTies_ShouldPickAlphabeticallyFirst()
    {
        // Arrange
        var analyzer = new WordFrequencyAnalyzer(Array.Empty<string>());

        // Act
        var result = analyzer.Analyze("zebra apple", topN: 10);

        // Assert
        result.LongestWord.Should().Be("apple");
        result.TotalWordCount.Should().Be(2);
        result.UniqueWordCount.Should().Be(2);
    }

    [Fact]
    public void Analyze_WhenTopNExceeded_ShouldReturnOnlyTop10()
    {
        // Arrange
        var analyzer = new WordFrequencyAnalyzer(Array.Empty<string>());
        var words = string.Join(' ', Enumerable.Range(1, 11).Select(i => $"w{i}"));

        // Act
        var result = analyzer.Analyze(words, topN: 10);

        // Assert
        result.TopWords.Should().HaveCount(10);
        result.TotalWordCount.Should().Be(11);
        result.UniqueWordCount.Should().Be(11);
        result.TopWords.Select(x => x.Word).Should().NotContain("w9");
    }

    [Fact]
    public void Analyze_WhenTopNIsZero_ShouldReturnNoTopWordsButKeepMetrics()
    {
        // Arrange
        var analyzer = new WordFrequencyAnalyzer(Array.Empty<string>());

        // Act
        var result = analyzer.Analyze("apple apple banana", topN: 0);

        // Assert
        result.TopWords.Should().BeEmpty();
        result.TotalWordCount.Should().Be(3);
        result.UniqueWordCount.Should().Be(2);
        result.LongestWord.Should().Be("banana");
    }

    [Fact]
    public void Analyze_WhenTopNIsNegative_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var analyzer = new WordFrequencyAnalyzer(Array.Empty<string>());

        // Act
        var action = () => analyzer.Analyze("apple banana", topN: -1);

        // Assert
        action.Should().Throw<ArgumentOutOfRangeException>()
            .Which.ParamName.Should().Be("topN");
    }

    [Fact]
    public void Analyze_WhenStopWordsContainMixedCaseAndWhitespace_ShouldNormalizeAndExclude()
    {
        // Arrange
        var analyzer = new WordFrequencyAnalyzer(new[] { " The ", "AND" });

        // Act
        var result = analyzer.Analyze("the apple and banana THE", topN: 10);

        // Assert
        result.TopWords.Select(x => new { x.Word, x.Count }).Should().Equal(
            new[]
            {
                new { Word = "apple", Count = 1 },
                new { Word = "banana", Count = 1 }
            });
        result.TotalWordCount.Should().Be(2);
        result.UniqueWordCount.Should().Be(2);
        result.LongestWord.Should().Be("banana");
    }

    [Fact]
    public void Analyze_WhenInputContainsUnicodeLetters_ShouldTokenizeAndCountCorrectly()
    {
        // Arrange
        var analyzer = new WordFrequencyAnalyzer(Array.Empty<string>());

        // Act
        var result = analyzer.Analyze("Žodis žodis Ąžuolas", topN: 10);

        // Assert
        result.TopWords.Select(x => new { x.Word, x.Count }).Should().Equal(
            new[]
            {
                new { Word = "žodis", Count = 2 },
                new { Word = "ąžuolas", Count = 1 }
            });
        result.TotalWordCount.Should().Be(3);
        result.UniqueWordCount.Should().Be(2);
        result.LongestWord.Should().Be("ąžuolas");
    }

    [Fact]
    public void Analyze_WhenInputContainsDigitsAndWords_ShouldTreatBothAsTokens()
    {
        // Arrange
        var analyzer = new WordFrequencyAnalyzer(Array.Empty<string>());

        // Act
        var result = analyzer.Analyze("r2d2 R2D2 model3", topN: 10);

        // Assert
        result.TopWords.Select(x => new { x.Word, x.Count }).Should().Equal(
            new[]
            {
                new { Word = "r2d2", Count = 2 },
                new { Word = "model3", Count = 1 }
            });
        result.TotalWordCount.Should().Be(3);
        result.UniqueWordCount.Should().Be(2);
    }

    [Fact]
    public void Analyze_WhenInputContainsApostrophesAndHyphens_ShouldSplitPerCurrentRegexRules()
    {
        // Arrange
        var analyzer = new WordFrequencyAnalyzer(Array.Empty<string>());

        // Act
        var result = analyzer.Analyze("it's e-mail re-enter", topN: 10);

        // Assert
        result.TopWords.Select(x => x.Word).Should().Equal("e", "enter", "it", "mail", "re", "s");
        result.TotalWordCount.Should().Be(6);
        result.UniqueWordCount.Should().Be(6);
    }

    [Fact]
    public void Analyze_WhenCountsAndLengthsTie_ShouldApplyBothDeterministicRules()
    {
        // Arrange
        var analyzer = new WordFrequencyAnalyzer(Array.Empty<string>());

        // Act
        var result = analyzer.Analyze("zeta beta", topN: 10);

        // Assert
        result.TopWords.Select(x => x.Word).Should().Equal("beta", "zeta");
        result.LongestWord.Should().Be("beta");
    }

    [Fact]
    public void Analyze_WhenTextIsNullAtRuntime_ShouldReturnZeroedMetrics()
    {
        // Arrange
        var analyzer = new WordFrequencyAnalyzer(Array.Empty<string>());

        // Act
        var result = analyzer.Analyze(null!, topN: 10);

        // Assert
        result.TopWords.Should().BeEmpty();
        result.TotalWordCount.Should().Be(0);
        result.UniqueWordCount.Should().Be(0);
        result.LongestWord.Should().BeEmpty();
    }
}
