namespace TaxNavi.Domain.Exceptions;

/// <summary>A row changed in the database after it was read, so the write was rejected rather than overwriting it.</summary>
public class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(string entity, Exception? inner = null)
        : base($"{entity} was modified by another operation.", inner)
    {
        Entity = entity;
    }

    public string Entity { get; }
}
