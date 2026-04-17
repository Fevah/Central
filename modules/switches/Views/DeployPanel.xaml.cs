namespace Central.Module.Switches.Views;

public partial class DeployPanel : System.Windows.Controls.UserControl
{
    public DeployPanel()
    {
        InitializeComponent();
    }

    // ── Public API for host ──

    public string HeaderText { get => DeployHeaderText.Text; set => DeployHeaderText.Text = value; }
    public string StatusText { get => DeployStatusText.Text; set => DeployStatusText.Text = value; }
    public string ConfigA { get => DeployConfigA.Text; set => DeployConfigA.Text = value; }
    public string ConfigB { get => DeployConfigB.Text; set => DeployConfigB.Text = value; }
    public string LogText { get => DeployLogText.Text; set => DeployLogText.Text = value; }
    public string TabAHeader { set => DeployTabA.Header = value; }
    public string TabBHeader { set => DeployTabB.Header = value; }
    public bool ConfirmEnabled { get => DeployConfirmButton.IsEnabled; set => DeployConfirmButton.IsEnabled = value; }

    public void AppendLog(string line) => DeployLogText.Text += line + "\n";
    public void SelectLogTab() => DeployTabs.SelectedItem = DeployTabLog;

    // ── Events ──

    public event Func<Task>? ConfirmDeployClicked;
    public event Action? CancelClicked;

    private async void DeployConfirmButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (ConfirmDeployClicked != null)
            await ConfirmDeployClicked.Invoke();
    }

    private void DeployCancelButton_Click(object sender, System.Windows.RoutedEventArgs e)
        => CancelClicked?.Invoke();
}
