using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DevExpress.Xpf.Core;
using Central.Core.Auth;
using Central.Core.Modules;
using Central.Core.Shell;
using Central.Data.Repositories;
using Central.Data;
using Central.Desktop.Services;

namespace Central.Desktop;

public partial class App : System.Windows.Application
{
    private static string _logPath = "";
    private static void Log(string msg) => System.IO.File.AppendAllText(_logPath, $"{DateTime.Now:HH:mm:ss.fff} {msg}\n");

    internal static bool IsDbOnline { get; set; }
    internal static ConnectivityManager? Connectivity { get; private set; }
    internal static string Dsn { get; private set; } = "";
    internal static List<IModule> Modules { get; set; } = new();
    internal static RibbonBuilder RibbonBuilder { get; } = new();
    internal static PanelBuilder PanelBuilder { get; } = new();
    internal static SettingsProvider? Settings { get; set; }
    internal static Task SessionReady { get; private set; } = Task.CompletedTask;
    internal static SplashWindow? Splash { get; set; }

    protected override async void OnStartup(System.Windows.StartupEventArgs e)
    {
        _logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup.log");
        System.IO.File.WriteAllText(_logPath, $"{DateTime.Now:HH:mm:ss.fff} OnStartup begin\n");

        try
        {
            ApplicationThemeHelper.ApplicationThemeName = Theme.Office2019ColorfulName;
            base.OnStartup(e);

            // ── Show splash immediately ──
            var splash = new SplashWindow();
            Splash = splash;
            splash.Show();
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
            splash.UpdateStatus($"Central v{version} — Initializing...", 0);
            Log($"Central v{version} starting on {Environment.MachineName} (.NET {Environment.Version})");

            // Global exception handlers — catch XAML layout errors, missing resources, etc.
            DispatcherUnhandledException += (_, args) =>
            {
                var ex = args.Exception;
                var crashPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log");
                System.IO.File.AppendAllText(crashPath, $"\n{DateTime.Now:O} DISPATCHER:\n{ex}\n");
                AppLogger.LogExceptionSync("Unhandled", ex, "App.DispatcherUnhandledException");

                // Recoverable XAML errors: missing resource, converter, layout failure
                // Keep app alive instead of crashing — show notification + log
                if (ex is System.Windows.Markup.XamlParseException
                    || ex is InvalidOperationException { Message: var m } && m.Contains("Cannot find resource")
                    || ex.InnerException is System.Windows.Markup.XamlParseException)
                {
                    args.Handled = true; // Prevent crash
                    var msg = ex.InnerException?.Message ?? ex.Message;
                    Log($"XAML ERROR (recovered): {msg}");
                    try
                    {
                        // Show toast if notification service is available
                        Central.Core.Services.NotificationService.Instance?.Error($"Layout error: {msg}");
                    }
                    catch { }
                    return;
                }

                // Layout measure overflow (infinite loop) — recover
                if (ex is System.StackOverflowException || ex.Message.Contains("layout cycle"))
                {
                    args.Handled = true;
                    Log($"LAYOUT OVERFLOW (recovered): {ex.Message}");
                    return;
                }

                // Log to audit trail
                try { _ = Central.Core.Services.AuditService.Instance.LogAsync("UnhandledException", "System", details: ex.Message); } catch { }
                // Show toast notification
                try { Central.Core.Services.NotificationService.Instance?.Error($"Unhandled error: {ex.Message}"); } catch { }
                // For truly fatal errors, show MessageBox before crashing
                try
                {
                    System.Windows.MessageBox.Show(
                        $"Unhandled error:\n\n{ex.Message}\n\nDetails logged to crash.log",
                        "Central — Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
                catch { }
            };
            TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                var ex = args.Exception?.InnerException ?? args.Exception!;
                AppLogger.LogExceptionSync("Task", ex, "TaskScheduler.UnobservedTaskException");
                args.SetObserved(); // Prevent crash from unobserved task exceptions

                // Log to audit trail + show toast
                try
                {
                    _ = Central.Core.Services.AuditService.Instance.LogAsync(
                        "UnhandledException", "System", details: $"Task exception: {ex.Message}");
                }
                catch { }
                try
                {
                    System.Windows.Application.Current?.Dispatcher?.InvokeAsync(() =>
                        Central.Core.Services.NotificationService.Instance?.Error(
                            $"Background error: {ex.Message}"));
                }
                catch { }
            };
            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                if (args.ExceptionObject is Exception ex)
                {
                    AppLogger.LogExceptionSync("AppDomain", ex, "AppDomain.UnhandledException");
                    System.IO.File.AppendAllText(
                        System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log"),
                        $"\n{DateTime.Now:O} APPDOMAIN:\n{ex}\n");
                }
            };

            // ── Infrastructure ──
            splash.UpdateStatus("Connecting to database...", 10);
            Dsn = Environment.GetEnvironmentVariable("CENTRAL_DSN")
                ?? Environment.GetEnvironmentVariable("CENTRAL_DSN")
                ?? "Host=127.0.0.1;Port=5432;Database=central;Username=central;Password=central";
            if (Environment.GetEnvironmentVariable("CENTRAL_DSN") == null && Environment.GetEnvironmentVariable("CENTRAL_DSN") == null)
                Log("WARNING: No CENTRAL_DSN or CENTRAL_DSN env var set — using localhost default");
            Connectivity = new ConnectivityManager(Dsn, connectTimeoutSeconds: 5);
            var repo = new DbRepository(Dsn);
            AppLogger.Init(repo);
            Connectivity.RegisterDirectDb(new DirectDbDataService(repo));
            Connectivity.SwitchMode(Central.Core.Data.DataServiceMode.DirectDb);

            // ── File storage ──
            var fileStoragePath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Central", "file_storage");
            Central.Core.Services.FileManagementService.Instance.Configure(fileStoragePath);

            // ── Module discovery ──
            splash.UpdateStatus("Discovering modules...", 20);
            Bootstrapper.Initialize();
            Log($"Bootstrapper found {Modules.Count} modules");

            // ── Database connectivity ──
            splash.UpdateStatus("Testing database...", 35);
            IsDbOnline = await Connectivity.TestConnectionAsync();

            // ── Parse command-line args ──
            var startupArgs = StartupArgs.Parse(Environment.GetCommandLineArgs());
            if (!string.IsNullOrEmpty(startupArgs.Dsn))
            {
                Dsn = startupArgs.Dsn;
                Log($"DSN overridden by --dsn arg");
                Connectivity = new ConnectivityManager(Dsn, connectTimeoutSeconds: 5);
                var newRepo = new DbRepository(Dsn);
                AppLogger.Init(newRepo);
                Connectivity.RegisterDirectDb(new DirectDbDataService(newRepo));
                Connectivity.SwitchMode(Central.Core.Data.DataServiceMode.DirectDb);
            }

            // ── Authentication (via AuthenticationService) ──
            splash.UpdateStatus("Authenticating...", 50);
            var authService = new Auth.AuthenticationService(Dsn);
            bool authenticated = false;

            // ── Auto-apply pending migrations ──
            if (IsDbOnline)
            {
                try
                {
                    var runner = new Central.Data.MigrationRunner(Dsn);
                    var migrationsDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "db", "migrations");
                    if (!System.IO.Directory.Exists(migrationsDir))
                        migrationsDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "migrations");

                    if (System.IO.Directory.Exists(migrationsDir))
                    {
                        var applied = await runner.ApplyPendingAsync(migrationsDir, "startup");
                        if (applied > 0)
                        {
                            splash.UpdateStatus($"Applied {applied} database migrations", 38);
                            Log($"Auto-applied {applied} pending migrations");
                        }
                    }
                }
                catch (Exception ex) { Log($"Auto-migration: {ex.Message}"); }
            }

