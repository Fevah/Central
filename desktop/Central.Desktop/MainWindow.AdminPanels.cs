// MainWindow partial — Admin panel loading methods.
// Extracted to reduce MainWindow.xaml.cs size.

using Central.Core.Auth;
using Central.Data;

namespace Central.Desktop;

public partial class MainWindow
{
    // ── Icon Defaults / Overrides Panel Loading ─────────────────────────
    private bool _iconDefaultsLoaded;
    private bool _iconOverridesLoaded;

    private async Task LoadIconDefaultsAsync()
    {
        try
        {
            var defaults = await VM.Repo.GetAllIconDefaultsAsync();
            IconDefaultsGridPanel.Load(defaults);

            IconDefaultsGridPanel.SaveDefault = async item =>
            {
                await VM.Repo.UpsertIconDefaultAsync(item);
                var all = await VM.Repo.GetAllIconDefaultsAsync();
                var userId = AuthContext.Instance.CurrentUser?.Id ?? 0;
                var userOv = userId > 0 ? await VM.Repo.GetUserIconOverridesAsync(userId) : new();
                Central.Core.Services.IconOverrideService.Instance.Load(all, userOv);
            };

            IconDefaultsGridPanel.DeleteDefault = async item =>
            {
                await VM.Repo.DeleteIconDefaultAsync(item.Context, item.ElementKey);
                var all = await VM.Repo.GetAllIconDefaultsAsync();
                var userId = AuthContext.Instance.CurrentUser?.Id ?? 0;
                var userOv = userId > 0 ? await VM.Repo.GetUserIconOverridesAsync(userId) : new();
                Central.Core.Services.IconOverrideService.Instance.Load(all, userOv);
            };

            IconDefaultsGridPanel.RefreshRequested = async () => await LoadIconDefaultsAsync();
            _iconDefaultsLoaded = true;
        }
        catch (Exception ex) { AppLogger.LogException("Icons", ex, "LoadIconDefaultsAsync"); }
    }

    private async Task LoadIconOverridesAsync()
    {
        try
        {
            var userId = AuthContext.Instance.CurrentUser?.Id ?? 0;
            if (userId == 0) return;

            var overrides = await VM.Repo.GetUserIconOverridesAsync(userId);
            IconOverridesGridPanel.Load(overrides);

            IconOverridesGridPanel.SaveOverride = async item =>
            {
                await VM.Repo.UpsertUserIconOverrideAsync(userId, item);
                var adminDefs = await VM.Repo.GetAllIconDefaultsAsync();
                var userOv = await VM.Repo.GetUserIconOverridesAsync(userId);
                Central.Core.Services.IconOverrideService.Instance.Load(adminDefs, userOv);
            };

            IconOverridesGridPanel.DeleteOverride = async item =>
            {
                await VM.Repo.DeleteUserIconOverrideAsync(userId, item.Context, item.ElementKey);
                var adminDefs = await VM.Repo.GetAllIconDefaultsAsync();
                var userOv = await VM.Repo.GetUserIconOverridesAsync(userId);
                Central.Core.Services.IconOverrideService.Instance.Load(adminDefs, userOv);
            };

            IconOverridesGridPanel.ResetAllRequested = async () =>
            {
                await VM.Repo.ResetAllUserIconOverridesAsync(userId);
                var adminDefs = await VM.Repo.GetAllIconDefaultsAsync();
                Central.Core.Services.IconOverrideService.Instance.Load(adminDefs, new());
            };

            IconOverridesGridPanel.RefreshRequested = async () => await LoadIconOverridesAsync();
            _iconOverridesLoaded = true;
        }
        catch (Exception ex) { AppLogger.LogException("Icons", ex, "LoadIconOverridesAsync"); }
    }

    // ── AD Browser Panel Loading ─────────────────────────────────────────
    private bool _adBrowserLoaded;

