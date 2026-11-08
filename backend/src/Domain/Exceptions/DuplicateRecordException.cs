namespace TaxNavi.Domain.Exceptions;

/// <summary>An insert or update violated a unique constraint — typically a concurrent request already wrote the same record.</summary>
public class DuplicateRecordException : Exception
{
    public DuplicateRecordException(string entity, Exception? inner = null)
        : base($"A {entity} with the same unique key already exists.", inner)
    {
        Entity = entity;
    }

    public string Entity { get; }
}
