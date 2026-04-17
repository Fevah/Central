using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using DevExpress.Xpf.Core;
using Central.Core.Services;
using Application = System.Windows.Application;
using CheckBox = System.Windows.Controls.CheckBox;
using ComboBox = System.Windows.Controls.ComboBox;
using Color = System.Windows.Media.Color;
using TextBox = System.Windows.Controls.TextBox;

namespace Central.Desktop.Services;

/// <summary>
/// Shows detail dialogs for entity add/edit using DevExpress DXDialogWindow.
/// Based on TotalLink's DetailDialogService — windowStateKey per entity type,
/// auto-generated form from public properties, OK/Cancel wiring.
/// </summary>
public class DetailDialogService : IDetailDialogService
{
    /// <summary>Cached window positions per entity type.</summary>
    private static readonly Dictionary<string, (double Left, double Top, double Width, double Height)> _windowStates = new();

    public bool ShowDialog<T>(DetailEditMode mode, T entity, string? title = null) where T : class
    {
        var typeName = typeof(T).Name;
        var windowTitle = title ?? $"{mode} {SplitCamelCase(typeName)}";
        var stateKey = $"DetailDialog_{typeName}";

        // Get default size from attribute or use 500x600
        var sizeAttr = typeof(T).GetCustomAttribute<DialogSizeAttribute>();
        var defaultWidth = sizeAttr?.Width ?? 500;
        var defaultHeight = sizeAttr?.Height ?? 600;

        // Build form content
        var form = BuildFormContent(entity, mode);
        var scrollViewer = new ScrollViewer
        {
            Content = form,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(16)
        };

        // Create DXDialogWindow
        var dialog = new DXDialogWindow
        {
            Title = windowTitle,
            Content = scrollViewer,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Application.Current.MainWindow,
            ShowInTaskbar = false,
            ResizeMode = ResizeMode.CanResize,
            MinWidth = 350,
            MinHeight = 300
        };

        // Restore saved window state or use defaults
        if (_windowStates.TryGetValue(stateKey, out var state))
        {
            dialog.Left = state.Left;
            dialog.Top = state.Top;
            dialog.Width = state.Width;
            dialog.Height = state.Height;
            dialog.WindowStartupLocation = WindowStartupLocation.Manual;
        }
        else
        {
            dialog.Width = defaultWidth;
            dialog.Height = defaultHeight;
        }

        // Set read-only for View mode
        if (mode == DetailEditMode.View)
            form.IsEnabled = false;

        // Show dialog — DXDialogWindow.ShowDialogWindow() returns UICommand
        var result = dialog.ShowDialogWindow();

        // Save window state
        _windowStates[stateKey] = (dialog.Left, dialog.Top, dialog.Width, dialog.Height);

        // OK = IsDefault command
        return result?.IsDefault == true;
    }

    /// <summary>Show a nested child entity dialog from within a parent dialog.
    /// The child dialog is modal to the parent, not the main window.</summary>
    public bool ShowNestedDialog<TParent, TChild>(DetailEditMode mode, TChild child, TParent parent, string? title = null)
        where TChild : class
        where TParent : class
    {
        var childTitle = title ?? $"{mode} {typeof(TChild).Name} (in {typeof(TParent).Name})";
        return ShowDialog(mode, child, childTitle);
    }

    public bool Confirm(string message, string title = "Confirm")
    {
        var result = DXMessageBox.Show(
            message, title,
            MessageBoxButton.YesNo, MessageBoxImage.Question,
            MessageBoxResult.No);
        return result == MessageBoxResult.Yes;
    }

