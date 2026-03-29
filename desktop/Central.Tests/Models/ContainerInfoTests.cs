using Central.Core.Models;

namespace Central.Tests.Models;

public class ContainerInfoTests
{
    [Fact]
    public void StateColor_AllStates()
    {
        Assert.Equal("#22C55E", new ContainerInfo { State = "running" }.StateColor);
        Assert.Equal("#EF4444", new ContainerInfo { State = "exited" }.StateColor);
        Assert.Equal("#F59E0B", new ContainerInfo { State = "paused" }.StateColor);
        Assert.Equal("#6B7280", new ContainerInfo { State = "created" }.StateColor);
        Assert.Equal("#6B7280", new ContainerInfo { State = "" }.StateColor);
    }

    [Fact]
    public void IsRunning()
    {
        Assert.True(new ContainerInfo { State = "running" }.IsRunning);
        Assert.False(new ContainerInfo { State = "exited" }.IsRunning);
        Assert.False(new ContainerInfo { State = "" }.IsRunning);
    }

    [Fact]
    public void PropertyChanged_Fires()
    {
        var c = new ContainerInfo();
        var props = new List<string>();
        c.PropertyChanged += (_, e) => props.Add(e.PropertyName!);
        c.Name = "postgres";
        c.State = "running";
        c.Image = "postgres:18";
        Assert.Contains("Name", props);
        Assert.Contains("State", props);
        Assert.Contains("Image", props);
    }
}
