using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AnagramSolver.Contracts;

namespace AnagramSolver.BusinessLogic.Decorators
{
    public sealed class CachingAnagramSolver : IAnagramSolver
    {
        private readonly IAnagramSolver _inner;
        private readonly MemoryCache<IList<string>> _cache = new();

        private readonly int _maxResults;
        private readonly int _maxWords;

        public CachingAnagramSolver(IAnagramSolver inner, int maxResults, int maxWords)
        {
            _inner = inner;
            _maxResults = maxResults;
            _maxWords = maxWords;
        }

        public async Task<IList<string>> GetAnagramsAsync(string myWords, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(myWords))
            {
                return new List<string>();
            }

            var cacheKey = $"{myWords}|mw:{_maxWords}|mr:{_maxResults}";

            if(_cache.TryGet(cacheKey, out var cached))
            {
                return cached;
            }

            var results = await _inner.GetAnagramsAsync(myWords, ct);

            _cache.Set(cacheKey, results);

            return results;
        }
    }
}
