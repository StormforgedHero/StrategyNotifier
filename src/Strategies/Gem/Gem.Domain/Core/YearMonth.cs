namespace Gem.Domain.Core
{
    public readonly struct YearMonth : IEquatable<YearMonth>, IComparable<YearMonth>, IComparable
    {
        public int Year { get; }

        public int Month { get; }

        public YearMonth(int year, int month)
        {
            if (month is < 1 or > 12)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(month),
                    month,
                    "Month must be between 1 and 12.");
            }

            Year = year;
            Month = month;
        }

        public YearMonth AddMonths(int monthsToAdd)
        {
            if (monthsToAdd == 0)
            {
                return this;
            }

            // Convert to a zero-based "month index", apply the offset,
            // then convert back to Year and Month.
            var totalMonths = Year * 12 + (Month - 1) + monthsToAdd;

            var newYear = Math.DivRem(totalMonths, 12, out var monthRemainder);
            var newMonth = monthRemainder + 1;

            return new YearMonth(newYear, newMonth);
        }

        public int CompareTo(YearMonth other)
        {
            var yearComparison = Year.CompareTo(other.Year);
            if (yearComparison != 0)
            {
                return yearComparison;
            }

            return Month.CompareTo(other.Month);
        }

        int IComparable.CompareTo(object? obj)
        {
            if (obj is null)
            {
                return 1;
            }

            if (obj is not YearMonth other)
            {
                throw new ArgumentException(
                    $"Object must be of type {nameof(YearMonth)}.",
                    nameof(obj));
            }

            return CompareTo(other);
        }

        public bool Equals(YearMonth other)
        {
            return Year == other.Year && Month == other.Month;
        }

        public override bool Equals(object? obj)
        {
            return obj is YearMonth other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Year, Month);
        }

        public static bool operator ==(YearMonth left, YearMonth right) => left.Equals(right);

        public static bool operator !=(YearMonth left, YearMonth right) => !left.Equals(right);

        public static bool operator <(YearMonth left, YearMonth right) => left.CompareTo(right) < 0;

        public static bool operator >(YearMonth left, YearMonth right) => left.CompareTo(right) > 0;

        public static bool operator <=(YearMonth left, YearMonth right) => left.CompareTo(right) <= 0;

        public static bool operator >=(YearMonth left, YearMonth right) => left.CompareTo(right) >= 0;
    }
}
