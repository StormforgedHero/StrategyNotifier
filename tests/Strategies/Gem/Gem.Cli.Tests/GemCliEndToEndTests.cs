using System.Globalization;
using System.Text.Json;
using Gem.Cli;
using Gem.Cli.Tests.TestSupport;

namespace Gem.Cli.Tests;

public sealed class GemCliEndToEndTests
{
    [Fact]
    public void Program_GeneratesSignalsFromDailyPrices()
    {
        using var workspace = new TemporaryWorkspace();
        string dataDirectory = workspace.GetPath("data", "gem", "cache");
        WriteSamplePrices(dataDirectory);

        string outputPath = workspace.GetPath("dist", "gem", "signals.json");
        WriteConfig(workspace, dataDirectory, outputPath, windowMonths: 3, updateEnabled: false);

        int exitCode = Program.Run(workspace.Root, new StringWriter(), new StringWriter(), Array.Empty<string>());

        Assert.Equal(Program.ExitCodeSuccess, exitCode);
        Assert.True(File.Exists(outputPath));
        Assert.False(HasUtf8Bom(outputPath));

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(outputPath, Utf8TestEncoding.Utf8NoBom));
        JsonElement root = document.RootElement;

        Assert.Equal(JsonValueKind.Array, root.ValueKind);
        Assert.True(root.GetArrayLength() >= 1);

        JsonElement first = root[0];
        Assert.True(first.TryGetProperty("date", out JsonElement dateElement));

        string? dateString = dateElement.GetString();
        Assert.False(string.IsNullOrWhiteSpace(dateString));

        DateOnly firstDate = DateOnly.ParseExact(dateString!, "yyyy-MM-dd", CultureInfo.InvariantCulture);

        if (root.GetArrayLength() > 1)
        {
            string secondDateString = root[1].GetProperty("date").GetString() ?? throw new InvalidOperationException("Missing date in second signal.");
            DateOnly secondDate = DateOnly.ParseExact(secondDateString, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            Assert.True(firstDate <= secondDate);
        }

        Assert.True(first.TryGetProperty("windowMonths", out _));
        Assert.True(first.TryGetProperty("isRiskOn", out _));
        Assert.True(first.TryGetProperty("absoluteReturn", out _));
        Assert.True(first.TryGetProperty("relativeRank", out _));
        Assert.True(first.TryGetProperty("comment", out _));

        JsonElement allocations = first.GetProperty("allocations");
        Assert.Equal(JsonValueKind.Array, allocations.ValueKind);
        Assert.True(allocations.GetArrayLength() >= 1);

        JsonElement allocation = allocations[0];
        Assert.True(allocation.TryGetProperty("ticker", out _));
        Assert.True(allocation.TryGetProperty("name", out _));
        Assert.True(allocation.TryGetProperty("weight", out _));
    }

    [Fact]
    public void Program_NoUpdateFlag_SkipsHttpEvenWhenCacheIsStale()
    {
        using var workspace = new TemporaryWorkspace();
        string dataDirectory = workspace.GetPath("data", "gem", "cache");
        WriteSamplePrices(dataDirectory);

        foreach (string file in Directory.GetFiles(dataDirectory))
        {
            File.SetLastWriteTimeUtc(file, DateTime.UtcNow.Subtract(TimeSpan.FromDays(90)));
        }

        string outputPath = workspace.GetPath("dist", "gem", "signals.json");
        WriteConfig(workspace, dataDirectory, outputPath, windowMonths: 3, updateEnabled: true);

        int exitCode = Program.Run(workspace.Root, new StringWriter(), new StringWriter(), new[] { "--no-update" });

        Assert.Equal(Program.ExitCodeSuccess, exitCode);
        Assert.True(File.Exists(outputPath));
    }

    private static bool HasUtf8Bom(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        return bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
    }

    private static void WriteSamplePrices(string dataDirectory)
    {
        Directory.CreateDirectory(dataDirectory);

        File.WriteAllText(
            Path.Combine(dataDirectory, "voo.us.csv"),
            """
            Date,Close
            2024-01-31,100
            2024-02-29,105
            2024-03-29,110
            2024-04-30,120
            2024-05-31,118
            """,
            Utf8TestEncoding.Utf8NoBom);

        File.WriteAllText(
            Path.Combine(dataDirectory, "veu.us.csv"),
            """
            Date,Close
            2024-01-31,100
            2024-02-29,102
            2024-03-28,103
            2024-04-30,104
            2024-05-31,110
            """,
            Utf8TestEncoding.Utf8NoBom);

        File.WriteAllText(
            Path.Combine(dataDirectory, "agg.us.csv"),
            """
            Date,Close
            2024-01-31,100
            2024-02-29,101
            2024-03-29,101.5
            2024-04-30,102
            2024-05-31,102.5
            """,
            Utf8TestEncoding.Utf8NoBom);
    }

    private static void WriteConfig(
        TemporaryWorkspace workspace,
        string dataDirectory,
        string outputPath,
        int windowMonths,
        bool updateEnabled)
    {
        string json = $$"""
        {
          "windowMonths": {{windowMonths}},
          "rankingMode": "Top1",
          "instruments": {
            "usEquity": { "ticker": "VOO.US", "name": "US Equity", "sourceSymbol": "voo.us" },
            "exUsEquity": { "ticker": "VEU.US", "name": "Ex-US Equity", "sourceSymbol": "veu.us" },
            "safeAsset": { "ticker": "AGG.US", "name": "Bonds", "sourceSymbol": "agg.us" }
          },
          "dataDirectory": "{{dataDirectory.Replace("\\", "\\\\")}}",
          "outputPath": "{{outputPath.Replace("\\", "\\\\")}}",
          "update": {
            "enabledByDefault": {{(updateEnabled ? "true" : "false")}},
            "freshnessDays": 2,
            "minHoursBetweenUpdates": 0.01
          }
        }
        """;

        workspace.WriteText(json, "config", "gem", "gem.config.json");
    }
}
