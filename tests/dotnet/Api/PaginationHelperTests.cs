using Central.Api.Endpoints;

namespace Central.Tests.Api;

/// <summary>
/// Tests for pagination, filtering, and search helpers.
/// </summary>
public class PaginationHelperTests
{
    private static readonly HashSet<string> Columns = new(StringComparer.OrdinalIgnoreCase)
    {
        "building", "status", "hostname", "site"
    };

    // ── PaginatedQuery ──

    [Fact]
    public void EffectiveLimit_Default_Returns100()
    {
        var q = new PaginatedQuery();
        Assert.Equal(100, q.EffectiveLimit);
    }

    [Fact]
    public void EffectiveLimit_ExceedsMax_ClampedTo1000()
    {
        var q = new PaginatedQuery(Limit: 5000);
        Assert.Equal(1000, q.EffectiveLimit);
    }

    [Fact]
    public void EffectiveLimit_Zero_ClampedTo1()
    {
        var q = new PaginatedQuery(Limit: 0);
        Assert.Equal(1, q.EffectiveLimit);
    }

    [Fact]
    public void EffectiveOffset_Negative_ClampedToZero()
    {
        var q = new PaginatedQuery(Offset: -10);
        Assert.Equal(0, q.EffectiveOffset);
    }

    [Fact]
    public void EffectiveOrder_Desc_ReturnsDesc()
    {
        var q = new PaginatedQuery(Order: "desc");
        Assert.Equal("DESC", q.EffectiveOrder);
    }

    [Fact]
    public void EffectiveOrder_Default_ReturnsAsc()
    {
        var q = new PaginatedQuery();
        Assert.Equal("ASC", q.EffectiveOrder);
    }

    // ── Filter Clause ──

    [Fact]
    public void BuildFilterClause_ValidFilter_BuildsWhereClause()
    {
        var (where, parms) = PaginationHelpers.BuildFilterClause("building:MEP-91,status:Active", Columns);
        Assert.Contains("building = @f0", where);
        Assert.Contains("status = @f1", where);
        Assert.Equal(2, parms.Count);
        Assert.Equal("MEP-91", parms[0].Value);
    }

    [Fact]
    public void BuildFilterClause_WildcardFilter_UsesILIKE()
    {
        var (where, parms) = PaginationHelpers.BuildFilterClause("building:MEP*", Columns);
        Assert.Contains("ILIKE", where);
        Assert.Equal("MEP%", parms[0].Value);
    }

    [Fact]
    public void BuildFilterClause_UnknownColumn_Skipped()
    {
        var (where, parms) = PaginationHelpers.BuildFilterClause("password_hash:evil", Columns);
        Assert.Empty(where);
        Assert.Empty(parms);
    }

    [Fact]
    public void BuildFilterClause_Empty_ReturnsEmpty()
    {
        var (where, parms) = PaginationHelpers.BuildFilterClause("", Columns);
        Assert.Empty(where);
        Assert.Empty(parms);
    }

    [Fact]
    public void BuildFilterClause_Null_ReturnsEmpty()
    {
        var (where, parms) = PaginationHelpers.BuildFilterClause(null, Columns);
        Assert.Empty(where);
        Assert.Empty(parms);
    }

    // ── Search Clause ──

    [Fact]
    public void BuildSearchClause_ValidSearch_BuildsOrClause()
    {
        var (where, parms) = PaginationHelpers.BuildSearchClause("core", new[] { "hostname", "site" });
        Assert.Contains("OR", where);
        Assert.Contains("ILIKE", where);
        Assert.Single(parms);
        Assert.Equal("%core%", parms[0].Value);
    }

    [Fact]
    public void BuildSearchClause_Empty_ReturnsEmpty()
    {
        var (where, parms) = PaginationHelpers.BuildSearchClause("", new[] { "hostname" });
        Assert.Empty(where);
        Assert.Empty(parms);
    }

    // ── Sort Clause ──

    [Fact]
    public void BuildSortClause_ValidColumn_UsesIt()
    {
        var q = new PaginatedQuery(Sort: "building", Order: "desc");
        var sort = PaginationHelpers.BuildSortClause(q, "hostname ASC", Columns);
        Assert.Equal("building DESC", sort);
    }

    [Fact]
    public void BuildSortClause_InvalidColumn_UsesDefault()
    {
        var q = new PaginatedQuery(Sort: "password_hash");
        var sort = PaginationHelpers.BuildSortClause(q, "hostname ASC", Columns);
        Assert.Equal("hostname ASC", sort);
    }

    [Fact]
    public void BuildSortClause_SqlInjection_UsesDefault()
    {
        var q = new PaginatedQuery(Sort: "1; DROP TABLE--");
        var sort = PaginationHelpers.BuildSortClause(q, "hostname ASC", Columns);
        Assert.Equal("hostname ASC", sort);
    }
}
