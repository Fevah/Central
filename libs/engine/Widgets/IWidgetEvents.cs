namespace Central.Engine.Widgets;

/// <summary>
/// Lifecycle events for panel widgets.
/// </summary>
public interface IWidgetEvents
{
    event EventHandler? WidgetLoaded;
    event EventHandler? WidgetClosed;
}
