using Gem.Domain.Core;
using Gem.Domain.Model;
using System.Globalization;
using System.Text;

namespace Gem.Cli.IO
{
    /// <summary>
    /// Loads GEM input data from CSV files containing monthly return series
    /// for US equity, ex-US equity and a safe asset.
    /// </summary>
    public sealed class GemCsvInputLoader
    {
        private static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;
        private static readonly CultureInfo CommaDecimalCulture = CultureInfo.GetCultureInfo("pl-PL");

        private const string DefaultUsEquityFile = "us-equity.csv";
        private const string DefaultExUsEquityFile = "exus-equity.csv";
        private const string DefaultSafeAssetFile = "safe-asset.csv";

        private const NumberStyles DecimalStyles =
            NumberStyles.AllowLeadingWhite
            | NumberStyles.AllowTrailingWhite
            | NumberStyles.AllowLeadingSign
            | NumberStyles.AllowDecimalPoint;

        private readonly string _baseDirectory;

        public GemCsvInputLoader(string baseDirectory)
        {
            if (string.IsNullOrWhiteSpace(baseDirectory))
            {
                throw new ArgumentException(
                    "Base directory must not be null or whitespace.",
                    nameof(baseDirectory));
            }

            _baseDirectory = baseDirectory;
        }

        public GemInputData Load()
        {
            return Load(DefaultUsEquityFile, DefaultExUsEquityFile, DefaultSafeAssetFile);
        }

        public GemInputData Load(
            string usEquityFileName,
            string exUsEquityFileName,
            string safeAssetFileName)
        {
            AssetReturnSeries usEquitySeries = LoadAssetSeries(usEquityFileName, AssetKind.UsEquity);
            AssetReturnSeries exUsEquitySeries = LoadAssetSeries(exUsEquityFileName, AssetKind.ExUsEquity);
            AssetReturnSeries safeAssetSeries = LoadAssetSeries(safeAssetFileName, AssetKind.SafeAsset);

            return new GemInputData(usEquitySeries, exUsEquitySeries, safeAssetSeries);
        }

        private AssetReturnSeries LoadAssetSeries(string fileName, AssetKind assetKind)
        {
            string path = Path.Combine(_baseDirectory, fileName);

            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    $"CSV file for {assetKind} was not found. Expected path: '{path}'.",
                    path);
            }

            using var stream = File.OpenRead(path);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

            int lineNumber = 0;

            // Find header (skip blanks and comment lines).
            string? headerLine = ReadNextNonIgnorableLine(reader, ref lineNumber);

            if (headerLine is null)
            {
                // Empty file or only comments -> treat as empty series.
                return new AssetReturnSeries(assetKind, Array.Empty<MonthlyReturn>());
            }

            char delimiter = DetectDelimiter(headerLine);

            string[] headerColumns = SplitCsvLine(headerLine, delimiter);

            if (!IsValidHeader(headerColumns))
            {
                throw new FormatException(
                    $"Invalid CSV header in '{path}'. Expected columns: Year{delimiter}Month{delimiter}Return.");
            }

            var items = new List<MonthlyReturn>();
            string? line;

            while ((line = reader.ReadLine()) is not null)
            {
                lineNumber++;

                if (IsIgnorableLine(line))
                {
                    continue;
                }

                string[] columns = SplitCsvLine(line, delimiter);

                if (columns.Length < 3)
                {
                    throw new FormatException(
                        $"Line {lineNumber} in '{path}' does not contain at least three columns (Year,Month,Return).");
                }

                string yearToken = NormalizeToken(columns[0]);
                string monthToken = NormalizeToken(columns[1]);
                string returnToken = NormalizeToken(columns[2]);

                // Allow trailing comments in the Return column.
                returnToken = StripTrailingComment(returnToken);

                if (!int.TryParse(yearToken, NumberStyles.Integer, InvariantCulture, out int year))
                {
                    throw new FormatException(
                        $"Invalid year value '{columns[0]}' on line {lineNumber} in '{path}'.");
                }

                if (!int.TryParse(monthToken, NumberStyles.Integer, InvariantCulture, out int month))
                {
                    throw new FormatException(
                        $"Invalid month value '{columns[1]}' on line {lineNumber} in '{path}'.");
                }

                if (month < 1 || month > 12)
                {
                    throw new FormatException(
                        $"Invalid month value '{columns[1]}' on line {lineNumber} in '{path}'.");
                }

                if (!TryParseDecimalFlexible(returnToken, out decimal rate))
                {
                    throw new FormatException(
                        $"Invalid return value '{columns[2]}' on line {lineNumber} in '{path}'.");
                }

                var period = new YearMonth(year, month);
                items.Add(new MonthlyReturn(period, rate));
            }

