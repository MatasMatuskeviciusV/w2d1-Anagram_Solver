using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnagramSolver.Contracts
{
    public interface IWordRepository
    {
        Task<IEnumerable<string>> GetAllWordsAsync(CancellationToken ct = default);
    }
}
