namespace JaneERP.Infrastructure
{
    /// <summary>
    /// Thrown when an optimistic concurrency check fails — i.e., the row was modified by another
    /// user between when it was loaded and when the current user tried to save it.
    /// Catch this in form Save handlers and prompt the user to reload before retrying.
    /// </summary>
    public class ConcurrencyException : Exception
    {
        public ConcurrencyException(string message) : base(message) { }
    }
}
