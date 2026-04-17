using Central.Core.Models;

namespace Central.Core.Shell;

/// <summary>
/// Enterprise grid-to-grid linking engine.
/// Manages link rules (DB-persisted) and auto-applies cross-panel filters
/// when selections change. Replaces hardcoded cross-panel filtering.
///
/// Architecture:
///   LinkEngine subscribes to LinkSelectionMessage via Mediator
///   → Looks up matching LinkRules for the source panel
///   → Publishes ApplyFilterMessage to each target panel
///   → Target grid handlers apply the DX filter expression
///
/// Link rules are stored in panel_customizations (setting_type='link').
/// Users configure rules via the Link Customizer dialog.
/// </summary>
public sealed class LinkEngine
{
    private static LinkEngine? _instance;
    public static LinkEngine Instance => _instance ??= new();

    private readonly List<LinkRule> _rules = new();
    private readonly Dictionary<string, Action<string, string, object?>> _gridFilterHandlers = new();
    private IDisposable? _subscription;

    /// <summary>Currently active link rules.</summary>
    public IReadOnlyList<LinkRule> Rules => _rules;

    /// <summary>Load link rules from DB and start listening for selection events.</summary>
    public void Initialize(IEnumerable<LinkRule> rules)
    {
        _rules.Clear();
        _rules.AddRange(rules.Where(r => r.FilterOnSelect));

        // Subscribe to link selection messages via the enterprise mediator
        _subscription?.Dispose();
        _subscription = Mediator.Instance.Subscribe<LinkSelectionMessage>(OnLinkSelection, "LinkEngine");
    }

    /// <summary>
    /// Register a grid's filter handler. Called once per grid during panel wiring.
    /// The handler receives (targetField, filterOperator, value) and applies it to the grid.
    /// </summary>
    public void RegisterGrid(string panelName, Action<string, string, object?> applyFilter)
    {
        _gridFilterHandlers[panelName] = applyFilter;
    }

    /// <summary>Unregister a grid (on panel close).</summary>
    public void UnregisterGrid(string panelName)
    {
        _gridFilterHandlers.Remove(panelName);
    }

    /// <summary>Add a rule at runtime (also persists via callback).</summary>
    public void AddRule(LinkRule rule)
    {
        _rules.Add(rule);
    }

    /// <summary>Remove a rule by index.</summary>
    public void RemoveRule(LinkRule rule)
    {
        _rules.Remove(rule);
    }

    /// <summary>Clear all rules.</summary>
    public void ClearRules() => _rules.Clear();

    /// <summary>
    /// Called when any panel publishes a LinkSelectionMessage.
    /// Looks up matching rules and applies filters to target grids.
    /// </summary>
    private void OnLinkSelection(LinkSelectionMessage msg)
    {
        var matchingRules = _rules
            .Where(r => r.FilterOnSelect
                && string.Equals(r.SourcePanel, msg.SourcePanel, StringComparison.OrdinalIgnoreCase)
                && string.Equals(r.SourceField, msg.Field, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var rule in matchingRules)
        {
            if (_gridFilterHandlers.TryGetValue(rule.TargetPanel, out var applyFilter))
            {
                try
                {
                    applyFilter(rule.TargetField, "=", msg.Value);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[LinkEngine] Filter error {rule.SourcePanel}→{rule.TargetPanel}: {ex.Message}");
                }
            }
        }
    }

    /// <summary>Get all registered grid names for the link customizer UI.</summary>
    public IReadOnlyList<string> GetRegisteredGrids() => _gridFilterHandlers.Keys.ToList();

    public void Dispose()
    {
        _subscription?.Dispose();
    }
}

/// <summary>
/// Message published when a link filter should be applied to a target grid.
/// Consumed by the grid's filter handler registered via LinkEngine.RegisterGrid().
/// </summary>
public record ApplyFilterMessage(string TargetPanel, string TargetField, string Operator, object? Value) : IPanelMessage;
