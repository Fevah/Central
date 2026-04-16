using DevExpress.Xpf.Core;

namespace Central.Module.GlobalAdmin.Views.Dialogs;

public partial class ResetPasswordDialog : DXDialogWindow
{
    public string NewPassword => PasswordEdit.EditValue?.ToString() ?? "";
    public bool ForceEmailVerification => ForceVerifyCheck.IsChecked == true;
    public bool IsValid { get; private set; }

    public ResetPasswordDialog(string userEmail)
    {
        InitializeComponent();
        UserLabel.EditValue = userEmail;
        PasswordEdit.Focus();
    }

    public bool Validate()
    {
        if (string.IsNullOrWhiteSpace(NewPassword) || NewPassword.Length < 6)
        {
            ValidationMsg.Text = "Password must be at least 6 characters";
            return false;
        }
        if (NewPassword != ConfirmEdit.EditValue?.ToString())
        {
            ValidationMsg.Text = "Passwords do not match";
            return false;
        }
        IsValid = true;
        return true;
    }
}
