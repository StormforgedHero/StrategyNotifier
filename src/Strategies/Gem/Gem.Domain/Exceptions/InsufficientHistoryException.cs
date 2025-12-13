namespace Gem.Domain.Exceptions
{
    /// <summary>
    /// Represents a domain validation failure caused by insufficient history
    /// to build the requested lookback window.
    /// </summary>
    public sealed class InsufficientHistoryException : DomainValidationException
    {
        public InsufficientHistoryException()
        {
        }

        public InsufficientHistoryException(string message)
            : base(message)
        {
        }

        public InsufficientHistoryException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
