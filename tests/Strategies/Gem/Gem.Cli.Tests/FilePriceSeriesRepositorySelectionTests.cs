using Gem.Cli.Pricing;
using Gem.Cli.Tests.TestSupport;
using Gem.Domain.Model;

namespace Gem.Cli.Tests;

public sealed class FilePriceSeriesRepositorySelectionTests
{
    private static readonly Instrument Instrument = new("AAA.US", "AAA", "aaa.us");

    [Fact]
    public void CacheNewerThanStore_Ignored_StoreUsed()
    {
        using var workspace = new TemporaryWorkspace();
        string cacheDirectory = workspace.GetPath("data", "gem", "cache");
        string storeDirectory = workspace.GetPath("data", "gem", "sample");

        WriteSeries(workspace, storeDirectory, Instrument, Series(
            (new DateOnly(2024, 3, 29), 100m)));
        WriteSeries(workspace, cacheDirectory, Instrument, Series(
            (new DateOnly(2024, 3, 29), 100m),
            (new DateOnly(2024, 4, 30), 101m)));

        var repository = new FilePriceSeriesRepository(storeDirectory, new LocalCsvPriceDataProvider());

        IReadOnlyList<PricePoint> series = repository.GetSeries(Instrument);

        Assert.Equal(new DateOnly(2024, 3, 29), series[^1].Date);
    }

    [Fact]
    public void StoreNewerThanCache_UsesStore()
    {
        using var workspace = new TemporaryWorkspace();
        string cacheDirectory = workspace.GetPath("data", "gem", "cache");
        string storeDirectory = workspace.GetPath("data", "gem", "sample");

        WriteSeries(workspace, cacheDirectory, Instrument, Series(
            (new DateOnly(2024, 2, 29), 99m),
            (new DateOnly(2024, 3, 29), 100m)));
        WriteSeries(workspace, storeDirectory, Instrument, Series(
            (new DateOnly(2024, 2, 29), 100m),
            (new DateOnly(2024, 4, 30), 105m)));

        var repository = new FilePriceSeriesRepository(storeDirectory, new LocalCsvPriceDataProvider());

        IReadOnlyList<PricePoint> series = repository.GetSeries(Instrument);

        Assert.Equal(new DateOnly(2024, 4, 30), series[^1].Date);
    }

    [Fact]
    public void EqualDates_PrefersStore()
    {
        using var workspace = new TemporaryWorkspace();
        string cacheDirectory = workspace.GetPath("data", "gem", "cache");
        string storeDirectory = workspace.GetPath("data", "gem", "sample");

        WriteSeries(workspace, cacheDirectory, Instrument, Series(
            (new DateOnly(2024, 3, 29), 100m)));
        WriteSeries(workspace, storeDirectory, Instrument, Series(
            (new DateOnly(2024, 3, 29), 200m)));

        var repository = new FilePriceSeriesRepository(storeDirectory, new LocalCsvPriceDataProvider());

        IReadOnlyList<PricePoint> series = repository.GetSeries(Instrument);

        Assert.Equal(200m, series[^1].Close);
    }

    [Fact]
    public void InvalidCache_DoesNotMatter_WhenStoreValid()
    {
        using var workspace = new TemporaryWorkspace();
        string cacheDirectory = workspace.GetPath("data", "gem", "cache");
        string storeDirectory = workspace.GetPath("data", "gem", "sample");

        workspace.WriteText("this,is,not,csv", "data", "gem", "cache", Instrument.GetCacheFileName());
        WriteSeries(workspace, storeDirectory, Instrument, Series(
            (new DateOnly(2024, 3, 29), 100m),
            (new DateOnly(2024, 4, 30), 150m)));

        var repository = new FilePriceSeriesRepository(storeDirectory, new LocalCsvPriceDataProvider());

        IReadOnlyList<PricePoint> series = repository.GetSeries(Instrument);

        Assert.Equal(150m, series[^1].Close);
    }

    [Fact]
    public void UsesSourceSymbolForFileNaming()
    {
        using var workspace = new TemporaryWorkspace();
        string cacheDirectory = workspace.GetPath("data", "gem", "cache");
        string storeDirectory = workspace.GetPath("data", "gem", "store");

        var instrument = new Instrument("DIFF.TICKER", "Diff", "custom.symbol");

        WriteSeries(workspace, storeDirectory, instrument, Series(
            (new DateOnly(2024, 3, 29), 100m)));
        WriteSeries(workspace, cacheDirectory, instrument, Series(
            (new DateOnly(2024, 3, 29), 100m)));

        var repository = new FilePriceSeriesRepository(storeDirectory, new LocalCsvPriceDataProvider());

        IReadOnlyList<PricePoint> series = repository.GetSeries(instrument);

        Assert.Single(series);
        Assert.Equal(new DateOnly(2024, 3, 29), series[0].Date);
    }

    private static void WriteSeries(
        TemporaryWorkspace workspace,
        string baseDirectory,
        Instrument instrument,
        IReadOnlyList<PricePoint> series)
    {
        string path = Path.Combine(baseDirectory, instrument.GetCacheFileName());
        string csv = "Date,Close\n" + string.Join(
            "\n",
            series.Select(point => $"{point.Date:yyyy-MM-dd},{point.Close}"));

        workspace.WriteText(csv, Path.GetRelativePath(workspace.Root, path));
    }

    private static IReadOnlyList<PricePoint> Series(params (DateOnly Date, decimal Close)[] points)
    {
        return points.Select(point => new PricePoint(point.Date, point.Close)).ToList();
    }
}
