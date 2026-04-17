using Central.Core.Models;

namespace Central.Tests.Models;

public class LocationModelTests
{
    [Fact]
    public void Country_PropertyChanged()
    {
        var c = new Country();
        string? changed = null;
        c.PropertyChanged += (_, e) => changed = e.PropertyName;
        c.Code = "GBR";
        Assert.Equal("Code", changed);
    }

    [Fact]
    public void Region_CountryName_Settable()
    {
        var r = new Region { CountryName = "United Kingdom" };
        Assert.Equal("United Kingdom", r.CountryName);
    }

    [Fact]
    public void Postcode_LatLong()
    {
        var p = new Postcode { Latitude = 51.5074m, Longitude = -0.1278m };
        Assert.Equal(51.5074m, p.Latitude);
        Assert.Equal(-0.1278m, p.Longitude);
    }

    [Fact]
    public void Postcode_PropertyChanged()
    {
        var p = new Postcode();
        bool fired = false;
        p.PropertyChanged += (_, _) => fired = true;
        p.Locality = "London";
        Assert.True(fired);
    }
}
