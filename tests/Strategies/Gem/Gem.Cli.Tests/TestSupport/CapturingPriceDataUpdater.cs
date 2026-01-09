using Gem.Cli.Pricing;
using Gem.Domain.Model;

namespace Gem.Cli.Tests.TestSupport;

internal sealed class CapturingPriceDataUpdater : IPriceDataUpdater
{
    public bool WasCalled => Calls > 0;

    public bool LastForceUpdate { get; private set; }

    public List<Instrument> LastInstruments { get; } = new();

    public int Calls { get; private set; }

    public Task UpdateAsync(IEnumerable<Instrument> instruments, bool forceUpdate, CancellationToken cancellationToken = default)
    {
        Calls++;
        LastForceUpdate = forceUpdate;

        LastInstruments.Clear();
        LastInstruments.AddRange(instruments);

        return Task.CompletedTask;
    }
}
