using AnagramSolver.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AnagramSolver.BusinessLogic.Strategies;

namespace AnagramSolver.BusinessLogic
{
    public class DefaultAnagramSolver : IAnagramSolver
    {
        private Dictionary<string, List<string>>? _map;
        private readonly IWordRepository? _repo;
        private readonly int _maxResults;
        private readonly int _maxWords;

        private readonly IAnagramSearchStrategy _strategy;

        private Dictionary<char, int> _charIndex = new();
        private int _alphabetSize;
        private List<string> _keys = new();
        private Dictionary<string, int[]> _keyCounts = new();

        public DefaultAnagramSolver(IWordRepository repo, int maxResults, int maxWords, IAnagramSearchStrategy? strategy = null)
        {
            _repo = repo;
            _maxResults = maxResults;
            _maxWords = maxWords;
            _strategy = strategy ?? new SearchExactStrategy();
        }

        public DefaultAnagramSolver(Dictionary<string, List<string>> map, int maxResults, int maxWords, IAnagramSearchStrategy? strategy = null)
        {
            _map = map;
            _maxResults = maxResults;
            _maxWords = maxWords;
            _strategy = strategy ?? new SearchExactStrategy();
        }

        public async Task<IList<string>> GetAnagramsAsync(string myWords, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(myWords))
            {
                return new List<string>();
            }

            if (_map == null)
            {
                if (_repo == null)
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
            foreach (var key in _map.Keys)
            {
                if (key.Length <= myWords.Length && key.All(c => _charIndex.ContainsKey(c)))
                {
                    _keys.Add(key);
                }
            }

            _keyCounts.Clear();
            foreach (var key in _keys)
            {
                _keyCounts[key] = BuildCounts(key);
            }

            var inputCounts = BuildCounts(myWords);

            var results = new List<string>();

            _strategy.Search(_keys, _keyCounts, _map, inputCounts, myWords.Length, _maxWords, _maxResults, results, ct);

            return results;
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
    }
}
