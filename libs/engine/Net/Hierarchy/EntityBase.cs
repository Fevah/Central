using System.Text.Json.Nodes;

namespace Central.Engine.Net.Hierarchy;

/// <summary>
/// Base class for every entity in the networking engine. Mirrors the universal
/// attributes defined in docs/NETWORKING_ATTRIBUTE_SYSTEM.md §0.2. Every net.*
/// table carries these same 17 fields.
///
/// The pattern is lookup-heavy; properties are simple getters/setters without
/// INotifyPropertyChanged. WPF view-model wrappers add change-notification
/// where needed.
/// </summary>
public abstract class EntityBase
{
    /// <summary>Surrogate primary key. Immutable.</summary>
    public Guid Id { get; set; }

    /// <summary>Tenant / organisation root. FK to central_platform.tenants(id).</summary>
    public Guid OrganizationId { get; set; }

    public EntityStatus Status { get; set; } = EntityStatus.Planned;

    public LockState LockState { get; set; } = LockState.Open;
    public string? LockReason { get; set; }
    public int? LockedBy { get; set; }
    public DateTime? LockedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? UpdatedBy { get; set; }

    public DateTime? DeletedAt { get; set; }
    public int? DeletedBy { get; set; }

    public string? Notes { get; set; }

    /// <summary>Free-form key/value tags as a JSON object.</summary>
    public JsonObject Tags { get; set; } = new();

    /// <summary>List of <c>{system, id}</c> objects for external-system cross-references.</summary>
    public JsonArray ExternalRefs { get; set; } = new();

    /// <summary>Optimistic-concurrency token. Increments on every successful update.</summary>
    public int Version { get; set; } = 1;
}
