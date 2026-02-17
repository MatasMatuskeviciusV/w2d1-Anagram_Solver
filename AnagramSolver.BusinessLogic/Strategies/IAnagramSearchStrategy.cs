using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AnagramSolver.BusinessLogic.Strategies
{
    public interface IAnagramSearchStrategy
    {
        void Search(IReadOnlyList<string> keys,
            IReadOnlyDictionary<string, int[]> keyCounts,
            IReadOnlyDictionary<string, List<string>> map,
            int[] inputCounts,
            int inputLength,
            int maxWords,
            int maxResults,
            List<string> results,
            CancellationToken ct = default);
    }
}
