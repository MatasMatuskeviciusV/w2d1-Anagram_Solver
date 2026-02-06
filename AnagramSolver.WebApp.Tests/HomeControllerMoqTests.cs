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

namespace AnagramSolver.WebApp.Tests
{
    public class HomeControllerMoqTests
    {
        [Fact]
        public void Index_WhenNoInput_ShouldReturnEmptyModel_AndDontCallSolver()
        {
            var solver = new Mock<IAnagramSolver>();
            var user = new UserProcessor(3);
            var controller = new HomeController(solver.Object, user);
            string id = null;

            var result = controller.Index(id);

            var view = result.Should().BeOfType<ViewResult>().Subject;
            var model = view.Model.Should().BeOfType<AnagramViewModel>().Subject;

            model.Query.Should().Be("");
            model.Results.Should().BeEmpty();

            solver.Verify(s => s.GetAnagrams(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void Index_WhenInputIsShorterThanMinLen_ShouldCallErrorNotSolver()
        {
            var solver = new Mock<IAnagramSolver>();
            var user = new UserProcessor(3);
            var controller = new HomeController(solver.Object, user);
            string id = "a labas";

            var result = controller.Index(id);

            var view = result.Should().BeOfType<ViewResult>().Subject;
            var model = view.Model.Should().BeOfType<AnagramViewModel>().Subject;

            model.Error.Should().NotBeNullOrWhiteSpace();
            model.Results.Should().BeEmpty();

            solver.Verify(s => s.GetAnagrams(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void Index_WhenInputIsValid_AndSolverReturnsResults_ShouldReturnModelWithResults()
        {
            var solver = new Mock<IAnagramSolver>();
            solver.Setup(s => s.GetAnagrams(It.IsAny<string>())).Returns(new List<string> { "alus" });
            var user = new UserProcessor(3);
            var controller = new HomeController(solver.Object, user);
            string id = "alus";

            var result = controller.Index(id);

            var view = result.Should().BeOfType<ViewResult>().Subject;
            var model = view.Model.Should().BeOfType<AnagramViewModel>().Subject;

            model.Query.Should().Be("alus");
            model.Results.Should().Contain("alus");
            model.Error.Should().BeNullOrEmpty();

            solver.Verify(s => s.GetAnagrams(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public void Index_WhenInputIsValid_AndSolverDoesNotReturnResults_ShouldReturnModelWithNoResults()
        {
            var solver = new Mock<IAnagramSolver>();
            solver.Setup(s => s.GetAnagrams(It.IsAny<string>())).Returns(new List<string>());
            var user = new UserProcessor(3);
            var controller = new HomeController(solver.Object, user);

            string id = "aaaaaaa";

            var result = controller.Index(id);

            var view = result.Should().BeOfType<ViewResult>().Subject;
            var model = view.Model.Should().BeOfType<AnagramViewModel>().Subject;

            model.Query.Should().Be("aaaaaaa");
            model.Results.Should().BeEmpty();
            model.Error.Should().BeNullOrEmpty();

            solver.Verify(s => s.GetAnagrams(It.IsAny<string>()), Times.Once);
        }
    }
}
