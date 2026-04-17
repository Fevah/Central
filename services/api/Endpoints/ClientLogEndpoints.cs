using Central.Core.Models;
using Central.Data;

namespace Central.Api.Endpoints;

/// <summary>
/// Inbound logging endpoint for browser/mobile clients.
/// Lets the Angular web client (and any non-WPF client) push runtime errors,
/// failed HTTP calls, and warnings into Central's app_log table so they show
/// up in the WPF Admin → Application Log panel alongside server-side errors.
///
/// POST /api/log/client  — anonymous; client sends batches of entries.
/// Body: ClientLogBatch { entries: [{ level, tag, message, detail, source, url, userAgent, username }] }
/// </summary>
public static class ClientLogEndpoints
{
    // Reject obvious abuse: cap entries-per-request and field sizes.
    private const int MaxEntriesPerRequest = 50;
    private const int MaxMessageLength     = 4_000;
    private const int MaxDetailLength      = 16_000;

    public static RouteGroupBuilder MapClientLogEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/", async (ClientLogBatch batch, DbConnectionFactory db, HttpContext ctx) =>
        {
            if (batch?.Entries == null || batch.Entries.Count == 0)
                return Results.BadRequest(new { error = "No entries provided" });

            if (batch.Entries.Count > MaxEntriesPerRequest)
                return Results.BadRequest(new { error = $"Too many entries ({batch.Entries.Count}); max {MaxEntriesPerRequest}" });

            var repo = new DbRepository(db.ConnectionString);
            var clientIp = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var written = 0;

            foreach (var e in batch.Entries)
            {
                if (string.IsNullOrWhiteSpace(e.Message)) continue;

                // Compose a detail blob that preserves the URL + UA + stack so
                // the WPF AppLog panel shows the full picture.
                var detailParts = new List<string>();
                if (!string.IsNullOrWhiteSpace(e.Url))       detailParts.Add($"URL: {e.Url}");
                if (!string.IsNullOrWhiteSpace(e.UserAgent)) detailParts.Add($"UA: {e.UserAgent}");
                detailParts.Add($"ClientIP: {clientIp}");
                if (!string.IsNullOrWhiteSpace(e.Detail))    detailParts.Add(e.Detail);
                var combinedDetail = string.Join("\n", detailParts);

                var entry = new AppLogEntry
                {
                    Level    = NormalizeLevel(e.Level),
                    Tag      = Truncate(e.Tag      ?? "client", 64),
                    Source   = Truncate(e.Source   ?? "angular-web", 128),
                    Message  = Truncate(e.Message,                MaxMessageLength),
                    Detail   = Truncate(combinedDetail,           MaxDetailLength),
                    Username = Truncate(e.Username ?? "anonymous", 64)
                };

                await repo.InsertAppLogAsync(entry);
                written++;
            }

            return Results.Ok(new { accepted = written });
        });

        return group;
    }

    private static string NormalizeLevel(string? level) => (level ?? "Error").ToLowerInvariant() switch
    {
        "error"   or "err" or "fatal" or "critical" => "Error",
        "warning" or "warn"                          => "Warning",
        "info"    or "information"                   => "Info",
        "debug"                                      => "Info",
        _                                            => "Error"
    };

    private static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) ? "" : (s.Length <= max ? s : s.Substring(0, max));
}

public class ClientLogBatch
{
    public List<ClientLogEntry> Entries { get; set; } = new();
}

public class ClientLogEntry
{
    public string? Level     { get; set; }   // Error | Warning | Info
    public string? Tag       { get; set; }   // e.g. "ng-runtime", "ng-http"
    public string? Source    { get; set; }   // e.g. "angular-web"
    public string? Message   { get; set; }   // human-readable summary
    public string? Detail    { get; set; }   // stack trace / response body
    public string? Url       { get; set; }   // page URL where it happened
    public string? UserAgent { get; set; }   // navigator.userAgent
    public string? Username  { get; set; }   // logged-in user, if known
}
