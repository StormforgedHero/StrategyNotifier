using System.Globalization;
using System.Text;
using Gem.Domain.Model;

namespace Gem.Domain.Pricing
{
    public static class PriceCsvParser
    {
        private static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;
        private static readonly CultureInfo CommaCulture = CultureInfo.GetCultureInfo("pl-PL");

        public static IReadOnlyList<PricePoint> ParseFromFile(Stream stream)
        {
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            return Parse(reader);
        }

        public static IReadOnlyList<PricePoint> ParseFromText(string content)
        {
            using var reader = new StringReader(content ?? string.Empty);
            return Parse(reader);
        }

        private static IReadOnlyList<PricePoint> Parse(TextReader reader)
        {
            string? headerLine = ReadNextNonEmpty(reader);

            if (headerLine is null)
            {
                throw new FormatException("CSV file is empty.");
            }

            char delimiter = DetectDelimiter(headerLine);
            string[] header = Split(headerLine, delimiter);
            (int DateIndex, int CloseIndex) map = BuildColumnMap(header, headerLine, delimiter);

            var rows = new Dictionary<DateOnly, PricePoint>();
            string? line;
            int lineNumber = 1;

            while ((line = reader.ReadLine()) is not null)
            {
                lineNumber++;

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                string[] columns = Split(line, delimiter);

                if (columns.Length <= Math.Max(map.DateIndex, map.CloseIndex))
                {
                    continue;
                }

                string dateToken = columns[map.DateIndex].Trim();
                string closeToken = columns[map.CloseIndex].Trim();

                if (string.IsNullOrWhiteSpace(dateToken) || string.IsNullOrWhiteSpace(closeToken))
                {
                    continue;
                }

                DateOnly date = ParseDate(dateToken, lineNumber);
                decimal close = ParseDecimal(closeToken, lineNumber);

                rows[date] = new PricePoint(date, close);
            }

            if (rows.Count == 0)
            {
                throw new FormatException("CSV file does not contain any price data rows.");
            }

            return rows.Values
                .OrderBy(p => p.Date)
                .ToList();
        }

        private static string? ReadNextNonEmpty(TextReader reader)
        {
            string? line;

            while ((line = reader.ReadLine()) is not null)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    return line;
                }
            }

            return null;
        }

        private static char DetectDelimiter(string line)
        {
            char[] candidates = new[] { ',', ';', '\t' };
            char best = ',';
            int bestScore = -1;

            foreach (char candidate in candidates)
            {
                string[] parts = Split(line, candidate);
                int score = parts.Length;

                if (HasRecognizedColumns(parts))
                {
                    score += 10;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return best;
        }

        private static (int DateIndex, int CloseIndex) BuildColumnMap(string[] header, string rawHeader, char delimiter)
        {
            int dateIndex = -1;
            int closeIndex = -1;

            for (int i = 0; i < header.Length; i++)
            {
                string value = NormalizeHeader(header[i]);

                if (IsDateColumn(value) && dateIndex < 0)
                {
                    dateIndex = i;
                }
                else if (IsCloseColumn(value) && closeIndex < 0)
                {
                    closeIndex = i;
                }
            }

            if (dateIndex < 0 || closeIndex < 0)
            {
                string normalizedColumns = string.Join(", ", header.Select(NormalizeHeader));
                throw new FormatException(
                    $"CSV header must contain date and close columns. Header='{rawHeader}', delimiter='{delimiter}', normalized=[{normalizedColumns}]. Hint: Stooq uses Data/Zamkniecie.");
            }

            return (dateIndex, closeIndex);
        }

        private static bool IsDateColumn(string value)
        {
            return value.Equals("date", StringComparison.OrdinalIgnoreCase)
                || value.Equals("data", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsCloseColumn(string value)
        {
            return value.Equals("close", StringComparison.OrdinalIgnoreCase)
                || value.Equals("zamkniecie", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeHeader(string token)
        {
            string value = token.Trim();

            if (value.Length > 0 && value[0] == '\uFEFF')
            {
                value = value[1..];
            }

            string decomposed = value.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(decomposed.Length);

            foreach (char c in decomposed)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(c);

                if (category != UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(c);
                }
            }

            value = builder.ToString().Normalize(NormalizationForm.FormC);

            return value;
        }

        private static DateOnly ParseDate(string token, int lineNumber)
        {
            string value = token.Trim();

            if (DateOnly.TryParseExact(value, "yyyy-MM-dd", InvariantCulture, DateTimeStyles.None, out DateOnly date))
            {
                return date;
            }

            if (double.TryParse(value, NumberStyles.Any, InvariantCulture, out double oaDate))
            {
                try
                {
                    DateTime dt = DateTime.FromOADate(oaDate);
                    return DateOnly.FromDateTime(dt);
                }
                catch (ArgumentException ex)
                {
                    throw new FormatException($"Invalid OADate value '{token}' on line {lineNumber}.", ex);
                }
            }

            throw new FormatException($"Invalid date value '{token}' on line {lineNumber}.");
        }

        private static decimal ParseDecimal(string token, int lineNumber)
        {
            string value = token.Trim();

            if (decimal.TryParse(value, NumberStyles.Number, CommaCulture, out decimal result))
            {
                return result;
            }

            if (decimal.TryParse(value, NumberStyles.Number, InvariantCulture, out result))
            {
                return result;
            }

            string normalized = value.Replace(',', '.');

            if (decimal.TryParse(normalized, NumberStyles.Number, InvariantCulture, out result))
            {
                return result;
            }

            throw new FormatException($"Invalid close value '{token}' on line {lineNumber}.");
        }

        private static string[] Split(string line, char delimiter)
        {
            var result = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }

                    continue;
                }

                if (!inQuotes && c == delimiter)
                {
                    result.Add(current.ToString());
                    current.Clear();
                    continue;
                }

                current.Append(c);
            }

            result.Add(current.ToString());
            return result.ToArray();
        }

        private static bool HasRecognizedColumns(string[] parts)
        {
            int recognized = 0;

            foreach (string part in parts)
            {
                string normalized = NormalizeHeader(part);

                if (IsDateColumn(normalized) || IsCloseColumn(normalized))
                {
                    recognized++;
                }
            }

            return recognized >= 2;
        }
    }
}
