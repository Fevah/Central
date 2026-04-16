using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Central.Core.Models;
using DevExpress.Xpf.Core;

namespace Central.Module.GlobalAdmin.Views.Dialogs;

public partial class TenantSetupWizard : DXDialogWindow
{
    private readonly List<PlanItem> _plans;
    private readonly List<ModuleItem> _modules;

    // Result
    public bool Provisioned { get; private set; }

    // Delegates for execution
    public Func<string, string, string?, string, Task<Guid>>? CreateTenant { get; set; }
    public Func<Guid, int, string, DateTime?, Task<int>>? CreateSubscription { get; set; }
    public Func<Guid, List<int>, DateTime?, Task>? BulkGrantModules { get; set; }
    public Func<string, Task>? ProvisionSchema { get; set; }
    public Func<string, string?, string, string, bool, Task<Guid>>? CreateUser { get; set; }
    public Func<Guid, Guid, string, Task<int>>? AddMembership { get; set; }

    public TenantSetupWizard(List<PlanItem> plans, List<ModuleItem> modules)
    {
        _plans = plans;
        _modules = modules;
        InitializeComponent();

        WizTier.ItemsSource = new[] { "free", "professional", "enterprise" };
        WizTier.EditValue = "professional";
        WizPlanList.ItemsSource = plans;
        WizModuleList.ItemsSource = modules;

        // Pre-select base modules
        foreach (var m in modules.Where(m => m.IsBase))
            WizModuleList.SelectedItems?.Add(m);

        WizBackBtn.Click += (_, _) => Navigate(-1);
        WizNextBtn.Click += (_, _) => Navigate(1);
        WizProvisionBtn.Click += async (_, _) => await ProvisionAsync();

        UpdateNavButtons();
    }

    private void Navigate(int direction)
    {
        var idx = WizardTabs.SelectedIndex + direction;
        if (idx < 0 || idx >= WizardTabs.Items.Count) return;

        // On entering summary, build the text
        if (idx == 4) BuildSummary();

        WizardTabs.SelectedIndex = idx;
        UpdateNavButtons();
    }

    private void UpdateNavButtons()
    {
        var idx = WizardTabs.SelectedIndex;
        WizBackBtn.Visibility = idx > 0 ? Visibility.Visible : Visibility.Collapsed;
        WizNextBtn.Visibility = idx < 4 ? Visibility.Visible : Visibility.Collapsed;
        WizProvisionBtn.Visibility = idx == 4 && !Provisioned ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BuildSummary()
    {
        var slug = WizSlug.EditValue?.ToString()?.Trim() ?? "";
        var name = WizName.EditValue?.ToString()?.Trim() ?? "";
        var tier = WizTier.EditValue?.ToString() ?? "free";
        var plan = _plans.Find(p => WizPlanList.SelectedItems?.Contains(p) == true);
        var moduleCount = WizModuleList.SelectedItems?.Count ?? 0;
        var email = WizAdminEmail.EditValue?.ToString()?.Trim() ?? "";

        WizSummary.Text = $"Tenant: {name} ({slug})\n" +
                          $"Tier: {tier}\n" +
                          $"Plan: {plan?.DisplayName ?? "(none)"}\n" +
                          $"Modules: {moduleCount} selected\n" +
                          $"Admin: {email}\n\n" +
                          "Click 'Provision' to create the tenant, assign the plan,\n" +
                          "grant modules, provision the database schema, and create\nthe admin user.";
    }

    private async Task ProvisionAsync()
    {
        var slug = WizSlug.EditValue?.ToString()?.Trim() ?? "";
        var name = WizName.EditValue?.ToString()?.Trim() ?? "";
        var tier = WizTier.EditValue?.ToString() ?? "free";
        var domain = WizDomain.EditValue?.ToString()?.Trim();
        var email = WizAdminEmail.EditValue?.ToString()?.Trim() ?? "";
        var adminName = WizAdminName.EditValue?.ToString()?.Trim();
        var password = WizAdminPassword.EditValue?.ToString() ?? "";

        if (string.IsNullOrWhiteSpace(slug) || string.IsNullOrWhiteSpace(name))
        {
            WizStatusMsg.Text = "Slug and display name are required";
            WizStatusMsg.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68));
            return;
        }

        WizProgress.Visibility = Visibility.Visible;
        WizProvisionBtn.IsEnabled = false;
        WizStatusMsg.Text = "Creating tenant...";

        try
        {
            // 1. Create tenant
            var tenantId = CreateTenant != null ? await CreateTenant(slug, name, domain, tier) : Guid.Empty;
            WizStatusMsg.Text = "Assigning plan...";

            // 2. Assign plan
            var plan = _plans.Find(p => WizPlanList.SelectedItems?.Contains(p) == true);
            if (plan != null && CreateSubscription != null)
                await CreateSubscription(tenantId, plan.Id, "active", null);
            WizStatusMsg.Text = "Granting modules...";

            // 3. Grant modules
            var moduleIds = WizModuleList.SelectedItems?.Cast<ModuleItem>().Select(m => m.Id).ToList() ?? new();
            if (moduleIds.Count > 0 && BulkGrantModules != null)
                await BulkGrantModules(tenantId, moduleIds, null);
            WizStatusMsg.Text = "Provisioning schema...";

            // 4. Provision schema
            if (ProvisionSchema != null && slug != "default")
                await ProvisionSchema(slug);
            WizStatusMsg.Text = "Creating admin user...";

            // 5. Create admin user + membership
            if (!string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(password) && CreateUser != null)
            {
                var salt = Central.Core.Auth.PasswordHasher.GenerateSalt();
                var hash = Central.Core.Auth.PasswordHasher.Hash(password, salt);
                var userId = await CreateUser(email, adminName, hash, salt, false);
                if (AddMembership != null)
                    await AddMembership(userId, tenantId, "Admin");
            }

            WizProgress.Visibility = Visibility.Collapsed;
            WizStatusMsg.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(34, 197, 94));
            WizStatusMsg.Text = $"Tenant '{slug}' provisioned successfully!";
            Provisioned = true;
            UpdateNavButtons();
        }
        catch (Exception ex)
        {
            WizProgress.Visibility = Visibility.Collapsed;
            WizProvisionBtn.IsEnabled = true;
            WizStatusMsg.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68));
            WizStatusMsg.Text = $"Error: {ex.Message}";
        }
    }
}
