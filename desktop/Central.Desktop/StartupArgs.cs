namespace Central.Desktop;

/// <summary>
/// Command-line startup arguments for automated/kiosk deployments.
/// Usage: Central.exe --server 10.0.0.1 --auth-method windows
/// </summary>
public class StartupArgs
{
    public string? Server { get; set; }
    public string? User { get; set; }
    public string? Password { get; set; }
    public string? AuthMethod { get; set; }  // windows, password, offline
    public string? Dsn { get; set; }

    public bool IsOffline => string.Equals(AuthMethod, "offline", StringComparison.OrdinalIgnoreCase);
    public bool IsWindows => string.Equals(AuthMethod, "windows", StringComparison.OrdinalIgnoreCase);
    public bool IsPassword => string.Equals(AuthMethod, "password", StringComparison.OrdinalIgnoreCase);
    public bool HasArgs => Dsn != null || Server != null || AuthMethod != null;

    public static StartupArgs Parse(string[] args)
    {
        var result = new StartupArgs();
        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            var next = i + 1 < args.Length ? args[i + 1] : null;

            switch (arg)
            {
                case "-s": case "--server":
                    result.Server = next; i++; break;
                case "-u": case "--user":
                    result.User = next; i++; break;
                case "-p": case "--password":
                    result.Password = next; i++; break;
                case "-a": case "--auth-method":
                    result.AuthMethod = next; i++; break;
                case "-d": case "--dsn":
                    result.Dsn = next; i++; break;
            }
        }
        return result;
    }

    /// <summary>Clear sensitive data after use.</summary>
    public void ClearPassword() => Password = null;
}
