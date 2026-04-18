using Central.Engine.Net.Dialogs;

namespace Central.Tests.Net;

/// <summary>
/// Chunk-C dialog-validation tests for AllocateDialog — the five
/// modes (ASN / VLAN / MLAG / IP / subnet-carve).
/// </summary>
public class AllocationValidationTests
{
    // ── ASN ─────────────────────────────────────────────────────────

    [Fact]
    public void Asn_HappyPath_OK()
    {
        var errors = AllocationValidation.ValidateAsn(
            new AllocateAsnInput(Guid.NewGuid(), "Device", Guid.NewGuid().ToString()));
        Assert.Empty(errors);
    }

    [Fact]
    public void Asn_EmptyBlock_Rejected()
    {
        var errors = AllocationValidation.ValidateAsn(
            new AllocateAsnInput(Guid.Empty, "Device", Guid.NewGuid().ToString()));
        Assert.Contains(errors, e => e.Contains("ASN block"));
    }

    [Fact]
    public void Asn_ConsumerIdNonGuid_Rejected()
    {
        // Operators occasionally paste a hostname into the consumer
        // ID field — we expect a GUID and must say so clearly.
        var errors = AllocationValidation.ValidateAsn(
            new AllocateAsnInput(Guid.NewGuid(), "Device", "MEP-91-CORE02"));
        Assert.Contains(errors, e => e.Contains("GUID"));
    }

    [Fact]
    public void Asn_ConsumerTypeBlank_Rejected()
    {
        var errors = AllocationValidation.ValidateAsn(
            new AllocateAsnInput(Guid.NewGuid(), "", Guid.NewGuid().ToString()));
        Assert.Contains(errors, e => e.Contains("Consumer type"));
    }

    // ── VLAN ────────────────────────────────────────────────────────

    [Fact]
    public void Vlan_HappyPath_OK()
    {
        var errors = AllocationValidation.ValidateVlan(
            new AllocateVlanInput(Guid.NewGuid(), "Servers"));
        Assert.Empty(errors);
    }

    [Fact]
    public void Vlan_MissingNameAndBlock_ReportsBoth()
    {
        var errors = AllocationValidation.ValidateVlan(
            new AllocateVlanInput(Guid.Empty, ""));
        Assert.Equal(2, errors.Count);
    }

    // ── MLAG ────────────────────────────────────────────────────────

    [Fact]
    public void Mlag_EmptyPool_Rejected()
    {
        var errors = AllocationValidation.ValidateMlag(
            new AllocateMlagInput(Guid.Empty, "MEP-91-MLAG"));
        Assert.Contains(errors, e => e.Contains("MLAG pool"));
    }

    // ── IP ──────────────────────────────────────────────────────────

    [Fact]
    public void Ip_HappyPath_OK()
    {
        var errors = AllocationValidation.ValidateIp(
            new AllocateIpInput(Guid.NewGuid(), ""));
        Assert.Empty(errors);
    }

    [Fact]
    public void Ip_BlankAssignedIdOptional()
    {
        // Blank assigned-id is fine — the IP just isn't bound to a
        // consumer yet. Non-blank must be a valid GUID.
        var errors = AllocationValidation.ValidateIp(
            new AllocateIpInput(Guid.NewGuid(), "   "));
        Assert.Empty(errors);
    }

    [Fact]
    public void Ip_NonGuidAssignedId_Rejected()
    {
        var errors = AllocationValidation.ValidateIp(
            new AllocateIpInput(Guid.NewGuid(), "not-a-guid"));
        Assert.Contains(errors, e => e.Contains("GUID"));
    }

    // ── SubnetCarve ─────────────────────────────────────────────────

    [Fact]
    public void SubnetCarve_HappyPath_OK()
    {
        var errors = AllocationValidation.ValidateSubnetCarve(
            new CarveSubnetInput(Guid.NewGuid(), "SUB-A", "subnet A", 30));
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(129)]
    [InlineData(-1)]
    public void SubnetCarve_PrefixOutOfRange_Rejected(int prefix)
    {
        var errors = AllocationValidation.ValidateSubnetCarve(
            new CarveSubnetInput(Guid.NewGuid(), "S", "S", prefix));
        Assert.Contains(errors, e => e.Contains("Prefix"));
    }

    [Fact]
    public void SubnetCarve_EmptyPool_Rejected()
    {
        var errors = AllocationValidation.ValidateSubnetCarve(
            new CarveSubnetInput(Guid.Empty, "S", "S", 30));
        Assert.Contains(errors, e => e.Contains("IP pool"));
    }

    [Fact]
    public void SubnetCarve_Prefix128_AllowedForV6Host()
    {
        // /128 on IPv6 = single-host subnet; must be accepted even
        // though IPv4's hard upper is /32.
        var errors = AllocationValidation.ValidateSubnetCarve(
            new CarveSubnetInput(Guid.NewGuid(), "S", "S", 128));
        Assert.Empty(errors);
    }
}
