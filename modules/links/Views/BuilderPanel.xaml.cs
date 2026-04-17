using System.Windows.Documents;

namespace Central.Module.Links.Views;

public partial class BuilderPanel : System.Windows.Controls.UserControl
{
    public BuilderPanel()
    {
        InitializeComponent();
    }

    // ── Public API ──

    public DevExpress.Xpf.Editors.ComboBoxEdit DeviceCombo => BuilderDeviceCombo;
    public System.Windows.Controls.RichTextBox PreviewBox => BuilderConfigPreview;
    public string LineCountText { get => BuilderLineCount.Text; set => BuilderLineCount.Text = value; }
    public string StatusText { get => BuilderStatusText.Text; set => BuilderStatusText.Text = value; }

    public void SetPreviewDocument(FlowDocument doc)
    {
        BuilderConfigPreview.Document = doc;
    }

    // ── Events ──

    public event Action? DeviceChanged;
    public event Action? RegenerateRequested;
    public event Action? CopyRequested;
    public event Action? DownloadRequested;

    private void BuilderDeviceCombo_EditValueChanged(object sender,
        DevExpress.Xpf.Editors.EditValueChangedEventArgs e)
        => DeviceChanged?.Invoke();

    private void Section_Toggled(object sender, System.Windows.RoutedEventArgs e)
        => RegenerateRequested?.Invoke();

    private void Item_Toggled(object sender, System.Windows.RoutedEventArgs e)
        => RegenerateRequested?.Invoke();

    private void CopyButton_Click(object sender, System.Windows.RoutedEventArgs e)
        => CopyRequested?.Invoke();

    private void DownloadButton_Click(object sender, System.Windows.RoutedEventArgs e)
        => DownloadRequested?.Invoke();
}
