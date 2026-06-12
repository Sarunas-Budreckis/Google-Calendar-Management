namespace GoogleCalendarManagement.Services;

/// <summary>
/// Dispatch registry mapping <c>source_key</c> to its <see cref="IDataPointProjector"/>.
/// Populated once during startup.
/// </summary>
public interface IDataPointProjectorRegistry
{
    /// <summary>Stores the projector by <see cref="IDataPointProjector.SourceKey"/>.</summary>
    /// <exception cref="InvalidOperationException">Thrown when a projector for the key is already registered.</exception>
    void Register(IDataPointProjector projector);

    /// <summary>Returns the projector for <paramref name="sourceKey"/>, or <c>null</c> if none is registered. Never throws.</summary>
    IDataPointProjector? GetProjector(string sourceKey);

    /// <summary>Returns a snapshot of all registered projectors.</summary>
    IReadOnlyCollection<IDataPointProjector> GetAllProjectors();
}
