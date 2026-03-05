using AnagramSolver.Contracts;
using AnagramSolver.BusinessLogic;
using AnagramSolver.BusinessLogic.Strategies;
using Moq;
using Xunit;
using FluentAssertions;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;

namespace AnagramSolver.Tests
{
    public class DefaultAnagramSolverAdvancedMockTests
    {
        #region Mock Data Setup

        private static Dictionary<string, List<string>> CreateSimpleMap()
        {
            return new Dictionary<string, List<string>>
            {
                { "abt", new List<string> { "bat", "tab" } },
                { "aelmst", new List<string> { "metal", "steam", "meats" } },
                { "aeimnrst", new List<string> { "anagrams", "martians", "migrate" } }
            };
        }

        private static Mock<IWordRepository> CreateRepositoryMock(IEnumerable<string> words)
        {
            var repoMock = new Mock<IWordRepository>();
            repoMock.Setup(r => r.GetAllWordsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(words.ToList().AsEnumerable());
            return repoMock;
        }

        private static Mock<IAnagramSearchStrategy> CreateStrategyMock(Action<List<string>>? setupResults = null)
        {
            var strategyMock = new Mock<IAnagramSearchStrategy>();
            strategyMock.Setup(s => s.Search(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<IReadOnlyDictionary<string, int[]>>(),
                It.IsAny<IReadOnlyDictionary<string, List<string>>>(),
                It.IsAny<int[]>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<List<string>>(),
                It.IsAny<CancellationToken>()))
                .Callback<IReadOnlyList<string>, IReadOnlyDictionary<string, int[]>, 
                         IReadOnlyDictionary<string, List<string>>, int[], int, int, int, 
                         List<string>, CancellationToken>
                    ((keys, keyCounts, dict, inputCounts, inputLength, maxWords, maxResults, results, ct) =>
                    {
                        setupResults?.Invoke(results);
                    });
            
            return strategyMock;
        }

        #endregion

        #region Constructor with Mock Repository Tests

        [Fact]
        public void Constructor_WithMockedRepository_ShouldInitializeSuccessfully()
        {
            // Arrange
            var repoMock = CreateRepositoryMock(new[] { "test" });

            // Act
            var solver = new DefaultAnagramSolver(repoMock.Object, 50, 1);

            // Assert
            solver.Should().NotBeNull();
        }

        [Fact]
        public void Constructor_WithMockedRepositoryAndStrategy_ShouldInitializeWithCustomStrategy()
        {
            // Arrange
            var repoMock = CreateRepositoryMock(new[] { "test" });
            var strategyMock = CreateStrategyMock();

            // Act
            var solver = new DefaultAnagramSolver(repoMock.Object, 50, 1, strategyMock.Object);

            // Assert
            solver.Should().NotBeNull();
        }

        #endregion

        #region GetAnagramsAsync with Mocked Strategy Tests

        [Fact]
        public async Task GetAnagramsAsync_WithValidInput_ShouldInvokeStrategySearch()
        {
            // Arrange
            var map = CreateSimpleMap();
            var strategyMock = CreateStrategyMock();
            var solver = new DefaultAnagramSolver(map, 50, 1, strategyMock.Object);

            // Act
            await solver.GetAnagramsAsync("bat");

            // Assert
            strategyMock.Verify(s => s.Search(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<IReadOnlyDictionary<string, int[]>>(),
                It.IsAny<IReadOnlyDictionary<string, List<string>>>(),
                It.IsAny<int[]>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<List<string>>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetAnagramsAsync_WithEmptyInput_ShouldNotInvokeStrategy()
        {
            // Arrange
            var map = CreateSimpleMap();
            var strategyMock = CreateStrategyMock();
            var solver = new DefaultAnagramSolver(map, 50, 1, strategyMock.Object);

            // Act
            await solver.GetAnagramsAsync(string.Empty);

            // Assert
            strategyMock.Verify(s => s.Search(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<IReadOnlyDictionary<string, int[]>>(),
                It.IsAny<IReadOnlyDictionary<string, List<string>>>(),
                It.IsAny<int[]>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<List<string>>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task GetAnagramsAsync_WithValidInput_ShouldReturnStrategiesResults()
        {
            // Arrange
            var map = CreateSimpleMap();
            var expectedResults = new List<string> { "bat", "tab" };
            var strategyMock = CreateStrategyMock(results => 
            {
                results.AddRange(expectedResults);
            });
            var solver = new DefaultAnagramSolver(map, 50, 1, strategyMock.Object);

            // Act
            var results = await solver.GetAnagramsAsync("abt");

            // Assert
            results.Should().BeEquivalentTo(expectedResults);
        }

        #endregion

        #region GetAnagramsAsync with Mocked Repository Tests

        [Fact]
        public async Task GetAnagramsAsync_WithMockedRepository_ShouldFetchWordsOnFirstCall()
        {
            // Arrange
            var words = new[] { "bat", "tab", "at", "a" };
            var repoMock = CreateRepositoryMock(words);
            var solver = new DefaultAnagramSolver(repoMock.Object, 50, 1);

            // Act
            await solver.GetAnagramsAsync("bat");

            // Assert
            repoMock.Verify(r => r.GetAllWordsAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetAnagramsAsync_WithMockedRepository_ShouldNotRefetchWordsOnSecondCall()
        {
            // Arrange
            var words = new[] { "bat", "tab", "at", "a" };
            var repoMock = CreateRepositoryMock(words);
            var solver = new DefaultAnagramSolver(repoMock.Object, 50, 1);

            // Act
            await solver.GetAnagramsAsync("bat");
            await solver.GetAnagramsAsync("tab");

            // Assert
            repoMock.Verify(r => r.GetAllWordsAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetAnagramsAsync_WithEmptyMockedRepository_ShouldReturnEmptyResults()
        {
            // Arrange
            var repoMock = CreateRepositoryMock(Enumerable.Empty<string>());
            var solver = new DefaultAnagramSolver(repoMock.Object, 50, 1);

            // Act
            var results = await solver.GetAnagramsAsync("test");

            // Assert
            results.Should().BeEmpty();
        }

        [Fact]
        public async Task GetAnagramsAsync_WithMockedRepository_ShouldPassCorrectCancellationToken()
        {
            // Arrange
            var words = new[] { "test" };
            var repoMock = CreateRepositoryMock(words);
            var cts = new CancellationTokenSource();
            var solver = new DefaultAnagramSolver(repoMock.Object, 50, 1);

            // Act
            await solver.GetAnagramsAsync("test", cts.Token);

            // Assert
            repoMock.Verify(r => r.GetAllWordsAsync(cts.Token), Times.Once);
        }

        #endregion

        #region Key Filtering with Mock Strategy Tests

        [Fact]
        public async Task GetAnagramsAsync_ShouldFilterKeysLongerThanInput()
        {
            // Arrange
            var map = CreateSimpleMap();
            var strategyMock = CreateStrategyMock();
            var solver = new DefaultAnagramSolver(map, 50, 1, strategyMock.Object);

            // Act
            await solver.GetAnagramsAsync("ab");

            // Assert
            // Verify that only keys with length <= 2 are passed to strategy
            strategyMock.Verify(s => s.Search(
                It.Is<IReadOnlyList<string>>(keys => 
                    keys.All(k => k.Length <= 2)),
                It.IsAny<IReadOnlyDictionary<string, int[]>>(),
                It.IsAny<IReadOnlyDictionary<string, List<string>>>(),
                It.IsAny<int[]>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<List<string>>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetAnagramsAsync_ShouldOnlyIncludeKeysWithAvailableCharacters()
        {
            // Arrange
            var map = new Dictionary<string, List<string>>
            {
                { "abc", new List<string> { "cab" } },
                { "xyz", new List<string> { "zyx" } },
                { "ab", new List<string> { "ba" } }
            };
            var strategyMock = CreateStrategyMock();
            var solver = new DefaultAnagramSolver(map, 50, 1, strategyMock.Object);

            // Act
            await solver.GetAnagramsAsync("abcde");

            // Assert
            // Verify that only "abc" and "ab" keys are included (xyz contains 'x', 'y', 'z' not in input)
            strategyMock.Verify(s => s.Search(
                It.Is<IReadOnlyList<string>>(keys => 
                    keys.Count <= 2),
                It.IsAny<IReadOnlyDictionary<string, int[]>>(),
                It.IsAny<IReadOnlyDictionary<string, List<string>>>(),
                It.IsAny<int[]>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<List<string>>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region Strategy Parameter Verification Tests

        [Fact]
        public async Task GetAnagramsAsync_ShouldPassCorrectMaxWordsToStrategy()
        {
            // Arrange
            var map = CreateSimpleMap();
            int expectedMaxWords = 5;
            var strategyMock = CreateStrategyMock();
            var solver = new DefaultAnagramSolver(map, 50, expectedMaxWords, strategyMock.Object);

            // Act
            await solver.GetAnagramsAsync("test");

            // Assert
            strategyMock.Verify(s => s.Search(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<IReadOnlyDictionary<string, int[]>>(),
                It.IsAny<IReadOnlyDictionary<string, List<string>>>(),
                It.IsAny<int[]>(),
                It.IsAny<int>(),
                expectedMaxWords,
                It.IsAny<int>(),
                It.IsAny<List<string>>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetAnagramsAsync_ShouldPassCorrectMaxResultsToStrategy()
        {
            // Arrange
            var map = CreateSimpleMap();
            int expectedMaxResults = 25;
            var strategyMock = CreateStrategyMock();
            var solver = new DefaultAnagramSolver(map, expectedMaxResults, 1, strategyMock.Object);

            // Act
            await solver.GetAnagramsAsync("test");

            // Assert
            strategyMock.Verify(s => s.Search(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<IReadOnlyDictionary<string, int[]>>(),
                It.IsAny<IReadOnlyDictionary<string, List<string>>>(),
                It.IsAny<int[]>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                expectedMaxResults,
                It.IsAny<List<string>>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetAnagramsAsync_ShouldPassCorrectInputLengthToStrategy()
        {
            // Arrange
            var map = CreateSimpleMap();
            var inputText = "anagram";
            var strategyMock = CreateStrategyMock();
            var solver = new DefaultAnagramSolver(map, 50, 1, strategyMock.Object);

            // Act
            await solver.GetAnagramsAsync(inputText);

            // Assert
            strategyMock.Verify(s => s.Search(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<IReadOnlyDictionary<string, int[]>>(),
                It.IsAny<IReadOnlyDictionary<string, List<string>>>(),
                It.IsAny<int[]>(),
                inputText.Length,
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<List<string>>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region Multiple Calls and State Management Tests

        [Fact]
        public async Task GetAnagramsAsync_CalledMultipleTimes_ShouldHandleStateCorrectly()
        {
            // Arrange
            var map = CreateSimpleMap();
            var strategyMock = CreateStrategyMock(results =>
            {
                results.Add("result");
            });
            var solver = new DefaultAnagramSolver(map, 50, 1, strategyMock.Object);

            // Act
            var results1 = await solver.GetAnagramsAsync("abt");
            var results2 = await solver.GetAnagramsAsync("aelmst");
            var results3 = await solver.GetAnagramsAsync("bat");

            // Assert
            results1.Should().Contain("result");
            results2.Should().Contain("result");
            results3.Should().Contain("result");
            strategyMock.Verify(s => s.Search(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<IReadOnlyDictionary<string, int[]>>(),
                It.IsAny<IReadOnlyDictionary<string, List<string>>>(),
                It.IsAny<int[]>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<List<string>>(),
                It.IsAny<CancellationToken>()), Times.Exactly(3));
        }

        #endregion

        #region Null Input Edge Cases Tests

        [Fact]
        public async Task GetAnagramsAsync_WithNullInput_ShouldReturnEmptyListWithoutInvokingStrategy()
        {
            // Arrange
            var map = CreateSimpleMap();
            var strategyMock = CreateStrategyMock();
            var solver = new DefaultAnagramSolver(map, 50, 1, strategyMock.Object);

            // Act
            var results = await solver.GetAnagramsAsync(null);

            // Assert
            results.Should().BeEmpty();
            strategyMock.Verify(s => s.Search(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<IReadOnlyDictionary<string, int[]>>(),
                It.IsAny<IReadOnlyDictionary<string, List<string>>>(),
                It.IsAny<int[]>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<List<string>>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task GetAnagramsAsync_WithNullMapAndNullRepository_ShouldReturnEmptyList()
        {
            // Arrange
            var solver = new DefaultAnagramSolver((IWordRepository)null, 50, 1);

            // Act
            var results = await solver.GetAnagramsAsync("test");

            // Assert
            results.Should().BeEmpty();
        }

        #endregion

        #region Complex Mock Scenarios Tests

        [Fact]
        public async Task GetAnagramsAsync_WithMockedRepositoryAndStrategy_ShouldUseMapBuiltFromRepository()
        {
            // Arrange
            var words = new[] { "bat", "tab", "cat" };
            var repoMock = CreateRepositoryMock(words);
            var strategyMock = CreateStrategyMock(results =>
            {
                results.Add("bat");
                results.Add("tab");
            });
            var solver = new DefaultAnagramSolver(repoMock.Object, 50, 1, strategyMock.Object);

            // Act
            var results = await solver.GetAnagramsAsync("tab");

            // Assert
            repoMock.Verify(r => r.GetAllWordsAsync(It.IsAny<CancellationToken>()), Times.Once);
            strategyMock.Verify(s => s.Search(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<IReadOnlyDictionary<string, int[]>>(),
                It.IsAny<IReadOnlyDictionary<string, List<string>>>(),
                It.IsAny<int[]>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<List<string>>(),
                It.IsAny<CancellationToken>()), Times.Once);
            results.Should().BeEquivalentTo(new[] { "bat", "tab" });
        }

        [Fact]
        public async Task GetAnagramsAsync_RepositoryReturnsEmptyList_ShouldResultInEmptyAnagrams()
        {
            // Arrange
            var repoMock = CreateRepositoryMock(Enumerable.Empty<string>());
            var strategyMock = CreateStrategyMock();
            var solver = new DefaultAnagramSolver(repoMock.Object, 50, 1, strategyMock.Object);

            // Act
            var results = await solver.GetAnagramsAsync("test");

            // Assert
            results.Should().BeEmpty();
        }

        #endregion
    }
}
