using Gem.Domain.Core;
using Gem.Domain.Model;
using System.Globalization;

namespace Gem.Cli.IO
{
    /// <summary>
    /// Loads GEM input data from CSV files containing monthly return series
    /// for US equity, ex-US equity and a safe asset.
    /// </summary>
    public sealed class GemCsvInputLoader
    {
        private readonly string _baseDirectory;

        /// <summary>
        /// Initializes a new instance of the <see cref="GemCsvInputLoader"/> class.
        /// </summary>
        /// <param name="baseDirectory">
        /// Base directory containing the CSV files:
        /// <c>us-equity.csv</c>, <c>exus-equity.csv</c> and <c>safe-asset.csv</c>.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="baseDirectory"/> is <c>null</c>, empty or whitespace.
        /// </exception>
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

        /// <summary>
        /// Loads GEM input data from CSV files located in the configured base directory.
        /// </summary>
        /// <returns>
        /// A <see cref="GemInputData"/> instance built from the CSV files.
        /// </returns>
        /// <exception cref="FileNotFoundException">
        /// Thrown when any of the expected CSV files is missing.
        /// </exception>
        /// <exception cref="FormatException">
        /// Thrown when any CSV row contains an invalid numeric value or malformed columns.
        /// </exception>
        public GemInputData Load()
        {
            AssetReturnSeries usEquitySeries = LoadAssetSeries("us-equity.csv", AssetKind.UsEquity);
            AssetReturnSeries exUsEquitySeries = LoadAssetSeries("exus-equity.csv", AssetKind.ExUsEquity);
            AssetReturnSeries safeAssetSeries = LoadAssetSeries("safe-asset.csv", AssetKind.SafeAsset);

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
            using var reader = new StreamReader(stream);

            // Read header line (e.g. "Year,Month,Return").
            // If the file is empty, return an empty series.
            if (reader.ReadLine() is null)
            {
                return new AssetReturnSeries(assetKind, Array.Empty<MonthlyReturn>());
            }

            var items = new List<MonthlyReturn>();
            string? line;
            int lineNumber = 1;

            while ((line = reader.ReadLine()) is not null)
            {
                lineNumber++;

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                string[] columns = line.Split(',');

                if (columns.Length < 3)
                {
                    throw new FormatException(
                        $"Line {lineNumber} in '{path}' does not contain at least three columns (Year,Month,Return).");
                }

                if (!int.TryParse(columns[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int year))
                {
                    throw new FormatException(
                        $"Invalid year value '{columns[0]}' on line {lineNumber} in '{path}'.");
                }

                if (!int.TryParse(columns[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int month))
                {
                    throw new FormatException(
                        $"Invalid month value '{columns[1]}' on line {lineNumber} in '{path}'.");
                }

                if (!decimal.TryParse(columns[2], NumberStyles.Number, CultureInfo.InvariantCulture, out decimal rate))
                {
                    throw new FormatException(
                        $"Invalid return value '{columns[2]}' on line {lineNumber} in '{path}'.");
                }

                var period = new YearMonth(year, month);
                var monthlyReturn = new MonthlyReturn(period, rate);

                items.Add(monthlyReturn);
            }

            return new AssetReturnSeries(assetKind, items);
        }
    }
}
