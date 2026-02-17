using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GenericsDelegatesExperiments
{
    public class GenericMethods
    {
        public void Swap<T>(ref T a, ref T b)
        {
            T temp = a;
            a = b;
            b = temp;
        }

        public List<T> Where<T>(IEnumerable<T> source, Predicate<T> condition)
        {
            var result = new List<T>();

            foreach (var item in source)
            {
                if (condition(item))
                {
                    result.Add(item); 
                }
            }

            return result;
        }


        public string DelegateOperations(string command, string input)
        {
            var operations = new Dictionary<string, Func<string, string>>
            {
                ["lower"] = s => s.ToLower(),
                ["upper"] = s => s.ToUpper(),
                ["reverse"] = s => new string(s.Reverse().ToArray()),
            };

            return operations[command](input);
        }
    }
}
