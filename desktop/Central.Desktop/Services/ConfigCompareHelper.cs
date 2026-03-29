using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Central.Core.Services;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using RichTextBox = System.Windows.Controls.RichTextBox;

namespace Central.Desktop.Services;

/// <summary>
/// Builds the side-by-side config compare UI.
/// Extracted from MainWindow.xaml.cs ShowConfigCompare + TryWireSyncedScrolling.
/// </summary>
public static class ConfigCompareHelper
{
    private static readonly SolidColorBrush DiffBg = new(Color.FromArgb(0x33, 0xEF, 0x44, 0x44));
    private static readonly SolidColorBrush DiffFg = new(Color.FromRgb(0xFF, 0x8A, 0x80));
    private static readonly SolidColorBrush DiffLineNumFg = new(Color.FromRgb(0xEF, 0x53, 0x50));
    private static readonly SolidColorBrush NormalFg = new(Color.FromRgb(0xD4, 0xD4, 0xD4));
    private static readonly SolidColorBrush LineNumFg = new(Color.FromRgb(0x55, 0x55, 0x55));

    public static FlowDocument BuildNumberedConfigDoc(string[] lines, bool[]? changed = null)
    {
        var doc = new FlowDocument { FontFamily = new FontFamily("Consolas"), FontSize = 11, PageWidth = 6000 };
        var gutterWidth = lines.Length.ToString().Length;
        for (int i = 0; i < lines.Length; i++)
        {
            var lineNum = (i + 1).ToString().PadLeft(gutterWidth);
            var para = new Paragraph { Margin = new Thickness(0) };
            bool isDiff = changed != null && i < changed.Length && changed[i];
            para.Inlines.Add(new Run($"{lineNum}  ") { Foreground = isDiff ? DiffLineNumFg : LineNumFg });
            para.Inlines.Add(new Run(lines[i]) { Foreground = isDiff ? DiffFg : NormalFg });
            if (isDiff) para.Background = DiffBg;
            doc.Blocks.Add(para);
        }
        return doc;
    }

    public static void BuildCompareView(Grid container,
        string olderText, string newerText,
        int olderVer, int newerVer, string olderDate, string newerDate)
    {
        container.Children.Clear();
        container.RowDefinitions.Clear();
        container.ColumnDefinitions.Clear();

        container.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        container.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        container.ColumnDefinitions.Add(new ColumnDefinition());
        container.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
        container.ColumnDefinitions.Add(new ColumnDefinition());

        var olderLines = olderText.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
        var newerLines = newerText.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();

        ConfigDiffService.BuildAlignedDiff(olderLines, newerLines,
            out var leftLines, out var leftChanged, out var rightLines, out var rightChanged);

        var leftHeader = new TextBlock
        {
            Text = $"v{olderVer}  ·  {olderDate}  ·  {leftLines.Length} lines",
            FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(0x90, 0xCA, 0xF9)),
            Margin = new Thickness(8, 4, 8, 4)
        };
        Grid.SetRow(leftHeader, 0); Grid.SetColumn(leftHeader, 0);

        var rightHeader = new TextBlock
        {
            Text = $"v{newerVer}  ·  {newerDate}  ·  {rightLines.Length} lines",
            FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(0x81, 0xC7, 0x84)),
            Margin = new Thickness(8, 4, 8, 4)
        };
        Grid.SetRow(rightHeader, 0); Grid.SetColumn(rightHeader, 2);

        var leftRtb = new RichTextBox
        {
            IsReadOnly = true, Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)),
            BorderThickness = new Thickness(0), VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        leftRtb.Document = BuildNumberedConfigDoc(leftLines, leftChanged);
        Grid.SetRow(leftRtb, 1); Grid.SetColumn(leftRtb, 0);

        var rightRtb = new RichTextBox
        {
            IsReadOnly = true, Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)),
            BorderThickness = new Thickness(0), VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        rightRtb.Document = BuildNumberedConfigDoc(rightLines, rightChanged);
        Grid.SetRow(rightRtb, 1); Grid.SetColumn(rightRtb, 2);

        var splitter = new GridSplitter
        {
            Width = 4, HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33))
        };
        Grid.SetRow(splitter, 0); Grid.SetRowSpan(splitter, 2); Grid.SetColumn(splitter, 1);

        container.Children.Add(leftHeader);
        container.Children.Add(rightHeader);
        container.Children.Add(leftRtb);
        container.Children.Add(splitter);
        container.Children.Add(rightRtb);

        // Synced scrolling
        leftRtb.Loaded += (_, _) => WireSyncedScrolling(leftRtb, rightRtb);
        rightRtb.Loaded += (_, _) => WireSyncedScrolling(leftRtb, rightRtb);
    }

    private static void WireSyncedScrolling(System.Windows.Controls.RichTextBox left, System.Windows.Controls.RichTextBox right)
    {
        var leftSv = GetScrollViewer(left);
        var rightSv = GetScrollViewer(right);
        if (leftSv == null || rightSv == null) return;

        bool syncing = false;
        leftSv.ScrollChanged += (_, e) =>
        {
            if (syncing) return;
            syncing = true;
            rightSv.ScrollToVerticalOffset(leftSv.VerticalOffset);
            rightSv.ScrollToHorizontalOffset(leftSv.HorizontalOffset);
            syncing = false;
        };
        rightSv.ScrollChanged += (_, e) =>
        {
            if (syncing) return;
            syncing = true;
            leftSv.ScrollToVerticalOffset(rightSv.VerticalOffset);
            leftSv.ScrollToHorizontalOffset(rightSv.HorizontalOffset);
            syncing = false;
        };
    }

    private static ScrollViewer? GetScrollViewer(DependencyObject o)
    {
        if (o is ScrollViewer sv) return sv;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(o); i++)
        {
            var child = GetScrollViewer(VisualTreeHelper.GetChild(o, i));
            if (child != null) return child;
        }
        return null;
    }
}