            return new AssetReturnSeries(assetKind, items);
        }

        private static string? ReadNextNonIgnorableLine(StreamReader reader, ref int lineNumber)
        {
            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                lineNumber++;

                if (!IsIgnorableLine(line))
                {
                    return line;
                }
            }

            return null;
        }

        private static bool IsIgnorableLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return true;
            }

            string trimmed = line.TrimStart();

            return trimmed.StartsWith('#')
                || trimmed.StartsWith("//", StringComparison.Ordinal);
        }

        private static char DetectDelimiter(string headerLine)
        {
            // Prefer the delimiter that appears more often.
            int semicolons = 0;
            int commas = 0;

            foreach (char c in headerLine)
            {
                if (c == ';')
                {
                    semicolons++;
                }
                else if (c == ',')
                {
                    commas++;
                }
            }

            return semicolons > commas ? ';' : ',';
        }

        private static bool IsValidHeader(string[] columns)
        {
            if (columns.Length < 3)
            {
                return false;
            }

            string c0 = NormalizeHeaderToken(columns[0]);
            string c1 = NormalizeHeaderToken(columns[1]);
            string c2 = NormalizeHeaderToken(columns[2]);

            return string.Equals(c0, "Year", StringComparison.OrdinalIgnoreCase)
                && string.Equals(c1, "Month", StringComparison.OrdinalIgnoreCase)
                && string.Equals(c2, "Return", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeHeaderToken(string token)
        {
            string value = NormalizeToken(token);

            // Handle potential UTF-8 BOM at the beginning of the first header column.
            if (value.Length > 0 && value[0] == '\uFEFF')
            {
                value = value[1..];
            }

            return value.Trim();
        }

        private static string NormalizeToken(string token)
        {
            string value = token.Trim();

            // If token is still quoted (edge cases), unquote it.
            if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
            {
                value = value[1..^1];
            }

            value = value.Trim();

            return value;
        }

        private static string StripTrailingComment(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            int hashIndex = value.IndexOf('#');
            int slashIndex = value.IndexOf("//", StringComparison.Ordinal);

            int cutIndex = -1;

            if (hashIndex >= 0)
            {
                cutIndex = hashIndex;
            }

            if (slashIndex >= 0)
            {
                cutIndex = cutIndex < 0 ? slashIndex : Math.Min(cutIndex, slashIndex);
            }

            if (cutIndex >= 0)
            {
                value = value[..cutIndex];
            }

            return value.Trim();
        }

        private static bool TryParseDecimalFlexible(string token, out decimal value)
        {
            string trimmed = token.Trim();

            bool hasComma = trimmed.Contains(',', StringComparison.Ordinal);
            bool hasDot = trimmed.Contains('.', StringComparison.Ordinal);

            // Critical: when only comma is present, treat it as DECIMAL separator,
            // not as thousands separator (InvariantCulture would parse "0,02" as 2).
            if (hasComma && !hasDot)
            {
                if (decimal.TryParse(trimmed, DecimalStyles, CommaDecimalCulture, out value))
                {
                    return true;
                }

                string normalized = trimmed.Replace(',', '.');
                if (decimal.TryParse(normalized, DecimalStyles, InvariantCulture, out value))
                {
                    return true;
                }

                value = default;
                return false;
            }

            // Mixed separators: allow common thousands+decimal patterns.
            if (hasComma && hasDot)
            {
                if (decimal.TryParse(trimmed, NumberStyles.Number, InvariantCulture, out value))
                {
                    return true;
                }

                if (decimal.TryParse(trimmed, NumberStyles.Number, CommaDecimalCulture, out value))
                {
                    return true;
                }

                value = default;
                return false;
            }

            // Dot-only or plain number.
            if (decimal.TryParse(trimmed, DecimalStyles, InvariantCulture, out value))
            {
                return true;
            }

            if (decimal.TryParse(trimmed, DecimalStyles, CommaDecimalCulture, out value))
            {
                return true;
            }

            value = default;
            return false;
        }

        private static string[] SplitCsvLine(string line, char delimiter)
        {
            // Minimal CSV splitter: supports quoting with double quotes and escaped quotes ("").
            // It is sufficient for our tested scenarios.
            var results = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        // Escaped quote inside a quoted value.
                        current.Append('"');
                        i++;
                        continue;
                    }

                    inQuotes = !inQuotes;
                    continue;
                }

                if (!inQuotes && c == delimiter)
                {
                    results.Add(current.ToString());
                    current.Clear();
                    continue;
                }

                current.Append(c);
            }

            results.Add(current.ToString());

            return results.ToArray();
        }
    }
}
