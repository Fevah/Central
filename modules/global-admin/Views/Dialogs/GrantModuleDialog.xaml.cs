using System.Collections.Generic;
using System.Linq;
using Central.Engine.Models;
using DevExpress.Xpf.Core;

namespace Central.Module.GlobalAdmin.Views.Dialogs;

public partial class GrantModuleDialog : DXDialogWindow
{
    private readonly List<ModuleItem> _modules;

    /// <summary>IDs of modules selected for granting.</summary>
    public List<int> SelectedModuleIds =>
        ModuleList.SelectedItems?.Cast<ModuleItem>().Select(m => m.Id).ToList() ?? new();

    public DateTime? SelectedExpiry => ExpiryDate.EditValue as DateTime?;

    public GrantModuleDialog(string tenantSlug, List<ModuleItem> availableModules)
    {
        _modules = availableModules;
        InitializeComponent();

        TenantLabel.EditValue = tenantSlug;
        ModuleList.ItemsSource = _modules;

        SelectAllBtn.Click += (_, _) =>
        {
            foreach (var m in _modules)
                if (!ModuleList.SelectedItems!.Contains(m))
                    ModuleList.SelectedItems.Add(m);
        };
        SelectNoneBtn.Click += (_, _) => ModuleList.SelectedItems?.Clear();
    }
}
