namespace Central.Observability;

/// <summary>
/// Correlation context for distributed tracing.
/// Propagates correlation ID through the request pipeline.
/// Each request gets a unique ID; child operations inherit it.
/// </summary>
public class CorrelationContext
{
    private static readonly AsyncLocal<string?> _correlationId = new();

    /// <summary>The current correlation ID for this async context.</summary>
    public static string CorrelationId
    {
        get => _correlationId.Value ?? Guid.NewGuid().ToString("N");
        set => _correlationId.Value = value;
    }

    /// <summary>Start a new correlation scope.</summary>
    public static IDisposable BeginScope(string? existingId = null)
    {
        var previous = _correlationId.Value;
        _correlationId.Value = existingId ?? Guid.NewGuid().ToString("N");
        return new CorrelationScope(previous);
    }

    private class CorrelationScope : IDisposable
    {
        private readonly string? _previous;
        public CorrelationScope(string? previous) => _previous = previous;
        public void Dispose() => _correlationId.Value = _previous;
    }
}

/// <summary>
/// Structured log entry with correlation and tenant context.
/// Extend with any fields needed for SIEM export.
/// </summary>
public class StructuredLogEntry
{
    public string Timestamp { get; set; } = DateTime.UtcNow.ToString("O");
    public string Level { get; set; } = "Information";
    public string Message { get; set; } = "";
    public string? CorrelationId { get; set; }
    public string? TenantSlug { get; set; }
    public string? Username { get; set; }
    public string? Source { get; set; }
    public int? DurationMs { get; set; }
    public Dictionary<string, object?>? Properties { get; set; }

    /// <summary>Format as CEF for SIEM export.</summary>
    public string ToCef()
    {
        return $"CEF:0|Central|Platform|1.0|{Source ?? "app"}|{Message}|{LevelToSeverity()}|" +
               $"correlationId={CorrelationId} tenant={TenantSlug} user={Username} durationMs={DurationMs}";
    }

    private int LevelToSeverity() => Level switch
    {
        "Critical" => 10,
        "Error" => 7,
        "Warning" => 5,
        "Information" => 3,
        "Debug" => 1,
        _ => 3
    };
}
