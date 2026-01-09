using Gem.Cli.Tests.TestSupport;
using System.Globalization;
using System.Text.Json;

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
        WriteConfig(workspace, dataDirectory, outputPath, windowMonths: 3);

        int exitCode = Program.Run(workspace.Root, new StringWriter(), new StringWriter(), Array.Empty<string>());

        Assert.Equal(Program.ExitCodeSuccess, exitCode);
        Assert.True(File.Exists(outputPath));
        Assert.False(HasUtf8Bom(outputPath));

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(outputPath, Utf8TestEncoding.Utf8NoBom));
        JsonElement root = document.RootElement;

        Assert.Equal(JsonValueKind.Array, root.ValueKind);
        Assert.True(root.GetArrayLength() >= 1);

        var dates = new List<DateOnly>();
        var seenMonths = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < root.GetArrayLength(); i++)
        {
            JsonElement signal = root[i];
            Assert.True(signal.TryGetProperty("date", out JsonElement dateElement));
            Assert.True(signal.TryGetProperty("windowMonths", out JsonElement windowElement));
            Assert.True(signal.TryGetProperty("isRiskOn", out JsonElement riskOnElement));
            Assert.True(signal.TryGetProperty("absoluteReturn", out JsonElement absoluteReturnElement));
            Assert.True(signal.TryGetProperty("relativeRank", out JsonElement relativeRankElement));
            Assert.True(signal.TryGetProperty("comment", out JsonElement commentElement));
            Assert.True(signal.TryGetProperty("allocations", out JsonElement allocationsElement));

            Assert.Equal(JsonValueKind.String, dateElement.ValueKind);
            Assert.True(windowElement.ValueKind is JsonValueKind.Number);
            Assert.True(riskOnElement.ValueKind is JsonValueKind.True or JsonValueKind.False);
            Assert.True(absoluteReturnElement.ValueKind is JsonValueKind.Number);
            Assert.True(relativeRankElement.ValueKind is JsonValueKind.Number);
            Assert.Equal(JsonValueKind.String, commentElement.ValueKind);
            Assert.Equal(JsonValueKind.Array, allocationsElement.ValueKind);

            string dateString = dateElement.GetString() ?? throw new InvalidOperationException("Missing date value.");
            DateOnly parsedDate = DateOnly.ParseExact(dateString, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            dates.Add(parsedDate);

            string monthKey = $"{parsedDate.Year:D4}-{parsedDate.Month:D2}";
            Assert.True(seenMonths.Add(monthKey), "Duplicate month detected in signals.");

            Assert.True(allocationsElement.GetArrayLength() >= 1);

            JsonElement allocation = allocationsElement[0];
            Assert.True(allocation.TryGetProperty("ticker", out JsonElement tickerElement));
            Assert.True(allocation.TryGetProperty("name", out JsonElement nameElement));
            Assert.True(allocation.TryGetProperty("weight", out JsonElement weightElement));

            Assert.Equal(JsonValueKind.String, tickerElement.ValueKind);
            Assert.Equal(JsonValueKind.String, nameElement.ValueKind);
            Assert.True(weightElement.ValueKind is JsonValueKind.Number);
            Assert.False(string.IsNullOrWhiteSpace(commentElement.GetString()));
        }

        for (int i = 1; i < dates.Count; i++)
        {
            Assert.True(dates[i - 1] >= dates[i], "Signals must be ordered newest-first by date.");
        }
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
        WriteConfig(workspace, dataDirectory, outputPath, windowMonths: 3);

        int exitCode = Program.Run(workspace.Root, new StringWriter(), new StringWriter(), new[] { "--no-update" });

        Assert.Equal(Program.ExitCodeSuccess, exitCode);
        Assert.True(File.Exists(outputPath));
    }

    [Fact]
    public void Program_CreatesOutputDirectoryWhenMissing()
    {
        using var workspace = new TemporaryWorkspace();
        string dataDirectory = workspace.GetPath("data", "gem", "cache");
        WriteSamplePrices(dataDirectory);

        string outputPath = workspace.GetPath("out", "nested", "gem", "signals.json");
        WriteConfig(workspace, dataDirectory, outputPath, windowMonths: 3);

        int exitCode = Program.Run(workspace.Root, new StringWriter(), new StringWriter(), Array.Empty<string>());

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
        int windowMonths)
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
          "storeDirectory": "{{dataDirectory.Replace("\\", "\\\\")}}",
          "cacheDirectory": "{{workspace.GetPath("cache").Replace("\\", "\\\\")}}",
          "outputPath": "{{outputPath.Replace("\\", "\\\\")}}",
          "update": {
            "autoUpdateEnabled": false,
            "maxAgeDays": 2,
            "minMinutesBetweenAttempts": 0,
            "saveUpdatedDataToStore": true
          }
        }
        """;

        workspace.WriteText(json, "config", "gem", "gem.config.json");
    }
}