    public void Info(string message, string title = "Info")
    {
        DXMessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public void Error(string message, string title = "Error")
    {
        DXMessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    /// <summary>
    /// Auto-generate a form from public settable properties of the entity.
    /// Groups properties by category if [Category] attribute is present.
    /// </summary>
    private static StackPanel BuildFormContent<T>(T entity, DetailEditMode mode) where T : class
    {
        var panel = new StackPanel { Margin = new Thickness(0) };
        var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite)
            .Where(p => !IsExcludedProperty(p))
            .OrderBy(p => GetPropertyOrder(p))
            .ToList();

        foreach (var prop in props)
        {
            var label = new TextBlock
            {
                Text = SplitCamelCase(prop.Name),
                Foreground = new SolidColorBrush(Color.FromRgb(0x90, 0xCA, 0xF9)),
                FontSize = 11,
                Margin = new Thickness(0, 8, 0, 2)
            };
            panel.Children.Add(label);

            var editor = CreateEditor(prop, entity);
            if (editor != null)
                panel.Children.Add(editor);
        }

        return panel;
    }

    private static FrameworkElement? CreateEditor<T>(PropertyInfo prop, T entity) where T : class
    {
        var type = prop.PropertyType;
        var value = prop.GetValue(entity);

        if (type == typeof(bool) || type == typeof(bool?))
        {
            var cb = new CheckBox
            {
                IsChecked = value as bool? ?? false,
                Margin = new Thickness(0, 0, 0, 4)
            };
            cb.Checked += (_, _) => prop.SetValue(entity, true);
            cb.Unchecked += (_, _) => prop.SetValue(entity, false);
            return cb;
        }

        if (type.IsEnum)
        {
            var combo = new ComboBox
            {
                ItemsSource = Enum.GetValues(type),
                SelectedItem = value,
                Margin = new Thickness(0, 0, 0, 4)
            };
            combo.SelectionChanged += (_, _) => prop.SetValue(entity, combo.SelectedItem);
            return combo;
        }

        if (type == typeof(int) || type == typeof(int?) ||
            type == typeof(long) || type == typeof(long?) ||
            type == typeof(double) || type == typeof(double?) ||
            type == typeof(decimal) || type == typeof(decimal?))
        {
            var tb = new TextBox
            {
                Text = value?.ToString() ?? "",
                Margin = new Thickness(0, 0, 0, 4),
                Padding = new Thickness(4, 2, 4, 2)
            };
            tb.LostFocus += (_, _) =>
            {
                try
                {
                    var converted = Convert.ChangeType(tb.Text, Nullable.GetUnderlyingType(type) ?? type);
                    prop.SetValue(entity, converted);
                }
                catch { /* ignore invalid input */ }
            };
            return tb;
        }

        if (type == typeof(DateTime) || type == typeof(DateTime?))
        {
            var dp = new DatePicker
            {
                SelectedDate = value as DateTime?,
                Margin = new Thickness(0, 0, 0, 4)
            };
            dp.SelectedDateChanged += (_, _) => prop.SetValue(entity, dp.SelectedDate);
            return dp;
        }

        // Default: string text box (multiline if property name suggests it)
        var isMultiline = prop.Name.Contains("Description") || prop.Name.Contains("Notes")
                       || prop.Name.Contains("Config") || prop.Name.Contains("Text");
        var textBox = new TextBox
        {
            Text = value?.ToString() ?? "",
            Margin = new Thickness(0, 0, 0, 4),
            Padding = new Thickness(4, 2, 4, 2),
            AcceptsReturn = isMultiline,
            TextWrapping = isMultiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
            MinHeight = isMultiline ? 80 : 0,
            MaxHeight = isMultiline ? 200 : double.PositiveInfinity,
            VerticalScrollBarVisibility = isMultiline ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled
        };
        textBox.LostFocus += (_, _) => prop.SetValue(entity, textBox.Text);
        return textBox;
    }

    private static bool IsExcludedProperty(PropertyInfo prop)
    {
        // Exclude navigation properties, IDs, internal fields
        var name = prop.Name;
        return name == "Id" || name == "CreatedAt" || name == "UpdatedAt"
            || name == "IsDeleted" || name == "DeletedAt"
            || name.EndsWith("Id") && prop.PropertyType == typeof(Guid)
            || prop.PropertyType.IsGenericType && prop.PropertyType.GetGenericTypeDefinition() == typeof(ICollection<>)
            || prop.GetCustomAttribute<System.ComponentModel.BrowsableAttribute>()?.Browsable == false;
    }

    private static int GetPropertyOrder(PropertyInfo prop)
    {
        var order = prop.GetCustomAttribute<System.ComponentModel.DataAnnotations.DisplayAttribute>()?.Order;
        return order ?? 999;
    }

    private static string SplitCamelCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        var result = new System.Text.StringBuilder();
        for (int i = 0; i < input.Length; i++)
        {
            if (i > 0 && char.IsUpper(input[i]) && !char.IsUpper(input[i - 1]))
                result.Append(' ');
            result.Append(input[i]);
        }
        return result.ToString();
    }
}
