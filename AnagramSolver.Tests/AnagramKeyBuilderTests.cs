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
    public class AnagramKeyBuilderTests
    {
        [Fact]
        public void BuildKey_ShouldSortLettersCorrectly()
        {
            var letters = "cbadfe";

            var results = AnagramKeySorter.BuildKey(letters);

            results.Should().Be("abcdef");
        }

        [Fact]
        public void BuildKeys_WhenEmptyInput_ShouldReturnEmptyString()
        {
            var letters = "";

            var results = AnagramKeySorter.BuildKey(letters);

            results.Should().BeEmpty();
        }

        [Fact]
        public void BuildKeys_WhenNoInput_ShouldReturnEmptyString()
        {
            string letters = null;

            var results = AnagramKeySorter.BuildKey(letters);

            results.Should().BeEmpty();
        }

        [Fact]
        public void BuildKeys_ShouldSortRepeatingLettersCorrectly()
        {
            var letters = "cbaaddcfee";

            var results = AnagramKeySorter.BuildKey(letters);

            results.Should().Be("aabccddeef");
        }

        [Fact]
        public void BuildKey_WhenAlreadySorted_ShouldReturnSameString()
        {
            var letters = "abcd";

            var results = AnagramKeySorter.BuildKey(letters);

            results.Should().Be("abcd");
        }

        [Fact]
        public void BuildKey_ShouldHandleNumbers()
        {
            var letters = "3b2c1a";

            var results = AnagramKeySorter.BuildKey(letters);

            results.Should().Be("123abc");
        }
    }
}
