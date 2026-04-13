using Central.Core.Models;

namespace Central.Tests.Models;

public class ApiKeyRecordTests
{
    [Fact]
    public void Defaults()
    {
        var k = new ApiKeyRecord();
        Assert.Equal(0, k.Id);
        Assert.Equal("", k.Name);
        Assert.Equal("Viewer", k.Role);
        Assert.True(k.IsActive);
        Assert.Null(k.CreatedAt);
        Assert.Null(k.LastUsedAt);
        Assert.Equal(0, k.UseCount);
        Assert.Null(k.ExpiresAt);
        Assert.Null(k.RawKey);
    }

    [Fact]
    public void PropertyChanged_AllFields()
    {
        var k = new ApiKeyRecord();
        var changed = new List<string>();
        k.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        k.Id = 1;
        k.Name = "ci-deploy";
        k.Role = "Admin";
        k.IsActive = false;
        k.CreatedAt = DateTime.UtcNow;
        k.LastUsedAt = DateTime.UtcNow;
        k.UseCount = 42;
        k.ExpiresAt = DateTime.UtcNow.AddDays(30);

        Assert.Contains("Id", changed);
        Assert.Contains("Name", changed);
        Assert.Contains("Role", changed);
        Assert.Contains("IsActive", changed);
        Assert.Contains("CreatedAt", changed);
        Assert.Contains("LastUsedAt", changed);
        Assert.Contains("UseCount", changed);
        Assert.Contains("ExpiresAt", changed);
    }

    [Fact]
    public void RawKey_SetOnCreate()
    {
        var k = new ApiKeyRecord { RawKey = "ck_live_abc123xyz" };
        Assert.Equal("ck_live_abc123xyz", k.RawKey);
    }
}
