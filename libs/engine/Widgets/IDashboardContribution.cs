#if WINDOWS
using System.Windows;

namespace Central.Engine.Widgets;

/// <summary>
/// A feature module's contribution to the platform-wide landing dashboard.
///
/// Each feature module that wants its data on the dashboard implements this
/// interface in its own assembly and calls
/// <see cref="DashboardContributionRegistry.Register"/> during module load.
/// When a tenant disables a module, its contribution deregisters and the
/// corresponding section disappears from the dashboard automatically — no
/// knowledge of absent modules in the dashboard's own XAML.
///
/// This inverts the old coupling where the dashboard hard-coded sections for
/// Devices, Networking, Projects etc. Now the dashboard is a shell that
/// renders whatever contributes.
/// </summary>
public interface IDashboardContribution
{
    /// <summary>Section header rendered above this contribution's cards.</summary>
    string SectionTitle { get; }

    /// <summary>Ordering between sections on the dashboard. Lower = higher up.</summary>
    int SortOrder { get; }

    /// <summary>
    /// Optional permission code gating visibility. Null = always visible to
    /// authenticated users. Evaluated at render time; the section is silently
    /// omitted (not shown as a blank section) when the user lacks the claim.
    /// </summary>
    string? RequiredPermission { get; }

    /// <summary>
    /// Build the cards for this section. Runs async so contributions can hit
    /// the DB / API without blocking the dashboard load. Return an empty
    /// sequence to hide the section for this render (e.g. no data yet).
    /// </summary>
    Task<IEnumerable<UIElement>> BuildCardsAsync(string dsn, CancellationToken ct = default);
}

/// <summary>
/// In-process registry of all live dashboard contributions. Feature modules
/// register at startup, the dashboard panel reads on render.
///
/// Thread-safe writes; reads via <see cref="All"/> take a stable snapshot.
/// </summary>
public static class DashboardContributionRegistry
{
    private static readonly List<IDashboardContribution> _items = new();
    private static readonly object _lock = new();

    /// <summary>Register a contribution. Idempotent per concrete type — registering the same type twice is a no-op.</summary>
    public static void Register(IDashboardContribution contribution)
    {
        if (contribution is null) throw new ArgumentNullException(nameof(contribution));
        lock (_lock)
        {
            var type = contribution.GetType();
            if (_items.Any(x => x.GetType() == type)) return;
            _items.Add(contribution);
        }
    }

    /// <summary>Remove a contribution (e.g. when a module is unloaded at runtime).</summary>
    public static void Unregister(IDashboardContribution contribution)
    {
        lock (_lock)
        {
            _items.Remove(contribution);
        }
    }

    /// <summary>Snapshot of all contributions, already sorted by <see cref="IDashboardContribution.SortOrder"/>.</summary>
    public static IReadOnlyList<IDashboardContribution> All
    {
        get
        {
            lock (_lock)
            {
                return _items.OrderBy(c => c.SortOrder).ThenBy(c => c.SectionTitle).ToList();
            }
        }
    }
}
#endif
