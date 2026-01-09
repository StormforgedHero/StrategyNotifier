using System.Net;
using System.Text;

namespace Gem.Cli.Tests.TestSupport;

internal sealed class FakeStooqHttpMessageHandler : HttpMessageHandler
{
    private readonly Dictionary<string, string> _responses;
    public Dictionary<string, int> RequestCounts { get; } = new(StringComparer.OrdinalIgnoreCase);
    public string? LastRequestUri { get; private set; }

    public FakeStooqHttpMessageHandler(Dictionary<string, string> responses)
    {
        _responses = responses ?? throw new ArgumentNullException(nameof(responses));
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string symbol = ExtractSymbol(request.RequestUri);

        LastRequestUri = request.RequestUri?.ToString();
        RequestCounts[symbol] = RequestCounts.TryGetValue(symbol, out int count) ? count + 1 : 1;

        if (_responses.TryGetValue(symbol, out string? csv))
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(csv, Encoding.UTF8, "text/csv")
            });
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }

    private static string ExtractSymbol(Uri? uri)
    {
        if (uri is null)
        {
            return string.Empty;
        }

        const string marker = "s=";
        string query = uri.Query;
        int idx = query.IndexOf(marker, StringComparison.OrdinalIgnoreCase);

        if (idx < 0)
        {
            return string.Empty;
        }

        string remainder = query[(idx + marker.Length)..];
        int end = remainder.IndexOfAny(new[] { '&', '#' });
        if (end >= 0)
        {
            remainder = remainder[..end];
        }

        return remainder.Trim();
    }
}
