using System.ComponentModel;
using AnagramSolver.BusinessLogic;
using AnagramSolver.Contracts;
using AnagramMsAgentFramework.Console.Workflows.Handoff.Models;
using AnagramMsAgentFramework.Console.Workflows.SequentialAnagram.Models;

namespace AnagramMsAgentFramework.Console;

public sealed class AnagramTools
{
	private const int MinOutputWordLength = 4;

	private readonly IAnagramSolver _anagramSolver;
	private readonly IWordRepository _wordRepository;
	private readonly UserProcessor _userProcessor;
	private readonly WordNormalizer _wordNormalizer;
	private readonly IWordFrequencyAnalyzer _wordFrequencyAnalyzer;

	public AnagramTools(
		IAnagramSolver anagramSolver,
		IWordRepository wordRepository,
		UserProcessor userProcessor,
		WordNormalizer wordNormalizer,
		IWordFrequencyAnalyzer wordFrequencyAnalyzer)
	{
		_anagramSolver = anagramSolver;
		_wordRepository = wordRepository;
		_userProcessor = userProcessor;
		_wordNormalizer = wordNormalizer;
		_wordFrequencyAnalyzer = wordFrequencyAnalyzer;
	}

	[Description("Find anagrams for a word or phrase.")]
	public async Task<string> FindAnagramsAsync(
		[Description("Word or phrase to search anagrams for.")] string input,
		CancellationToken ct = default)
	{
		if (!_userProcessor.IsValid(input))
		{
			return "Input is invalid or too short.";
		}

		var normalized = _wordNormalizer.NormalizeUserWords(input);
		if (string.IsNullOrWhiteSpace(normalized))
		{
			return "Input normalizes to an empty value.";
		}

		var key = AnagramKeySorter.BuildKey(normalized);
		var anagrams = await _anagramSolver.GetAnagramsAsync(key, ct);
		var filteredAnagrams = FilterAndOrderAnagrams(anagrams);

		if (filteredAnagrams.Count == 0)
		{
			return $"No anagrams found for '{input}'.";
		}

		var preview = string.Join(", ", filteredAnagrams.Take(20));
		var moreCount = filteredAnagrams.Count - 20;

		return moreCount > 0
			? $"Anagrams for '{input}': {preview} (and {moreCount} more)."
			: $"Anagrams for '{input}': {preview}.";
	}

	[Description("Find anagrams and return structured workflow-friendly output.")]
	public async Task<FinderResult> FindAnagramsStructuredAsync(
		[Description("Word or phrase to search anagrams for.")] string input,
		CancellationToken ct = default)
	{
		if (!_userProcessor.IsValid(input))
		{
			return new FinderResult(
				IsValid: false,
				NormalizedInput: string.Empty,
				Anagrams: Array.Empty<string>(),
				Error: "Input is invalid or too short.");
		}

		var normalized = _wordNormalizer.NormalizeUserWords(input);
		if (string.IsNullOrWhiteSpace(normalized))
		{
			return new FinderResult(
				IsValid: false,
				NormalizedInput: string.Empty,
				Anagrams: Array.Empty<string>(),
				Error: "Input normalizes to an empty value.");
		}

		var key = AnagramKeySorter.BuildKey(normalized);
		var anagrams = await _anagramSolver.GetAnagramsAsync(key, ct);
		var ordered = FilterAndOrderAnagrams(anagrams);

		return new FinderResult(
			IsValid: true,
			NormalizedInput: normalized,
			Anagrams: ordered,
			Error: null);
	}

	[Description("Count anagrams for a word or phrase.")]
	public async Task<string> CountAnagramsAsync(
		[Description("Word or phrase to count anagrams for.")] string input,
		CancellationToken ct = default)
	{
		if (!_userProcessor.IsValid(input))
		{
			return "Input is invalid or too short.";
		}

		var normalized = _wordNormalizer.NormalizeUserWords(input);
		if (string.IsNullOrWhiteSpace(normalized))
		{
			return "Input normalizes to an empty value.";
		}

		var key = AnagramKeySorter.BuildKey(normalized);
		var anagrams = await _anagramSolver.GetAnagramsAsync(key, ct);
		var filteredAnagrams = FilterAndOrderAnagrams(anagrams);

		return $"Found {filteredAnagrams.Count} anagram(s) for '{input}'.";
	}

