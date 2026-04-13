using System;

namespace TIG.TotalLink.Client.Editor.Interface
{
    public delegate void WidgetLoadedEventHandler(object sender, EventArgs e);
    public delegate void WidgetStartedEventHandler(object sender, EventArgs e);
    public delegate void WidgetClosedEventHandler(object sender, EventArgs e);

    public interface IWidgetEvents
    {
        event WidgetLoadedEventHandler WidgetLoaded;
        event WidgetStartedEventHandler WidgetStarted;
        event WidgetClosedEventHandler WidgetClosed;
    }
}
