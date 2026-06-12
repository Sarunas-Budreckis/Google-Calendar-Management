namespace GoogleCalendarManagement.Services;

/// <inheritdoc />
public sealed class DataPointProjectorRegistry : IDataPointProjectorRegistry
{
    // Register is called only during startup, so a plain Dictionary is sufficient (no ConcurrentDictionary needed).
    private readonly Dictionary<string, IDataPointProjector> _projectors = new(StringComparer.Ordinal);

    public void Register(IDataPointProjector projector)
    {
        ArgumentNullException.ThrowIfNull(projector);

        if (_projectors.ContainsKey(projector.SourceKey))
        {
            throw new InvalidOperationException(
                $"Projector for source key '{projector.SourceKey}' is already registered.");
        }

        _projectors[projector.SourceKey] = projector;
    }

    public IDataPointProjector? GetProjector(string sourceKey)
    {
        return string.IsNullOrWhiteSpace(sourceKey)
            ? null
            : _projectors.GetValueOrDefault(sourceKey);
    }

    public IReadOnlyCollection<IDataPointProjector> GetAllProjectors()
    {
        return _projectors.Values.ToList().AsReadOnly();
    }
}
