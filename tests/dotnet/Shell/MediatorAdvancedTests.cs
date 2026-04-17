using Central.Engine.Shell;

namespace Central.Tests.Shell;

public class MediatorAdvancedTests
{
    [Fact]
    public void Subscribe_WithSubscriberId_TrackedInDiagnostics()
    {
        var m = new Mediator();
        m.Subscribe<SelectionChangedMessage>(_ => { }, "TestPanel");
        var diag = m.GetDiagnostics();
        Assert.True(diag.ContainsKey("subscribers_SelectionChangedMessage"));
        Assert.Equal(1, diag["subscribers_SelectionChangedMessage"]);
    }

    [Fact]
    public void Publish_TracksMessageCount()
    {
        var m = new Mediator();
        m.Subscribe<DataModifiedMessage>(_ => { });
        m.Publish(new DataModifiedMessage("src", "Entity", "Create"));
        m.Publish(new DataModifiedMessage("src", "Entity", "Update"));
        var diag = m.GetDiagnostics();
        Assert.True(diag["total_published"] >= 2);
    }

    [Fact]
    public void MultipleSubscribers_AllReceive()
    {
        var m = new Mediator();
        int count = 0;
        m.Subscribe<RefreshPanelMessage>(_ => Interlocked.Increment(ref count));
        m.Subscribe<RefreshPanelMessage>(_ => Interlocked.Increment(ref count));
        m.Subscribe<RefreshPanelMessage>(_ => Interlocked.Increment(ref count));

        m.Publish(new RefreshPanelMessage("test"));
        Assert.Equal(3, count);
    }

    [Fact]
    public void FilteredSubscription_MultipleFilters()
    {
        var m = new Mediator();
        var received = new List<string>();

        m.Subscribe<LinkSelectionMessage>(
            msg => received.Add(msg.Field),
            msg => msg.SourcePanel == "Devices");

        m.Subscribe<LinkSelectionMessage>(
            msg => received.Add($"other:{msg.Field}"),
            msg => msg.SourcePanel == "Switches");

        m.Publish(new LinkSelectionMessage("Devices", "Building", "MEP-91"));
        m.Publish(new LinkSelectionMessage("Switches", "Hostname", "SW01"));
        m.Publish(new LinkSelectionMessage("Users", "Username", "admin")); // no match

        Assert.Equal(2, received.Count);
        Assert.Contains("Building", received);
        Assert.Contains("other:Hostname", received);
    }

    [Fact]
    public void LoggingBehavior_DoesNotThrow()
    {
        var m = new Mediator();
        m.AddBehavior(new MediatorLoggingBehavior());
        m.Subscribe<SelectionChangedMessage>(_ => { });
        m.Publish(new SelectionChangedMessage("test", null));
    }
}
