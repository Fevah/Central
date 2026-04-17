using System.Diagnostics;
using Npgsql;
using Central.Core.Models;

namespace Central.Data;

/// <summary>Applies SQL migrations and tracks them in migration_history.</summary>
public class MigrationRunner
{
    private readonly string _connectionString;

    public MigrationRunner(string connectionString) => _connectionString = connectionString;

    public async Task<List<MigrationRecord>> GetAppliedMigrationsAsync()
    {
        var list = new List<MigrationRecord>();
        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT id, migration_name, applied_at, duration_ms, applied_by FROM migration_history ORDER BY migration_name", conn);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                list.Add(new MigrationRecord
                {
                    Id = r.GetInt32(0),
                    MigrationName = r.GetString(1),
                    AppliedAt = r.GetDateTime(2),
                    DurationMs = r.IsDBNull(3) ? null : r.GetInt32(3),
                    AppliedBy = r.IsDBNull(4) ? "system" : r.GetString(4),
                    IsApplied = true
                });
        }
        catch { } // migration_history may not exist yet
        return list;
    }

    public async Task<List<MigrationRecord>> GetAllMigrationsAsync(string migrationsDir)
    {
        var applied = await GetAppliedMigrationsAsync();
        var appliedSet = applied.Select(m => m.MigrationName).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var all = new List<MigrationRecord>(applied);

        if (Directory.Exists(migrationsDir))
        {
            foreach (var file in Directory.GetFiles(migrationsDir, "*.sql").OrderBy(f => Path.GetFileName(f)))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                if (!appliedSet.Contains(name))
                {
                    all.Add(new MigrationRecord
                    {
                        MigrationName = name,
                        IsApplied = false
                    });
                }
            }
        }

        return all.OrderBy(m => m.MigrationName).ToList();
    }

    public async Task<int> ApplyPendingAsync(string migrationsDir, string appliedBy = "admin")
    {
        var applied = (await GetAppliedMigrationsAsync()).Select(m => m.MigrationName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var pending = Directory.GetFiles(migrationsDir, "*.sql")
            .Where(f => !applied.Contains(Path.GetFileNameWithoutExtension(f)))
            .OrderBy(f => Path.GetFileName(f))
            .ToList();

        int count = 0;
        foreach (var file in pending)
        {
            await ApplyMigrationAsync(file, appliedBy);
            count++;
        }
        return count;
    }

    public async Task ApplyMigrationAsync(string filePath, string appliedBy = "admin")
    {
        var name = Path.GetFileNameWithoutExtension(filePath);
        var sql = await File.ReadAllTextAsync(filePath);

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var sw = Stopwatch.StartNew();
        await using var tx = await conn.BeginTransactionAsync();
        try
        {
            await using var cmd = new NpgsqlCommand(sql, conn, tx);
            cmd.CommandTimeout = 120;
            await cmd.ExecuteNonQueryAsync();

            // Record in migration_history
            await using var rec = new NpgsqlCommand(
                "INSERT INTO migration_history (migration_name, duration_ms, applied_by) VALUES (@n, @d, @by) ON CONFLICT DO NOTHING", conn, tx);
            rec.Parameters.AddWithValue("n", name);
            rec.Parameters.AddWithValue("d", (int)sw.ElapsedMilliseconds);
            rec.Parameters.AddWithValue("by", appliedBy);
            await rec.ExecuteNonQueryAsync();

            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }
}