	private static IReadOnlyList<string> FilterAndOrderAnagrams(IEnumerable<string> anagrams)
	{
		return anagrams
			.Where(x => !string.IsNullOrWhiteSpace(x))
			.Select(x => x.Trim())
			.Where(ContainsOnlyLongEnoughWords)
			.Distinct(StringComparer.Ordinal)
			.OrderBy(x => x, StringComparer.Ordinal)
			.ToList();
	}

	private static bool ContainsOnlyLongEnoughWords(string anagram)
	{
		var tokens = anagram.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		if (tokens.Length == 0)
		{
			return false;
		}

		return tokens.All(token => token.Length >= MinOutputWordLength);
	}

	[Description("Add a new word to the dictionary.")]
	public async Task<string> AddDictionaryWordAsync(
		[Description("Word to add to dictionary.")] string word,
		CancellationToken ct = default)
	{
		var result = await _wordRepository.AddWordAsync(word, ct);

		return result switch
		{
			AddWordResult.Added => $"Word '{word}' added successfully.",
			AddWordResult.AlreadyExists => $"Word '{word}' already exists.",
			AddWordResult.Invalid => "Provided word is invalid.",
			_ => "Unknown dictionary operation result."
		};
	}

	[Description("Analyze word frequencies in a text and return a summary.")]
	public string AnalyzeWordFrequency(
		[Description("Text to analyze.")] string text,
		[Description("How many top words to return.")] int topN = 10)
	{
		var safeTopN = Math.Max(1, topN);
		var result = _wordFrequencyAnalyzer.Analyze(text, safeTopN);

		if (result.TopWords.Count == 0)
		{
			return "No analyzable words found in the text.";
		}

		var top = string.Join(
			", ",
			result.TopWords.Select(w => $"{w.Word}:{w.Count}"));

		return $"Top words: {top}. Total words: {result.TotalWordCount}. Unique words: {result.UniqueWordCount}. Longest word: {result.LongestWord}.";
	}

	[Description("Analyze word frequencies and return structured payload.")]
	public WordAnalysisPayload AnalyzeWordFrequencyStructured(
		[Description("Text to analyze.")] string text,
		[Description("How many top words to return.")] int topN = 10)
	{
		var safeTopN = Math.Max(1, topN);
		var result = _wordFrequencyAnalyzer.Analyze(text, safeTopN);

		return new WordAnalysisPayload
		{
			TopWords = result.TopWords
				.Select(x => new WordAnalysisPayload.WordFrequencyItem(x.Word, x.Count))
				.ToArray(),
			TotalWordCount = result.TotalWordCount,
			UniqueWordCount = result.UniqueWordCount,
			LongestWord = result.LongestWord
		};
	}

	[Description("Get current local date and time.")]
	public string GetCurrentTime()
	{
		return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
	}

	[Description("Get normalized dictionary candidates for deterministic workflow steps.")]
	public async Task<IReadOnlyList<string>> GetDictionaryWordsAsync(
		[Description("Minimum accepted word length.")] int minLength,
		[Description("Maximum number of words returned.")] int maxItems,
		CancellationToken ct = default)
	{
		var safeMinLength = Math.Max(1, minLength);
		var safeMaxItems = Math.Max(1, maxItems);

		var words = await _wordRepository.GetAllWordsAsync(ct);
		var normalized = _wordNormalizer.NormalizeFileWords(words);

		return normalized
			.Where(w => w.Length >= safeMinLength)
			.OrderBy(w => w, StringComparer.Ordinal)
			.Take(safeMaxItems)
			.ToArray();
	}
}