using Central.Engine.Shell;

namespace Central.Tests.Shell;

public class MediatorTests
{
    private readonly Mediator _mediator = new();

    [Fact]
    public void Publish_NoSubscribers_DoesNotThrow()
    {
        _mediator.Publish(new SelectionChangedMessage("test", null));
    }

    [Fact]
    public void Subscribe_ReceivesPublishedMessage()
    {
        string? received = null;
        _mediator.Subscribe<SelectionChangedMessage>(msg => received = msg.SourcePanel);

        _mediator.Publish(new SelectionChangedMessage("devices", null));

        Assert.Equal("devices", received);
    }

    [Fact]
    public void Subscribe_MultipleHandlers_AllReceive()
    {
        int count = 0;
        _mediator.Subscribe<DataModifiedMessage>(_ => count++);
        _mediator.Subscribe<DataModifiedMessage>(_ => count++);
        _mediator.Subscribe<DataModifiedMessage>(_ => count++);

        _mediator.Publish(new DataModifiedMessage("src", "Device", "Insert"));

        Assert.Equal(3, count);
    }

    [Fact]
    public void Unsubscribe_StopsReceiving()
    {
        int count = 0;
        var sub = _mediator.Subscribe<SelectionChangedMessage>(_ => count++);

        _mediator.Publish(new SelectionChangedMessage("a", null));
        Assert.Equal(1, count);

        sub.Dispose();
        _mediator.Publish(new SelectionChangedMessage("b", null));
        Assert.Equal(1, count); // no increment
    }

    [Fact]
    public void FilteredSubscription_OnlyReceivesMatching()
    {
        string? received = null;
        _mediator.Subscribe<LinkSelectionMessage>(
            msg => received = msg.Field,
            msg => msg.SourcePanel == "devices" // only devices panel
        );

        _mediator.Publish(new LinkSelectionMessage("switches", "hostname", "SW01"));
        Assert.Null(received); // filtered out

        _mediator.Publish(new LinkSelectionMessage("devices", "building", "MEP-91"));
        Assert.Equal("building", received);
    }

    [Fact]
    public void GetSubscriptionCount_ReturnsCorrectCount()
    {
        _mediator.Subscribe<SelectionChangedMessage>(_ => { });
        _mediator.Subscribe<SelectionChangedMessage>(_ => { });

        Assert.Equal(2, _mediator.GetSubscriptionCount<SelectionChangedMessage>());
        Assert.Equal(0, _mediator.GetSubscriptionCount<DataModifiedMessage>());
    }

    [Fact]
    public void GetDiagnostics_TracksCounts()
    {
        _mediator.Subscribe<SelectionChangedMessage>(_ => { });
        _mediator.Publish(new SelectionChangedMessage("test", null));
        _mediator.Publish(new SelectionChangedMessage("test2", null));

        var diag = _mediator.GetDiagnostics();
        Assert.True(diag["total_published"] >= 2);
        Assert.True(diag["total_delivered"] >= 2);
    }

    [Fact]
    public void Handler_Exception_DoesNotBreakOtherHandlers()
    {
        int count = 0;
        _mediator.Subscribe<SelectionChangedMessage>(_ => throw new Exception("boom"));
        _mediator.Subscribe<SelectionChangedMessage>(_ => count++);

        _mediator.Publish(new SelectionChangedMessage("test", null));
        Assert.Equal(1, count); // second handler still ran
    }

    [Fact]
    public async Task PublishAsync_Works()
    {
        string? received = null;
        _mediator.Subscribe<SelectionChangedMessage>(async msg =>
        {
            await Task.Delay(1);
            received = msg.SourcePanel;
        });

        await _mediator.PublishAsync(new SelectionChangedMessage("async-test", null));
        Assert.Equal("async-test", received);
    }

    [Fact]
    public void PerformanceBehavior_TracksMetrics()
    {
        var perf = new MediatorPerformanceBehavior();
        _mediator.AddBehavior(perf);
        _mediator.Subscribe<SelectionChangedMessage>(_ => { });

        _mediator.Publish(new SelectionChangedMessage("perf", null));
        _mediator.Publish(new SelectionChangedMessage("perf2", null));

        var metrics = perf.GetMetrics();
        Assert.Contains("SelectionChangedMessage", metrics.Keys);
        Assert.Equal(2, metrics["SelectionChangedMessage"].Count);
    }

    [Fact]
    public void DifferentMessageTypes_RouteToCorrectHandlers()
    {
        string? selectionResult = null;
        string? dataResult = null;

        _mediator.Subscribe<SelectionChangedMessage>(msg => selectionResult = msg.SourcePanel);
        _mediator.Subscribe<DataModifiedMessage>(msg => dataResult = msg.EntityType);

        _mediator.Publish(new DataModifiedMessage("admin", "User", "Insert"));

        Assert.Null(selectionResult); // selection handler not called
        Assert.Equal("User", dataResult);
    }
}
