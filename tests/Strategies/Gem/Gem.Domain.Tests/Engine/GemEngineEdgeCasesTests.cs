using Gem.Domain.Core;
using Gem.Domain.Engine;
using Gem.Domain.Model;
using Gem.Domain.Tests.TestData;

namespace Gem.Domain.Tests.Engine
{
    public class GemEngineEdgeCasesTests
    {
        [Fact]
        public void GenerateSignals_NullInput_ThrowsArgumentNullException()
        {
            // Arrange
            var engine = new GemEngine();
            var parameters = new GemParameters(lookbackMonths: 3);

            // Act
            var exception = Assert.Throws<ArgumentNullException>(
                () => engine.GenerateSignals(input: null!, parameters));

            // Assert
            Assert.Equal("input", exception.ParamName);
        }

        [Fact]
        public void GenerateSignals_NullParameters_ThrowsArgumentNullException()
        {
            // Arrange
            var usSeries = GemTestDataFactory.CreateSeries(
                AssetKind.UsEquity,
                2025,
                1,
                0.01m);

            var exUsSeries = GemTestDataFactory.CreateSeries(
                AssetKind.ExUsEquity,
                2025,
                1,
                0.01m);

            var safeSeries = GemTestDataFactory.CreateSeries(
                AssetKind.SafeAsset,
                2025,
                1,
                0.01m);

            var input = new GemInputData(usSeries, exUsSeries, safeSeries);
            var engine = new GemEngine();

            // Act
            var exception = Assert.Throws<ArgumentNullException>(
                () => engine.GenerateSignals(input, parameters: null!));

            // Assert
            Assert.Equal("parameters", exception.ParamName);
        }

        [Fact]
        public void GenerateSignals_WithInsufficientLookback_ReturnsEmptyList()
        {
            // Arrange
            var usSeries = GemTestDataFactory.CreateSeries(
                AssetKind.UsEquity,
                2025,
                1,
                0.01m,
                0.02m);

            var exUsSeries = GemTestDataFactory.CreateSeries(
                AssetKind.ExUsEquity,
                2025,
                1,
                0.01m,
                0.03m);

            var safeSeries = GemTestDataFactory.CreateSeries(
                AssetKind.SafeAsset,
                2025,
                1,
                0.005m,
                0.006m);

            var input = new GemInputData(usSeries, exUsSeries, safeSeries);
            var parameters = new GemParameters(lookbackMonths: 3);
            var engine = new GemEngine();

            // Act
            var signals = engine.GenerateSignals(input, parameters);

            // Assert
            Assert.Empty(signals);
        }

        [Fact]
        public void GenerateSignals_WhenSafeBecomesSuperior_SwitchesToSafeAsset()
        {
            // Arrange
            var usSeries = GemTestDataFactory.CreateSeries(
                AssetKind.UsEquity,
                2025,
                1,
                0.10m,   // 2025-01
                0.10m,   // 2025-02
                -0.10m,  // 2025-03
                -0.10m); // 2025-04

            var exUsSeries = GemTestDataFactory.CreateSeries(
                AssetKind.ExUsEquity,
                2025,
                1,
                0.05m,   // 2025-01
                0.05m,   // 2025-02
                0.00m,   // 2025-03
                0.00m);  // 2025-04

            var safeSeries = GemTestDataFactory.CreateSeries(
                AssetKind.SafeAsset,
                2025,
                1,
                0.01m,   // 2025-01
                0.01m,   // 2025-02
                0.50m,   // 2025-03
                0.50m);  // 2025-04

            var input = new GemInputData(usSeries, exUsSeries, safeSeries);
            var parameters = new GemParameters(lookbackMonths: 2);
            var engine = new GemEngine();

            // Act
            var signals = engine.GenerateSignals(input, parameters);

            // Assert
            Assert.Equal(3, signals.Count);

            Assert.Equal(new YearMonth(2025, 2), signals[0].Period);
            Assert.Equal(AssetKind.UsEquity, signals[0].Position);

            Assert.Equal(new YearMonth(2025, 3), signals[1].Period);
            Assert.Equal(AssetKind.SafeAsset, signals[1].Position);

            Assert.Equal(new YearMonth(2025, 4), signals[2].Period);
            Assert.Equal(AssetKind.SafeAsset, signals[2].Position);
        }

        [Fact]
        public void GenerateSignals_EqualUsAndExUsMomentum_PrefersUsEquity()
        {
            // Arrange
            var usSeries = GemTestDataFactory.CreateSeries(
                AssetKind.UsEquity,
                2025,
                1,
                0.10m,
                0.10m,
                0.10m);

            var exUsSeries = GemTestDataFactory.CreateSeries(
                AssetKind.ExUsEquity,
                2025,
                1,
                0.10m,
                0.10m,
                0.10m);

            var safeSeries = GemTestDataFactory.CreateSeries(
                AssetKind.SafeAsset,
                2025,
                1,
                0.01m,
                0.01m,
                0.01m);

            var input = new GemInputData(usSeries, exUsSeries, safeSeries);
            var parameters = new GemParameters(lookbackMonths: 2);
            var engine = new GemEngine();

            // Act
            var signals = engine.GenerateSignals(input, parameters);

            // Assert
            Assert.Equal(2, signals.Count);

            Assert.All(signals, signal =>
            {
                Assert.Equal(AssetKind.UsEquity, signal.Position);
            });

            Assert.Equal(new YearMonth(2025, 2), signals[0].Period);
            Assert.Equal(new YearMonth(2025, 3), signals[1].Period);
        }

        [Fact]
        public void GenerateSignals_WhenAnySeriesIsEmpty_ReturnsEmptyList()
        {
            // Arrange
            var usNonEmptySeries = GemTestDataFactory.CreateSeries(
                AssetKind.UsEquity,
                2025,
                1,
                0.01m,
                0.02m);

            var exUsNonEmptySeries = GemTestDataFactory.CreateSeries(
                AssetKind.ExUsEquity,
                2025,
                1,
                0.01m,
                0.03m);

            var safeNonEmptySeries = GemTestDataFactory.CreateSeries(
                AssetKind.SafeAsset,
                2025,
                1,
                0.005m,
                0.006m);

            var emptyUsSeries = new AssetReturnSeries(
                AssetKind.UsEquity,
                Enumerable.Empty<MonthlyReturn>());

            var emptyExUsSeries = new AssetReturnSeries(
                AssetKind.ExUsEquity,
                Enumerable.Empty<MonthlyReturn>());

            var emptySafeSeries = new AssetReturnSeries(
                AssetKind.SafeAsset,
                Enumerable.Empty<MonthlyReturn>());

            var parameters = new GemParameters(lookbackMonths: 2);
            var engine = new GemEngine();

            // Act
            var signalsWithEmptyUs = engine.GenerateSignals(
                new GemInputData(emptyUsSeries, exUsNonEmptySeries, safeNonEmptySeries),
                parameters);

            var signalsWithEmptyExUs = engine.GenerateSignals(
                new GemInputData(usNonEmptySeries, emptyExUsSeries, safeNonEmptySeries),
                parameters);

            var signalsWithEmptySafe = engine.GenerateSignals(
                new GemInputData(usNonEmptySeries, exUsNonEmptySeries, emptySafeSeries),
                parameters);

            // Assert
            Assert.Empty(signalsWithEmptyUs);
            Assert.Empty(signalsWithEmptyExUs);
            Assert.Empty(signalsWithEmptySafe);
        }
    }
}