    private async Task LoadAdBrowserAsync()
    {
        try
        {
            AdBrowserGridPanel.BrowseAd = async () =>
            {
                var integration = (await VM.Repo.GetIntegrationsAsync())
                    .FirstOrDefault(i => i.Name == "activedirectory");
                if (integration == null || !integration.IsEnabled)
                {
                    AdBrowserGridPanel.Status.Text = "Configure Active Directory in Integrations panel first";
                    return;
                }
                var config = System.Text.Json.JsonSerializer.Deserialize<Central.Core.Models.AdConfig>(integration.ConfigJson)
                    ?? new Central.Core.Models.AdConfig();
                if (!config.IsConfigured)
                {
                    AdBrowserGridPanel.Status.Text = "AD domain not configured";
                    return;
                }
                var existingGuids = (await VM.Repo.GetAllUsersAsync())
                    .Where(u => !string.IsNullOrEmpty(u.AdGuid)).Select(u => u.AdGuid).ToHashSet();
                var adUsers = await Central.Core.Services.ActiveDirectoryService.BrowseUsersAsync(config, existingGuids);
                AdBrowserGridPanel.Load(adUsers);
            };
            AdBrowserGridPanel.ImportSelected = async () =>
            {
                var selected = AdBrowserGridPanel.Grid.SelectedItems?
                    .OfType<Central.Core.Models.AdUser>()
                    .Where(u => !u.IsImported).ToList();
                if (selected == null || selected.Count == 0) return;
                var imports = Central.Core.Services.ActiveDirectoryService.BuildImportUsers(selected);
                await VM.Repo.BulkUpsertAdUsersAsync(imports);
                AdBrowserGridPanel.Status.Text = $"Imported {imports.Count} users";
                await VM.LoadPanelDataAsync("admin", force: true);
            };
            _adBrowserLoaded = true;
        }
        catch (Exception ex) { AppLogger.LogException("AD", ex, "LoadAdBrowserAsync"); }
    }

    // ── Migrations Panel Loading ──────────────────────────────────────────
    private bool _migrationsLoaded;

    private async Task LoadMigrationsAsync()
    {
        try
        {
            var runner = new MigrationRunner(App.Dsn);
            var migrationsDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "db", "migrations");
            if (!System.IO.Directory.Exists(migrationsDir))
                migrationsDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "migrations");

            var all = await runner.GetAllMigrationsAsync(migrationsDir);
            MigrationsGridPanel.Load(all);

