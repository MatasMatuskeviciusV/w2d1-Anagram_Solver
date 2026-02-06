using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using AnagramSolver.BusinessLogic;

namespace AnagramSolver.Tests
{
    public class DefaultAnagramSolverTests
    {
        [Fact]
        public async Task GetAnagrams_ShouldReturnOneWordAnagrams()
        {
            var map = new Dictionary<string, List<string>>
            {
                {"aabls", new List<string> {"labas"} }
            };

            int maxResults = 50;
            int maxWords = 1;

            var solver = new DefaultAnagramSolver(map, maxResults, maxWords);

            var inputKey = "aabls";

            var results = await solver.GetAnagramsAsync(inputKey);

            results.Should().ContainSingle();
            results.Should().Contain("labas");
        }

        [Fact]

        public async Task GetAnagrams_WhenNoInput_ShouldReturnEmptyList()
        {
            var map = new Dictionary<string, List<string>>
            {
                {"aabls", new List<string> {"labas"} }
            };

            int maxResults = 50;
            int maxWords = 10;

            var solver = new DefaultAnagramSolver(map, maxResults, maxWords);

            string inputKey = null;

            var results = await solver.GetAnagramsAsync(inputKey);

            results.Should().BeEmpty();
        }

        [Fact]
        public async Task GetAnagrams_WhenEmptyInput_ShouldReturnEmptyList()
        {
            var map = new Dictionary<string, List<string>>
            {
                {"aabls", new List<string> {"labas"} }
            };

            int maxResults = 50;
            int maxWords = 10;

            var solver = new DefaultAnagramSolver(map, maxResults, maxWords);

            var inputKey = "";

            var results = await solver.GetAnagramsAsync(inputKey);

            results.Should().BeEmpty();
        }

        [Fact]
        public async Task GetAnagrams_ShouldReturnMultipleWordAnagrams()
        {
            var map = new Dictionary<string, List<string>>
            {
                {"aakv", new List<string> {"kava"} },
                {"aiikmprst", new List<string> {"trikampis"} }
            };

            int maxResults = 50;
            int maxWords = 10;

            var solver = new DefaultAnagramSolver(map, maxResults, maxWords);

            var inputKey = "aaaiikkmprstv";

            var results = await solver.GetAnagramsAsync(inputKey);

            results.Should().Contain("kava trikampis");
        }

        [Fact]
        public async Task GetAnagrams_WhenNoAnagramsFound_ShouldReturnEmptyList()
        {
            var map = new Dictionary<string, List<string>>
            {
                {"aakv", new List<string> {"kava"} },
                {"aiikmprst", new List<string> {"trikampis"} }
            };

            int maxResults = 50;
            int maxWords = 10;

            var solver = new DefaultAnagramSolver(map, maxResults, maxWords);

            var inputKey = "aaaaaaaaaaaaaaaaaa";

            var results = await solver.GetAnagramsAsync(inputKey);

            results.Should().BeEmpty();
        }

        [Fact]
        public async Task GetAnagrams_ShouldNotReturnMoreThanMaxResults()
        {
            var map = new Dictionary<string, List<string>>
            {
                {"abcde", new List<string> {"edcba", "bdcae", "bcdae", "acdeb"} }
            };

            int maxResults = 2;
            int maxWords = 10;

            var solver = new DefaultAnagramSolver(map, maxResults, maxWords);

            var inputKey = "abcde";

            var results = await solver.GetAnagramsAsync(inputKey);

            results.Should().HaveCount(maxResults);
        }

        [Fact]
        public async Task GetAnagrams_ShouldNotReturnMoreThanMaxWords()
        {
            var map = new Dictionary<string, List<string>>
            {
                {"abc", new List<string> {"bca", "cab", "bac", "cba"} },
                {"def", new List<string> {"fde", "fed", "dfe", "def"} },

            };

            int maxResults = 10;
            int maxWords = 2;

            var solver = new DefaultAnagramSolver(map, maxResults, maxWords);

            var inputKey = "abcdef";

            var results = await solver.GetAnagramsAsync(inputKey);

            results.Should().OnlyContain(r => r.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= maxWords);

        }

        [Fact]
        public async Task GetAnagrams_ShouldUseAllLettersExactly()
        {
            var map = new Dictionary<string, List<string>>
            {
                {"abc", new List<string> {"bac"} }
            };

            int maxResults = 10;
            int maxWords = 10;

            var solver = new DefaultAnagramSolver(map, maxResults, maxWords);

            var inputKey = "abcd";

            var results = await solver.GetAnagramsAsync(inputKey);

            results.Should().BeEmpty();
        }

        [Fact]
        public async Task GetAnagrams_WhenMaxWordsTooSmall_ShouldReturnEmptyList()
        {
            var map = new Dictionary<string, List<string>>
            {
                {"abc", new List<string> {"bac" } },
                {"def", new List<string> {"fed"} }
            };

            int maxResults = 10;
            int maxWords = 1;

            var solver = new DefaultAnagramSolver(map, maxResults, maxWords);

            var inputKey = "abcdef";

            var results = await solver.GetAnagramsAsync(inputKey);

            results.Should().BeEmpty();
        }

        [Fact]
        public async Task GetAnagrams_WhenDifferentKeys_ShouldIgnore()
        {
            var map = new Dictionary<string, List<string>>
            {
                {"abc", new List<string> {"bac" } },
                {"def", new List<string> {"fed"} }
            };

            int maxResults = 10;
            int maxWords = 5;

            var solver = new DefaultAnagramSolver(map, maxResults, maxWords);

            var inputKey = "abc";

            var results = await solver.GetAnagramsAsync(inputKey);

            results.Should().Contain("bac");
        }
    }
}
