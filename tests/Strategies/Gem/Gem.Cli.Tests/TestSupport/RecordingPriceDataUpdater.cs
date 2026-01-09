using System.Globalization;
using System.Text;
using Gem.Cli.Pricing;
using Gem.Domain.Model;

namespace Gem.Cli.Tests.TestSupport;

internal sealed class RecordingPriceDataUpdater : IPriceDataUpdater
{
    private readonly string _cacheDirectory;
    private readonly string? _storeDirectory;
    private readonly bool _saveUpdatedDataToStore;
    private readonly Func<Instrument, IReadOnlyList<PricePoint>> _seriesFactory;

    public RecordingPriceDataUpdater(
        string cacheDirectory,
        Func<Instrument, IReadOnlyList<PricePoint>> seriesFactory,
        bool saveUpdatedDataToStore,
        string? storeDirectory = null)
    {
        if (string.IsNullOrWhiteSpace(cacheDirectory))
        {
            throw new ArgumentException("Cache directory must not be empty.", nameof(cacheDirectory));
        }

        _cacheDirectory = cacheDirectory;
        _saveUpdatedDataToStore = saveUpdatedDataToStore;

        if (_saveUpdatedDataToStore)
        {
            if (string.IsNullOrWhiteSpace(storeDirectory))
            {
                throw new ArgumentException("Store directory must not be empty when saving updates.", nameof(storeDirectory));
            }

            _storeDirectory = storeDirectory;
        }

        _seriesFactory = seriesFactory ?? throw new ArgumentNullException(nameof(seriesFactory));
    }

    public bool WasCalled => Calls > 0;

    public bool LastForceUpdate { get; private set; }

    public int Calls { get; private set; }

    public List<string> WrittenFiles { get; } = new();

    public Task UpdateAsync(IEnumerable<Instrument> instruments, bool forceUpdate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instruments);

        Calls++;
        LastForceUpdate = forceUpdate;

        Directory.CreateDirectory(_cacheDirectory);

        foreach (Instrument instrument in instruments)
        {
            IReadOnlyList<PricePoint> series = _seriesFactory(instrument);
            string path = Path.Combine(_cacheDirectory, instrument.GetCacheFileName());
            WriteSeries(path, series);
            WrittenFiles.Add(path);

            if (_saveUpdatedDataToStore)
            {
                string storePath = Path.Combine(_storeDirectory!, instrument.GetCacheFileName());
                WriteSeries(storePath, series);
            }
        }

        return Task.CompletedTask;
    }

    private static void WriteSeries(string path, IReadOnlyList<PricePoint> series)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Date,Close");

        foreach (PricePoint point in series.OrderBy(p => p.Date))
        {
            builder.Append(point.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.AppendLine(point.Close.ToString(CultureInfo.InvariantCulture));
        }

        File.WriteAllText(path, builder.ToString(), Utf8TestEncoding.Utf8NoBom);
    }
}
