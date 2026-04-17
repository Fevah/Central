using System.Windows;
using DevExpress.Xpf.Core;

namespace Central.Desktop.Services;

/// <summary>
/// Engine input prompt — replaces Microsoft.VisualBasic.Interaction.InputBox.
/// Uses DXDialogWindow with a TextEdit for user input.
/// </summary>
public static class InputPrompt
{
    /// <summary>Show a text input dialog. Returns entered text or null if cancelled.</summary>
    public static string? Show(string title, string prompt, string defaultValue = "", Window? owner = null)
    {
        var textBox = new DevExpress.Xpf.Editors.TextEdit
        {
            EditValue = defaultValue,
            Width = 350,
            Margin = new Thickness(10)
        };

        var panel = new System.Windows.Controls.StackPanel { Margin = new Thickness(10) };
        panel.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = prompt,
            Margin = new Thickness(0, 0, 0, 8),
            FontSize = 13
        });
        panel.Children.Add(textBox);

        var dialog = new DXDialogWindow
        {
            Title = title,
            Content = panel,
            Width = 400,
            Height = 160,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = owner
        };

        textBox.Focus();

        if (dialog.ShowDialogWindow()?.IsDefault == true)
            return textBox.EditValue?.ToString();

        return null;
    }
}
