using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using Central.ApiClient;

namespace Central.Module.Networking.Governance;

/// <summary>
/// Generic item-add dialog. Covers every <c>(entity_type, action)</c> pair
/// the engine supports by exposing the raw JSON payloads as textareas.
/// Admin picks entity_type + action from the dropdowns, pastes / types
/// the before/after JSON, and the dialog calls
/// <see cref="NetworkingEngineClient.AddChangeSetItemAsync"/>.
///
/// <para>Per-entity-type convenience forms (e.g. pick-a-device-rename
/// it) arrive as follow-on slices. This dialog is the catch-all that
/// lets admins draft any item today without waiting on per-type UX.</para>
///
/// <para>Only the supported pairs appear in the Action dropdown when an
/// entity type is picked — validation still runs server-side, but the
/// dropdown reflects what'll actually apply cleanly.</para>
/// </summary>
public partial class AddChangeSetItemDialog : DevExpress.Xpf.Core.DXWindow
{
    private readonly string _baseUrl;
    private readonly Guid _tenantId;
    private readonly int? _actorUserId;
    private readonly Guid _setId;

    public ChangeSetItemDto? CreatedItem { get; private set; }

    // Authoritative action catalog per entity type — keep in step with
    // `dispatch_apply` in services/networking-engine/src/change_sets.rs.
    // Rather than fetching from the server (adds a round-trip + a new
    // endpoint), we mirror the static matrix here. A mismatch would show
    // up as a clean 400 from the engine, not a silent misfire.
    private static readonly Dictionary<string, string[]> SupportedActions = new()
    {
        { "Device",        new[] { "Rename", "Create", "Update", "Delete" } },
        { "Link",          new[] { "Rename" } },
        { "Server",        new[] { "Rename" } },
        { "Vlan",          new[] { "Create" } },
        { "AsnAllocation", new[] { "Create" } },
        { "MlagDomain",    new[] { "Create" } },
        { "Subnet",        new[] { "Create" } },
        { "IpAddress",     new[] { "Create" } },
    };

    public AddChangeSetItemDialog(string baseUrl, Guid tenantId, int? actorUserId,
        ChangeSetRow row)
    {
        InitializeComponent();
        _baseUrl = baseUrl;
        _tenantId = tenantId;
        _actorUserId = actorUserId;
        _setId = row.Id;

        HeaderLabel.Text = $"Add item to \u201C{row.Title}\u201D ({row.Status}, currently {row.ItemCount} item{(row.ItemCount == 1 ? "" : "s")})";

        EntityTypeCombo.ItemsSource = SupportedActions.Keys.ToArray();
        EntityTypeCombo.SelectedIndex = 0;

        Loaded += (_, _) => EntityIdBox.Focus();
    }

    private void OnEntityTypeChanged(object? sender, EventArgs e)
    {
        var et = EntityTypeCombo.SelectedItem as string;
        if (et is null || !SupportedActions.TryGetValue(et, out var actions))
        {
            ActionCombo.ItemsSource = Array.Empty<string>();
            return;
        }
        ActionCombo.ItemsSource = actions;
        ActionCombo.SelectedIndex = 0;
        UpdateHint();
    }

    private void OnActionChanged(object? sender, EventArgs e) => UpdateHint();

