using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Central.Engine.Auth;

namespace Central.Engine.Widgets;

/// <summary>
/// Base for all panel content ViewModels. Provides:
/// - [WidgetCommand] auto-ribbon via reflection
/// - Text replacement for command names
/// - Permission gating on generated ribbon items
///
/// Based on TotalLink's WidgetViewModelBase.InitializeWidgetCommands().
/// </summary>
public abstract class WidgetViewModelBase : INotifyPropertyChanged, IWidgetEvents
{
    private bool _isLoading;

    /// <summary>Ribbon groups generated from [WidgetCommand] attributes on this ViewModel.</summary>
    public List<RibbonGroupModel> RibbonGroups { get; } = new();

    public bool IsLoading
    {
        get => _isLoading;
        set { _isLoading = value; OnPropertyChanged(); }
    }

    /// <summary>Override to provide text replacements ({Type}, {TypePlural}).</summary>
    public virtual WidgetCommandData GetWidgetCommandData() => new();

    /// <summary>
    /// Reflects [WidgetCommand] attributes on ICommand properties,
    /// builds RibbonGroupModel/RibbonItemModel collection.
    /// Called once during initialization.
    ///
    /// TotalLink pattern: InitializeWidgetCommands() in WidgetViewModelBase.cs
    /// </summary>
    public void InitializeWidgetCommands()
    {
        RibbonGroups.Clear();
        var auth = AuthContext.Instance;
        var data = GetWidgetCommandData();

        var widgetCommands = GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.IsDefined(typeof(WidgetCommandAttribute), true))
            .Select(p => new
            {
                Property = p,
                Attr = p.GetCustomAttribute<WidgetCommandAttribute>()!
            })
            .ToList();

        foreach (var wc in widgetCommands)
        {
            // Permission gate: skip commands the user can't access
            if (!string.IsNullOrEmpty(wc.Attr.CommandParameter as string))
            {
                var perm = wc.Attr.CommandParameter as string;
                if (perm != null && !auth.HasPermission(perm))
                    continue;
            }

            // Find or create ribbon group
            var group = RibbonGroups.FirstOrDefault(g => g.Name == wc.Attr.GroupName);
            if (group == null)
            {
                group = new RibbonGroupModel { Name = wc.Attr.GroupName };
                RibbonGroups.Add(group);
            }

            // Create ribbon item
            var item = new RibbonItemModel
            {
                Content = data.Apply(wc.Attr.Name),
                Description = data.Apply(wc.Attr.Description),
                Command = wc.Property.GetValue(this) as ICommand,
                CommandParameter = wc.Attr.CommandParameter
            };
            group.Items.Add(item);
        }
    }

    // ── Lifecycle events ──

    public event EventHandler? WidgetLoaded;
    public event EventHandler? WidgetClosed;

    protected void RaiseWidgetLoaded() => WidgetLoaded?.Invoke(this, EventArgs.Empty);
    protected void RaiseWidgetClosed() => WidgetClosed?.Invoke(this, EventArgs.Empty);

    // ── INotifyPropertyChanged ──

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
