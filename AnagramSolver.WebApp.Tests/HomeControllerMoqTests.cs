using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Moq;
using FluentAssertions;
using Xunit;
using AnagramSolver.WebApp.Controllers;
using AnagramSolver.WebApp.Models;
using AnagramSolver.Contracts;
using AnagramSolver.BusinessLogic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

namespace AnagramSolver.WebApp.Tests
{
    public class HomeControllerMoqTests
    {
        private static HomeController CreateController(IAnagramSolver solver, UserProcessor userProcessor)
        {
            var controller = new HomeController(solver, userProcessor);
            var httpContext = new DefaultHttpContext
            {
                Session = new TestSession()
            };

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            return controller;
        }

        private sealed class TestSession : ISession
        {
            private readonly Dictionary<string, byte[]> _store = new();

            public IEnumerable<string> Keys => _store.Keys;
            public string Id => "test-session";
            public bool IsAvailable => true;

            public void Clear() => _store.Clear();

            public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

            public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

            public void Remove(string key) => _store.Remove(key);

            public void Set(string key, byte[] value) => _store[key] = value;

            public bool TryGetValue(string key, out byte[] value) => _store.TryGetValue(key, out value!);
        }

        [Fact]
        public async Task Index_WhenNoInput_ShouldReturnEmptyModel_AndDontCallSolver()
        {
            var solver = new Mock<IAnagramSolver>();
            var user = new UserProcessor(3);
            var controller = CreateController(solver.Object, user);
            string id = null;

            var result = await controller.Index(id, CancellationToken.None);

            var view = result.Should().BeOfType<ViewResult>().Subject;
            var model = view.Model.Should().BeOfType<AnagramViewModel>().Subject;

            model.Query.Should().Be("");
            model.Results.Should().BeEmpty();

            solver.Verify(s => s.GetAnagramsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Index_WhenInputIsShorterThanMinLen_ShouldCallErrorNotSolver()
        {
            var solver = new Mock<IAnagramSolver>();
            var user = new UserProcessor(3);
            var controller = CreateController(solver.Object, user);
            string id = "a labas";

            var result = await controller.Index(id, CancellationToken.None);

            var view = result.Should().BeOfType<ViewResult>().Subject;
            var model = view.Model.Should().BeOfType<AnagramViewModel>().Subject;

            model.Error.Should().NotBeNullOrWhiteSpace();
            model.Results.Should().BeEmpty();

            solver.Verify(s => s.GetAnagramsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Index_WhenInputIsValid_AndSolverReturnsResults_ShouldReturnModelWithResults()
        {
            var solver = new Mock<IAnagramSolver>();
            solver.Setup(s => s.GetAnagramsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<string> { "alus" });
            var user = new UserProcessor(3);
            var controller = CreateController(solver.Object, user);
            string id = "alus";

            var result = await controller.Index(id, CancellationToken.None);

            var view = result.Should().BeOfType<ViewResult>().Subject;
            var model = view.Model.Should().BeOfType<AnagramViewModel>().Subject;

            model.Query.Should().Be("alus");
            model.Results.Should().Contain("alus");
            model.Error.Should().BeNullOrEmpty();

            solver.Verify(s => s.GetAnagramsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Index_WhenInputIsValid_AndSolverDoesNotReturnResults_ShouldReturnModelWithNoResults()
        {
            var solver = new Mock<IAnagramSolver>();
            solver.Setup(s => s.GetAnagramsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<string>());
            var user = new UserProcessor(3);
            var controller = CreateController(solver.Object, user);

            string id = "aaaaaaa";

            var result = await controller.Index(id, CancellationToken.None);

            var view = result.Should().BeOfType<ViewResult>().Subject;
            var model = view.Model.Should().BeOfType<AnagramViewModel>().Subject;

            model.Query.Should().Be("aaaaaaa");
            model.Results.Should().BeEmpty();
            model.Error.Should().BeNullOrEmpty();

            solver.Verify(s => s.GetAnagramsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Index_WhenSearchingVismaPraktika_ShouldUseNormalizedKey_AndReturnExpectedScenarioResults()
        {
            var expectedResults = new List<string>
            {
                "praktikavimas",
                "tik parkavimas",
                "kava trikampis",
                "pikta vikramas",
                "pikti vakarams",
                "kvapai kartims",
                "kvapas kartimi",
                "kvapas ritmika",
                "piktam vakaris",
                "piktam vikaras",
                "tarkim kvapais",
                "tikram kvapais",
                "tvarka pasiimk",
                "kartimi kvapas",
                "ritmika kvapas",
                "kartims kvapai",
                "kvapais tarkim",
                "kvapais tikram",
                "pasiimk tvarka",
                "vakaris piktam",
                "vikaras piktam",
                "vakarams pikti",
                "vikramas pikta",
                "trikampis kava",
                "parkavimas tik"
            };

            var solver = new Mock<IAnagramSolver>();
            solver
                .Setup(s => s.GetAnagramsAsync("aaaiikkmprstv", It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResults);

            var user = new UserProcessor(3);
            var controller = CreateController(solver.Object, user);

            var result = await controller.Index("visma praktika", CancellationToken.None);

            var view = result.Should().BeOfType<ViewResult>().Subject;
            var model = view.Model.Should().BeOfType<AnagramViewModel>().Subject;

            model.Query.Should().Be("visma praktika");
            model.Error.Should().BeNullOrEmpty();
            model.Results.Should().HaveCount(25);
            model.Results.Should().BeEquivalentTo(expectedResults, options => options.WithStrictOrdering());

            solver.Verify(s => s.GetAnagramsAsync("aaaiikkmprstv", It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
