namespace Central.Persistence.Net;

/// <summary>
/// Thrown when every value in the target pool/block is either already
/// allocated or still on the reservation shelf waiting out its
/// cool-down. The caller should decide whether to extend the pool or
/// fail the upstream request.
/// </summary>
public class PoolExhaustedException(string resource, Guid containerId)
    : InvalidOperationException(
        $"Pool exhausted: no free {resource} available in container {containerId}. " +
        "Either the pool is fully used, or all unused values are still within their cool-down window.")
{
    public string Resource { get; } = resource;
    public Guid ContainerId { get; } = containerId;
}

/// <summary>
/// Thrown when a referenced pool / block / subnet doesn't exist, has
/// been soft-deleted, or doesn't belong to the calling tenant.
/// </summary>
public class AllocationContainerNotFoundException(string resource, Guid containerId)
    : InvalidOperationException(
        $"Allocation container not found: {resource} {containerId} is missing, deleted, or belongs to a different tenant.")
{
    public string Resource { get; } = resource;
    public Guid ContainerId { get; } = containerId;
}

/// <summary>
/// Thrown when the proposed allocation value falls outside the
/// enclosing pool / block range. Should never happen with the built-in
/// <c>AllocateNext*</c> methods — only surfaces from
/// <c>AllocateSpecific*</c> (Phase 3d+) where callers hand in a
/// preferred value.
/// </summary>
public class AllocationRangeException(string resource, long value, long first, long last)
    : InvalidOperationException(
        $"Allocation out of range: {resource} {value} is outside the container range [{first}, {last}].")
{
    public string Resource { get; } = resource;
    public long Value { get; } = value;
    public long First { get; } = first;
    public long Last { get; } = last;
}
