using Gem.Domain.Core;

namespace Gem.Domain.Tests.TestData
{
    public static class GemTestDataFactory
    {
        public static AssetReturnSeries CreateSeries(
            AssetKind assetKind,
            int startYear,
            int startMonth,
            params decimal[] monthlyReturns)
        {
            ArgumentNullException.ThrowIfNull(monthlyReturns);

            var items = new List<MonthlyReturn>(monthlyReturns.Length);

            var year = startYear;
            var month = startMonth;

            foreach (var rate in monthlyReturns)
            {
                items.Add(new MonthlyReturn(new YearMonth(year, month), rate));

                month++;
                if (month > 12)
                {
                    month = 1;
                    year++;
                }
            }

            return new AssetReturnSeries(assetKind, items);
        }
    }
}
