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
    public class UserInputProcessorTests
    {
        [Fact]
        public void IsValid_WhenInputIsShorterThanMinLen_ShouldReturnFalse()
        {
            var input = "abc";
            int minLen = 4;

            var user = new UserProcessor(minLen);

            bool result = user.IsValid(input);

            result.Should().BeFalse();
        }

        [Fact]
        public void IsValid_WhenInputMeetsMinLen_ShouldReturTrue()
        {
            var input = "abcd";
            int minLen = 4;

            var user = new UserProcessor(minLen);

            bool result = user.IsValid(input);

            result.Should().BeTrue();
        }

        [Fact]
        public void IsValid_WhenNoInput_ShouldReturnFalse()
        {
            string input = null;
            int minLen = 4;
            var user = new UserProcessor(minLen);

            bool result = user.IsValid(input);

            result.Should().BeFalse();
        }
    }
}
