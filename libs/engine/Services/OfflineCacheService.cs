using System.IO;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Central.Engine.Services;

/// <summary>
/// Local SQLite cache for offline mode. Stores tasks, projects, sprints
/// and pending changes while disconnected. Syncs to server via sync-service
/// when connectivity is restored.
///
/// DB location: %LocalAppData%/Central/offline_cache.db
/// </summary>
public sealed class OfflineCacheService : IDisposable
{
    private readonly string _dbPath;
    private SqliteConnection? _conn;
    private static OfflineCacheService? _instance;
    public static OfflineCacheService Instance => _instance ??= new OfflineCacheService();

    public OfflineCacheService(string? dbPath = null)
    {
        _dbPath = dbPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Central", "offline_cache.db");
        Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);
        _instance = this;
    }

    private SqliteConnection GetConnection()
    {
        if (_conn == null)
        {
            _conn = new SqliteConnection($"Data Source={_dbPath}");
            _conn.Open();
            InitSchema();
        }
        return _conn;
    }

    private void InitSchema()
    {
        using var cmd = _conn!.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS cached_entities (
                entity_type TEXT NOT NULL,
                entity_id   TEXT NOT NULL,
                data        TEXT NOT NULL,
                server_version INTEGER DEFAULT 0,
                cached_at   TEXT NOT NULL DEFAULT (datetime('now')),
                PRIMARY KEY (entity_type, entity_id)
            );
            CREATE TABLE IF NOT EXISTS pending_changes (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                entity_type TEXT NOT NULL,
                entity_id   TEXT NOT NULL,
                operation   TEXT NOT NULL,
                data        TEXT NOT NULL,
                created_at  TEXT NOT NULL DEFAULT (datetime('now')),
                synced      INTEGER DEFAULT 0
            );
            CREATE TABLE IF NOT EXISTS sync_state (
                entity_type    TEXT PRIMARY KEY,
                server_version INTEGER DEFAULT 0,
                last_sync      TEXT
            );
            """;
        cmd.ExecuteNonQuery();
    }

    // ── Cache operations ──

    /// <summary>Store an entity in the local cache (upsert).</summary>
    public void Put(string entityType, string entityId, object data, long serverVersion = 0)
    {
        var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO cached_entities (entity_type, entity_id, data, server_version)
            VALUES ($type, $id, $data, $ver)
            ON CONFLICT (entity_type, entity_id) DO UPDATE SET data=$data, server_version=$ver, cached_at=datetime('now')
            """;
        cmd.Parameters.AddWithValue("$type", entityType);
        cmd.Parameters.AddWithValue("$id", entityId);
        cmd.Parameters.AddWithValue("$data", JsonSerializer.Serialize(data));
        cmd.Parameters.AddWithValue("$ver", serverVersion);
        cmd.ExecuteNonQuery();
    }

    /// <summary>Bulk cache a list of entities.</summary>
    public void PutMany<T>(string entityType, IEnumerable<T> items, Func<T, string> getId, long serverVersion = 0)
    {
        var conn = GetConnection();
        using var tx = conn.BeginTransaction();
        foreach (var item in items)
            Put(entityType, getId(item), item!, serverVersion);
        tx.Commit();
    }

    /// <summary>Get a cached entity by type + id.</summary>
    public T? Get<T>(string entityType, string entityId) where T : class
    {
        var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT data FROM cached_entities WHERE entity_type=$type AND entity_id=$id";
        cmd.Parameters.AddWithValue("$type", entityType);
        cmd.Parameters.AddWithValue("$id", entityId);
        var json = cmd.ExecuteScalar() as string;
        return json == null ? null : JsonSerializer.Deserialize<T>(json);
    }

    /// <summary>Get all cached entities of a given type.</summary>
    public List<T> GetAll<T>(string entityType) where T : class
    {
        var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT data FROM cached_entities WHERE entity_type=$type ORDER BY cached_at DESC";
        cmd.Parameters.AddWithValue("$type", entityType);
        var list = new List<T>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var json = reader.GetString(0);
            var item = JsonSerializer.Deserialize<T>(json);
            if (item != null) list.Add(item);
        }
        return list;
    }

    // ── Pending changes (offline edits) ──

    /// <summary>Record a local change to sync later.</summary>
    public void RecordChange(string entityType, string entityId, string operation, object data)
    {
        var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO pending_changes (entity_type, entity_id, operation, data)
            VALUES ($type, $id, $op, $data)
            """;
        cmd.Parameters.AddWithValue("$type", entityType);
        cmd.Parameters.AddWithValue("$id", entityId);
        cmd.Parameters.AddWithValue("$op", operation);
        cmd.Parameters.AddWithValue("$data", JsonSerializer.Serialize(data));
        cmd.ExecuteNonQuery();
    }

    /// <summary>Get all unsynced changes.</summary>
    public List<(int Id, string EntityType, string EntityId, string Operation, string Data)> GetPendingChanges()
    {
        var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, entity_type, entity_id, operation, data FROM pending_changes WHERE synced=0 ORDER BY id";
        var list = new List<(int, string, string, string, string)>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            list.Add((reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4)));
        return list;
    }

    /// <summary>Mark a pending change as synced.</summary>
    public void MarkSynced(int changeId)
    {
        var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE pending_changes SET synced=1 WHERE id=$id";
        cmd.Parameters.AddWithValue("$id", changeId);
        cmd.ExecuteNonQuery();
    }

    /// <summary>Get the last known server version for an entity type.</summary>
    public long GetServerVersion(string entityType)
    {
        var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT server_version FROM sync_state WHERE entity_type=$type";
        cmd.Parameters.AddWithValue("$type", entityType);
        return cmd.ExecuteScalar() is long v ? v : 0;
    }

    /// <summary>Update the server version watermark after a successful sync pull.</summary>
    public void SetServerVersion(string entityType, long version)
    {
        var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO sync_state (entity_type, server_version, last_sync)
            VALUES ($type, $ver, datetime('now'))
            ON CONFLICT (entity_type) DO UPDATE SET server_version=$ver, last_sync=datetime('now')
            """;
        cmd.Parameters.AddWithValue("$type", entityType);
        cmd.Parameters.AddWithValue("$ver", version);
        cmd.ExecuteNonQuery();
    }

    /// <summary>Number of pending unsynced changes.</summary>
    public int PendingCount
    {
        get
        {
            var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM pending_changes WHERE synced=0";
            return Convert.ToInt32(cmd.ExecuteScalar());
        }
    }

    public void Dispose()
    {
        _conn?.Dispose();
        _conn = null;
    }
}
