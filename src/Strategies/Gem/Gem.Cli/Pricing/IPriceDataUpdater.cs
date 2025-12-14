using Gem.Domain.Model;

namespace Gem.Cli.Pricing
{
    public interface IPriceDataUpdater
    {
        Task UpdateAsync(
            IEnumerable<Instrument> instruments,
            bool forceUpdate,
            CancellationToken cancellationToken = default);
    }
}
