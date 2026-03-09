using System.Text.RegularExpressions;
using AnagramSolver.Contracts;

namespace AnagramSolver.BusinessLogic;

public sealed class WordFrequencyAnalyzer : IWordFrequencyAnalyzer
{
    private static readonly Regex TokenRegex = new(@"[\p{L}\p{Nd}]+", RegexOptions.Compiled);
    private static readonly WordFrequencyAnalysisResult EmptyResult = new()
    {
        TopWords = Array.Empty<FrequentWordResult>(),
        TotalWordCount = 0,
        UniqueWordCount = 0,
        LongestWord = string.Empty
    };

    private readonly HashSet<string> _stopWords;

    public WordFrequencyAnalyzer(IEnumerable<string> stopWords)
    {
        ArgumentNullException.ThrowIfNull(stopWords);

        _stopWords = stopWords
            .Select(NormalizeToken)
            .Where(static token => !string.IsNullOrWhiteSpace(token))
            .ToHashSet(StringComparer.Ordinal);
    }

    public WordFrequencyAnalysisResult Analyze(string text, int topN = 10)
    {
        if (topN < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(topN), "topN must be greater than or equal to 0.");
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return EmptyResult;
        }

        var frequencyMap = new Dictionary<string, int>(StringComparer.Ordinal);
        var totalCount = 0;
        var longestWord = string.Empty;

        foreach (var token in Tokenize(text))
        {
            if (_stopWords.Contains(token))
            {
                continue;
            }

            totalCount++;
            frequencyMap.TryGetValue(token, out var currentCount);
            frequencyMap[token] = currentCount + 1;

            if (token.Length > longestWord.Length ||
                (token.Length == longestWord.Length &&
                 string.CompareOrdinal(token, longestWord) < 0))
            {
                longestWord = token;
            }
        }

        if (frequencyMap.Count == 0)
        {
            return EmptyResult;
        }

        var topWords = frequencyMap
            .OrderByDescending(static pair => pair.Value)
            .ThenBy(static pair => pair.Key, StringComparer.Ordinal)
            .Take(topN)
            .Select(static pair => new FrequentWordResult
            {
                Word = pair.Key,
                Count = pair.Value
            })
            .ToArray();

        return new WordFrequencyAnalysisResult
        {
            TopWords = topWords,
            TotalWordCount = totalCount,
            UniqueWordCount = frequencyMap.Count,
            LongestWord = longestWord
        };
    }

    private static IEnumerable<string> Tokenize(string text)
    {
        foreach (Match match in TokenRegex.Matches(text))
        {
            var normalizedToken = NormalizeToken(match.Value);
            if (!string.IsNullOrWhiteSpace(normalizedToken))
            {
                yield return normalizedToken;
            }
        }
    }

    private static string NormalizeToken(string token)
    {
        return token.Trim().ToLowerInvariant();
    }
}
