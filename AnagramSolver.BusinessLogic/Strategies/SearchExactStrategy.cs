using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AnagramSolver.BusinessLogic.Strategies
{
    public sealed class SearchExactStrategy : IAnagramSearchStrategy
    {
        public void Search(
            IReadOnlyList<string> keys,
            IReadOnlyDictionary<string, int[]> keyCounts,
            IReadOnlyDictionary<string, List<string>> map,
            int[] inputCounts,
            int inputLength,
            int maxWords,
            int maxResults,
            List<string> results,
            CancellationToken ct = default)
        {
            var sortedKeys = keys.Count > 0 
                ? keys.OrderBy(k => k.Length).ToList() 
                : new List<string>(0);

            int minKeyLength = sortedKeys.Count > 0 ? sortedKeys[0].Length : 0;

            for(int target = 1; target <= maxWords; target++)
            {
                SearchExact(sortedKeys, keyCounts, map, inputCounts, inputLength, target, maxResults, new List<string>(), results, minKeyLength, ct);

                if(results.Count >= maxResults)
                {
                    return;
                }
            }
        }

        private void SearchExact(
            IReadOnlyList<string> keys,
            IReadOnlyDictionary<string, int[]> keyCounts,
            IReadOnlyDictionary<string, List<string>> map,
            int[] remainingCounts,
            int remainingLetters,
            int targetWords,
            int maxResults,
            List<string> currentWords,
            List<string> results,
            int minKeyLength,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            if(results.Count >= maxResults)
            {
                return;
            }

            if(remainingLetters == 0)
            {
                if(currentWords.Count == targetWords)
                {
                    results.Add(string.Join(" ", currentWords));
                }

                return;
            }

            if(remainingLetters < minKeyLength)
            {
                return;
            }

            if(currentWords.Count >= targetWords)
            {
                return;
            }

            foreach(var key in keys)
            {
                if(key.Length > remainingLetters)
                {
                    continue;
                }

                var remove = keyCounts[key];

                var newRemaining = TrySubtract(remainingCounts, remove);
                if(newRemaining == null)
                {
                    continue;
                }

                var candidates = map[key];
                for(int i = 0; i< candidates.Count; i++)
                {
                    currentWords.Add(candidates[i]);

                    SearchExact(keys, keyCounts, map, newRemaining, remainingLetters - key.Length, targetWords, maxResults, currentWords, results, minKeyLength, ct);

                    currentWords.RemoveAt(currentWords.Count - 1);

                    if(results.Count >= maxResults)
                    {
                        return;
                    }
                }
            }
        }

        private static int[] TrySubtract(int[] remaining, int[] remove)
        {
            var result = new int[remaining.Length];
            for(int i = 0; i < remaining.Length; i++)
            {
                int newValue = remaining[i] - remove[i];
                if (newValue < 0)
                {
                    return null;
                }
                result[i] = newValue;
            }
            return result;
        }
    }
}
