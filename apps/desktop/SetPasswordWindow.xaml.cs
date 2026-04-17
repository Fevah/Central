using System;
using System.Windows;
using Npgsql;
using Central.Core.Auth;
using Central.Core.Models;

namespace Central.Desktop;

public partial class SetPasswordWindow : DevExpress.Xpf.Core.DXDialogWindow
{
    private readonly string _dsn;
    private readonly int _userId;
    private readonly PasswordPolicy _policy;

    public SetPasswordWindow(string dsn, AppUser user)
    {
        _dsn = dsn;
        _userId = user.Id;
        _policy = PasswordPolicy.Default;
        InitializeComponent();
        UserLabel.Text = $"Set password for: {user.DisplayName} ({user.Username})";
        PolicyLabel.Text = _policy.Description;
    }

    private async void SetPassword_Click(object sender, RoutedEventArgs e)
    {
        var pw1 = Password1.Password ?? "";
        var pw2 = Password2.Password ?? "";

        if (pw1 != pw2)
        {
            ErrorText.Text = "Passwords do not match.";
            return;
        }

        // Validate against password policy
        var validation = _policy.Validate(pw1);
        if (!validation.IsValid)
        {
            ErrorText.Text = validation.ErrorSummary;
            return;
        }

        try
        {
            var salt = PasswordHasher.GenerateSalt();
            var hash = PasswordHasher.Hash(pw1, salt);

            await using var conn = new NpgsqlConnection(_dsn);
            await conn.OpenAsync();

            // Check password history
            if (_policy.PasswordHistoryCount > 0)
            {
                var historyHashes = new List<string>();
                await using var histCmd = new NpgsqlCommand(
                    "SELECT password_hash FROM password_history WHERE user_id = @id ORDER BY changed_at DESC LIMIT @limit", conn);
                histCmd.Parameters.AddWithValue("id", _userId);
                histCmd.Parameters.AddWithValue("limit", _policy.PasswordHistoryCount);
                await using var r = await histCmd.ExecuteReaderAsync();
                while (await r.ReadAsync()) historyHashes.Add(r.GetString(0));
                await r.CloseAsync();

                // Re-validate with history
                var histValidation = _policy.Validate(pw1, historyHashes, salt);
                if (!histValidation.IsValid)
                {
                    ErrorText.Text = histValidation.ErrorSummary;
                    return;
                }
            }

            // Update password
            await using var cmd = new NpgsqlCommand(
                @"UPDATE app_users SET password_hash = @hash, salt = @salt, password_changed_at = NOW(),
                  user_type = CASE WHEN user_type = 'ActiveDirectory' THEN 'ActiveDirectory' ELSE 'Standard' END
                  WHERE id = @id", conn);
            cmd.Parameters.AddWithValue("hash", hash);
            cmd.Parameters.AddWithValue("salt", salt);
            cmd.Parameters.AddWithValue("id", _userId);
            await cmd.ExecuteNonQueryAsync();

            // Save to password history
            await using var histIns = new NpgsqlCommand(
                "INSERT INTO password_history (user_id, password_hash) VALUES (@id, @hash)", conn);
            histIns.Parameters.AddWithValue("id", _userId);
            histIns.Parameters.AddWithValue("hash", hash);
            await histIns.ExecuteNonQueryAsync();

            // Audit log
            _ = Central.Core.Services.AuditService.Instance.LogAsync("PasswordChange", "User",
                _userId.ToString(), details: "Password changed via SetPasswordWindow");

            DialogResult = true;
        }
        catch (Exception ex)
        {
            ErrorText.Text = $"Failed: {ex.Message}";
        }
    }
}
