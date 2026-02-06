using AnagramSolver.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AnagramSolver.BusinessLogic
{
    public class DefaultAnagramSolver : IAnagramSolver
    {
        private Dictionary<string, List<string>>? _map;
        private readonly IWordRepository? _repo;
        private readonly int _maxResults;
        private readonly int _maxWords;

        private Dictionary<char, int> _charIndex = new();
        private int _alphabetSize;
        private List<string> _keys = new();
        private Dictionary<string, int[]> _keyCounts = new();

        public DefaultAnagramSolver(IWordRepository repo, int maxResults, int maxWords)
        {
            _repo = repo;
            _maxResults = maxResults;
            _maxWords = maxWords;
        }

        public DefaultAnagramSolver(Dictionary<string, List<string>> map, int maxResults, int maxWords)
        {
            _map = map;
            _maxResults = maxResults;
            _maxWords = maxWords;
        }

        public async Task<IList<string>> GetAnagramsAsync(string myWords, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(myWords))
            {
                return new List<string>();
            }

            if(_map == null)
            {
                if(_repo == null)
                {
                    return new List<string>();
                }

                var normalizer = new WordNormalizer();
                var mapBuilder = new AnagramMapBuilder();

                var raw = await _repo.GetAllWordsAsync(ct);
                var clean = normalizer.NormalizeFileWords(raw);
                _map = mapBuilder.Build(clean);
            }
            BuildAlphabet(myWords);

            _keys.Clear();
            foreach(var key in _map.Keys)
            {
                if (key.Length <= myWords.Length && key.All(c => _charIndex.ContainsKey(c)))
                {
                    _keys.Add(key);
                }
            }

            _keyCounts.Clear();
            foreach(var key in _keys)
            {
                    _keyCounts[key] = BuildCounts(key);
            }

            var inputCounts = BuildCounts(myWords);

            var results = new List<string>();

            for (int target = 1; target <= _maxWords; target++)
            {
                SearchExact(inputCounts, myWords.Length, target, new List<string>(), results);

                if(results.Count >= _maxResults)
                {
                    break;
                }
            }

            return results;
        }

        private void SearchExact(int[] remainingCounts, int remainingLetters, int targetWords, List<string> currentWords, List<string> results)
        {
            if (results.Count >= _maxResults)
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

            if (currentWords.Count >= targetWords)
            {
                return;
            }

            foreach (var key in _keys)
            {
                if(key.Length > remainingLetters)
                {
                    continue;
                }

                var remove = _keyCounts[key];

                if(!CanSubtract(remainingCounts, remove))
                {
                    continue;
                }

                var newRemaining = Subtract(remainingCounts, remove);

                var candidates = _map[key];
                for(int i = 0; i < candidates.Count; i++)
                {
                    currentWords.Add(candidates[i]);

                    SearchExact(newRemaining, remainingLetters - key.Length, targetWords, currentWords, results);

                    currentWords.RemoveAt(currentWords.Count - 1);

                    if(results.Count >= _maxResults)
                    {
                        return;
                    }
                }
            }
        }

        private void BuildAlphabet(string inputSortedLetters)
        {
            _charIndex.Clear();

            char? prev = null;
            int index = 0;

            foreach(var c in inputSortedLetters)
            {
                if(prev == null || prev.Value != c)
                {
                    _charIndex[c] = index++;
                    prev = c;
                }
            }

            _alphabetSize = index;
        }

        private int[] BuildCounts(string s)
        {
            var counts = new int[_alphabetSize];

            foreach(var c in s)
            {
                counts[_charIndex[c]]++;
            }

            return counts;
        }

        private bool CanSubtract(int[] remaining, int[] remove)
        {
            for(int i = 0; i < remaining.Length; i++)
            {
                if (remove[i] > remaining[i])
                {
                    return false;
                }
            }
            return true;
        }

        private int[] Subtract(int[] remaining, int[] remove)
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
