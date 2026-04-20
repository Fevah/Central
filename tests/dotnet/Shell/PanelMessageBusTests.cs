using Central.Engine.Shell;

namespace Central.Tests.Shell;

/// <summary>
/// <see cref="PanelMessageBus"/> is a static singleton — any test
/// that subscribes on it is visible to every other test xUnit runs
/// in parallel. The <c>AllModules*AuditTests</c> classes invoke
/// ribbon OnClick handlers that publish messages on the same bus;
/// without a shared <c>[Collection]</c> those publishes race into
/// the subscribers below and the <c>Assert.Equal()</c> on captured
/// content fails non-deterministically. Every bus-touching test
/// class is tagged <c>[Collection("RibbonAudit")]</c> so xUnit
/// serialises them.
/// </summary>
[Collection("RibbonAudit")]
public class PanelMessageBusTests
{
    [Fact]
    public void Publish_Subscribe_ReceivesMessage()
    {
        NavigateToPanelMessage? received = null;
        using var sub = PanelMessageBus.Subscribe<NavigateToPanelMessage>(msg => received = msg);

        PanelMessageBus.Publish(new NavigateToPanelMessage("devices", "switch1"));

        Assert.NotNull(received);
        Assert.Equal("devices", received!.TargetPanel);
        Assert.Equal("switch1", received.SelectItem);
    }

    [Fact]
    public void Publish_NoSubscriber_NoError()
    {
        // Should not throw
        PanelMessageBus.Publish(new RefreshPanelMessage("nonexistent"));
    }

    [Fact]
    public void Unsubscribe_StopsReceiving()
    {
        int count = 0;
        var sub = PanelMessageBus.Subscribe<RefreshPanelMessage>(_ => count++);

        PanelMessageBus.Publish(new RefreshPanelMessage("test"));
        Assert.Equal(1, count);

        sub.Dispose();
        PanelMessageBus.Publish(new RefreshPanelMessage("test"));
        Assert.Equal(1, count); // no increment
    }

    [Fact]
    public void LinkSelectionMessage_CarriesFieldAndValue()
    {
        LinkSelectionMessage? received = null;
        using var sub = PanelMessageBus.Subscribe<LinkSelectionMessage>(msg => received = msg);

        PanelMessageBus.Publish(new LinkSelectionMessage("sdtechnicians", "TechnicianName", "John Smith"));

        Assert.NotNull(received);
        Assert.Equal("sdtechnicians", received!.SourcePanel);
        Assert.Equal("TechnicianName", received.Field);
        Assert.Equal("John Smith", received.Value);
    }

    [Fact]
    public void SelectionChangedMessage_CarriesItem()
    {
        SelectionChangedMessage? received = null;
        using var sub = PanelMessageBus.Subscribe<SelectionChangedMessage>(msg => received = msg);

        PanelMessageBus.Publish(new SelectionChangedMessage("devices", "item1"));

        Assert.NotNull(received);
        Assert.Equal("devices", received!.SourcePanel);
        Assert.Equal("item1", received.SelectedItem);
    }

    [Fact]
    public void MultipleSubscribers_AllReceive()
    {
        int count1 = 0, count2 = 0;
        using var sub1 = PanelMessageBus.Subscribe<DataModifiedMessage>(_ => count1++);
        using var sub2 = PanelMessageBus.Subscribe<DataModifiedMessage>(_ => count2++);

        PanelMessageBus.Publish(new DataModifiedMessage("test", "entity", "update"));

        Assert.Equal(1, count1);
        Assert.Equal(1, count2);
    }

    [Fact]
    public void DifferentMessageTypes_DoNotCross()
    {
        int navCount = 0, refreshCount = 0;
        using var sub1 = PanelMessageBus.Subscribe<NavigateToPanelMessage>(_ => navCount++);
        using var sub2 = PanelMessageBus.Subscribe<RefreshPanelMessage>(_ => refreshCount++);

        PanelMessageBus.Publish(new NavigateToPanelMessage("test"));

        Assert.Equal(1, navCount);
        Assert.Equal(0, refreshCount);
    }
}
