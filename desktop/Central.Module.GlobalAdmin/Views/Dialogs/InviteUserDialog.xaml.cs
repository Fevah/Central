using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Central.Core.Models;
using DevExpress.Xpf.Core;

namespace Central.Module.GlobalAdmin.Views.Dialogs;

public partial class InviteUserDialog : DXDialogWindow
{
    public string Email => EmailEdit.EditValue?.ToString()?.Trim() ?? "";
    public string DisplayName => NameEdit.EditValue?.ToString()?.Trim() ?? "";
    public string Password => PasswordEdit.EditValue?.ToString() ?? "";
    public bool IsGlobalAdmin => AdminCheck.IsChecked == true;
    public List<Guid> SelectedTenantIds =>
        TenantList.SelectedItems?.Cast<TenantOption>().Select(t => t.Id).ToList() ?? new();

    /// <summary>Set to true after validation passes — check before using result.</summary>
    public bool IsValid { get; private set; }

    public InviteUserDialog(List<TenantOption> tenants)
    {
        InitializeComponent();
        TenantList.ItemsSource = tenants;
        EmailEdit.Focus();
    }

    /// <summary>Validate fields. Call after ShowDialogWindow when result is OK.</summary>
    public bool Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(Email) || !Regex.IsMatch(Email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            errors.Add("Valid email is required");
        if (string.IsNullOrWhiteSpace(Password) || Password.Length < 6)
            errors.Add("Password must be at least 6 characters");
        if (Password != ConfirmEdit.EditValue?.ToString())
            errors.Add("Passwords do not match");

        if (errors.Count > 0)
        {
            ValidationMsg.Text = string.Join("\n", errors);
            return false;
        }
        IsValid = true;
        return true;
    }
}
