using Gem.Domain.Core;
using Gem.Domain.Exceptions;
using Gem.Domain.Model;
using Gem.Domain.Tests.TestData;

namespace Gem.Domain.Tests.Model
{
    public class GemInputDataTests
    {
        [Fact]
        public void Constructor_ValidSeries_SetsProperties()
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

            // Act
            var input = new GemInputData(usSeries, exUsSeries, safeSeries);

            // Assert
            Assert.Same(usSeries, input.UsEquity);
            Assert.Same(exUsSeries, input.ExUsEquity);
            Assert.Same(safeSeries, input.SafeAsset);
        }

        [Fact]
        public void Constructor_NullUsSeries_ThrowsArgumentNullException()
        {
            // Arrange
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

            // Act
            var exception = Assert.Throws<ArgumentNullException>(
                () => new GemInputData(null!, exUsSeries, safeSeries));

            // Assert
            Assert.Equal("usEquity", exception.ParamName);
        }

        [Fact]
        public void Constructor_NullExUsSeries_ThrowsArgumentNullException()
        {
            // Arrange
            var usSeries = GemTestDataFactory.CreateSeries(
                AssetKind.UsEquity,
                2025,
                1,
                0.01m);

            var safeSeries = GemTestDataFactory.CreateSeries(
                AssetKind.SafeAsset,
                2025,
                1,
                0.01m);

            // Act
            var exception = Assert.Throws<ArgumentNullException>(
                () => new GemInputData(usSeries, null!, safeSeries));

            // Assert
            Assert.Equal("exUsEquity", exception.ParamName);
        }

        [Fact]
        public void Constructor_NullSafeSeries_ThrowsArgumentNullException()
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

            // Act
            var exception = Assert.Throws<ArgumentNullException>(
                () => new GemInputData(usSeries, exUsSeries, null!));

            // Assert
            Assert.Equal("safeAsset", exception.ParamName);
        }

        [Fact]
        public void Constructor_UsSeriesWithWrongAssetKind_ThrowsDomainValidationException()
        {
            // Arrange
            var usSeriesWithWrongKind = GemTestDataFactory.CreateSeries(
                AssetKind.SafeAsset,
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

            // Act & Assert
            Assert.Throws<DomainValidationException>(
                () => new GemInputData(usSeriesWithWrongKind, exUsSeries, safeSeries));
        }

        [Fact]
        public void Constructor_ExUsSeriesWithWrongAssetKind_ThrowsDomainValidationException()
        {
            // Arrange
            var usSeries = GemTestDataFactory.CreateSeries(
                AssetKind.UsEquity,
                2025,
                1,
                0.01m);

            var exUsSeriesWithWrongKind = GemTestDataFactory.CreateSeries(
                AssetKind.SafeAsset,
                2025,
                1,
                0.01m);

            var safeSeries = GemTestDataFactory.CreateSeries(
                AssetKind.SafeAsset,
                2025,
                1,
                0.01m);

            // Act & Assert
            Assert.Throws<DomainValidationException>(
                () => new GemInputData(usSeries, exUsSeriesWithWrongKind, safeSeries));
        }

        [Fact]
        public void Constructor_SafeSeriesWithWrongAssetKind_ThrowsDomainValidationException()
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

            var safeSeriesWithWrongKind = GemTestDataFactory.CreateSeries(
                AssetKind.UsEquity,
                2025,
                1,
                0.01m);

            // Act & Assert
            Assert.Throws<DomainValidationException>(
                () => new GemInputData(usSeries, exUsSeries, safeSeriesWithWrongKind));
        }
    }
}
