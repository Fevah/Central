using Central.Engine.Models;

namespace Central.Tests.Models;

public class Stage5ModelsTests
{
    // ─── PortalUser ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("customer", true,  false)]
    [InlineData("partner",  false, true)]
    [InlineData("other",    false, false)]
    public void PortalUser_TypeFlags_MatchPortalType(string type, bool expCustomer, bool expPartner)
    {
        var u = new PortalUser { PortalType = type };
        Assert.Equal(expCustomer, u.IsCustomer);
        Assert.Equal(expPartner, u.IsPartner);
    }

    [Fact]
    public void PortalUser_IsVerified_TrueWhenEmailVerifiedAtSet()
    {
        Assert.True(new PortalUser { EmailVerifiedAt = DateTime.UtcNow }.IsVerified);
        Assert.False(new PortalUser { EmailVerifiedAt = null }.IsVerified);
    }

    // ─── PartnerDealRegistration ───────────────────────────────────────────

    [Theory]
    [InlineData("approved", true)]
    [InlineData("converted", true)]
    [InlineData("submitted", false)]
    [InlineData("rejected", false)]
    public void PartnerDealRegistration_IsApproved_MapsFromStatus(string status, bool expected)
    {
        Assert.Equal(expected, new PartnerDealRegistration { Status = status }.IsApproved);
    }

    // ─── KbArticle ─────────────────────────────────────────────────────────

    [Fact]
    public void KbArticle_IsPublished_TrueWhenStatusPublished()
    {
        Assert.True(new KbArticle { Status = "published" }.IsPublished);
        Assert.False(new KbArticle { Status = "draft" }.IsPublished);
    }

    [Fact]
    public void KbArticle_HelpfulnessPct_Calculated()
    {
        var a = new KbArticle { HelpfulCount = 8, NotHelpfulCount = 2 };
        Assert.Equal(80m, a.HelpfulnessPct);
    }

    [Fact]
    public void KbArticle_HelpfulnessPct_ZeroWhenNoVotes()
    {
        Assert.Equal(0m, new KbArticle { HelpfulCount = 0, NotHelpfulCount = 0 }.HelpfulnessPct);
    }

    // ─── RuleExecutionEntry ────────────────────────────────────────────────

    [Theory]
    [InlineData("pass",              true)]
    [InlineData("workflow_started",  true)]
    [InlineData("fail",              false)]
    [InlineData("error",             false)]
    public void RuleExecutionEntry_IsSuccess_MapsFromResult(string result, bool expected)
    {
        Assert.Equal(expected, new RuleExecutionEntry { Result = result }.IsSuccess);
    }

    // ─── CustomField ───────────────────────────────────────────────────────

    [Theory]
    [InlineData("picklist", true, false, false)]
    [InlineData("multipick", true, false, false)]
    [InlineData("lookup", false, true, false)]
    [InlineData("number", false, false, true)]
    [InlineData("currency", false, false, true)]
    [InlineData("percent", false, false, true)]
    [InlineData("text", false, false, false)]
    public void CustomField_TypeFlags_MapCorrectly(string type, bool picklist, bool lookup, bool numeric)
    {
        var f = new CustomField { FieldType = type };
        Assert.Equal(picklist, f.IsPicklist);
        Assert.Equal(lookup, f.IsLookup);
        Assert.Equal(numeric, f.IsNumeric);
    }

    // ─── FieldPermission ───────────────────────────────────────────────────

    [Theory]
    [InlineData("hidden", true, false, false)]
    [InlineData("read",   false, true, false)]
    [InlineData("write",  false, false, true)]
    public void FieldPermission_Flags_MapFromPermission(string perm, bool hidden, bool read, bool write)
    {
        var fp = new FieldPermission { Permission = perm };
        Assert.Equal(hidden, fp.IsHidden);
        Assert.Equal(read, fp.IsReadOnly);
        Assert.Equal(write, fp.IsWritable);
    }

    // ─── ImportJob ─────────────────────────────────────────────────────────

    [Fact]
    public void ImportJob_CompletionPct_Calculated()
    {
        var j = new ImportJob { RowCount = 200, RowsProcessed = 50 };
        Assert.Equal(25m, j.CompletionPct);
    }

    [Fact]
    public void ImportJob_CompletionPct_ZeroWhenNoRows()
    {
        Assert.Equal(0m, new ImportJob { RowCount = 0 }.CompletionPct);
    }

    [Fact]
    public void ImportJob_SuccessPct_Calculated()
    {
        var j = new ImportJob { RowsProcessed = 100, RowsCreated = 60, RowsUpdated = 20 };
        Assert.Equal(80m, j.SuccessPct);
    }

    [Fact]
    public void ImportJob_SuccessPct_ZeroWhenNothingProcessed()
    {
        Assert.Equal(0m, new ImportJob { RowsProcessed = 0, RowsCreated = 5 }.SuccessPct);
    }

    // ─── ShoppingCart ──────────────────────────────────────────────────────

    [Fact]
    public void ShoppingCart_IsActive_WhenStatusActive()
    {
        Assert.True(new ShoppingCart { Status = "active" }.IsActive);
        Assert.False(new ShoppingCart { Status = "abandoned" }.IsActive);
    }

    [Fact]
    public void ShoppingCart_IsEmpty_WhenSubtotalZero()
    {
        Assert.True(new ShoppingCart { Subtotal = 0 }.IsEmpty);
        Assert.False(new ShoppingCart { Subtotal = 0.01m }.IsEmpty);
    }

    // ─── Payment ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("succeeded", true, false, false)]
    [InlineData("refunded",  false, true, false)]
    [InlineData("failed",    false, false, true)]
    [InlineData("pending",   false, false, false)]
    public void Payment_StatusFlags_MapCorrectly(string status, bool success, bool refunded, bool failed)
    {
        var p = new Payment { Status = status };
        Assert.Equal(success, p.IsSuccess);
        Assert.Equal(refunded, p.IsRefunded);
        Assert.Equal(failed, p.IsFailed);
    }
}
