using Gem.Cli.Pricing;
using Gem.Domain.Model;

namespace Gem.Cli.Tests.TestSupport;

internal sealed class NoOpPriceDataUpdater : IPriceDataUpdater
{
    public Task UpdateAsync(IEnumerable<Instrument> instruments, bool forceUpdate, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
