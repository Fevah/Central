namespace Central.Core.Models;

public class BackupRecord
{
    public int Id { get; set; }
    public string BackupType { get; set; } = "full";
    public string FilePath { get; set; } = "";
    public long? FileSizeBytes { get; set; }
    public string[]? TablesIncluded { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Status { get; set; } = "running";
    public string? ErrorMessage { get; set; }
    public string TriggeredBy { get; set; } = "admin";

    public string FileSizeDisplay => FileSizeBytes switch
    {
        null => "",
        < 1024 => $"{FileSizeBytes} B",
        < 1024 * 1024 => $"{FileSizeBytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{FileSizeBytes / (1024.0 * 1024):F1} MB",
        _ => $"{FileSizeBytes / (1024.0 * 1024 * 1024):F2} GB"
    };

    public string StatusColor => Status switch
    {
        "success" => "#22C55E",
        "running" => "#3B82F6",
        "failed"  => "#EF4444",
        _         => "#6B7280"
    };
}
