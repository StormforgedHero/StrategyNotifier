using Gem.Domain.Model;
using Gem.Domain.Pricing;

namespace Gem.Cli.Tests;

public sealed class PriceCsvParserTests
{
    [Fact]
    public void ParsesPolishHeaderWithComma()
    {
        string csv = """
        Data,Otwarcie,Najwyzszy,Najnizszy,Zamkniecie,Wolumen
        2024-01-31,0,0,0,100.5,0
        2024-02-29,0,0,0,101.25,0
        """;

        IReadOnlyList<PricePoint> points = PriceCsvParser.ParseFromText(csv);

        Assert.Equal(2, points.Count);
        Assert.Equal(new DateOnly(2024, 2, 29), points[1].Date);
        Assert.Equal(101.25m, points[1].Close);
    }

    [Fact]
    public void ParsesPolishHeaderWithSemicolon()
    {
        string csv = """
        Data;Otwarcie;Najwyzszy;Najnizszy;Zamkniecie;Wolumen
        2024-01-31;0;0;0;100.5;0
        """;

        IReadOnlyList<PricePoint> points = PriceCsvParser.ParseFromText(csv);

        Assert.Single(points);
        Assert.Equal(100.5m, points[0].Close);
    }

    [Fact]
    public void ParsesMinimalDateClose()
    {
        string csv = """
        Date,Close
        2024-01-31,100
        """;

        IReadOnlyList<PricePoint> points = PriceCsvParser.ParseFromText(csv);

        Assert.Single(points);
        Assert.Equal(new DateOnly(2024, 1, 31), points[0].Date);
        Assert.Equal(100m, points[0].Close);
    }

    [Fact]
    public void ParsesEnglishHeaderWithSemicolon()
    {
        string csv = """
        Date;Open;High;Low;Close;Volume
        2024-01-31;0;0;0;99.5;0
        """;

        IReadOnlyList<PricePoint> points = PriceCsvParser.ParseFromText(csv);

        Assert.Single(points);
        Assert.Equal(99.5m, points[0].Close);
    }

    [Fact]
    public void ParsesOaDate()
    {
        string csv = """
        Date,Close
        44197,100
        """;

        IReadOnlyList<PricePoint> points = PriceCsvParser.ParseFromText(csv);

        Assert.Single(points);
        Assert.Equal(new DateOnly(2021, 1, 1), points[0].Date);
    }

    [Fact]
    public void BrakDanych_ThrowsFormatException()
    {
        string csv = "Brak danych";
        var ex = Assert.Throws<FormatException>(() => PriceCsvParser.ParseFromText(csv));
        Assert.Contains("Brak danych", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParsesPolishHeaderWithDiacritics()
    {
        string csv = """
        Data;Otwarcie;Najwyższy;Najnizszy;Zamknięcie;Wolumen
        2024-01-31;0;0;0;101,5;0
        """;

        IReadOnlyList<PricePoint> points = PriceCsvParser.ParseFromText(csv);

        Assert.Single(points);
        Assert.Equal(101.5m, points[0].Close);
    }
}
