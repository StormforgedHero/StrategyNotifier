using Gem.Domain.Model;

namespace Gem.Domain.Tests.Model
{
    public class GemParametersTests
    {
        [Fact]
        public void Constructor_ValidLookbackMonths_SetsProperty()
        {
            // Arrange
            const int lookbackMonths = 12;

            // Act
            var parameters = new GemParameters(lookbackMonths);

            // Assert
            Assert.Equal(lookbackMonths, parameters.LookbackMonths);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-12)]
        public void Constructor_LookbackMonthsLessThanOne_ThrowsArgumentOutOfRangeException(int lookbackMonths)
        {
            // Act
            var exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => new GemParameters(lookbackMonths));

            // Assert
            Assert.Equal("lookbackMonths", exception.ParamName);
        }
    }
}
