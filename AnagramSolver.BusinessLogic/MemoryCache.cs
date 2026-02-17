using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnagramSolver.BusinessLogic
{
    public class MemoryCache<T>
    {
        private readonly Dictionary<string, T> _cache = new();

        public bool TryGet(string key, out T value)
        {
            return _cache.TryGetValue(key, out value);
        }

        public void Set(string key, T value)
        {
            _cache[key] = value; 
        }

        public void Clear()
        {
            _cache.Clear();
        }
    }
}
