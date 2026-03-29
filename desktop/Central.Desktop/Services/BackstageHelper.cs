using DevExpress.Xpf.Bars;
using Central.Core.Auth;
using Central.Data;

namespace Central.Desktop.Services;

/// <summary>
/// Extracts backstage population logic from MainWindow code-behind.
/// Handles user profile, connection info, theme gallery, and backstage button wiring.
/// </summary>
public static class BackstageHelper
{
    public static void PopulateUserProfile(
        System.Windows.Controls.TextBlock displayName,
        System.Windows.Controls.TextBlock username,
        System.Windows.Controls.TextBlock role,
        System.Windows.Controls.TextBlock email,
        System.Windows.Controls.TextBlock initials,
        System.Windows.Controls.TextBlock loginType,
        System.Windows.Controls.TextBlock permCount,
        System.Windows.Controls.TextBlock sites)
    {
        var user = AuthContext.Instance.CurrentUser;
        if (user != null)
        {
            displayName.Text = user.DisplayName;
            username.Text = user.Username;
            role.Text = user.RoleName;
            email.Text = !string.IsNullOrEmpty(user.Email) ? user.Email : "";

            var parts = user.DisplayName.Split(' ', '.', StringSplitOptions.RemoveEmptyEntries);
            initials.Text = parts.Length >= 2
                ? $"{parts[0][0]}{parts[^1][0]}".ToUpper()
                : user.DisplayName.Length >= 2 ? user.DisplayName[..2].ToUpper() : user.DisplayName.ToUpper();

            loginType.Text = user.UserType;
            permCount.Text = $"{AuthContext.Instance.PermissionCount} granted";

            var siteList = AuthContext.Instance.AllowedSites;
            sites.Text = siteList.Count == 0 ? "All sites" : string.Join(", ", siteList);
        }
        else
        {
            displayName.Text = "Offline User";
            username.Text = Environment.UserName;
            role.Text = "Admin (Offline)";
            initials.Text = "OF";
            loginType.Text = "Offline";
            permCount.Text = "All (offline mode)";
            sites.Text = "All sites";
        }
    }

    public static void PopulateConnectionInfo(
        System.Windows.Controls.TextBlock connMode,
        System.Windows.Controls.TextBlock connDb,
        System.Windows.Controls.TextBlock connStatus,
        System.Windows.Controls.TextBlock apiUrl)
    {
        connMode.Text = App.Connectivity?.Mode.ToString() ?? "DirectDb";
        connDb.Text = App.Dsn.Contains("Host=") ? App.Dsn : "(not set)";
        connStatus.Text = App.IsDbOnline ? "Connected" : "Offline";
        var url = App.Settings?.Get<string>("api.url") ?? App.Connectivity?.ApiUrl ?? "Not configured";
        apiUrl.Text = url;
    }

    public static void PopulateGalleryControl(Gallery gallery)
    {
        gallery.Groups.Clear();
        var currentTheme = DevExpress.Xpf.Core.ThemeManager.ApplicationThemeName;
        var groups = new Dictionary<string, GalleryItemGroup>();

        foreach (var theme in DevExpress.Xpf.Core.Theme.Themes.OrderBy(t => t.Category).ThenBy(t => t.Name))
        {
            if (string.IsNullOrEmpty(theme.Name) || theme.Name == "HybridApp") continue;

            try { var _ = theme.Assembly; }
            catch { continue; }

            var category = string.IsNullOrEmpty(theme.Category) ? "Other" : theme.Category;
            if (!groups.TryGetValue(category, out var group))
            {
                group = new GalleryItemGroup { Caption = category };
                groups[category] = group;
                gallery.Groups.Add(group);
            }

            var isDefault = theme.Name == DevExpress.Xpf.Core.Theme.Office2019ColorfulName;
            var item = new GalleryItem
            {
                Caption = isDefault ? $"{theme.Name}  (Default)" : theme.Name,
                Tag = theme.Name,
                IsChecked = theme.Name == currentTheme
            };

            try { if (theme.SmallGlyph != null) item.Glyph = new System.Windows.Media.Imaging.BitmapImage(theme.SmallGlyph); }
            catch { }

            group.Items.Add(item);
        }
    }

    public static void UpdateThemeCheckMarks(Gallery?[] galleries, string themeName)
    {
        foreach (var gallery in galleries)
            if (gallery != null)
                foreach (var group in gallery.Groups)
                    foreach (var item in group.Items)
                        item.IsChecked = (item.Tag as string) == themeName;
    }
}
