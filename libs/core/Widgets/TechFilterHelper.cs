#if WINDOWS
using System.Windows;
using System.Windows.Media;
using WC = System.Windows.Controls;

namespace Central.Core.Widgets;

/// <summary>
/// Engine-level helper to build tech/team filter checkbox panels.
/// Reusable across any module that needs technician/team filtering.
/// </summary>
public static class TechFilterHelper
{
    /// <summary>Build filter checkboxes into a WrapPanel — All/None buttons, team buttons, individual tech checkboxes.</summary>
    public static void BuildFilter(WC.WrapPanel panel, List<string> techs, List<Models.SdTeam> teams, Action onChanged)
    {
        panel.Children.Clear();

        var selectAll = MakeButton("All", () => { SetAll(panel, true); onChanged(); });
        var selectNone = MakeButton("None", () => { SetAll(panel, false); onChanged(); });
        panel.Children.Add(selectAll);
        panel.Children.Add(selectNone);

        foreach (var team in teams)
        {
            var members = team.Members;
            var teamBtn = MakeButton(team.Name, () =>
            {
                SetAll(panel, false);
                foreach (var cb in panel.Children.OfType<WC.CheckBox>())
                    if (members.Contains(cb.Content as string ?? "")) cb.IsChecked = true;
                onChanged();
            });
            panel.Children.Add(teamBtn);
        }

        if (teams.Count > 0)
            panel.Children.Add(new WC.Separator { Width = double.NaN, Margin = new Thickness(0, 0, 0, 4) });

        foreach (var tech in techs)
        {
            var cb = new WC.CheckBox
            {
                Content = tech, IsChecked = true,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C0C0C0")),
                Margin = new Thickness(0, 0, 10, 4), FontSize = 11
            };
            cb.Checked += (_, _) => onChanged();
            cb.Unchecked += (_, _) => onChanged();
            panel.Children.Add(cb);
        }
    }

    /// <summary>Get checked tech names. Returns null if all are checked (= no filter).</summary>
    public static List<string>? GetChecked(WC.WrapPanel panel)
    {
        var all = panel.Children.OfType<WC.CheckBox>().ToList();
        if (all.Count == 0) return null;
        var selected = all.Where(cb => cb.IsChecked == true).Select(cb => cb.Content as string ?? "").ToList();
        return selected.Count == all.Count ? null : selected;
    }

    private static void SetAll(WC.WrapPanel panel, bool isChecked)
    {
        foreach (var cb in panel.Children.OfType<WC.CheckBox>()) cb.IsChecked = isChecked;
    }

    private static WC.Button MakeButton(string text, Action onClick)
    {
        var btn = new WC.Button
        {
            Content = text, Padding = new Thickness(6, 2, 6, 2),
            Margin = new Thickness(0, 0, 4, 4), FontSize = 10
        };
        btn.Click += (_, _) => onClick();
        return btn;
    }
}
#endif
