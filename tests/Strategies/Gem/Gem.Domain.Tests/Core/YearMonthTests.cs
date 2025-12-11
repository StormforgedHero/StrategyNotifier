using Gem.Domain.Core;

namespace Gem.Domain.Tests.Core
{
    public class YearMonthTests
    {
        [Fact]
        public void Constructor_ValidYearAndMonth_SetsPropertiesCorrectly()
        {
            // Arrange
            const int year = 2025;
            const int month = 12;

            // Act
            var yearMonth = new YearMonth(year, month);

            // Assert
            Assert.Equal(year, yearMonth.Year);
            Assert.Equal(month, yearMonth.Month);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-12)]
        public void Constructor_MonthLessThanOne_ThrowsArgumentOutOfRangeException(int month)
        {
            // Arrange
            const int year = 2025;

            // Act
            var exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => new YearMonth(year, month));

            // Assert
            Assert.Equal("month", exception.ParamName);
        }

        [Theory]
        [InlineData(13)]
        [InlineData(99)]
        [InlineData(1000)]
        public void Constructor_MonthGreaterThanTwelve_ThrowsArgumentOutOfRangeException(int month)
        {
            // Arrange
            const int year = 2025;

            // Act
            var exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => new YearMonth(year, month));

            // Assert
            Assert.Equal("month", exception.ParamName);
        }

        [Fact]
        public void Equals_SameYearAndMonth_InstancesAreEqual()
        {
            // Arrange
            var first = new YearMonth(2025, 5);
            var second = new YearMonth(2025, 5);

            // Act & Assert
            Assert.Equal(first, second);
            Assert.True(first == second);
            Assert.False(first != second);
            Assert.True(first.Equals(second));
            Assert.True(((object)first).Equals(second));
            Assert.Equal(first.GetHashCode(), second.GetHashCode());
        }

        [Fact]
        public void Equals_DifferentYearOrMonth_InstancesAreNotEqual()
        {
            // Arrange
            var first = new YearMonth(2025, 5);
            var differentMonth = new YearMonth(2025, 6);
            var differentYear = new YearMonth(2024, 5);

            // Act & Assert
            Assert.NotEqual(first, differentMonth);
            Assert.True(first != differentMonth);
            Assert.True(first != differentYear);
            Assert.False(first.Equals(differentMonth));
            Assert.False(first.Equals(differentYear));
        }

        [Fact]
        public void CompareTo_SortsChronologically()
        {
            // Arrange
            var ym1 = new YearMonth(2025, 3);
            var ym2 = new YearMonth(2024, 12);
            var ym3 = new YearMonth(2025, 1);

            var list = new List<YearMonth> { ym1, ym2, ym3 };

            // Act
            list.Sort();

            // Assert
            Assert.Equal(new[] { ym2, ym3, ym1 }, list);
        }

        [Fact]
        public void AddMonths_WithZeroOffset_ReturnsEquivalentYearMonth()
        {
            // Arrange
            var original = new YearMonth(2025, 5);

            // Act
            var result = original.AddMonths(0);

            // Assert
            Assert.Equal(original, result);
        }

        [Theory]
        [InlineData(2025, 12, 1, 2026, 1)]
        [InlineData(2025, 1, -1, 2024, 12)]
        [InlineData(2025, 3, 10, 2026, 1)]
        [InlineData(2025, 1, -12, 2024, 1)]
        public void AddMonths_WithPositiveOrNegativeOffset_ReturnsExpectedYearMonth(
            int startYear,
            int startMonth,
            int offset,
            int expectedYear,
            int expectedMonth)
        {
            // Arrange
            var start = new YearMonth(startYear, startMonth);

            // Act
            var result = start.AddMonths(offset);

            // Assert
            var expected = new YearMonth(expectedYear, expectedMonth);
            Assert.Equal(expected, result);
        }
    }
}
