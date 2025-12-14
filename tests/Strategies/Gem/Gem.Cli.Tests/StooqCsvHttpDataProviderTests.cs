using System.Net;
using System.Net.Http;
using Gem.Cli.Pricing;

namespace Gem.Cli.Tests;

public sealed class StooqCsvHttpDataProviderTests
{
    [Fact]
    public async Task LoadsCsvWithPolishHeader()
    {
        string csv = """
        Data,Otwarcie,Najwyzszy,Najnizszy,Zamkniecie,Wolumen
        2024-01-31,0,0,0,100.5,0
        """;

        var handler = new FakeHandler(csv);
        using var client = new HttpClient(handler);
        var provider = new StooqCsvHttpDataProvider(client);

        var points = await provider.LoadSeriesAsync("TeSt.US");

        Assert.Single(points);
        Assert.Equal(100.5m, points[0].Close);
        Assert.Equal("https://stooq.pl/q/d/l/?s=test.us&i=d", handler.LastRequestUri);
    }

    [Fact]
    public async Task ThrowsWhenResponseIsHtml()
    {
        var handler = new FakeHandler("<html>error</html>");
        using var client = new HttpClient(handler);
        var provider = new StooqCsvHttpDataProvider(client);

        await Assert.ThrowsAsync<FormatException>(() => provider.LoadSeriesAsync("TEST"));
    }

    [Fact]
    public async Task ThrowsWhenResponseIsNoData()
    {
        var handler = new FakeHandler("Brak danych");
        using var client = new HttpClient(handler);
        var provider = new StooqCsvHttpDataProvider(client);

        var ex = await Assert.ThrowsAsync<FormatException>(() => provider.LoadSeriesAsync("VOO.US"));
        Assert.Contains("Brak danych", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("voo.us", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly string _content;
        public string? LastRequestUri { get; private set; }

        public FakeHandler(string content)
        {
            _content = content;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri?.ToString();

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_content)
            };

            return Task.FromResult(response);
        }
    }
}
