using Central.Engine.Models;

namespace Central.Tests.Models;

public class LocationModelExtendedTests
{
    // ── Country extended tests ──

    [Fact]
    public void Country_Defaults()
    {
        var c = new Country();
        Assert.Equal(0, c.Id);
        Assert.Equal("", c.Code);
        Assert.Equal("", c.Name);
        Assert.Equal(0, c.SortOrder);
    }

    [Fact]
    public void Country_AllProperties_FirePropertyChanged()
    {
        var c = new Country();
        var changed = new List<string>();
        c.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        c.Id = 1;
        c.Code = "GBR";
        c.Name = "United Kingdom";
        c.SortOrder = 5;

        Assert.Equal(4, changed.Count);
        Assert.Contains("Id", changed);
        Assert.Contains("Code", changed);
        Assert.Contains("Name", changed);
        Assert.Contains("SortOrder", changed);
    }

    // ── Region extended tests ──

    [Fact]
    public void Region_Defaults()
    {
        var r = new Region();
        Assert.Equal(0, r.Id);
        Assert.Equal(0, r.CountryId);
        Assert.Equal("", r.Code);
        Assert.Equal("", r.Name);
        Assert.Equal(0, r.SortOrder);
        Assert.Equal("", r.CountryName);
    }

    [Fact]
    public void Region_AllProperties_FirePropertyChanged()
    {
        var r = new Region();
        var changed = new List<string>();
        r.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        r.Id = 1;
        r.CountryId = 10;
        r.Code = "ENG";
        r.Name = "England";
        r.SortOrder = 1;
        r.CountryName = "United Kingdom";

        Assert.Equal(6, changed.Count);
        Assert.Contains("Id", changed);
        Assert.Contains("CountryId", changed);
        Assert.Contains("Code", changed);
        Assert.Contains("Name", changed);
        Assert.Contains("SortOrder", changed);
        Assert.Contains("CountryName", changed);
    }

    // ── Postcode extended tests ──

    [Fact]
    public void Postcode_Defaults()
    {
        var p = new Postcode();
        Assert.Equal(0, p.Id);
        Assert.Equal(0, p.RegionId);
        Assert.Equal("", p.Code);
        Assert.Equal("", p.Locality);
        Assert.Null(p.Latitude);
        Assert.Null(p.Longitude);
        Assert.Equal("", p.RegionName);
    }

    [Fact]
    public void Postcode_AllProperties_FirePropertyChanged()
    {
        var p = new Postcode();
        var changed = new List<string>();
        p.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        p.Id = 1;
        p.RegionId = 5;
        p.Code = "OX4 2AA";
        p.Locality = "Oxford";
        p.Latitude = 51.7520m;
        p.Longitude = -1.2577m;
        p.RegionName = "Oxfordshire";

        Assert.Equal(7, changed.Count);
        Assert.Contains("Id", changed);
        Assert.Contains("RegionId", changed);
        Assert.Contains("Code", changed);
        Assert.Contains("Locality", changed);
        Assert.Contains("Latitude", changed);
        Assert.Contains("Longitude", changed);
        Assert.Contains("RegionName", changed);
    }

    [Fact]
    public void Postcode_Latitude_NullToValue()
    {
        var p = new Postcode();
        Assert.Null(p.Latitude);
        p.Latitude = 0.0m;
        Assert.Equal(0.0m, p.Latitude);
    }

    [Fact]
    public void Postcode_Longitude_NegativeValue()
    {
        var p = new Postcode { Longitude = -180.0m };
        Assert.Equal(-180.0m, p.Longitude);
    }

    [Fact]
    public void Postcode_LatLon_Precision()
    {
        var p = new Postcode { Latitude = 51.5073509m, Longitude = -0.1277583m };
        Assert.Equal(51.5073509m, p.Latitude);
        Assert.Equal(-0.1277583m, p.Longitude);
    }
}
