using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AnagramSolver.Contracts;

namespace AnagramSolver.BusinessLogic
{
    public class FileWordRepository : IWordRepository
    {
        private readonly string _path;

        public FileWordRepository(string path)
        {
            _path = path;
        }

        public async Task<IEnumerable<string>> GetAllWordsAsync(CancellationToken ct = default)
        {
            var lines = await File.ReadAllLinesAsync(_path, Encoding.UTF8, ct);
            return lines;
        }
    }
}
