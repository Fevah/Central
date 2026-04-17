using System.Diagnostics;
using Npgsql;
using Central.Engine.Models;

namespace Central.Persistence;

/// <summary>Database backup/restore via pg_dump/pg_restore CLI.</summary>
public class BackupService
{
    private readonly string _connectionString;

    public BackupService(string connectionString) => _connectionString = connectionString;

    public async Task<BackupRecord> BackupAsync(string outputPath, string backupType = "full", string triggeredBy = "admin")
    {
        var record = new BackupRecord
        {
            FilePath = outputPath,
            BackupType = backupType,
            StartedAt = DateTime.UtcNow,
            TriggeredBy = triggeredBy
        };

        try
        {
            var (host, port, db, user, pass) = ParseDsn();
            var args = $"-h {host} -p {port} -U {user} -d {db} -Fc -f \"{outputPath}\"";

            var psi = new ProcessStartInfo("pg_dump", args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.Environment["PGPASSWORD"] = pass;

            using var proc = Process.Start(psi);
            if (proc == null) throw new Exception("Failed to start pg_dump");

            var stderr = await proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync();

            if (proc.ExitCode != 0)
                throw new Exception($"pg_dump exited with code {proc.ExitCode}: {stderr}");

            record.Status = "success";
            record.CompletedAt = DateTime.UtcNow;

            if (File.Exists(outputPath))
                record.FileSizeBytes = new FileInfo(outputPath).Length;

            await SaveBackupRecordAsync(record);
        }
        catch (Exception ex)
        {
            record.Status = "failed";
            record.ErrorMessage = ex.Message;
            record.CompletedAt = DateTime.UtcNow;
            try { await SaveBackupRecordAsync(record); } catch { }
            throw;
        }

        return record;
    }

    public async Task<List<BackupRecord>> GetBackupHistoryAsync()
    {
        var list = new List<BackupRecord>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT id, backup_type, file_path, file_size_bytes, started_at, completed_at, status, error_message, triggered_by FROM backup_history ORDER BY started_at DESC LIMIT 100", conn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new BackupRecord
            {
                Id = r.GetInt32(0),
                BackupType = r.GetString(1),
                FilePath = r.GetString(2),
                FileSizeBytes = r.IsDBNull(3) ? null : r.GetInt64(3),
                StartedAt = r.GetDateTime(4),
                CompletedAt = r.IsDBNull(5) ? null : r.GetDateTime(5),
                Status = r.GetString(6),
                ErrorMessage = r.IsDBNull(7) ? null : r.GetString(7),
                TriggeredBy = r.IsDBNull(8) ? "admin" : r.GetString(8),
            });
        return list;
    }

    public async Task<List<string>> GetTableListAsync()
    {
        var list = new List<string>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT tablename FROM pg_tables WHERE schemaname='public' ORDER BY tablename", conn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(r.GetString(0));
        return list;
    }

    private async Task SaveBackupRecordAsync(BackupRecord record)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            @"INSERT INTO backup_history (backup_type, file_path, file_size_bytes, started_at, completed_at, status, error_message, triggered_by)
               VALUES (@type, @path, @size, @start, @end, @status, @err, @by)", conn);
        cmd.Parameters.AddWithValue("type", record.BackupType);
        cmd.Parameters.AddWithValue("path", record.FilePath);
        cmd.Parameters.AddWithValue("size", (object?)record.FileSizeBytes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("start", record.StartedAt);
        cmd.Parameters.AddWithValue("end", (object?)record.CompletedAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("status", record.Status);
        cmd.Parameters.AddWithValue("err", (object?)record.ErrorMessage ?? DBNull.Value);
        cmd.Parameters.AddWithValue("by", record.TriggeredBy);
        await cmd.ExecuteNonQueryAsync();
    }

    private (string host, string port, string db, string user, string pass) ParseDsn()
    {
        var builder = new NpgsqlConnectionStringBuilder(_connectionString);
        return (builder.Host ?? "localhost", (builder.Port > 0 ? builder.Port : 5432).ToString(),
                builder.Database ?? "central", builder.Username ?? "central",
                builder.Password ?? "central");
    }
}
