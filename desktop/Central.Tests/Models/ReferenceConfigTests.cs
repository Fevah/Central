using Central.Core.Models;

namespace Central.Tests.Models;

public class ReferenceConfigTests
{
    [Fact]
    public void SampleOutput_DefaultFormat()
    {
        var rc = new ReferenceConfig { Prefix = "TKT-", PadLength = 6, NextValue = 1 };
        Assert.Equal("TKT-000001", rc.SampleOutput);
    }

    [Fact]
    public void SampleOutput_WithSuffix()
    {
        var rc = new ReferenceConfig { Prefix = "DEV-", Suffix = "-UK", PadLength = 4, NextValue = 42 };
        Assert.Equal("DEV-0042-UK", rc.SampleOutput);
    }

    [Fact]
    public void SampleOutput_LargeNumber()
    {
        var rc = new ReferenceConfig { Prefix = "REF-", PadLength = 6, NextValue = 999999 };
        Assert.Equal("REF-999999", rc.SampleOutput);
    }

    [Fact]
    public void SampleOutput_NumberExceedsPadding()
    {
        var rc = new ReferenceConfig { Prefix = "X", PadLength = 3, NextValue = 12345 };
        Assert.Equal("X12345", rc.SampleOutput);
    }

    [Fact]
    public void SampleOutput_EmptyPrefix()
    {
        var rc = new ReferenceConfig { Prefix = "", PadLength = 4, NextValue = 7 };
        Assert.Equal("0007", rc.SampleOutput);
    }

    [Fact]
    public void PropertyChanged_PrefixChange_NotifiesSampleOutput()
    {
        var rc = new ReferenceConfig();
        var changed = new List<string>();
        rc.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        rc.Prefix = "NEW-";
        Assert.Contains("SampleOutput", changed);
        Assert.Contains("Prefix", changed);
    }

    [Fact]
    public void PropertyChanged_PadLengthChange_NotifiesSampleOutput()
    {
        var rc = new ReferenceConfig();
        var changed = new List<string>();
        rc.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        rc.PadLength = 8;
        Assert.Contains("SampleOutput", changed);
    }

    [Fact]
    public void PropertyChanged_NextValueChange_NotifiesSampleOutput()
    {
        var rc = new ReferenceConfig();
        var changed = new List<string>();
        rc.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        rc.NextValue = 100;
        Assert.Contains("SampleOutput", changed);
    }

    [Fact]
    public void PropertyChanged_SuffixChange_NotifiesSampleOutput()
    {
        var rc = new ReferenceConfig();
        var changed = new List<string>();
        rc.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        rc.Suffix = "-A";
        Assert.Contains("SampleOutput", changed);
    }

    [Fact]
    public void Defaults()
    {
        var rc = new ReferenceConfig();
        Assert.Equal("", rc.EntityType);
        Assert.Equal("", rc.Prefix);
        Assert.Equal("", rc.Suffix);
        Assert.Equal(6, rc.PadLength);
        Assert.Equal(1, rc.NextValue);
        Assert.Equal("", rc.Description);
    }
}
