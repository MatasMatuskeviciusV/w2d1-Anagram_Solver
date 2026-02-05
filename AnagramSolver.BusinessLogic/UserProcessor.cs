using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnagramSolver.BusinessLogic
{
    public class UserProcessor
    {
        private readonly int _minLength;

        public UserProcessor(int minUserWordLength)
        {
            _minLength = minUserWordLength;
        }

        public bool IsValid(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return false; 
            }

            var words = input.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var word in words)
            {
                if(word.Trim().Length < _minLength)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
