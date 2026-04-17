using System.Net;
using System.Net.Mail;

namespace Central.Core.Services;

/// <summary>
/// Enterprise email notification service.
/// Sends alerts for: sync failures, auth lockouts, backup completion, system errors.
/// Config stored in app settings or integrations table.
/// </summary>
public class EmailService
{
    private static EmailService? _instance;
    public static EmailService Instance => _instance ??= new();

    private string _smtpHost = "";
    private int _smtpPort = 587;
    private string _username = "";
    private string _password = "";
    private string _fromAddress = "";
    private string _fromName = "Central Platform";
    private bool _useSsl = true;
    private bool _isConfigured;

    /// <summary>Configure SMTP settings. Call once at startup.</summary>
    public void Configure(string host, int port, string username, string password,
        string fromAddress, string fromName = "Central Platform", bool useSsl = true)
    {
        _smtpHost = host;
        _smtpPort = port;
        _username = username;
        _password = password;
        _fromAddress = fromAddress;
        _fromName = fromName;
        _useSsl = useSsl;
        _isConfigured = !string.IsNullOrEmpty(host) && !string.IsNullOrEmpty(fromAddress);
    }

    /// <summary>Configure from a dictionary (loaded from DB settings).</summary>
    public void Configure(Dictionary<string, string> config)
    {
        Configure(
            config.GetValueOrDefault("smtp_host", ""),
            int.TryParse(config.GetValueOrDefault("smtp_port", "587"), out var p) ? p : 587,
            config.GetValueOrDefault("smtp_username", ""),
            config.GetValueOrDefault("smtp_password", ""),
            config.GetValueOrDefault("smtp_from_address", ""),
            config.GetValueOrDefault("smtp_from_name", "Central Platform"),
            config.GetValueOrDefault("smtp_use_ssl", "true") != "false"
        );
    }

    public bool IsConfigured => _isConfigured;

    /// <summary>Send a simple text email.</summary>
    public async Task<bool> SendAsync(string to, string subject, string body, bool isHtml = false)
    {
        if (!_isConfigured) return false;
        return await SendAsync(new[] { to }, subject, body, isHtml);
    }

    /// <summary>Send to multiple recipients.</summary>
    public async Task<bool> SendAsync(IEnumerable<string> to, string subject, string body, bool isHtml = false)
    {
        if (!_isConfigured) return false;

        try
        {
            using var client = new SmtpClient(_smtpHost, _smtpPort)
            {
                EnableSsl = _useSsl,
                Credentials = string.IsNullOrEmpty(_username)
                    ? null
                    : new NetworkCredential(_username, _password)
            };

            var message = new MailMessage
            {
                From = new MailAddress(_fromAddress, _fromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = isHtml
            };

            foreach (var addr in to)
                message.To.Add(addr);

            await client.SendMailAsync(message);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EmailService] Send failed: {ex.Message}");
            return false;
        }
    }

    // ── Predefined alert templates ──

    public Task<bool> SendSyncFailureAlertAsync(string[] adminEmails, string configName, string error) =>
        SendAsync(adminEmails, $"[Central] Sync Failed: {configName}",
            $"<h3>Sync Failure Alert</h3><p>Configuration: <b>{configName}</b></p><p>Error: {error}</p><p>Time: {DateTime.UtcNow:u}</p><hr><p><small>Central Platform — Automated Alert</small></p>",
            isHtml: true);

    public Task<bool> SendAuthLockoutAlertAsync(string[] adminEmails, string username, int attempts) =>
        SendAsync(adminEmails, $"[Central] Account Locked: {username}",
            $"<h3>Account Lockout Alert</h3><p>User <b>{username}</b> has been locked after {attempts} failed login attempts.</p><p>Time: {DateTime.UtcNow:u}</p>",
            isHtml: true);

    public Task<bool> SendBackupCompleteAsync(string[] adminEmails, string filePath, string sizeDisplay) =>
        SendAsync(adminEmails, $"[Central] Backup Complete",
            $"<h3>Database Backup Complete</h3><p>File: {filePath}</p><p>Size: {sizeDisplay}</p><p>Time: {DateTime.UtcNow:u}</p>",
            isHtml: true);

    public Task<bool> SendTestEmailAsync(string to) =>
        SendAsync(to, "[Central] Test Email",
            $"<h3>Email Configuration Test</h3><p>This is a test email from Central Platform.</p><p>If you're reading this, email is working correctly.</p><p>Sent at: {DateTime.UtcNow:u}</p>",
            isHtml: true);
}