            MigrationsGridPanel.ApplyPending = async () =>
            {
                var count = await runner.ApplyPendingAsync(migrationsDir,
                    AuthContext.Instance.CurrentUser?.Username ?? "admin");
                MigrationsGridPanel.Status.Text = $"Applied {count} migrations";
                MigrationsGridPanel.Load(await runner.GetAllMigrationsAsync(migrationsDir));
            };
            MigrationsGridPanel.RefreshRequested = async () =>
                MigrationsGridPanel.Load(await runner.GetAllMigrationsAsync(migrationsDir));
            _migrationsLoaded = true;
        }
        catch (Exception ex) { AppLogger.LogException("Migrations", ex, "LoadMigrationsAsync"); }
    }

    // ── Purge Panel Loading ───────────────────────────────────────────────
    private bool _purgeLoaded;

    private async Task LoadPurgeAsync()
    {
        try
        {
            var counts = await VM.Repo.GetSoftDeletedCountsAsync();
            PurgeGridPanel.Load(counts);

            PurgeGridPanel.PurgeTable = async tableName =>
            {
                var purged = await VM.Repo.PurgeSoftDeletedAsync(tableName);
                PurgeGridPanel.Status.Text = $"Purged {purged} records from {tableName}";
                PurgeGridPanel.Load(await VM.Repo.GetSoftDeletedCountsAsync());
            };
            PurgeGridPanel.PurgeAll = async () =>
            {
                foreach (var kv in counts)
                    await VM.Repo.PurgeSoftDeletedAsync(kv.Key);
                PurgeGridPanel.Status.Text = "All soft-deleted records purged";
                PurgeGridPanel.Load(await VM.Repo.GetSoftDeletedCountsAsync());
            };
            PurgeGridPanel.RefreshRequested = async () =>
                PurgeGridPanel.Load(await VM.Repo.GetSoftDeletedCountsAsync());
            _purgeLoaded = true;
        }
        catch (Exception ex) { AppLogger.LogException("Purge", ex, "LoadPurgeAsync"); }
    }

    // ── Backup Panel Loading ──────────────────────────────────────────────
    private bool _backupLoaded;

    private async Task LoadBackupAsync()
    {
        try
        {
            var backupSvc = new BackupService(App.Dsn);
            var history = await backupSvc.GetBackupHistoryAsync();
            BackupGridPanel.Load(history);

            BackupGridPanel.RunBackup = async path =>
            {
                BackupGridPanel.Status.Text = "Backup running...";
                var record = await backupSvc.BackupAsync(path, "full",
                    AuthContext.Instance.CurrentUser?.Username ?? "admin");
                BackupGridPanel.Status.Text = $"Backup complete: {record.FileSizeDisplay}";
                BackupGridPanel.Load(await backupSvc.GetBackupHistoryAsync());
                Central.Core.Services.NotificationService.Instance?.NotifyEvent(
                    "backup_complete", "Backup Complete", $"{record.FileSizeDisplay} → {path}",
                    Central.Core.Services.NotificationType.Success);
            };
            BackupGridPanel.RefreshRequested = async () =>
                BackupGridPanel.Load(await backupSvc.GetBackupHistoryAsync());
            _backupLoaded = true;
        }
        catch (Exception ex) { AppLogger.LogException("Backup", ex, "LoadBackupAsync"); }
    }

    // ── Locations Panel Loading ───────────────────────────────────────────
    private bool _locationsLoaded;

    private async Task LoadLocationsAsync()
    {
        try
        {
            var countries = await VM.Repo.GetCountriesAsync();
            var regions = await VM.Repo.GetRegionsAsync();
            LocationsGridPanel.Load(countries, regions);

            LocationsGridPanel.SaveCountry = async c => await VM.Repo.UpsertCountryAsync(c);
            LocationsGridPanel.SaveRegion = async r => await VM.Repo.UpsertRegionAsync(r);
            LocationsGridPanel.DeleteCountry = async c => await VM.Repo.DeleteCountryAsync(c.Id);
            LocationsGridPanel.DeleteRegion = async r => await VM.Repo.DeleteRegionAsync(r.Id);
            LocationsGridPanel.RefreshRequested = async () =>
                LocationsGridPanel.Load(await VM.Repo.GetCountriesAsync(), await VM.Repo.GetRegionsAsync());
            _locationsLoaded = true;
        }
        catch (Exception ex) { AppLogger.LogException("Locations", ex, "LoadLocationsAsync"); }
    }

    // ── Reference Config Panel Loading ────────────────────────────────────
    private bool _referenceConfigLoaded;

    private async Task LoadReferenceConfigAsync()
    {
        try
        {
            var configs = await VM.Repo.GetReferenceConfigsAsync();
            ReferenceConfigGridPanel.Load(configs);

            ReferenceConfigGridPanel.SaveConfig = async rc => await VM.Repo.UpsertReferenceConfigAsync(rc);
            ReferenceConfigGridPanel.RefreshRequested = async () =>
                ReferenceConfigGridPanel.Load(await VM.Repo.GetReferenceConfigsAsync());
            _referenceConfigLoaded = true;
        }
        catch (Exception ex) { AppLogger.LogException("RefConfig", ex, "LoadReferenceConfigAsync"); }
    }

    // ── Podman Panel Loading ──────────────────────────────────────────────
    private bool _podmanLoaded;

    private async Task LoadPodmanAsync()
    {
        try
        {
            var containers = await Central.Core.Services.PodmanService.GetContainersAsync();
            PodmanGridPanel.Load(containers);

            PodmanGridPanel.RefreshContainers = async () =>
                PodmanGridPanel.Load(await Central.Core.Services.PodmanService.GetContainersAsync());
            PodmanGridPanel.StartContainer = async name =>
            {
                await Central.Core.Services.PodmanService.StartContainerAsync(name);
                PodmanGridPanel.Load(await Central.Core.Services.PodmanService.GetContainersAsync());
            };
            PodmanGridPanel.StopContainer = async name =>
            {
                await Central.Core.Services.PodmanService.StopContainerAsync(name);
                PodmanGridPanel.Load(await Central.Core.Services.PodmanService.GetContainersAsync());
            };
            PodmanGridPanel.RestartContainer = async name =>
            {
                await Central.Core.Services.PodmanService.RestartContainerAsync(name);
                PodmanGridPanel.Load(await Central.Core.Services.PodmanService.GetContainersAsync());
            };
            PodmanGridPanel.GetLogs = async name =>
                await Central.Core.Services.PodmanService.GetLogsAsync(name);
            _podmanLoaded = true;
        }
        catch (Exception ex) { AppLogger.LogException("Podman", ex, "LoadPodmanAsync"); }
    }

    // ── Scheduler Panel Loading ───────────────────────────────────────────
    private bool _schedulerLoaded;

    private async Task LoadSchedulerAsync()
    {
        try
        {
            var (start, end) = SchedulerGridPanel.GetVisibleRange();
            var appointments = await VM.Repo.GetAppointmentsAsync(start, end);
            var resources = await VM.Repo.GetAppointmentResourcesAsync();
            SchedulerGridPanel.Load(appointments, resources);

            SchedulerGridPanel.SaveAppointment = async appt =>
            {
                await VM.Repo.UpsertAppointmentAsync(appt);
                SchedulerGridPanel.Status.Text = $"Saved: {appt.Subject}";
            };
            SchedulerGridPanel.DeleteAppointment = async id =>
                await VM.Repo.DeleteAppointmentAsync(id);
            SchedulerGridPanel.RefreshRequested = async () =>
            {
                var (s, e) = SchedulerGridPanel.GetVisibleRange();
                SchedulerGridPanel.Load(await VM.Repo.GetAppointmentsAsync(s, e), await VM.Repo.GetAppointmentResourcesAsync());
            };
            _schedulerLoaded = true;
        }
        catch (Exception ex) { AppLogger.LogException("Scheduler", ex, "LoadSchedulerAsync"); }
    }

    // ── Identity Providers Panel Loading ─────────────────────────────────
    private bool _idpLoaded;

    private async Task LoadIdentityProvidersAsync()
    {
        try
        {
            var providers = await VM.Repo.GetIdentityProvidersAsync();
            var domains = await VM.Repo.GetDomainMappingsAsync();
            IdentityProvidersGridPanel.Load(providers, domains);

            IdentityProvidersGridPanel.SaveProvider = async p => await VM.Repo.UpsertIdentityProviderAsync(p);
            IdentityProvidersGridPanel.DeleteProvider = async id => await VM.Repo.DeleteIdentityProviderAsync(id);
            IdentityProvidersGridPanel.SaveDomainMapping = async (domain, pid) => await VM.Repo.UpsertDomainMappingAsync(domain, pid);
            IdentityProvidersGridPanel.RefreshRequested = async () =>
                IdentityProvidersGridPanel.Load(await VM.Repo.GetIdentityProvidersAsync(), await VM.Repo.GetDomainMappingsAsync());
            _idpLoaded = true;
        }
        catch (Exception ex) { AppLogger.LogException("IdP", ex, "LoadIdentityProvidersAsync"); }
    }

    // ── Auth Events Panel Loading ─────────────────────────────────────────
    private bool _authEventsLoaded;

    private async Task LoadAuthEventsAsync()
    {
        try
        {
            var events = await VM.Repo.GetAuthEventsAsync();
            AuthEventsGridPanel.Load(events);
            AuthEventsGridPanel.RefreshRequested = async () =>
                AuthEventsGridPanel.Load(await VM.Repo.GetAuthEventsAsync());
            _authEventsLoaded = true;
        }
        catch (Exception ex) { AppLogger.LogException("Auth", ex, "LoadAuthEventsAsync"); }
    }

    // ── Sync Config Panel Loading ─────────────────────────────────────────
    private bool _syncConfigLoaded;

    private async Task LoadSyncConfigAsync()
    {
        try
        {
            var configs = await VM.Repo.GetSyncConfigsAsync();
            SyncConfigGridPanel.Load(configs);

            SyncConfigGridPanel.SaveConfig = async config => await VM.Repo.UpsertSyncConfigAsync(config);
            SyncConfigGridPanel.LoadLog = async configId => await VM.Repo.GetSyncLogAsync(configId);
            SyncConfigGridPanel.RefreshRequested = async () =>
                SyncConfigGridPanel.Load(await VM.Repo.GetSyncConfigsAsync());

            SyncConfigGridPanel.RunSync = async config =>
            {
                try
                {
                    await VM.Repo.UpdateSyncStatusAsync(config.Id, "running");
                    config.LastSyncStatus = "running";

                    var entityMaps = await VM.Repo.GetSyncEntityMapsAsync(config.Id);
                    var allFieldMaps = new List<Central.Core.Integration.SyncFieldMap>();
                    foreach (var em in entityMaps)
                        allFieldMaps.AddRange(await VM.Repo.GetSyncFieldMapsAsync(em.Id));

                    var engine = Central.Core.Integration.SyncEngine.Instance;
                    engine.SetLogCallback(async (cid, status, entity, read, created, updated, failed, error) =>
                        await VM.Repo.InsertSyncLogAsync(cid, status, entity, read, created, updated, failed, error));

                    var result = await engine.ExecuteSyncAsync(config, entityMaps, allFieldMaps,
                        async (table, fields, key) => await VM.Repo.UpsertSyncRecordAsync(table, fields, key));

                    await VM.Repo.UpdateSyncStatusAsync(config.Id, result.Status, result.ErrorMessage);
                    config.LastSyncStatus = result.Status;
                    config.LastSyncAt = DateTime.UtcNow;
                    Central.Core.Services.NotificationService.Instance?.NotifyEvent(
                        "sync_complete", $"Sync Complete: {config.Name}",
                        $"{result.RecordsRead} read, {result.RecordsCreated} created, {result.RecordsFailed} failed",
                        result.RecordsFailed > 0 ? Central.Core.Services.NotificationType.Warning : Central.Core.Services.NotificationType.Success);

                    SyncConfigGridPanel.Status.Text = $"Sync complete: {result.RecordsRead} read, {result.RecordsCreated} created, {result.RecordsFailed} failed ({result.DurationMs}ms)";
                }
                catch (Exception ex)
                {
                    await VM.Repo.UpdateSyncStatusAsync(config.Id, "failed", ex.Message);
                    config.LastSyncStatus = "failed";
                    SyncConfigGridPanel.Status.Text = $"Sync failed: {ex.Message}";
                    Central.Core.Services.NotificationService.Instance?.NotifyEvent(
                        "sync_failure", $"Sync Failed: {config.Name}", ex.Message,
                        Central.Core.Services.NotificationType.Error);
                }
            };

            SyncConfigGridPanel.TestConnection = async config =>
            {
                var engine = Central.Core.Integration.SyncEngine.Instance;
                var agentTypes = engine.GetAgentTypes();
                if (!agentTypes.Contains(config.AgentType))
                    return $"No agent registered for: {config.AgentType}. Available: {string.Join(", ", agentTypes)}";
                return $"Agent type '{config.AgentType}' is registered. Configure entity/field mappings to sync.";
            };

            // Register built-in agents
            var syncEngine = Central.Core.Integration.SyncEngine.Instance;
            syncEngine.RegisterAgent(new Central.Module.ServiceDesk.Services.ManageEngineAgent());
            syncEngine.RegisterAgent(new Central.Core.Integration.CsvImportAgent());
            syncEngine.RegisterAgent(new Central.Core.Integration.RestApiAgent());

            // Register built-in converters
            syncEngine.RegisterConverter(new Central.Core.Integration.DirectConverter());
            syncEngine.RegisterConverter(new Central.Core.Integration.ConstantConverter());
            syncEngine.RegisterConverter(new Central.Core.Integration.CombineConverter());
            syncEngine.RegisterConverter(new Central.Core.Integration.SplitConverter());
            syncEngine.RegisterConverter(new Central.Core.Integration.LookupConverter());
            syncEngine.RegisterConverter(new Central.Core.Integration.DateFormatConverter());
            syncEngine.RegisterConverter(new Central.Core.Integration.ExpressionConverter());

            _syncConfigLoaded = true;
        }
        catch (Exception ex) { AppLogger.LogException("Sync", ex, "LoadSyncConfigAsync"); }
    }

    // ── API Keys Panel Loading ────────────────────────────────────────────
    private bool _apiKeysLoaded;

    private async Task LoadApiKeysAsync()
    {
        try
        {
            var keys = await VM.Repo.GetApiKeysAsync();
            ApiKeysGridPanel.Load(keys);

            ApiKeysGridPanel.GenerateKey = async (name, role) =>
            {
                var userId = Central.Core.Auth.AuthContext.Instance.CurrentUser?.Id ?? 0;
                var rawKey = await VM.Repo.CreateApiKeyAsync(name, role, userId);
                _ = Central.Core.Services.AuditService.Instance.LogCreateAsync("ApiKey", "", name);
                return rawKey;
            };
            ApiKeysGridPanel.RevokeKey = async id =>
            {
                await VM.Repo.RevokeApiKeyAsync(id);
                _ = Central.Core.Services.AuditService.Instance.LogAsync("Revoke", "ApiKey", id.ToString());
            };
            ApiKeysGridPanel.DeleteKey = async id => await VM.Repo.DeleteApiKeyAsync(id);
            ApiKeysGridPanel.RefreshRequested = async () =>
                ApiKeysGridPanel.Load(await VM.Repo.GetApiKeysAsync());
            _apiKeysLoaded = true;
        }
        catch (Exception ex) { AppLogger.LogException("ApiKeys", ex, "LoadApiKeysAsync"); }
    }

    // ── Audit Log Panel Loading ───────────────────────────────────────────
    private bool _auditLogLoaded;

    private async Task LoadAuditLogAsync()
    {
        try
        {
            var entries = await VM.Repo.GetAuditLogAsync();
            AuditLogGridPanel.Load(entries);
            AuditLogGridPanel.LoadAudit = async (limit, entityType, username) =>
                await VM.Repo.GetAuditLogAsync(limit, entityType, username);
            _auditLogLoaded = true;
        }
        catch (Exception ex) { AppLogger.LogException("Audit", ex, "LoadAuditLogAsync"); }
    }

    // ── Sessions Panel Loading ────────────────────────────────────────────
    private bool _sessionsLoaded;

    private async Task LoadSessionsAsync()
    {
        try
        {
            var sessions = await VM.Repo.GetActiveSessionsAsync();
            SessionsGridPanel.Load(sessions);

            SessionsGridPanel.ForceLogout = async id =>
            {
                await VM.Repo.ForceEndSessionAsync(id);
                _ = Central.Core.Services.AuditService.Instance.LogAsync("ForceLogout", "Session", id.ToString());
            };
            SessionsGridPanel.ForceLogoutAll = async userId =>
            {
                await VM.Repo.ForceEndAllSessionsAsync(userId);
                _ = Central.Core.Services.AuditService.Instance.LogAsync("ForceLogoutAll", "User", userId.ToString());
            };
            SessionsGridPanel.RefreshRequested = async () =>
                SessionsGridPanel.Load(await VM.Repo.GetActiveSessionsAsync());
            _sessionsLoaded = true;
        }
        catch (Exception ex) { AppLogger.LogException("Sessions", ex, "LoadSessionsAsync"); }
    }

    // ── Notification Preferences Panel Loading ────────────────────────────
    private bool _notifPrefsLoaded;

    private async Task LoadNotificationPrefsAsync()
    {
        try
        {
            var userId = Central.Core.Auth.AuthContext.Instance.CurrentUser?.Id ?? 0;
            if (userId == 0) return;

            var prefs = await VM.Repo.GetNotificationPreferencesAsync(userId);
            NotificationPrefsGridPanel.Load(prefs);

            NotificationPrefsGridPanel.SavePref = async pref =>
            {
                await VM.Repo.UpsertNotificationPreferenceAsync(userId, pref.EventType, pref.Channel, pref.IsEnabled);
                // Refresh cached prefs in NotificationService
                var allPrefs = await VM.Repo.GetNotificationPreferencesAsync(userId);
                Central.Core.Services.NotificationService.Instance.LoadPreferences(allPrefs);
            };
            NotificationPrefsGridPanel.RefreshRequested = async () =>
            {
                NotificationPrefsGridPanel.Load(await VM.Repo.GetNotificationPreferencesAsync(userId));
            };

            // Also load prefs into NotificationService cache
            Central.Core.Services.NotificationService.Instance.LoadPreferences(prefs);

            _notifPrefsLoaded = true;
        }
        catch (Exception ex) { AppLogger.LogException("NotifPrefs", ex, "LoadNotificationPrefsAsync"); }
    }
}
