using Gem.Domain.Model;

namespace Gem.Domain.Pricing
{
    public interface IPriceDataProvider
    {
        IReadOnlyList<PricePoint> LoadSeries(string path);
    }
}
