using AnagramSolver.Contracts;
using AnagramSolver.WebApp.Controllers;
using AnagramSolver.WebApp.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace AnagramSolver.WebApp.Tests;

public class AnalysisControllerTests
{
    [Fact]
    public void AnalyzeFrequency_WhenRequestBodyMissing_ShouldReturnBadRequest()
    {
        // Arrange
        var analyzer = new Mock<IWordFrequencyAnalyzer>();
        var controller = new AnalysisController(analyzer.Object);

        // Act
        var result = controller.AnalyzeFrequency(null, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public void AnalyzeFrequency_WhenRequestInvalid_ShouldReturnBadRequest()
    {
        // Arrange
        var analyzer = new Mock<IWordFrequencyAnalyzer>();
        var controller = new AnalysisController(analyzer.Object);
        controller.ModelState.AddModelError("Text", "The Text field is required.");

        // Act
        var result = controller.AnalyzeFrequency(new FrequencyAnalysisRequest { Text = null }, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public void AnalyzeFrequency_WhenNoAnalyzableTokens_ShouldReturnOkWithZeroMetrics()
    {
        // Arrange
        var analyzer = new Mock<IWordFrequencyAnalyzer>();
        analyzer.Setup(x => x.Analyze("!!! ###", 10)).Returns(new WordFrequencyAnalysisResult
        {
            TopWords = Array.Empty<FrequentWordResult>(),
            TotalWordCount = 0,
            UniqueWordCount = 0,
            LongestWord = string.Empty
        });

        var controller = new AnalysisController(analyzer.Object);

        // Act
        var result = controller.AnalyzeFrequency(new FrequencyAnalysisRequest { Text = "!!! ###" }, CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<FrequencyAnalysisResponse>().Subject;
        response.TopWords.Should().BeEmpty();
        response.TotalWordCount.Should().Be(0);
        response.UniqueWordCount.Should().Be(0);
        response.LongestWord.Should().BeEmpty();
    }

    [Fact]
    public void AnalyzeFrequency_WhenValidInput_ShouldReturnExpectedResponseModel()
    {
        // Arrange
        var analyzer = new Mock<IWordFrequencyAnalyzer>();
        analyzer.Setup(x => x.Analyze("apple banana apple", 10)).Returns(new WordFrequencyAnalysisResult
        {
            TopWords = new[]
            {
                new FrequentWordResult { Word = "apple", Count = 2 },
                new FrequentWordResult { Word = "banana", Count = 1 }
            },
            TotalWordCount = 3,
            UniqueWordCount = 2,
            LongestWord = "banana"
        });

        var controller = new AnalysisController(analyzer.Object);

        // Act
        var result = controller.AnalyzeFrequency(new FrequencyAnalysisRequest { Text = "apple banana apple" }, CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<FrequencyAnalysisResponse>().Subject;

        response.TopWords.Select(x => new { x.Word, x.Count }).Should().Equal(
            new[]
            {
                new { Word = "apple", Count = 2 },
                new { Word = "banana", Count = 1 }
            });
        response.TotalWordCount.Should().Be(3);
        response.UniqueWordCount.Should().Be(2);
        response.LongestWord.Should().Be("banana");
    }

    [Fact]
    public void AnalyzeFrequency_WhenTieOccurs_ShouldReturnDeterministicTopWords()
    {
        // Arrange
        var analyzer = new Mock<IWordFrequencyAnalyzer>();
        analyzer.Setup(x => x.Analyze("beta alpha", 10)).Returns(new WordFrequencyAnalysisResult
        {
            TopWords = new[]
            {
                new FrequentWordResult { Word = "alpha", Count = 1 },
                new FrequentWordResult { Word = "beta", Count = 1 }
            },
            TotalWordCount = 2,
            UniqueWordCount = 2,
            LongestWord = "alpha"
        });

        var controller = new AnalysisController(analyzer.Object);

        // Act
        var result = controller.AnalyzeFrequency(new FrequencyAnalysisRequest { Text = "beta alpha" }, CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<FrequencyAnalysisResponse>().Subject;
        response.TopWords.Select(x => x.Word).Should().Equal("alpha", "beta");
    }

    [Fact]
    public void AnalyzeFrequency_WhenCancellationIsRequested_ShouldThrowOperationCanceledException()
    {
        // Arrange
        var analyzer = new Mock<IWordFrequencyAnalyzer>();
        var controller = new AnalysisController(analyzer.Object);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var action = () => controller.AnalyzeFrequency(new FrequencyAnalysisRequest { Text = "apple" }, cts.Token);

        // Assert
        action.Should().Throw<OperationCanceledException>();
        analyzer.Verify(x => x.Analyze(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void AnalyzeFrequency_WhenModelStateInvalid_ShouldNotCallAnalyzer()
    {
        // Arrange
        var analyzer = new Mock<IWordFrequencyAnalyzer>();
        var controller = new AnalysisController(analyzer.Object);
        controller.ModelState.AddModelError("Text", "Invalid");

        // Act
        var result = controller.AnalyzeFrequency(new FrequencyAnalysisRequest { Text = "apple" }, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
        analyzer.Verify(x => x.Analyze(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void AnalyzeFrequency_WhenRequestTextIsNull_ShouldNotCallAnalyzer()
    {
        // Arrange
        var analyzer = new Mock<IWordFrequencyAnalyzer>();
        var controller = new AnalysisController(analyzer.Object);

        // Act
        var result = controller.AnalyzeFrequency(new FrequencyAnalysisRequest { Text = null }, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
        analyzer.Verify(x => x.Analyze(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void AnalyzeFrequency_WhenValidRequest_ShouldCallAnalyzerWithDefaultTopN10()
    {
        // Arrange
        var analyzer = new Mock<IWordFrequencyAnalyzer>();
        analyzer.Setup(x => x.Analyze("apple", 10)).Returns(new WordFrequencyAnalysisResult
        {
            TopWords = Array.Empty<FrequentWordResult>(),
            TotalWordCount = 0,
            UniqueWordCount = 0,
            LongestWord = string.Empty
        });

        var controller = new AnalysisController(analyzer.Object);

        // Act
        _ = controller.AnalyzeFrequency(new FrequencyAnalysisRequest { Text = "apple" }, CancellationToken.None);

        // Assert
        analyzer.Verify(x => x.Analyze("apple", 10), Times.Once);
    }
}
