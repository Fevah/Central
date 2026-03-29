namespace Central.Core.Models;

public class MigrationRecord
{
    public int Id { get; set; }
    public string MigrationName { get; set; } = "";
    public DateTime? AppliedAt { get; set; }
    public int? DurationMs { get; set; }
    public string AppliedBy { get; set; } = "system";
    public string Checksum { get; set; } = "";

    /// <summary>True if this migration has been applied to the database.</summary>
    public bool IsApplied { get; set; }

    /// <summary>Row colour: green=applied, amber=pending.</summary>
    public string StatusColor => IsApplied ? "#22C55E" : "#F59E0B";
    public string StatusText => IsApplied ? "Applied" : "Pending";
}
