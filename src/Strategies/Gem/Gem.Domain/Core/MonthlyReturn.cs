namespace Gem.Domain.Core
{
    public readonly struct MonthlyReturn : IEquatable<MonthlyReturn>
    {
        public YearMonth Period { get; }

        public decimal Rate { get; }

        public MonthlyReturn(YearMonth period, decimal rate)
        {
            Period = period;
            Rate = rate;
        }

        public bool Equals(MonthlyReturn other)
        {
            return Period.Equals(other.Period) && Rate == other.Rate;
        }

        public override bool Equals(object? obj)
        {
            return obj is MonthlyReturn other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Period, Rate);
        }

        public static bool operator ==(MonthlyReturn left, MonthlyReturn right) => left.Equals(right);

        public static bool operator !=(MonthlyReturn left, MonthlyReturn right) => !left.Equals(right);
    }
}
