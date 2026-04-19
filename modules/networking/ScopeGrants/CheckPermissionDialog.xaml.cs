using System;
using System.Windows;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;

namespace Central.Module.Networking.ScopeGrants;

public partial class CheckPermissionDialog : Window
{
    public CheckPermissionDialog()
    {
        InitializeComponent();
        ActionCombo.ItemsSource     = ScopeGrantsAdminPanel.KnownActions;
        EntityTypeCombo.ItemsSource = ScopeGrantsAdminPanel.KnownEntityTypes;
    }

    public int   CheckUserId     { get; private set; }
    public string CheckAction    { get; private set; } = "";
    public string CheckEntityType { get; private set; } = "";
    public Guid? CheckEntityId   { get; private set; }

    private void OnCheck(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(UserIdBox.Text?.Trim(), out var userId) || userId <= 0)
        {
            MessageBox.Show("User id must be a positive integer.",
                "Invalid input", MessageBoxButton.OK, MessageBoxImage.Warning);
            UserIdBox.Focus();
            return;
        }
        var action = (ActionCombo.EditValue as string)?.Trim();
        if (string.IsNullOrWhiteSpace(action))
        {
            MessageBox.Show("Action is required.",
                "Invalid input", MessageBoxButton.OK, MessageBoxImage.Warning);
            ActionCombo.Focus();
            return;
        }
        var entityType = (EntityTypeCombo.EditValue as string)?.Trim();
        if (string.IsNullOrWhiteSpace(entityType))
        {
            MessageBox.Show("Entity type is required.",
                "Invalid input", MessageBoxButton.OK, MessageBoxImage.Warning);
            EntityTypeCombo.Focus();
            return;
        }

        Guid? entityId = null;
        var raw = EntityIdBox.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(raw))
        {
            if (!Guid.TryParse(raw, out var g))
            {
                MessageBox.Show("Entity id must be a valid UUID (or empty).",
                    "Invalid input", MessageBoxButton.OK, MessageBoxImage.Warning);
                EntityIdBox.Focus();
                return;
            }
            entityId = g;
        }

        CheckUserId     = userId;
        CheckAction     = action!;
        CheckEntityType = entityType!;
        CheckEntityId   = entityId;
        DialogResult    = true;
        Close();
    }
}