            // ── Startup health check ──
            if (IsDbOnline)
            {
                var healthCheck = await Central.Data.StartupHealthCheck.CheckAsync(Dsn);
                Log($"Health check: {healthCheck.Summary}");
                if (!healthCheck.IsHealthy)
                    Log($"Missing tables: {string.Join(", ", healthCheck.MissingTables)}");
                foreach (var w in healthCheck.Warnings)
                    Log($"Health warning: {w}");
            }

            // Command-line offline mode
            if (startupArgs.IsOffline)
            {
                AuthContext.Instance.SetOfflineAdmin(startupArgs.User ?? Environment.UserName);
                authenticated = true;
                Log($"CLI offline login: {AuthContext.Instance.CurrentUser?.Username}");
            }
            // Command-line password login
            else if (startupArgs.IsPassword && !string.IsNullOrEmpty(startupArgs.User) && !string.IsNullOrEmpty(startupArgs.Password))
            {
                var cliResult = await authService.AuthenticateLocalAsync(startupArgs.User, startupArgs.Password);
                if (cliResult.Success && await authService.EstablishSessionAsync(cliResult))
                {
                    authenticated = true;
                    Log($"CLI password login: {startupArgs.User}");
                }
                startupArgs.ClearPassword();
            }
            // Auto-login via Windows (default)
            else if (IsDbOnline)
            {
                var windowsResult = await authService.AuthenticateWindowsAsync();
                if (windowsResult.Success && await authService.EstablishSessionAsync(windowsResult))
                {
                    authenticated = true;
                    splash.UpdateStatus($"Welcome, {AuthContext.Instance.CurrentUser?.DisplayName}", 60);
                    Log($"Auto-login: {Environment.UserName} as {AuthContext.Instance.CurrentUser?.RoleName}");
                }
            }

