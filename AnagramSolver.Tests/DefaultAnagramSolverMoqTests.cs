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

//viskas mockinta, neturi but int maxResults ir t.t.
//DI

namespace AnagramSolver.Tests
{
    public class DefaultAnagramSolverMoqTests
    {
        [Fact]
        public async Task GetAnagrams_ShouldReturnOneWordAnagrams_FromMockedRepository()
        {
            var repo = new Mock<IWordRepository>();
            repo.Setup(r => r.GetAllWordsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { "praktikavimas" });
            int maxResults = 10;
            int maxWords = 1;
            var solver = new DefaultAnagramSolver(repo.Object, maxResults, maxWords);
            var inputKey = AnagramKeySorter.BuildKey("vismapraktika");

            var results = await solver.GetAnagramsAsync(inputKey);

            results.Should().Contain("praktikavimas");

        }

        [Fact]
        public async Task GetAnagrams_ShouldReturnMultiWordAnagrams_FromMockedRepository()
        {
            var repo = new Mock<IWordRepository>();
            repo.Setup(r => r.GetAllWordsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { "kava", "trikampis" });
            int maxResults = 10;
            int maxWords = 2;
            var solver = new DefaultAnagramSolver(repo.Object, maxResults, maxWords);
            var inputKey = AnagramKeySorter.BuildKey("vismapraktika");

            var results = await solver.GetAnagramsAsync(inputKey);

            results.Should().Contain("kava trikampis");
        }
    }
}
