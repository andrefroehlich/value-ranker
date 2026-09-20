namespace ValueRanker.Web.Adapters;

/// <summary>Thrown when a run cannot be saved, e.g. because the browser's storage is full or blocked.</summary>
public sealed class RunStorageException(string message, Exception innerException) : Exception(message, innerException);
