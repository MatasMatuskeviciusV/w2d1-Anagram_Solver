using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AnagramSolver.WebApp.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace AnagramSolver.WebApp.Tests;

public sealed class AnalysisControllerApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public AnalysisControllerApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task AnalyzeFrequency_WhenRequestBodyMissing_ShouldReturnBadRequest()
    {
        // Arrange
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/analysis/frequency");

        // Act
        using var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AnalyzeFrequency_WhenTextFieldMissing_ShouldReturnBadRequest()
    {
        // Act
        using var response = await _client.PostAsJsonAsync("/api/analysis/frequency", new { });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("")]
    [InlineData("    ")]
    [InlineData("!!! ###")]
    public async Task AnalyzeFrequency_WhenInputIsAnalyzableEmpty_ShouldReturnOkWithZeroedMetrics(string text)
    {
        // Arrange
        var payload = new FrequencyAnalysisRequest { Text = text };

        // Act
        using var response = await _client.PostAsJsonAsync("/api/analysis/frequency", payload);
        var content = await response.Content.ReadFromJsonAsync<FrequencyAnalysisResponse>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().NotBeNull();
        content!.TopWords.Should().BeEmpty();
        content.TotalWordCount.Should().Be(0);
        content.UniqueWordCount.Should().Be(0);
        content.LongestWord.Should().BeEmpty();
    }

    [Fact]
    public async Task AnalyzeFrequency_WhenValidInput_ShouldReturnExpectedContractShapeAndValues()
    {
        // Arrange
        var payload = new FrequencyAnalysisRequest { Text = "Apple apple banana" };

        // Act
        using var response = await _client.PostAsJsonAsync("/api/analysis/frequency", payload);
        var raw = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(raw);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        json.RootElement.TryGetProperty("topWords", out var topWords).Should().BeTrue();
        json.RootElement.TryGetProperty("totalWordCount", out var totalWordCount).Should().BeTrue();
        json.RootElement.TryGetProperty("uniqueWordCount", out var uniqueWordCount).Should().BeTrue();
        json.RootElement.TryGetProperty("longestWord", out var longestWord).Should().BeTrue();

        totalWordCount.GetInt32().Should().Be(3);
        uniqueWordCount.GetInt32().Should().Be(2);
        longestWord.GetString().Should().Be("banana");

        topWords.GetArrayLength().Should().Be(2);
        topWords[0].GetProperty("word").GetString().Should().Be("apple");
        topWords[0].GetProperty("count").GetInt32().Should().Be(2);
        topWords[1].GetProperty("word").GetString().Should().Be("banana");
        topWords[1].GetProperty("count").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task AnalyzeFrequency_WhenTieInFrequency_ShouldReturnDeterministicOrdering()
    {
        // Arrange
        var payload = new FrequencyAnalysisRequest { Text = "beta alpha" };

        // Act
        using var response = await _client.PostAsJsonAsync("/api/analysis/frequency", payload);
        var content = await response.Content.ReadFromJsonAsync<FrequencyAnalysisResponse>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().NotBeNull();
        content!.TopWords.Select(x => x.Word).Should().Equal("alpha", "beta");
    }

    [Fact]
    public async Task AnalyzeFrequency_WhenOnlyStopWords_ShouldReturnOkWithZeroMetrics()
    {
        // Arrange
        var payload = new FrequencyAnalysisRequest { Text = "the and a" };

        // Act
        using var response = await _client.PostAsJsonAsync("/api/analysis/frequency", payload);
        var content = await response.Content.ReadFromJsonAsync<FrequencyAnalysisResponse>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().NotBeNull();
        content!.TopWords.Should().BeEmpty();
        content.TotalWordCount.Should().Be(0);
        content.UniqueWordCount.Should().Be(0);
        content.LongestWord.Should().BeEmpty();
    }

    [Fact]
    public async Task AnalyzeFrequency_WhenPunctuationHeavyInput_ShouldReturnOkAndCorrectTokenCounts()
    {
        // Arrange
        var payload = new FrequencyAnalysisRequest { Text = "Hello, hello... world-world!!!" };

        // Act
        using var response = await _client.PostAsJsonAsync("/api/analysis/frequency", payload);
        var content = await response.Content.ReadFromJsonAsync<FrequencyAnalysisResponse>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().NotBeNull();
        content!.TopWords.Select(x => new { x.Word, x.Count }).Should().Equal(
            new[]
            {
                new { Word = "hello", Count = 2 },
                new { Word = "world", Count = 2 }
            });
        content.TotalWordCount.Should().Be(4);
        content.UniqueWordCount.Should().Be(2);
    }

    [Fact]
    public async Task AnalyzeFrequency_WhenWrongContentType_ShouldReturnClientError()
    {
        // Arrange
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/analysis/frequency")
        {
            Content = new StringContent("text=apple", Encoding.UTF8, "text/plain")
        };

        // Act
        using var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.UnsupportedMediaType);
    }

    [Fact]
    public async Task AnalyzeFrequency_WhenAnalysisConfigStopWordsOverridden_ShouldUseOverriddenValues()
    {
        // Arrange
        var client = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Analysis:StopWords:0"] = "apple",
                        ["Analysis:StopWords:1"] = "banana"
                    });
                });
            })
            .CreateClient();

        var payload = new FrequencyAnalysisRequest { Text = "apple banana kiwi" };

        // Act
        using var response = await client.PostAsJsonAsync("/api/analysis/frequency", payload);
        var content = await response.Content.ReadFromJsonAsync<FrequencyAnalysisResponse>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().NotBeNull();
        content!.TopWords.Select(x => new { x.Word, x.Count }).Should().Equal(
            new[] { new { Word = "kiwi", Count = 1 } });
        content.TotalWordCount.Should().Be(1);
        content.UniqueWordCount.Should().Be(1);
        content.LongestWord.Should().Be("kiwi");
    }

    [Fact]
    public async Task AnalyzeFrequency_WhenRequestBodyIsLarge_ShouldReturnWithinReasonableTime()
    {
        // Arrange
        var payload = new FrequencyAnalysisRequest { Text = BuildLargeText(iterations: 10_000) };

        // Act
        using var response = await _client.PostAsJsonAsync("/api/analysis/frequency", payload);
        var content = await response.Content.ReadFromJsonAsync<FrequencyAnalysisResponse>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().NotBeNull();
        content!.TopWords.Should().NotBeEmpty();
        content.TopWords[0].Word.Should().Be("alpha");
        content.TopWords[0].Count.Should().Be(20_000);
        content.TotalWordCount.Should().Be(30_000);
        content.UniqueWordCount.Should().Be(2);
    }

    private static string BuildLargeText(int iterations)
    {
        var builder = new StringBuilder(iterations * 17);
        for (var i = 0; i < iterations; i++)
        {
            builder.Append("alpha beta alpha ");
        }

        return builder.ToString();
    }
}