            if (!authenticated)
            {
                splash.Hide();
                var login = new LoginWindow(Dsn, authService);
                var result = login.ShowDialog();
                if (result != true || !login.LoginSucceeded)
                {
                    Shutdown(0);
                    return;
                }
                if (!IsDbOnline && login.ResultMode == LoginWindow.LoginMode.Offline)
                    Connectivity.StartRetryLoop(intervalSeconds: 10);
                authenticated = true;
                splash.Show();
                splash.UpdateStatus($"Welcome, {AuthContext.Instance.CurrentUser?.DisplayName ?? "User"}", 60);
            }

            // ── Module settings ──
            splash.UpdateStatus("Loading settings...", 70);
            var userId = AuthContext.Instance.CurrentUser?.Id ?? 0;
            if (userId > 0 && IsDbOnline)
            {
                Settings = new SettingsProvider(new DbRepository(Dsn), userId);
                RegisterDefaultSettings(Settings);
                await Settings.LoadFromDbAsync();
            }

            // ── Load icon library ──
            splash.UpdateStatus("Loading icons...", 75);
            if (IsDbOnline)
            {
                try
                {
                    var iconSw = System.Diagnostics.Stopwatch.StartNew();
                    await IconService.Instance.LoadFromDbAsync(Dsn);
                    Log($"Icons loaded in {iconSw.ElapsedMilliseconds}ms ({IconService.Instance.AllIcons.Count} icons)");

                    Log("Icon overrides loading...");
                    try
                    {
                        var iconRepo = new Central.Data.DbRepository(Dsn);
                        var adminIcons = await iconRepo.GetAllIconDefaultsAsync();
                        var iconUserId = Central.Core.Auth.AuthContext.Instance.CurrentUser?.Id ?? 0;
                        var userIcons = iconUserId > 0 ? await iconRepo.GetUserIconOverridesAsync(iconUserId) : new();
                        Central.Core.Services.IconOverrideService.Instance.Load(adminIcons, userIcons);
                        Log($"Icon overrides loaded: {adminIcons.Count} admin, {userIcons.Count} user");
                    }
                    catch (Exception ex) { Log($"Icon overrides load: {ex.Message}"); }
                }
                catch (Exception ex) { Log($"Icon load failed: {ex.Message}"); }
            }
            Log("Theme restore...");

            // ── Restore saved theme ──
            if (Settings != null)
            {
                var savedTheme = Settings.Get<string>("app.theme");
                if (!string.IsNullOrEmpty(savedTheme) && savedTheme != Theme.Office2019ColorfulName)
                {
                    try { ApplicationThemeHelper.ApplicationThemeName = savedTheme; }
                    catch { }
                }
            }

