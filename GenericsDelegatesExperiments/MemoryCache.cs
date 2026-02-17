using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GenericsDelegatesExperiments
{
    public class MemoryCache<T>
    {
        private Dictionary<string, T> _cache = new();

        public void Set(string key, T value)
        {
            _cache[key] = value; 
        }

        public bool TryGet(string key, out T value)
        {
            return _cache.TryGetValue(key, out value);
        }

        public void Remove(string key)
        {
            _cache.Remove(key); 
        }

        public void Clear()
        {
            _cache.Clear();
        }

        public IEnumerable<string> Keys => _cache.Keys;
        public IEnumerable<T> Values => _cache.Values;
    }
}
