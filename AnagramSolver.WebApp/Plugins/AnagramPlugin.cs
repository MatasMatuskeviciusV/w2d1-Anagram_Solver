namespace AnagramSolver.WebApp.Plugins;

using AnagramSolver.BusinessLogic;
using AnagramSolver.Contracts;
using Microsoft.SemanticKernel;
using System.ComponentModel;

public class AnagramPlugin
{
    private readonly IAnagramSolver _anagramSolver;

    public AnagramPlugin(IAnagramSolver anagramSolver)
    {
        _anagramSolver = anagramSolver;
    }

    [KernelFunction("GetAnagrams")]
    [Description("Finds all anagrams for a given word or phrase")]
    public async Task<string> GetAnagrams(
        [Description("The word or phrase to find anagrams for")] string word)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(word))
            {
                return "Please provide a valid word or phrase to find anagrams for.";
            }

            // Normalize the input (convert to lowercase, remove spaces)
            var normalizer = new WordNormalizer();
            var normalized = normalizer.NormalizeUserWords(word);

            if (string.IsNullOrWhiteSpace(normalized))
            {
                return "The word or phrase you provided appears to be invalid.";
            }

            // Build the anagram key (sort characters alphabetically)
            var key = AnagramKeySorter.BuildKey(normalized);

            // Query the database for anagrams using the sorted key
            var anagrams = await _anagramSolver.GetAnagramsAsync(key);

            if (!anagrams.Any())
            {
                return $"No anagrams found for '{word}'.";
            }

            var anagramList = string.Join(", ", anagrams.Take(10));
            return $"Anagrams for '{word}': {anagramList}" +
                   (anagrams.Count() > 10 ? $" (and {anagrams.Count() - 10} more)" : "");
        }
        catch (Exception ex)
        {
            return $"Error finding anagrams: {ex.Message}";
        }
    }

    [KernelFunction("CountAnagrams")]
    [Description("Counts how many anagrams were found for a given word or phrase")]
    public async Task<string> CountAnagrams(
        [Description("The word or phrase to count anagrams for")] string word)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(word))
            {
                return "Please provide a valid word or phrase.";
            }

            // Normalize the input (convert to lowercase, remove spaces)
            var normalizer = new WordNormalizer();
            var normalized = normalizer.NormalizeUserWords(word);

            if (string.IsNullOrWhiteSpace(normalized))
            {
                return "The word or phrase you provided appears to be invalid.";
            }

            // Build the anagram key (sort characters alphabetically)
            var key = AnagramKeySorter.BuildKey(normalized);

            // Query the database for anagrams using the sorted key
            var anagrams = await _anagramSolver.GetAnagramsAsync(key);
            return $"Found {anagrams.Count} anagram(s) for '{word}'.";
        }
        catch (Exception ex)
        {
            return $"Error counting anagrams: {ex.Message}";
        }
    }
}
