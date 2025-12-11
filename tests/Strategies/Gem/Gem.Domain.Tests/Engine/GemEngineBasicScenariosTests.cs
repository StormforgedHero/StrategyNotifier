using Gem.Domain.Core;
using Gem.Domain.Engine;
using Gem.Domain.Model;
using Gem.Domain.Tests.TestData;

namespace Gem.Domain.Tests.Engine
{
    public class GemEngineBasicScenariosTests
    {
        [Fact]
        public void GenerateSignals_UsAlwaysBeatsOthers_AlwaysPositionsUsEquity()
        {
            // Arrange
            var usSeries = GemTestDataFactory.CreateSeries(
                AssetKind.UsEquity,
                2025,
                1,
                0.02m,
                0.02m,
                0.02m,
                0.02m,
                0.02m);

            var exUsSeries = GemTestDataFactory.CreateSeries(
                AssetKind.ExUsEquity,
                2025,
                1,
                0.01m,
                0.01m,
                0.01m,
                0.01m,
                0.01m);

            var safeSeries = GemTestDataFactory.CreateSeries(
                AssetKind.SafeAsset,
                2025,
                1,
                0.00m,
                0.00m,
                0.00m,
                0.00m,
                0.00m);

            var input = new GemInputData(usSeries, exUsSeries, safeSeries);
            var parameters = new GemParameters(lookbackMonths: 3);
            var engine = new GemEngine();

            // Act
            var signals = engine.GenerateSignals(input, parameters);

            // Assert
            Assert.Equal(3, signals.Count);

            Assert.All(signals, signal =>
            {
                Assert.Equal(AssetKind.UsEquity, signal.Position);
            });

            Assert.Equal(new YearMonth(2025, 3), signals[0].Period);
            Assert.Equal(new YearMonth(2025, 4), signals[1].Period);
            Assert.Equal(new YearMonth(2025, 5), signals[2].Period);
        }

        [Fact]
        public void GenerateSignals_ExUsAlwaysBeatsOthers_AlwaysPositionsExUsEquity()
        {
            // Arrange
            var usSeries = GemTestDataFactory.CreateSeries(
                AssetKind.UsEquity,
                2025,
                1,
                0.01m,
                0.01m,
                0.01m,
                0.01m,
                0.01m);

            var exUsSeries = GemTestDataFactory.CreateSeries(
                AssetKind.ExUsEquity,
                2025,
                1,
                0.02m,
                0.02m,
                0.02m,
                0.02m,
                0.02m);

            var safeSeries = GemTestDataFactory.CreateSeries(
                AssetKind.SafeAsset,
                2025,
                1,
                0.00m,
                0.00m,
                0.00m,
                0.00m,
                0.00m);

            var input = new GemInputData(usSeries, exUsSeries, safeSeries);
            var parameters = new GemParameters(lookbackMonths: 3);
            var engine = new GemEngine();

            // Act
            var signals = engine.GenerateSignals(input, parameters);

            // Assert
            Assert.Equal(3, signals.Count);

            Assert.All(signals, signal =>
            {
                Assert.Equal(AssetKind.ExUsEquity, signal.Position);
            });

            Assert.Equal(new YearMonth(2025, 3), signals[0].Period);
            Assert.Equal(new YearMonth(2025, 4), signals[1].Period);
            Assert.Equal(new YearMonth(2025, 5), signals[2].Period);
        }

        [Fact]
        public void GenerateSignals_SafeAlwaysBeatsRiskOn_AlwaysPositionsSafeAsset()
        {
            // Arrange
            var usSeries = GemTestDataFactory.CreateSeries(
                AssetKind.UsEquity,
                2025,
                1,
                0.01m,
                0.01m,
                0.01m,
                0.01m,
                0.01m);

            var exUsSeries = GemTestDataFactory.CreateSeries(
                AssetKind.ExUsEquity,
                2025,
                1,
                0.01m,
                0.01m,
                0.01m,
                0.01m,
                0.01m);

            var safeSeries = GemTestDataFactory.CreateSeries(
                AssetKind.SafeAsset,
                2025,
                1,
                0.05m,
                0.05m,
                0.05m,
                0.05m,
                0.05m);

            var input = new GemInputData(usSeries, exUsSeries, safeSeries);
            var parameters = new GemParameters(lookbackMonths: 3);
            var engine = new GemEngine();

            // Act
            var signals = engine.GenerateSignals(input, parameters);

            // Assert
            Assert.Equal(3, signals.Count);

            Assert.All(signals, signal =>
            {
                Assert.Equal(AssetKind.SafeAsset, signal.Position);
            });

            Assert.Equal(new YearMonth(2025, 3), signals[0].Period);
            Assert.Equal(new YearMonth(2025, 4), signals[1].Period);
            Assert.Equal(new YearMonth(2025, 5), signals[2].Period);
        }
    }
}
