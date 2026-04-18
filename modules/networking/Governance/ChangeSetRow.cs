using System;
using Central.ApiClient;

namespace Central.Module.Networking.Governance;

/// <summary>
/// Flat row shape for the Change Sets grid. Projects the engine's
/// <see cref="ChangeSetDto"/> into a WPF-friendly view model with
/// derived age / status-color / item-count fields that bind directly
/// to columns.
///
/// Grid is read-only; inline editing would bypass the lifecycle state
/// machine — mutations go through ribbon buttons which publish
/// <c>NavigateToPanelMessage</c> actions the panel then routes through
/// the <see cref="NetworkingEngineClient"/>.
/// </summary>
public sealed class ChangeSetRow
{
    public Guid Id { get; init; }
    public string Title { get; init; } = "";
    public string Status { get; init; } = "";
    public long ItemCount { get; init; }
    public string? RequestedByDisplay { get; init; }
    public int? RequiredApprovals { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? SubmittedAt { get; init; }
    public DateTime? ApprovedAt { get; init; }
    public DateTime? AppliedAt { get; init; }
    public DateTime? RolledBackAt { get; init; }
    public DateTime? CancelledAt { get; init; }
    public int Version { get; init; }

    /// <summary>Minutes since the Set was created. Grid sorts on this
    /// when admins want "show me stale drafts".</summary>
    public int AgeMinutes => (int)(DateTime.UtcNow - CreatedAt).TotalMinutes;

    /// <summary>Most-recent timestamp across the lifecycle stamps. Drives
    /// the "Last Activity" column so admins don't have to scan six date
    /// columns to see what's fresh.</summary>
    public DateTime LastActivityAt
    {
        get
        {
            var ts = CreatedAt;
            foreach (var candidate in new[] {
                SubmittedAt, ApprovedAt, AppliedAt, RolledBackAt, CancelledAt })
            {
                if (candidate is DateTime c && c > ts) ts = c;
            }
            return ts;
        }
    }

    public static ChangeSetRow FromDto(ChangeSetDto dto) => new()
    {
        Id = dto.Id,
        Title = dto.Title,
        Status = dto.Status,
        ItemCount = dto.ItemCount,
        RequestedByDisplay = dto.RequestedByDisplay,
        RequiredApprovals = dto.RequiredApprovals,
        CreatedAt = dto.CreatedAt,
        SubmittedAt = dto.SubmittedAt,
        ApprovedAt = dto.ApprovedAt,
        AppliedAt = dto.AppliedAt,
        RolledBackAt = dto.RolledBackAt,
        CancelledAt = dto.CancelledAt,
        Version = dto.Version,
    };
}
