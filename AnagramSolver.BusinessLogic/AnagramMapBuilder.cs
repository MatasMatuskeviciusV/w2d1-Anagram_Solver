using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnagramSolver.BusinessLogic
{
    public class AnagramMapBuilder
    {
        public Dictionary<string, List<string>> Build(IEnumerable<string> words)
        {
            var map = new Dictionary<string, List<string>>();

            foreach (var word in words)
            {
                var key = AnagramKeySorter.BuildKey(word);

                if (!map.TryGetValue(key, out var list))
                {
                    list = new List<string>();
                    map[key] = list;
                }
                list.Add(word);
            }
            return map;
        }
    }
}
