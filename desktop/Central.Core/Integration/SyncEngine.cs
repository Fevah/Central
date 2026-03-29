using System.Collections.Concurrent;
using System.Diagnostics;

namespace Central.Core.Integration;

/// <summary>
/// Enterprise sync engine — orchestrates data synchronization across external systems.
/// Modernised from TotalLink's IntegrationServer (Topshelf + OData + Quartz)
/// into a lightweight, in-process engine running within ASP.NET or WPF.
///
/// Architecture:
///   SyncEngine (singleton) → manages concurrent SyncTasks
///   Each SyncTask → loads config → creates agent → reads data → maps fields → writes to target
///   Throttled by SemaphoreSlim per config's max_concurrent setting
/// </summary>
public class SyncEngine
{
    private static SyncEngine? _instance;
    public static SyncEngine Instance => _instance ??= new();

    private readonly ConcurrentDictionary<string, IIntegrationAgent> _agents = new();
    private readonly ConcurrentDictionary<string, IFieldConverter> _converters = new();
    private readonly ConcurrentDictionary<int, CancellationTokenSource> _runningTasks = new();
    private Func<int, string, string, int, int, int, int, string?, Task>? _logCallback;
    private Action<int, string, string, string>? _failedRecordCallback;
    private Func<int, string, string, string?>? _hashLookup;
    private Action<int, string, string, string>? _hashUpdateCallback;

    /// <summary>Register an agent implementation by its type name.</summary>
    public void RegisterAgent(IIntegrationAgent agent)
    {
        _agents[agent.AgentType] = agent;
    }

    /// <summary>Register a field converter by its type name.</summary>
    public void RegisterConverter(IFieldConverter converter)
    {
        _converters[converter.ConverterType] = converter;
    }

    /// <summary>Set callback for logging sync results to DB.</summary>
    public void SetLogCallback(Func<int, string, string, int, int, int, int, string?, Task> callback)
        => _logCallback = callback;

    /// <summary>Set callback for dead letter queue (failed records).</summary>
    public void SetFailedRecordCallback(Action<int, string, string, string> callback)
        => _failedRecordCallback = callback;

    /// <summary>Set callback for hash lookup (change detection).</summary>
    public void SetHashLookup(Func<int, string, string, string?> lookup)
        => _hashLookup = lookup;

    /// <summary>Set callback for hash update after successful write.</summary>
    public void SetHashUpdateCallback(Action<int, string, string, string> callback)
        => _hashUpdateCallback = callback;

    /// <summary>Get all registered agent type names.</summary>
    public IReadOnlyList<string> GetAgentTypes() => _agents.Keys.ToList();

    /// <summary>Get all registered converter type names.</summary>
    public IReadOnlyList<string> GetConverterTypes() => _converters.Keys.ToList();

    /// <summary>
    /// Execute a sync for a specific configuration.
    /// Reads from source agent, maps fields, writes to target (DB).
    /// </summary>
    public async Task<SyncResult> ExecuteSyncAsync(
        SyncConfig config,
        List<SyncEntityMap> entityMaps,
        List<SyncFieldMap> fieldMaps,
        Func<string, Dictionary<string, object?>, string, Task> upsertTargetFunc,
        CancellationToken ct = default)
    {
        var result = new SyncResult { ConfigId = config.Id, ConfigName = config.Name };
        var sw = Stopwatch.StartNew();

        if (!_agents.TryGetValue(config.AgentType, out var agent))
        {
            result.Status = "failed";
            result.ErrorMessage = $"No agent registered for type: {config.AgentType}";
            return result;
        }

        try
        {
            // Initialize agent with config
            var agentConfig = ParseConfigJson(config.ConfigJson);
            await agent.InitializeAsync(agentConfig);

            // Throttle concurrent entity syncs
            using var throttle = new SemaphoreSlim(config.MaxConcurrent);
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _runningTasks[config.Id] = cts;

            var tasks = entityMaps
                .Where(m => m.IsEnabled)
                .OrderBy(m => m.SortOrder)
                .Select(async entityMap =>
                {
                    await throttle.WaitAsync(cts.Token);
                    try
                    {
                        var entityFieldMaps = fieldMaps.Where(f => f.EntityMapId == entityMap.Id).ToList();
                        var entityResult = await SyncEntityAsync(agent, entityMap, entityFieldMaps, upsertTargetFunc, cts.Token);

                        lock (result)
                        {
                            result.RecordsRead += entityResult.RecordsRead;
                            result.RecordsCreated += entityResult.RecordsCreated;
                            result.RecordsUpdated += entityResult.RecordsUpdated;
                            result.RecordsFailed += entityResult.RecordsFailed;
                            if (entityResult.ErrorMessage != null)
                                result.Errors.Add($"{entityMap.SourceEntity}: {entityResult.ErrorMessage}");
                        }
                    }
                    finally { throttle.Release(); }
                });

            await Task.WhenAll(tasks);

            result.Status = result.Errors.Count > 0 ? "partial" : "success";
        }
        catch (OperationCanceledException)
        {
            result.Status = "cancelled";
        }
        catch (Exception ex)
        {
            result.Status = "failed";
            result.ErrorMessage = ex.Message;
        }
        finally
        {
            _runningTasks.TryRemove(config.Id, out _);
            result.DurationMs = (int)sw.ElapsedMilliseconds;

            // Log to DB
            if (_logCallback != null)
            {
                try
                {
                    await _logCallback(config.Id, result.Status, "",
                        result.RecordsRead, result.RecordsCreated, result.RecordsUpdated,
                        result.RecordsFailed, result.ErrorMessage);
                }
                catch { }
            }
        }

        return result;
    }

