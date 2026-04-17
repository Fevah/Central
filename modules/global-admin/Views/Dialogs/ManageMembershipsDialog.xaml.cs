using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Central.Core.Models;
using DevExpress.Xpf.Core;

namespace Central.Module.GlobalAdmin.Views.Dialogs;

public partial class ManageMembershipsDialog : DXDialogWindow
{
    public ObservableCollection<MembershipRow> Memberships { get; } = new();
    private readonly Guid _userId;

    public Func<Guid, Guid, string, Task<int>>? OnAddMembership { get; set; }
    public Func<int, Task>? OnRemoveMembership { get; set; }
    public Func<int, string, Task>? OnChangeRole { get; set; }

    public ManageMembershipsDialog(Guid userId, string userEmail, List<MembershipRow> memberships, List<TenantOption> allTenants)
    {
        _userId = userId;
        InitializeComponent();

        UserLabel.Text = $"Memberships for {userEmail}";
        foreach (var m in memberships) Memberships.Add(m);
        MembershipsGrid.ItemsSource = Memberships;

        AddTenantCombo.ItemsSource = allTenants;
        AddRoleCombo.ItemsSource = new[] { "Admin", "Operator", "Viewer" };
        AddRoleCombo.EditValue = "Viewer";

        AddBtn.Click += async (_, _) => await AddMembershipAsync();
        RemoveBtn.Click += async (_, _) => await RemoveSelectedAsync();
        ChangeRoleBtn.Click += async (_, _) => await ChangeSelectedRoleAsync();
    }

    private async Task AddMembershipAsync()
    {
        if (AddTenantCombo.EditValue is not Guid tenantId || OnAddMembership == null) return;
        var role = AddRoleCombo.EditValue?.ToString() ?? "Viewer";

        if (Memberships.Any(m => m.TenantId == tenantId))
        {
            DXMessageBox.Show("User is already a member of this tenant.", "Duplicate", System.Windows.MessageBoxButton.OK);
            return;
        }

        var id = await OnAddMembership(_userId, tenantId, role);
        var tenant = (AddTenantCombo.ItemsSource as List<TenantOption>)?.Find(t => t.Id == tenantId);
        Memberships.Add(new MembershipRow
        {
            Id = id, UserId = _userId, TenantId = tenantId,
            TenantSlug = tenant?.Slug ?? "", TenantName = tenant?.DisplayName ?? "",
            Role = role, JoinedAt = DateTime.UtcNow
        });
    }

    private async Task RemoveSelectedAsync()
    {
        if (MembershipsGrid.CurrentItem is not MembershipRow row || OnRemoveMembership == null) return;
        await OnRemoveMembership(row.Id);
        Memberships.Remove(row);
    }

    private async Task ChangeSelectedRoleAsync()
    {
        if (MembershipsGrid.CurrentItem is not MembershipRow row || OnChangeRole == null) return;
        // Simple role picker using the same combo values
        var roles = new[] { "Admin", "Operator", "Viewer" };
        var currentIdx = Array.IndexOf(roles, row.Role);
        var nextIdx = (currentIdx + 1) % roles.Length;
        var newRole = roles[nextIdx];
        await OnChangeRole(row.Id, newRole);
        row.Role = newRole;
    }
}
