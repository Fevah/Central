namespace Central.Engine.Integration;

/// <summary>
/// Standard contract for any external system integration.
/// Each agent (ManageEngine, Entra SCIM, CSV, REST API, etc.) implements this.
/// The SyncEngine calls these methods during sync cycles.
/// </summary>
public interface IIntegrationAgent
{
    /// <summary>Agent type identifier (e.g. "manage_engine", "entra_id_scim", "csv_import").</summary>
    string AgentType { get; }

    /// <summary>Human-readable name.</summary>
    string DisplayName { get; }

    /// <summary>Initialize the agent with its configuration (from sync_configs.config_json).</summary>
    Task InitializeAsync(Dictionary<string, string> config);

    /// <summary>Test connectivity to the external system.</summary>
    Task<AgentTestResult> TestConnectionAsync();

    /// <summary>
    /// Read records from the external system.
    /// Supports delta sync via watermark (last synced ID/timestamp).
    /// </summary>
    Task<AgentReadResult> ReadAsync(ReadRequest request);

    /// <summary>Write a record to the external system (create or update).</summary>
    Task<AgentWriteResult> WriteAsync(WriteRequest request);

    /// <summary>Delete a record in the external system.</summary>
    Task<AgentWriteResult> DeleteAsync(string entityName, string externalId);

    /// <summary>Get available entity names this agent can sync (e.g. "requests", "technicians").</summary>
    Task<List<string>> GetEntityNamesAsync();

    /// <summary>Get available fields for an entity (for field mapping UI).</summary>
    Task<List<AgentFieldInfo>> GetFieldsAsync(string entityName);
}

/// <summary>Result of a connectivity test.</summary>
public class AgentTestResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public int? RecordCount { get; init; }

    public static AgentTestResult Ok(string msg = "Connected", int? count = null) => new() { Success = true, Message = msg, RecordCount = count };
    public static AgentTestResult Fail(string msg) => new() { Success = false, Message = msg };
}

/// <summary>Request to read records from an external system.</summary>
public class ReadRequest
{
    public string EntityName { get; init; } = "";
    public string? Watermark { get; init; }
    public string? Filter { get; init; }
    public int MaxRecords { get; init; } = 50000;
    public List<string>? Fields { get; init; }
}

/// <summary>Result of a read operation.</summary>
public class AgentReadResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public List<Dictionary<string, object?>> Records { get; init; } = new();
    public string? NewWatermark { get; init; }
    public int TotalAvailable { get; init; }
}

/// <summary>Request to write a record to an external system.</summary>
public class WriteRequest
{
    public string EntityName { get; init; } = "";
    public string? ExternalId { get; init; }
    public Dictionary<string, object?> Fields { get; init; } = new();
    public bool IsUpdate { get; init; }
}

/// <summary>Result of a write operation.</summary>
public class AgentWriteResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public string? ExternalId { get; init; }

    public static AgentWriteResult Ok(string? id = null) => new() { Success = true, ExternalId = id };
    public static AgentWriteResult Fail(string msg) => new() { Success = false, ErrorMessage = msg };
}

/// <summary>Metadata about a field in an external entity.</summary>
public class AgentFieldInfo
{
    public string Name { get; init; } = "";
    public string Type { get; init; } = "string";
    public bool IsRequired { get; init; }
    public bool IsReadOnly { get; init; }
}
