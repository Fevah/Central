using System.Collections.Generic;
using Central.Engine.Models;
using DevExpress.Xpf.Core;

namespace Central.Module.GlobalAdmin.Views.Dialogs;

public partial class AssignPlanDialog : DXDialogWindow
{
    public int SelectedPlanId => (int)(PlanCombo.EditValue ?? 0);
    public string SelectedStatus => StatusCombo.EditValue?.ToString() ?? "active";
    public DateTime? SelectedExpiry => ExpiryDate.EditValue as DateTime?;

    public AssignPlanDialog(string tenantSlug, List<PlanItem> plans)
    {
        InitializeComponent();

        TenantLabel.EditValue = tenantSlug;
        PlanCombo.ItemsSource = plans;
        StatusCombo.ItemsSource = new[] { "active", "trial" };
        StatusCombo.EditValue = "active";

        if (plans.Count > 0)
            PlanCombo.EditValue = plans[0].Id;

        PlanCombo.EditValueChanged += (_, _) => UpdateDetails(plans);
        UpdateDetails(plans);
    }

    private void UpdateDetails(List<PlanItem> plans)
    {
        var plan = plans.Find(p => p.Id == (int)(PlanCombo.EditValue ?? 0));
        if (plan != null)
        {
            var users = plan.MaxUsers.HasValue ? plan.MaxUsers.Value.ToString() : "Unlimited";
            var devices = plan.MaxDevices.HasValue ? plan.MaxDevices.Value.ToString() : "Unlimited";
            var price = plan.PriceMonthly.HasValue ? $"${plan.PriceMonthly:F2}/mo" : "Custom";
            PlanDetails.Text = $"Tier: {plan.Tier}  |  Users: {users}  |  Devices: {devices}  |  {price}";
        }
    }
}
