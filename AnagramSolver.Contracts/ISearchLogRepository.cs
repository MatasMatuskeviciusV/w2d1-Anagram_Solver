using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnagramSolver.Contracts
{
    public interface ISearchLogRepository
    {
        Task AddAsync(string input, IList<string> results, CancellationToken ct = default);
    }
}
