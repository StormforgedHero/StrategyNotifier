using Gem.Domain.Exceptions;

namespace Gem.Domain.Model
{
    public sealed class MomentumParameters
    {
        public MomentumParameters(
            int windowMonths,
            RankingMode rankingMode = RankingMode.Top1,
            bool useAbsoluteMomentum = true,
            decimal absoluteThreshold = 0m)
        {
            if (windowMonths <= 0)
            {
                throw new DomainValidationException("Momentum window must be positive.");
            }

            WindowMonths = windowMonths;
            RankingMode = rankingMode;
            UseAbsoluteMomentum = useAbsoluteMomentum;
            AbsoluteThreshold = absoluteThreshold;
        }

        public int WindowMonths { get; }

        public RankingMode RankingMode { get; }

        public bool UseAbsoluteMomentum { get; }

        public decimal AbsoluteThreshold { get; }
    }
}
