namespace Central.Core.Models;

/// <summary>
/// Marker interface for entities that should be audit-logged on changes.
/// </summary>
public interface IAuditable
{
    string AuditCategory { get; }
}
