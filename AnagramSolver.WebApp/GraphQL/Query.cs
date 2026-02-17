using AnagramSolver.Contracts;
using AnagramSolver.BusinessLogic;

namespace AnagramSolver.WebApp.GraphQL
{
    public class Query
    {
        public async Task<IList<string>> Anagrams(string word, [Service] IAnagramSolver solver, [Service] UserProcessor userProcessor, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(word))
            {
                return Array.Empty<string>();
            }

            if (!userProcessor.IsValid(word))
            {
                return Array.Empty<string>();
            }

            var normalizer = new WordNormalizer();
            var normalized = normalizer.NormalizeUserWords(word);
            var key = AnagramKeySorter.BuildKey(normalized);

            return await solver.GetAnagramsAsync(key, ct);
        }
    }
}