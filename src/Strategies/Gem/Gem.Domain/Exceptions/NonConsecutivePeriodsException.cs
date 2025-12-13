namespace Gem.Domain.Exceptions
{
    /// <summary>
    /// Represents a domain validation failure caused by missing periods
    /// within a requested lookback window.
    /// </summary>
    public sealed class NonConsecutivePeriodsException : DomainValidationException
    {
        public NonConsecutivePeriodsException()
        {
        }

        public NonConsecutivePeriodsException(string message)
            : base(message)
        {
        }

        public NonConsecutivePeriodsException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
