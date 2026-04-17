using System.Collections.Generic;
using System.Threading.Tasks;
using Central.Engine.Models;
using DevExpress.Xpf.Core;

namespace Central.Module.GlobalAdmin.Views.Dialogs;

public partial class TenantDetailDialog : DXDialogWindow
{
    public TenantRecord Tenant { get; }
    public bool IsEditMode { get; }

    /// <summary>Delegate to load per-tenant sub-grid data.</summary>
    public Func<Guid, Task<(List<SubscriptionRecord> Subs, List<ModuleLicenseRecord> Licenses,
        List<TenantMemberRecord> Members, List<TenantAddressRecord> Addresses, List<TenantContactRecord> Contacts)>>?
        LoadTenantDetails { get; set; }

    public TenantDetailDialog(TenantRecord tenant, bool isEdit = false)
    {
        Tenant = tenant;
        IsEditMode = isEdit;
        DataContext = tenant;
        InitializeComponent();

        TierCombo.ItemsSource = new[] { "free", "professional", "enterprise" };
        TierCombo.EditValue = tenant.Tier;

        if (!isEdit)
        {
            SlugEdit.IsReadOnly = false;
            SlugEdit.Focus();
        }
        else
        {
            NameEdit.Focus();
        }

        Loaded += async (_, _) => await LoadSubGridsAsync();
    }

    private async Task LoadSubGridsAsync()
    {
        if (LoadTenantDetails == null || Tenant.Id == Guid.Empty) return;
        try
        {
            var (subs, licenses, members, addresses, contacts) = await LoadTenantDetails(Tenant.Id);
            SubsGrid.ItemsSource = subs;
            LicensesGrid.ItemsSource = licenses;
            MembersGrid.ItemsSource = members;
            AddressesGrid.ItemsSource = addresses;
            ContactsGrid.ItemsSource = contacts;
        }
        catch { /* sub-grids are informational — don't block the dialog */ }
    }
}
