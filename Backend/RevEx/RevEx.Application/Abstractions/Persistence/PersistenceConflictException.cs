namespace RevEx.Application.Abstractions.Persistence;

public sealed class PersistenceConflictException : Exception
{
    public PersistenceConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
