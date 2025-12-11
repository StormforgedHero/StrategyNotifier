using Gem.Domain.Core;
using Gem.Domain.Exceptions;

namespace Gem.Domain.Tests.Core
{
    public class AssetReturnSeriesTests
    {
        [Fact]
        public void Constructor_UnsortedMonthlyReturns_SortsByPeriodAscending()
        {
            // Arrange
            var returns = new[]
            {
                new MonthlyReturn(new YearMonth(2025, 3), 0.03m),
                new MonthlyReturn(new YearMonth(2024, 12), 0.01m),
                new MonthlyReturn(new YearMonth(2025, 1), 0.02m)
            };

            // Act
            var series = new AssetReturnSeries(AssetKind.UsEquity, returns);

            // Assert
            var periods = series.Returns.Select(r => r.Period).ToArray();

            Assert.Equal(
                new[]
                {
                    new YearMonth(2024, 12),
                    new YearMonth(2025, 1),
                    new YearMonth(2025, 3)
                },
                periods);
        }

        [Fact]
        public void Constructor_DuplicatePeriods_ThrowsDomainValidationException()
        {
            // Arrange
            var period = new YearMonth(2025, 1);

            var returns = new[]
            {
                new MonthlyReturn(period, 0.01m),
                new MonthlyReturn(period, 0.02m)
            };

            // Act & Assert
            Assert.Throws<DomainValidationException>(
                () => new AssetReturnSeries(AssetKind.UsEquity, returns));
        }

        [Fact]
        public void GetLookbackWindow_ReturnsLastNMonthsUpToEndPeriod()
        {
            // Arrange
            var returns = new[]
            {
                new MonthlyReturn(new YearMonth(2024, 10), 0.01m),
                new MonthlyReturn(new YearMonth(2024, 11), 0.02m),
                new MonthlyReturn(new YearMonth(2024, 12), 0.03m),
                new MonthlyReturn(new YearMonth(2025, 1), 0.04m),
                new MonthlyReturn(new YearMonth(2025, 2), 0.05m),
                new MonthlyReturn(new YearMonth(2025, 3), 0.06m)
            };

            var series = new AssetReturnSeries(AssetKind.UsEquity, returns);
            var endPeriod = new YearMonth(2025, 3);
            const int lookbackMonths = 3;

            // Act
            var window = series.GetLookbackWindow(endPeriod, lookbackMonths);

            // Assert
            var periods = window.Select(r => r.Period).ToArray();

            Assert.Equal(
                new[]
                {
                    new YearMonth(2025, 1),
                    new YearMonth(2025, 2),
                    new YearMonth(2025, 3)
                },
                periods);
        }

        [Fact]
        public void GetLookbackWindow_NotEnoughData_ThrowsDomainValidationException()
        {
            // Arrange
            var returns = new[]
            {
                new MonthlyReturn(new YearMonth(2025, 1), 0.01m),
                new MonthlyReturn(new YearMonth(2025, 2), 0.02m)
            };

            var series = new AssetReturnSeries(AssetKind.UsEquity, returns);
            var endPeriod = new YearMonth(2025, 2);
            const int lookbackMonths = 3;

            // Act & Assert
            Assert.Throws<DomainValidationException>(
                () => series.GetLookbackWindow(endPeriod, lookbackMonths));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void GetLookbackWindow_LookbackMonthsLessThanOne_ThrowsArgumentOutOfRangeException(int lookbackMonths)
        {
            // Arrange
            var returns = new[]
            {
                new MonthlyReturn(new YearMonth(2025, 1), 0.01m)
            };

            var series = new AssetReturnSeries(AssetKind.UsEquity, returns);
            var endPeriod = new YearMonth(2025, 1);

            // Act
            var exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => series.GetLookbackWindow(endPeriod, lookbackMonths));

            // Assert
            Assert.Equal("lookbackMonths", exception.ParamName);
        }
    }
}
