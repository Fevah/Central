using System.Windows;
using System.Windows.Media;
using Binding = System.Windows.Data.Binding;
using Orientation = System.Windows.Controls.Orientation;
using StackPanel = System.Windows.Controls.StackPanel;
using Image = System.Windows.Controls.Image;
using TextBlock = System.Windows.Controls.TextBlock;

namespace Central.Desktop.Services;

/// <summary>
/// Composite caption model + CaptionTemplate factory for rendering an icon
/// next to a <see cref="DevExpress.Xpf.Ribbon.RibbonPage"/>'s caption text.
/// DX doesn't expose a Glyph property on RibbonPage, but Caption is typed as
/// <see cref="object"/> and CaptionTemplate is a <see cref="DataTemplate"/>.
/// We wrap the page's original caption text inside <see cref="RibbonPageCaption"/>
/// (Icon + Text) and bind the template with normal <c>{Binding Icon}</c> /
/// <c>{Binding Text}</c> bindings — no RelativeSource gymnastics required.
///
/// Usage:
///   var cap = PageHeaderIcon.Wrap(page, imageSource);
///   page.Caption = cap;
///   page.CaptionTemplate = PageHeaderIcon.BuildCaptionTemplate();
/// </summary>
public static class PageHeaderIcon
{
    /// <summary>
    /// Wrap the page's current caption text into a <see cref="RibbonPageCaption"/>
    /// with the supplied icon. Idempotent — if the Caption is already a wrapper,
    /// only the icon is updated so the original Text is preserved.
    /// </summary>
    public static RibbonPageCaption Wrap(DevExpress.Xpf.Ribbon.RibbonPage page, ImageSource? icon)
    {
        if (page.Caption is RibbonPageCaption existing)
        {
            existing.Icon = icon;
            return existing;
        }
        return new RibbonPageCaption { Icon = icon, Text = page.Caption?.ToString() ?? "" };
    }

    /// <summary>
    /// Build a DataTemplate that renders [Image 16×16][TextBlock] bound to
    /// <see cref="RibbonPageCaption.Icon"/> / <see cref="RibbonPageCaption.Text"/>.
    /// The Image collapses when Icon is null so plain-text pages render normally.
    /// </summary>
    public static DataTemplate BuildCaptionTemplate()
    {
        var stack = new FrameworkElementFactory(typeof(StackPanel));
        stack.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);

        var img = new FrameworkElementFactory(typeof(Image));
        img.SetValue(FrameworkElement.WidthProperty, 16.0);
        img.SetValue(FrameworkElement.HeightProperty, 16.0);
        img.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 4, 0));
        img.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        img.SetBinding(Image.SourceProperty, new Binding(nameof(RibbonPageCaption.Icon)));
        img.SetBinding(UIElement.VisibilityProperty, new Binding(nameof(RibbonPageCaption.Icon))
        {
            Converter = new NullToCollapsedConverter()
        });

        var text = new FrameworkElementFactory(typeof(TextBlock));
        text.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        text.SetBinding(TextBlock.TextProperty, new Binding(nameof(RibbonPageCaption.Text)));

        stack.AppendChild(img);
        stack.AppendChild(text);

        return new DataTemplate { VisualTree = stack };
    }

    private sealed class NullToCollapsedConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object? value, System.Type targetType, object? parameter, System.Globalization.CultureInfo culture)
            => value == null ? Visibility.Collapsed : Visibility.Visible;
        public object ConvertBack(object? value, System.Type targetType, object? parameter, System.Globalization.CultureInfo culture)
            => Binding.DoNothing;
    }
}

/// <summary>
/// Composite caption for a ribbon page — Image + Text. Used as
/// <see cref="DevExpress.Xpf.Ribbon.RibbonPage.Caption"/> when a page has an
/// icon so the caption template can render both. Overrides <c>ToString</c> so
/// legacy code reading <c>page.Caption?.ToString()</c> (e.g. lookup maps
/// keyed on caption) still sees the original text.
/// </summary>
public sealed class RibbonPageCaption : System.ComponentModel.INotifyPropertyChanged
{
    private ImageSource? _icon;
    private string _text = "";

    public ImageSource? Icon
    {
        get => _icon;
        set { if (!Equals(_icon, value)) { _icon = value; OnChanged(nameof(Icon)); } }
    }

    public string Text
    {
        get => _text;
        set { if (_text != value) { _text = value; OnChanged(nameof(Text)); } }
    }

    public override string ToString() => Text;

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged(string name)
        => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
}