            // ── Protection: Integrity check ──
            splash.UpdateStatus("Verifying integrity...", 75);
            try
            {
                var manifestPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "integrity.json");
                if (System.IO.File.Exists(manifestPath))
                {
                    var manifest = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(
                        System.IO.File.ReadAllText(manifestPath)) ?? new();
                    var integrityResult = Central.Protection.IntegrityChecker.VerifyAll(manifest);
                    Log($"Integrity: {integrityResult.Summary}");
                    if (!integrityResult.IsIntact)
                        Log($"WARNING: Tampered files: {string.Join(", ", integrityResult.TamperedFiles)}");
                }
            }
            catch (Exception ex) { Log($"Integrity check: {ex.Message}"); }

            // ── API server auto-connect ──
            if (Settings != null && Settings.Get<bool>("api.auto_connect"))
            {
                var apiUrl = Settings.Get<string>("api.url");
                if (!string.IsNullOrEmpty(apiUrl))
                {
                    splash.UpdateStatus("Connecting to API server...", 80);
                    Connectivity!.ApiUrl = apiUrl;
                    Connectivity.Mode = Central.Core.Data.DataServiceMode.Api;

                    try
                    {
                        // Login to API with current user's credentials
                        // Certificate pinning: use fingerprint from settings if configured
                        var certFingerprint = Settings?.Get<string>("api.cert_fingerprint");
                        var httpHandler = !string.IsNullOrEmpty(certFingerprint)
                            ? new Central.Protection.CertificatePinningHandler(certFingerprint)
                            : Central.Protection.CertificatePinningHandler.TrustAll();
                        var correlationHandler = new Central.Api.Client.CorrelationIdHandler { InnerHandler = httpHandler };
                        var apiClient = new Central.Api.Client.CentralApiClient(apiUrl, correlationHandler);
                        var loginResult = await apiClient.LoginAsync(AuthContext.Instance.CurrentUser?.Username ?? Environment.UserName);
                        if (loginResult != null)
                        {
                            // Connect SignalR for real-time updates
                            await Connectivity.ConnectSignalRAsync($"{apiUrl.TrimEnd('/')}/hubs/notify", loginResult.Token);
                            Log($"API connected: {apiUrl}, SignalR: {Connectivity.Mode}");
                        }
                        else
                        {
                            Log("API login failed — falling back to DirectDb");
                            Connectivity.Mode = Central.Core.Data.DataServiceMode.DirectDb;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"API connect failed: {ex.Message} — falling back to DirectDb");
                        Connectivity.Mode = Central.Core.Data.DataServiceMode.DirectDb;
                    }
                }
            }

            // ── Start session refresh timer ──
            if (authenticated && AuthContext.Instance.AuthState != AuthStates.Offline)
            {
                var refreshService = new Auth.SessionRefreshService(authService);
                refreshService.Start(intervalMinutes: 20);
                Log("Session refresh timer started (20min interval)");
            }

            // ── Initialize audit service ──
            if (IsDbOnline)
            {
                var auditRepo = new DbRepository(Dsn);
                Central.Core.Services.AuditService.Instance.SetPersistFunc(
                    entry => auditRepo.InsertAuditEntryAsync(entry));
                Log("Audit service initialized");
            }

            // ── Initialize notification preferences + email service ──
            if (IsDbOnline && authenticated)
            {
                try
                {
                    var notifRepo = new DbRepository(Dsn);
                    var notifUserId = AuthContext.Instance.CurrentUser?.Id ?? 0;
                    if (notifUserId > 0)
                    {
                        var prefs = await notifRepo.GetNotificationPreferencesAsync(notifUserId);
                        Central.Core.Services.NotificationService.Instance.LoadPreferences(prefs);
                    }

                    // Wire email sending when notification channel is "email" or "both"
                    Central.Core.Services.NotificationService.Instance.EmailRequested += (eventType, title, body) =>
                    {
                        if (!Central.Core.Services.EmailService.Instance.IsConfigured) return;
                        var adminEmail = AuthContext.Instance.CurrentUser?.Email;
                        if (!string.IsNullOrEmpty(adminEmail))
                            _ = Central.Core.Services.EmailService.Instance.SendAsync(adminEmail, $"[Central] {title}", body, isHtml: false);
                    };

                    // Load email config from settings if available
                    if (Settings != null)
                    {
                        Central.Core.Services.EmailService.Instance.Configure(new Dictionary<string, string>
                        {
                            ["smtp_host"] = Settings.Get<string>("email.smtp_host") ?? "",
                            ["smtp_port"] = Settings.Get<string>("email.smtp_port") ?? "587",
                            ["smtp_username"] = Settings.Get<string>("email.smtp_username") ?? "",
                            ["smtp_password"] = Settings.Get<string>("email.smtp_password") ?? "",
                            ["smtp_from_address"] = Settings.Get<string>("email.smtp_from") ?? "",
                        });
                    }

                    Log("Notification preferences + email service initialized");
                }
                catch (Exception ex) { Log($"Notification init: {ex.Message}"); }
            }

            // ── Register data validation rules ──
            Central.Core.Services.DataValidationService.Instance.RegisterDefaults();

            // ── Create MainWindow (hidden — splash stays until ribbon/data/layout loaded) ──
            Log("Pre-MainWindow construction");
            splash.UpdateStatus("Building interface...", 90);
            SessionReady = Task.CompletedTask;
            splash.UpdateStatus("Building interface...", 90);
            var mainWindow = new MainWindow();
            MainWindow = mainWindow;

            splash.UpdateStatus("Ready", 100);
            splash.Close();
            Splash = null;
            mainWindow.Show();
            Log("MainWindow shown");
        }
        catch (Exception ex)
        {
            System.IO.File.WriteAllText(
                System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log"),
                $"{DateTime.Now:O}\n{ex}\n");
            System.Windows.MessageBox.Show($"Startup failed:\n{ex.Message}", "Error");
            Shutdown(1);
        }
    }

    /// <summary>Register default module settings. Modules will add their own via ISettingsProvider.</summary>
    private static void RegisterDefaultSettings(SettingsProvider settings)
    {
        // App-level settings
        settings.Register("app.theme", "Application Theme", "Office2019Colorful", Core.Services.SettingType.String, "Appearance");
        settings.Register("app.auto_scan", "Auto Ping Scan", false, Core.Services.SettingType.Boolean, "Connectivity");
        settings.Register("app.scan_interval", "Scan Interval (minutes)", 10, Core.Services.SettingType.Integer, "Connectivity");

        // Switch module settings
        settings.Register("switch.ping_timeout", "Ping Timeout (ms)", 5000, Core.Services.SettingType.Integer, "Switches");
        settings.Register("switch.ssh_timeout", "SSH Timeout (s)", 30, Core.Services.SettingType.Integer, "Switches");
        settings.Register("switch.ssh_default_port", "Default SSH Port", 22, Core.Services.SettingType.Integer, "Switches");
        settings.Register("switch.ssh_default_user", "Default SSH Username", "admin", Core.Services.SettingType.String, "Switches");

        // Link module settings
        settings.Register("link.auto_description", "Auto-generate Link Description", true, Core.Services.SettingType.Boolean, "Links");
        settings.Register("link.default_subnet", "Default P2P Subnet", "/30", Core.Services.SettingType.String, "Links");

        // Server/API settings
        settings.Register("api.url", "API Server URL", "http://192.168.56.203:8000", Core.Services.SettingType.String, "Server");
        settings.Register("api.auto_connect", "Auto-connect to API on startup", false, Core.Services.SettingType.Boolean, "Server");
        settings.Register("api.use_server_ssh", "Use server-side SSH (credentials stay on server)", false, Core.Services.SettingType.Boolean, "Server");

        // Email/SMTP settings
        settings.Register("email.smtp_host", "SMTP Host", "", Core.Services.SettingType.String, "Email");
        settings.Register("email.smtp_port", "SMTP Port", "587", Core.Services.SettingType.String, "Email");
        settings.Register("email.smtp_username", "SMTP Username", "", Core.Services.SettingType.String, "Email");
        settings.Register("email.smtp_password", "SMTP Password", "", Core.Services.SettingType.String, "Email");
        settings.Register("email.smtp_from", "From Address", "", Core.Services.SettingType.String, "Email");
        settings.Register("email.smtp_ssl", "Use SSL/TLS", true, Core.Services.SettingType.Boolean, "Email");

        // Security settings
        settings.Register("security.password_min_length", "Password Min Length", 8, Core.Services.SettingType.Integer, "Security");
        settings.Register("security.lockout_threshold", "Login Lockout Threshold", 5, Core.Services.SettingType.Integer, "Security");
        settings.Register("security.lockout_duration", "Lockout Duration (minutes)", 30, Core.Services.SettingType.Integer, "Security");
        settings.Register("security.password_expiry_days", "Password Expiry (days, 0=never)", 90, Core.Services.SettingType.Integer, "Security");
        settings.Register("security.require_mfa", "Require MFA for all users", false, Core.Services.SettingType.Boolean, "Security");
    }
}