    /// <summary>Cancel a running sync.</summary>
    public void CancelSync(int configId)
    {
        if (_runningTasks.TryGetValue(configId, out var cts))
            cts.Cancel();
    }

    /// <summary>Check if a sync is currently running.</summary>
    public bool IsSyncRunning(int configId) => _runningTasks.ContainsKey(configId);

    // ── Private ──

    private async Task<EntitySyncResult> SyncEntityAsync(
        IIntegrationAgent agent,
        SyncEntityMap entityMap,
        List<SyncFieldMap> fieldMaps,
        Func<string, Dictionary<string, object?>, string, Task> upsertTargetFunc,
        CancellationToken ct)
    {
        var result = new EntitySyncResult();

        try
        {
            var readResult = await agent.ReadAsync(new ReadRequest
            {
                EntityName = entityMap.SourceEntity,
                Filter = string.IsNullOrEmpty(entityMap.FilterExpr) ? null : entityMap.FilterExpr
            });

            if (!readResult.Success)
            {
                result.ErrorMessage = readResult.ErrorMessage;
                return result;
            }

            result.RecordsRead = readResult.Records.Count;

            foreach (var sourceRecord in readResult.Records)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var targetRecord = MapRecord(sourceRecord, fieldMaps);

                    // Pre-write validation
                    var validationErrors = SyncFieldValidator.Validate(targetRecord, fieldMaps);
                    if (validationErrors.Count > 0)
                    {
                        result.RecordsFailed++;
                        _failedRecordCallback?.Invoke(entityMap.SyncConfigId, entityMap.SourceEntity,
                            System.Text.Json.JsonSerializer.Serialize(sourceRecord),
                            $"Validation: {string.Join("; ", validationErrors)}");
                        continue;
                    }

                    // Hash-based change detection — skip unchanged records
                    var contentHash = SyncHashDetector.ComputeHash(targetRecord);
                    var recordKey = targetRecord.GetValueOrDefault(entityMap.UpsertKey)?.ToString() ?? "";
                    if (_hashLookup != null && !string.IsNullOrEmpty(recordKey))
                    {
                        var prevHash = _hashLookup(entityMap.SyncConfigId, entityMap.SourceEntity, recordKey);
                        if (!SyncHashDetector.HasChanged(contentHash, prevHash))
                        {
                            result.RecordsSkipped++;
                            continue; // unchanged — skip
                        }
                    }

                    // Upsert with retry
                    var success = await SyncRetry.WithRetryAsync(
                        () => upsertTargetFunc(entityMap.TargetTable, targetRecord, entityMap.UpsertKey),
                        maxRetries: 2, baseDelayMs: 500);

                    if (success)
                    {
                        result.RecordsCreated++;
                        // Update hash cache
                        _hashUpdateCallback?.Invoke(entityMap.SyncConfigId, entityMap.SourceEntity, recordKey, contentHash);
                    }
                    else
                    {
                        result.RecordsFailed++;
                        _failedRecordCallback?.Invoke(entityMap.SyncConfigId, entityMap.SourceEntity,
                            System.Text.Json.JsonSerializer.Serialize(sourceRecord),
                            "Failed after 3 retry attempts");
                    }
                }
                catch (Exception ex)
                {
                    result.RecordsFailed++;
                    _failedRecordCallback?.Invoke(entityMap.SyncConfigId, entityMap.SourceEntity,
                        System.Text.Json.JsonSerializer.Serialize(sourceRecord), ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    private Dictionary<string, object?> MapRecord(Dictionary<string, object?> sourceRecord, List<SyncFieldMap> fieldMaps)
    {
        var target = new Dictionary<string, object?>();
        var context = new ConvertContext { SourceRecord = sourceRecord, TargetRecord = target };

        foreach (var map in fieldMaps.OrderBy(f => f.SortOrder))
        {
            var sourceValue = sourceRecord.GetValueOrDefault(map.SourceField);

            // Apply default if null
            if (sourceValue == null && map.DefaultValue != null)
                sourceValue = map.DefaultValue;

            // Apply converter
            if (_converters.TryGetValue(map.ConverterType, out var converter))
                sourceValue = converter.Convert(sourceValue, map.ConverterExpr, context);

            target[map.TargetColumn] = sourceValue;
        }

        return target;
    }

    private static Dictionary<string, string> ParseConfigJson(string json)
    {
        try
        {
            var doc = System.Text.Json.JsonDocument.Parse(json);
            var result = new Dictionary<string, string>();
            foreach (var prop in doc.RootElement.EnumerateObject())
                result[prop.Name] = prop.Value.ToString();
            return result;
        }
        catch { return new(); }
    }

    private class EntitySyncResult
    {
        public int RecordsRead;
        public int RecordsCreated;
        public int RecordsUpdated;
        public int RecordsFailed;
        public int RecordsSkipped;
        public string? ErrorMessage;
    }
}

/// <summary>Result of a sync execution.</summary>
public class SyncResult
{
    public int ConfigId { get; set; }
    public string ConfigName { get; set; } = "";
    public string Status { get; set; } = "running";
    public string? ErrorMessage { get; set; }
    public int RecordsRead { get; set; }
    public int RecordsCreated { get; set; }
    public int RecordsUpdated { get; set; }
    public int RecordsFailed { get; set; }
    public int DurationMs { get; set; }
    public List<string> Errors { get; } = new();
}
