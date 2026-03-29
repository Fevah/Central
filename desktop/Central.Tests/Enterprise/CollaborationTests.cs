using Central.Collaboration;

namespace Central.Tests.Enterprise;

public class CollaborationTests
{
    [Fact]
    public void Presence_JoinAndGet()
    {
        var svc = new PresenceService();
        svc.JoinEditing("acme", "Device", "42", "john", "conn1");
        var editors = svc.GetEditors("acme", "Device", "42");
        Assert.Single(editors);
        Assert.Equal("john", editors[0].Username);
    }

    [Fact]
    public void Presence_LeaveRemoves()
    {
        var svc = new PresenceService();
        svc.JoinEditing("acme", "Device", "42", "john", "conn1");
        svc.LeaveEditing("acme", "Device", "42", "conn1");
        Assert.Empty(svc.GetEditors("acme", "Device", "42"));
    }

    [Fact]
    public void Presence_DisconnectCleansAll()
    {
        var svc = new PresenceService();
        svc.JoinEditing("acme", "Device", "1", "john", "conn1");
        svc.JoinEditing("acme", "Switch", "2", "john", "conn1");
        svc.DisconnectAll("conn1");
        Assert.Equal(0, svc.ActiveCount);
    }

    [Fact]
    public void Presence_MultipleEditors()
    {
        var svc = new PresenceService();
        svc.JoinEditing("acme", "Device", "42", "john", "conn1");
        svc.JoinEditing("acme", "Device", "42", "jane", "conn2");
        Assert.Equal(2, svc.GetEditors("acme", "Device", "42").Count);
    }

    [Fact]
    public void Presence_TenantIsolation()
    {
        var svc = new PresenceService();
        svc.JoinEditing("acme", "Device", "1", "john", "conn1");
        svc.JoinEditing("other", "Device", "1", "jane", "conn2");
        Assert.Single(svc.GetEditors("acme", "Device", "1"));
        Assert.Single(svc.GetEditors("other", "Device", "1"));
    }

    [Fact]
    public void ConflictDetector_NoConflict()
    {
        var result = ConflictDetector.Detect(1, 1,
            new() { ["name"] = "A" }, new() { ["name"] = "A" });
        Assert.Null(result);
    }

    [Fact]
    public void ConflictDetector_VersionMismatch()
    {
        var result = ConflictDetector.Detect(1, 2,
            new() { ["name"] = "A" }, new() { ["name"] = "B" });
        Assert.NotNull(result);
        Assert.True(result!.HasFieldConflicts);
        Assert.Single(result.Conflicts);
    }

    [Fact]
    public void Merge_NonOverlapping_AutoMerges()
    {
        var baseVals = new Dictionary<string, object?> { ["name"] = "A", ["status"] = "Active" };
        var client = new Dictionary<string, object?> { ["name"] = "B", ["status"] = "Active" };
        var server = new Dictionary<string, object?> { ["name"] = "A", ["status"] = "Inactive" };

        var result = ConflictDetector.Merge(baseVals, client, server);
        Assert.True(result.CanAutoMerge);
        Assert.Equal("B", result.MergedValues["name"]);
        Assert.Equal("Inactive", result.MergedValues["status"]);
    }

    [Fact]
    public void Merge_Overlapping_NeedsResolution()
    {
        var baseVals = new Dictionary<string, object?> { ["name"] = "Original" };
        var client = new Dictionary<string, object?> { ["name"] = "Client Edit" };
        var server = new Dictionary<string, object?> { ["name"] = "Server Edit" };

        var result = ConflictDetector.Merge(baseVals, client, server);
        Assert.False(result.CanAutoMerge);
        Assert.Single(result.ConflictingFields);
        Assert.Equal("name", result.ConflictingFields[0].FieldName);
    }

    [Fact]
    public void Merge_BothSameValue_NoConflict()
    {
        var baseVals = new Dictionary<string, object?> { ["name"] = "Original" };
        var client = new Dictionary<string, object?> { ["name"] = "Same" };
        var server = new Dictionary<string, object?> { ["name"] = "Same" };

        var result = ConflictDetector.Merge(baseVals, client, server);
        Assert.True(result.CanAutoMerge);
    }
}