    /// <summary>Tight hint strings so admins don't have to chase the
    /// engine-side validation rules to figure out which fields matter.
    /// Matches the `validate_item` + `dispatch_apply` behaviour.</summary>
    private void UpdateHint()
    {
        var et = EntityTypeCombo.SelectedItem as string ?? "";
        var act = ActionCombo.SelectedItem as string ?? "";

        ActionHint.Text = (et, act) switch
        {
            (_, "Create")   => $"Create: leave Entity ID blank — apply assigns it. afterJson carries the new row's shape " +
                                (et switch
                                {
                                    "Vlan"         => "({{\"blockId\",\"displayName\",\"scopeLevel\",\"scopeEntityId\"?}}).",
                                    "AsnAllocation"=> "({{\"blockId\",\"allocatedToType\",\"allocatedToId\"}}).",
                                    "MlagDomain"   => "({{\"poolId\",\"displayName\",\"scopeLevel\",\"scopeEntityId\"?}}).",
                                    "Subnet"       => "({{\"poolId\",\"prefixLength\",\"subnetCode\",\"displayName\",\"scopeLevel\",\"scopeEntityId\"?,\"parentSubnetId\"?}}).",
                                    "IpAddress"    => "({{\"subnetId\",\"assignedToType\"?,\"assignedToId\"?}}).",
                                    "Device"       => "({{\"hostname\"}} at minimum).",
                                    _              => "(see the engine-side Create validator for required fields)."
                                }),
            ("Device", "Rename") => "Rename: Entity ID + afterJson={\"hostname\":\"NEW-NAME\"}. beforeJson optional (used by rollback to un-rename).",
            ("Link",   "Rename") => "Rename: Entity ID + afterJson={\"linkCode\":\"NEW-CODE\"}. beforeJson recommended for rollback.",
            ("Server", "Rename") => "Rename: Entity ID + afterJson={\"hostname\":\"NEW-NAME\"}. beforeJson recommended for rollback.",
            ("Device", "Update") => "Update: Entity ID + afterJson with any subset of the whitelisted device fields. Omitted keys keep current value.",
            ("Device", "Delete") => "Delete: Entity ID only. afterJson must be null/empty.",
            _ => "Select an entity type + action to see the expected payload shape."
        };
    }

    private async void OnOk(object sender, RoutedEventArgs e)
    {
        var entityType = EntityTypeCombo.SelectedItem as string;
        var action = ActionCombo.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(entityType) || string.IsNullOrWhiteSpace(action))
        {
            ShowError("Entity type and action are required.");
            return;
        }

        Guid? entityId = null;
        if (!string.IsNullOrWhiteSpace(EntityIdBox.Text))
        {
            if (!Guid.TryParse(EntityIdBox.Text.Trim(), out var parsed))
            {
                ShowError("Entity ID isn't a valid UUID.");
                return;
            }
            entityId = parsed;
        }

        int? expectedVersion = null;
        if (!string.IsNullOrWhiteSpace(ExpectedVersionBox.Text))
        {
            if (!int.TryParse(ExpectedVersionBox.Text.Trim(), out var v) || v < 1)
            {
                ShowError("Expected version must be a positive integer.");
                return;
            }
            expectedVersion = v;
        }

        object? beforeJson = ParseJsonOrNull(BeforeJsonBox.Text, out var beforeErr);
        if (beforeErr is not null) { ShowError($"Before JSON is invalid: {beforeErr}"); return; }

        object? afterJson = ParseJsonOrNull(AfterJsonBox.Text, out var afterErr);
        if (afterErr is not null) { ShowError($"After JSON is invalid: {afterErr}"); return; }

        var notes = NotesBox.Text?.Trim() is { Length: > 0 } n ? n : null;

        OkButton.IsEnabled = false;
        ClearError();
        try
        {
            using var client = new NetworkingEngineClient(_baseUrl);
            if (_actorUserId is int uid) client.SetActorUserId(uid);

            var req = new AddChangeSetItemRequest(
                entityType!, entityId, action!,
                BeforeJson: beforeJson,
                AfterJson: afterJson,
                ExpectedVersion: expectedVersion,
                Notes: notes);

            CreatedItem = await client.AddChangeSetItemAsync(_setId, _tenantId, req);
            DialogResult = true;
            Close();
        }
        catch (NetworkingEngineException ex) { ShowError($"Engine error ({ex.StatusCode}): {ex.Message}"); }
        catch (HttpRequestException ex)     { ShowError($"Network error: {ex.Message}"); }
        catch (Exception ex)                { ShowError($"Failed: {ex.Message}"); }
        finally { OkButton.IsEnabled = true; }
    }

    /// <summary>Parse a (possibly-empty) JSON textbox value into either
    /// null (empty input) or a <c>JsonElement</c> boxed as object (so
    /// System.Text.Json serialises it back to the original JSON when it
    /// hits the engine). Any parse failure yields a message in
    /// <paramref name="error"/>.</summary>
    private static object? ParseJsonOrNull(string? raw, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(raw)) return null;
        try
        {
            using var doc = JsonDocument.Parse(raw);
            // Clone the root — the using will dispose the doc, and we
            // need the JsonElement to outlive it inside the DTO.
            return doc.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return null;
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ShowError(string msg) { ErrorLabel.Text = msg; ErrorLabel.Visibility = Visibility.Visible; }
    private void ClearError()          { ErrorLabel.Text = "";  ErrorLabel.Visibility = Visibility.Collapsed; }
}
