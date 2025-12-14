using Gem.Domain.Model;

namespace Gem.Domain.Pricing
{
    public interface IPriceSeriesRepository
    {
        IReadOnlyList<PricePoint> GetSeries(Instrument instrument);
    }
}
