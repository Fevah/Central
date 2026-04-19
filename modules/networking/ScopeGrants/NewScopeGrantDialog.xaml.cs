using System;
using System.Windows;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;

namespace Central.Module.Networking.ScopeGrants;

/// <summary>Dialog for composing a net.scope_grant tuple. The engine
/// validates the (user_id, action, entity_type, scope_type) shape on
/// create — this dialog only enforces the minimum so a typo doesn't
/// trip the roundtrip.</summary>
public partial class NewScopeGrantDialog : Window
{
    public NewScopeGrantDialog()
    {
        InitializeComponent();
        ActionCombo.ItemsSource     = ScopeGrantsAdminPanel.KnownActions;
        EntityTypeCombo.ItemsSource = ScopeGrantsAdminPanel.KnownEntityTypes;
        ScopeTypeCombo.ItemsSource  = ScopeGrantsAdminPanel.KnownScopeTypes;
        ScopeTypeCombo.EditValue    = "Global";
    }

    /// <summary>Pre-fill EntityType + ScopeType=EntityId + scope uuid
    /// so the cross-panel "Grant access to this…" quick action lands
    /// an operator on the create dialog with only user_id + action
    /// left to fill. Call before ShowDialog().</summary>
    public void PrefillForEntity(string entityType, Guid scopeEntityId, string? preferredAction = null)
    {
        if (!string.IsNullOrWhiteSpace(entityType))
            EntityTypeCombo.EditValue = entityType;
        ScopeTypeCombo.EditValue = "EntityId";
        ScopeEntityIdBox.Text = scopeEntityId.ToString();
        if (!string.IsNullOrWhiteSpace(preferredAction))
            ActionCombo.EditValue = preferredAction;
        // Cursor lands on user-id so the operator's only typing step
        // is the user they're granting access to.
        UserIdBox.Focus();
    }

    /// <summary>Pre-fill every field from an existing grant except
    /// user_id — used by the "Clone grant for another user…" row
    /// context menu when operators need to issue the same scope to
    /// multiple users. user_id stays empty + focused because that's
    /// the one field the clone changes.</summary>
    public void PrefillFromExistingGrant(string action, string entityType,
        string scopeType, Guid? scopeEntityId, string? notes)
    {
        if (!string.IsNullOrWhiteSpace(action))      ActionCombo.EditValue     = action;
        if (!string.IsNullOrWhiteSpace(entityType))  EntityTypeCombo.EditValue = entityType;
        if (!string.IsNullOrWhiteSpace(scopeType))   ScopeTypeCombo.EditValue  = scopeType;
        if (scopeEntityId is Guid g)                 ScopeEntityIdBox.Text     = g.ToString();
        if (!string.IsNullOrWhiteSpace(notes))       NotesBox.Text             = notes;
        UserIdBox.Focus();
    }

    /// <summary>Parsed user_id. Populated only after the dialog OKs.</summary>
    public int GrantUserId { get; private set; }
    public string GrantAction     { get; private set; } = "";
    public string GrantEntityType { get; private set; } = "";
    public string GrantScopeType  { get; private set; } = "Global";
    public Guid? GrantScopeEntityId { get; private set; }
    public string? GrantNotes     { get; private set; }

    private void OnCreate(object sender, RoutedEventArgs e)
    {
        // Minimal client-side validation — defers the rest to the engine.
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
            MessageBox.Show("Action is required (e.g. read / write / delete).",
                "Invalid input", MessageBoxButton.OK, MessageBoxImage.Warning);
            ActionCombo.Focus();
            return;
        }
        var entityType = (EntityTypeCombo.EditValue as string)?.Trim();
        if (string.IsNullOrWhiteSpace(entityType))
        {
            MessageBox.Show("Entity type is required (e.g. Device / Vlan / Subnet).",
                "Invalid input", MessageBoxButton.OK, MessageBoxImage.Warning);
            EntityTypeCombo.Focus();
            return;
        }
        var scopeType = (ScopeTypeCombo.EditValue as string)?.Trim();
        if (string.IsNullOrWhiteSpace(scopeType))
        {
            scopeType = "Global";
        }

        Guid? scopeEntityId = null;
        var rawScopeId = ScopeEntityIdBox.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(rawScopeId))
        {
            if (!Guid.TryParse(rawScopeId, out var g))
            {
                MessageBox.Show("Scope entity id must be a valid UUID (or empty for Global).",
                    "Invalid input", MessageBoxButton.OK, MessageBoxImage.Warning);
                ScopeEntityIdBox.Focus();
                return;
            }
            scopeEntityId = g;
        }

        // For non-Global scope, scope_entity_id is required — the
        // resolver has nothing to walk otherwise.
        if (scopeType != "Global" && scopeEntityId is null)
        {
            MessageBox.Show(
                $"Scope type '{scopeType}' requires a scope entity id (uuid). " +
                "Pick 'Global' if you want the grant to apply everywhere.",
                "Invalid input", MessageBoxButton.OK, MessageBoxImage.Warning);
            ScopeEntityIdBox.Focus();
            return;
        }

        GrantUserId        = userId;
        GrantAction        = action!;
        GrantEntityType    = entityType!;
        GrantScopeType     = scopeType!;
        GrantScopeEntityId = scopeEntityId;
        GrantNotes         = NotesBox.Text;

        DialogResult = true;
        Close();
    }
}
