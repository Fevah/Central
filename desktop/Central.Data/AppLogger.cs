using System;
using System.Threading.Tasks;
using Central.Core.Auth;
using Central.Data;
using Central.Core.Models;

namespace Central.Data;

/// <summary>
/// Singleton logger that writes to the app_log DB table.
/// All methods are fire-and-forget safe — logging never throws.
/// </summary>
public static class AppLogger
{
    private static DbRepository? _repo;

    public static void Init(DbRepository repo) => _repo = repo;

    public static void Error(string tag, string message, string? detail = null, string? source = null)
        => Log("Error", tag, message, detail, source);

    public static void Warning(string tag, string message, string? detail = null, string? source = null)
        => Log("Warning", tag, message, detail, source);

    public static void Info(string tag, string message, string? detail = null, string? source = null)
        => Log("Info", tag, message, detail, source);

    public static void Audit(string tag, string message, string? detail = null, string? source = null)
        => Log("Audit", tag, message, detail, source);

    public static void LogException(string tag, Exception ex, string? source = null)
        => Log("Error", tag, $"{ex.GetType().Name}: {ex.Message}", ex.StackTrace, source);

    /// <summary>Synchronous — blocks until written. Use in crash handlers where async may not complete.</summary>
    public static void LogExceptionSync(string tag, Exception ex, string? source = null)
        => LogSync("Error", tag, $"{ex.GetType().Name}: {ex.Message}", ex.StackTrace, source);

    private static void Log(string level, string tag, string message, string? detail, string? source)
    {
        if (_repo == null) return;
        var entry = MakeEntry(level, tag, message, detail, source);
        // Fire and forget — don't block the UI
        _ = Task.Run(async () =>
        {
            try { await _repo.InsertAppLogAsync(entry); }
            catch { /* never throw from logger */ }
        });
    }

    private static void LogSync(string level, string tag, string message, string? detail, string? source)
    {
        if (_repo == null) return;
        var entry = MakeEntry(level, tag, message, detail, source);
        try { _repo.InsertAppLogAsync(entry).GetAwaiter().GetResult(); }
        catch { /* never throw from logger */ }
    }

    private static AppLogEntry MakeEntry(string level, string tag, string message, string? detail, string? source)
        => new()
        {
            Level    = level,
            Tag      = tag,
            Source   = source ?? "",
            Message  = message,
            Detail   = detail ?? "",
            Username = AuthContext.Instance.CurrentUser?.Username ?? Environment.UserName
        };
}
