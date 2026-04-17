using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using DevExpress.Xpf.Docking;
using DevExpress.Xpf.Grid;
using Central.Persistence;

namespace Central.Desktop.Services;

/// <summary>
/// Saves and restores DevExpress grid/dock layouts and window bounds to the database.
/// </summary>
public class LayoutService
{
    private readonly DbRepository _repo;
    private readonly int _userId;

    public LayoutService(DbRepository repo, int userId)
    {
        _repo   = repo;
        _userId = userId;
    }

    // ── Grid layouts ─────────────────────────────────────────────────────

    public async Task SaveGridLayoutAsync(GridControl grid, string key)
        => await SaveGridLayoutAsync((DataControlBase)grid, key);

    public async Task SaveGridLayoutAsync(TreeListControl grid, string key)
        => await SaveGridLayoutAsync((DataControlBase)grid, key);

    public async Task SaveGridLayoutAsync(DataControlBase grid, string key)
    {
        try
        {
            using var ms = new MemoryStream();
            grid.SaveLayoutToStream(ms);
            ms.Position = 0;
            using var reader = new StreamReader(ms);
            var xml = await reader.ReadToEndAsync();
            await _repo.SaveUserSettingAsync(_userId, key, xml);
        }
        catch { /* layout save is best-effort */ }
    }

    public async Task RestoreGridLayoutAsync(GridControl grid, string key)
        => await RestoreGridLayoutAsync((DataControlBase)grid, key);

    public async Task RestoreGridLayoutAsync(TreeListControl grid, string key)
        => await RestoreGridLayoutAsync((DataControlBase)grid, key);

    public async Task RestoreGridLayoutAsync(DataControlBase grid, string key)
    {
        try
        {
            var xml = await _repo.GetUserSettingAsync(_userId, key);
            if (string.IsNullOrEmpty(xml)) return;
            using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml));
            grid.RestoreLayoutFromStream(ms);
        }
        catch { /* ignore incompatible layouts */ }
    }

    // ── Dock layouts ─────────────────────────────────────────────────────

    public async Task SaveDockLayoutAsync(DockLayoutManager dock, string key)
    {
        try
        {
            using var ms = new MemoryStream();
            dock.SaveLayoutToStream(ms);
            ms.Position = 0;
            using var reader = new StreamReader(ms);
            var xml = await reader.ReadToEndAsync();
            await _repo.SaveUserSettingAsync(_userId, key, xml);
        }
        catch { /* best-effort */ }
    }

    public async Task RestoreDockLayoutAsync(DockLayoutManager dock, string key)
    {
        try
        {
            var xml = await _repo.GetUserSettingAsync(_userId, key);
            if (string.IsNullOrEmpty(xml)) return;
            using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml));
            dock.RestoreLayoutFromStream(ms);
        }
        catch { /* ignore incompatible layouts */ }
    }

    // ── Window bounds ────────────────────────────────────────────────────

    public async Task SaveWindowBoundsAsync(Window window)
    {
        try
        {
            var bounds = new WindowBounds
            {
                Left   = window.Left,
                Top    = window.Top,
                Width  = window.Width,
                Height = window.Height,
                State  = window.WindowState.ToString()
            };
            var json = JsonSerializer.Serialize(bounds);
            await _repo.SaveUserSettingAsync(_userId, "window.bounds", json);
        }
        catch { /* best-effort */ }
    }

    public async Task RestoreWindowBoundsAsync(Window window)
    {
        try
        {
            var json = await _repo.GetUserSettingAsync(_userId, "window.bounds");
            if (string.IsNullOrEmpty(json)) return;
            var bounds = JsonSerializer.Deserialize<WindowBounds>(json);
            if (bounds == null) return;

            // Basic bounds check against virtual screen
            var vw = SystemParameters.VirtualScreenWidth;
            var vh = SystemParameters.VirtualScreenHeight;
            var vl = SystemParameters.VirtualScreenLeft;
            var vt = SystemParameters.VirtualScreenTop;

            bool onScreen = bounds.Left >= vl - 50 && bounds.Top >= vt - 50
                         && bounds.Left + bounds.Width  <= vl + vw + 50
                         && bounds.Top  + bounds.Height <= vt + vh + 50;

            if (onScreen)
            {
                window.Left   = bounds.Left;
                window.Top    = bounds.Top;
                window.Width  = bounds.Width;
                window.Height = bounds.Height;
            }

            if (Enum.TryParse<WindowState>(bounds.State, out var state))
                window.WindowState = state;
        }
        catch { /* ignore bad data */ }
    }

    // ── Simple key/value preferences ─────────────────────────────────────

    public async Task SavePreferenceAsync(string key, string value)
        => await _repo.SaveUserSettingAsync(_userId, key, value);

    public async Task<string?> GetPreferenceAsync(string key)
        => await _repo.GetUserSettingAsync(_userId, key);

    /// <summary>
    /// Deletes all saved layout settings for this user, restoring defaults on next launch.
    /// </summary>
    public async Task ClearAllLayoutsAsync()
    {
        var keys = new[]
        {
            "layout.devices_grid", "layout.switch_grid", "layout.admin_grid",
            "layout.users_grid", "layout.roles_grid", "layout.dock",
            "window.bounds", "pref.hide_reserved",
            "layout.panel_states", "pref.site_selections",
            "pref.devices_search", "pref.active_ribbon_tab",
            "layout.interfaces_grid", "layout.detail_tab_order"
        };
        foreach (var key in keys)
        {
            try { await _repo.DeleteUserSettingAsync(_userId, key); }
            catch { /* best-effort */ }
        }
    }

    private class WindowBounds
    {
        public double Left   { get; set; }
        public double Top    { get; set; }
        public double Width  { get; set; }
        public double Height { get; set; }
        public string State  { get; set; } = "Normal";
    }
}
