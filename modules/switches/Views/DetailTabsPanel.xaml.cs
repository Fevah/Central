using DevExpress.Xpf.Grid;

namespace Central.Module.Switches.Views;

public partial class DetailTabsPanel : System.Windows.Controls.UserControl
{
    public DetailTabsPanel() => InitializeComponent();

    public DevExpress.Xpf.Core.DXTabControl Tabs => DetailsTabs;
    public System.Windows.Controls.Button SyncButton => SyncConfigButton;
    public System.Windows.Controls.Button CompareButton => CompareConfigButton;
    public System.Windows.Controls.Button DeleteVersionButton => DeleteConfigVersionButton;
    public System.Windows.Controls.TextBlock SyncStatus => SyncStatusText;
    public System.Windows.Controls.TextBlock BackupStatus => BackupStatusText;
    public System.Windows.Controls.TextBlock ConfigHeader => ConfigViewHeader;
    public System.Windows.Controls.ListBox VersionsList => ConfigVersionsList;
    public System.Windows.Controls.ListBox Backups => BackupsList;
    public System.Windows.Controls.ListBox AuditLog => AuditLogList;
    public System.Windows.Controls.RichTextBox ConfigBox => ConfigTextBox;
    public System.Windows.FrameworkElement SingleConfig => SingleConfigPanel;
    public GridControl Interfaces => InterfacesGrid;
    public System.Windows.Controls.TextBlock InterfaceSummary => InterfaceSummaryText;
    public System.Windows.Controls.TextBlock VerModel => VersionModel;
    public System.Windows.Controls.TextBlock VerMac => VersionMac;
    public System.Windows.Controls.TextBlock VerL2L3 => VersionL2L3;
    public System.Windows.Controls.TextBlock VerL2L3Date => VersionL2L3Date;
    public System.Windows.Controls.TextBlock VerLinux => VersionLinux;
    public System.Windows.Controls.TextBlock VerOvs => VersionOvs;
    public System.Windows.Controls.TextBlock VerCapturedAt => VersionCapturedAt;
    public System.Windows.Controls.TextBox VerRawText => VersionRawText;

    // Tab items for visibility toggling
    public DevExpress.Xpf.Core.DXTabItem ConfigTabItem => ConfigTab;
    public DevExpress.Xpf.Core.DXTabItem BackupsTabItem => BackupsTab;
    public DevExpress.Xpf.Core.DXTabItem VersionTabItem => VersionTab;
    public DevExpress.Xpf.Core.DXTabItem InterfacesTabItem => InterfacesTab;

    // ASN detail + SSH override
    public System.Windows.Controls.ListBox AsnDevices => AsnDevicesList;
    public System.Windows.Controls.TextBox SshOverrideIp => SshOverrideIpBox;
}
