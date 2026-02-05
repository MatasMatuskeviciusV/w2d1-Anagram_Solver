using AnagramSolver.Contracts;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AnagramSolver.BusinessLogic;
using Xunit;
using FluentAssertions;

namespace AnagramSolver.Tests
{
    public class DefaultAnagramSolverMoqTests
    {
        [Fact]
        public void GetAnagrams_ShouldReturnOneWordAnagrams_FromMockedRepository()
        {
            var repo = new Mock<IWordRepository>();
            repo.Setup(r => r.GetAllWords()).Returns(new[] { "praktikavimas" });
            int maxResults = 10;
            int maxWords = 1;
            var solver = new DefaultAnagramSolver(repo.Object, maxResults, maxWords);
            var inputKey = AnagramKeyBuilder.BuildKey("vismapraktika");

            var results = solver.GetAnagrams(inputKey);

            results.Should().Contain("praktikavimas");

        }

        [Fact]
        public void GetAnagrams_ShouldReturnMultiWordAnagrams_FromMockedRepository()
        {
            var repo = new Mock<IWordRepository>();
            repo.Setup(r => r.GetAllWords()).Returns(new[] { "kava", "trikampis" });
            int maxResults = 10;
            int maxWords = 2;
            var solver = new DefaultAnagramSolver(repo.Object, maxResults, maxWords);
            var inputKey = AnagramKeyBuilder.BuildKey("vismapraktika");

            var results = solver.GetAnagrams(inputKey);

            results.Should().Contain("kava trikampis");
        }
    }
}
