using System.Globalization;
using System.Text.Json;
using Gem.Cli;
using Gem.Cli.Contracts;
using Gem.Cli.Tests.TestSupport;

namespace Gem.Cli.Tests;

public sealed class GemCliGoldenTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private static string GoldenRoot => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "TestData", "Golden"));

    public static IEnumerable<object[]> GoldenCases =>
        Directory.GetDirectories(GoldenRoot)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => new object[] { Path.GetFileName(path)! });

    [Theory]
    [MemberData(nameof(GoldenCases))]
    public void Cli_OutputMatchesSnapshot(string caseName) => RunGoldenCase(caseName);

    [Fact]
    public void Cli_UsesInvariantCultureUnderPolishLocale()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            var polish = new CultureInfo("pl-PL");
            CultureInfo.CurrentCulture = polish;
            CultureInfo.CurrentUICulture = polish;

            RunGoldenCase("US_VOO_VEU_AGG_Top1");
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Fact]
    public void Normalization_SortsSignalsAndAllocations()
    {
        var unsorted = new List<SignalOutput>
        {
            new SignalOutput
            {
                Date = "2024-02-29",
                WindowMonths = 1,
                IsRiskOn = true,
                AbsoluteReturn = 0.1m,
                RelativeRank = 2,
                Comment = "test",
                Allocations = new[]
                {
                    new AllocationOutput { Ticker = "B", Name = "B", Weight = 0.2m },
                    new AllocationOutput { Ticker = "A", Name = "A", Weight = 0.8m }
                }
            },
            new SignalOutput
            {
                Date = "2024-01-31",
                WindowMonths = 1,
                IsRiskOn = true,
                AbsoluteReturn = 0.2m,
                RelativeRank = 1,
                Comment = "test",
                Allocations = new[]
                {
                    new AllocationOutput { Ticker = "C", Name = "C", Weight = 0.3m },
                    new AllocationOutput { Ticker = "D", Name = "D", Weight = 0.7m }
                }
            }
        };

        IReadOnlyList<SignalOutput> normalized = Normalize(unsorted);

        Assert.Equal("2024-01-31", normalized[0].Date);
        Assert.Equal(new[] { "D", "C" }, normalized[0].Allocations.Select(a => a.Ticker));
        Assert.Equal("2024-02-29", normalized[1].Date);
        Assert.Equal(new[] { "A", "B" }, normalized[1].Allocations.Select(a => a.Ticker));
    }

    private static void AssertSnapshotsMatch(string expectedPath, string actualPath)
    {
        string actualNormalized = NormalizeSnapshot(actualPath);
        bool update = string.Equals(Environment.GetEnvironmentVariable("UPDATE_GOLDEN"), "1", StringComparison.Ordinal);

        if (update)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(expectedPath)!);
            Utf8TestEncoding.WriteAllTextUtf8NoBom(expectedPath, actualNormalized);
            return;
        }

        Assert.True(File.Exists(expectedPath), $"Golden snapshot missing at {expectedPath}. Set UPDATE_GOLDEN=1 to regenerate.");

        string expectedNormalized = NormalizeSnapshot(expectedPath);
        Assert.Equal(expectedNormalized, actualNormalized);
    }

    private static string NormalizeSnapshot(string path)
    {
        IReadOnlyList<SignalOutput> signals = LoadSignals(path);
        IReadOnlyList<SignalOutput> normalized = Normalize(signals);
        return JsonSerializer.Serialize(normalized, SerializerOptions);
    }

    private static void RunGoldenCase(string caseName)
    {
        string casePath = Path.Combine(GoldenRoot, caseName);

        using var workspace = new TemporaryWorkspace();
        CopyDirectory(casePath, workspace.Root);

        using var scope = new CurrentDirectoryScope(workspace.Root);
        int exitCode = Program.Run(workspace.Root, new StringWriter(), new StringWriter(), new[] { "--no-update" });

        Assert.Equal(Program.ExitCodeSuccess, exitCode);

        string actualPath = Path.Combine(workspace.Root, "dist", "gem", "signals.json");
        Assert.True(File.Exists(actualPath), "signals.json was not produced by the CLI.");

        string expectedPath = Path.Combine(casePath, "signals.expected.json");
        AssertSnapshotsMatch(expectedPath, actualPath);
    }

    private static IReadOnlyList<SignalOutput> LoadSignals(string path)
    {
        string json = File.ReadAllText(path, Utf8TestEncoding.Utf8NoBom);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return JsonSerializer.Deserialize<List<SignalOutput>>(json, options) ?? new List<SignalOutput>();
    }

    private static IReadOnlyList<SignalOutput> Normalize(IEnumerable<SignalOutput> signals)
    {
        return signals
            .Select(signal => new SignalOutput
            {
                Date = ParseDate(signal.Date).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                WindowMonths = signal.WindowMonths,
                IsRiskOn = signal.IsRiskOn,
                AbsoluteReturn = signal.AbsoluteReturn,
                RelativeRank = signal.RelativeRank,
                Comment = signal.Comment ?? string.Empty,
                Allocations = NormalizeAllocations(signal.Allocations)
            })
            .OrderBy(signal => signal.Date, StringComparer.Ordinal)
            .ToList();
    }

    private static IReadOnlyList<AllocationOutput> NormalizeAllocations(IReadOnlyList<AllocationOutput> allocations)
    {
        return allocations
            .OrderByDescending(a => a.Weight)
            .ThenBy(a => a.Ticker, StringComparer.Ordinal)
            .Select(a => new AllocationOutput
            {
                Ticker = a.Ticker,
                Name = a.Name,
                Weight = a.Weight
            })
            .ToList();
    }

    private static DateOnly ParseDate(string value)
    {
        return DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        foreach (string directory in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(sourceDir, directory);
            Directory.CreateDirectory(Path.Combine(destinationDir, relative));
        }

        foreach (string file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(sourceDir, file);
            string target = Path.Combine(destinationDir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }
}
