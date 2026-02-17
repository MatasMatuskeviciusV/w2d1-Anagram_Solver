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
            for(int target = 1; target <= maxWords; target++)
            {
                SearchExact(keys, keyCounts, map, inputCounts, inputLength, target, maxResults, new List<string>(), results, ct);

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
                if(!CanSubtract(remainingCounts, remove))
                {
                    continue;
                }

                var newRemaining = Subtract(remainingCounts, remove);

                var candidates = map[key];
                for(int i = 0; i< candidates.Count; i++)
                {
                    currentWords.Add(candidates[i]);

                    SearchExact(keys, keyCounts, map, newRemaining, remainingLetters - key.Length, targetWords, maxResults, currentWords, results, ct);

                    currentWords.RemoveAt(currentWords.Count - 1);

                    if(results.Count >= maxResults)
                    {
                        return;
                    }
                }
            }
        }

        private static bool CanSubtract(int[] remaining, int[] remove)
        {
            for(int i = 0; i< remaining.Length; i++)
            {
                if (remove[i] > remaining[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static int[] Subtract(int[] remaining, int[] remove)
        {
            var result = new int[remaining.Length];
            for(int i = 0; i < remaining.Length; i++)
            {
                result[i] = remaining[i] - remove[i];
            }
            return result;
        }
    }
}
