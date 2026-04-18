using Central.Engine.Net.Dialogs;
using Central.Engine.Net.Pools;
using static Central.Engine.Net.Dialogs.PoolValidation;

namespace Central.Tests.Net;

/// <summary>
/// Chunk-C dialog-validation tests for PoolDetailDialog. Covers each
/// of the seven editable pool tiers.
/// </summary>
public class PoolValidationTests
{
    // ── AsnPool ─────────────────────────────────────────────────────

    [Fact]
    public void AsnPool_HappyPath_OK()
    {
        Assert.Empty(ValidateAsnPool(new AsnPool
        {
            PoolCode = "IMM-ASN", DisplayName = "Immunocore ASNs",
            AsnFirst = 64512, AsnLast = 65534,
        }));
    }

    [Fact]
    public void AsnPool_FirstAboveLast_Rejected()
    {
        var errors = ValidateAsnPool(new AsnPool
        {
            PoolCode = "X", DisplayName = "X", AsnFirst = 200, AsnLast = 100,
        });
        Assert.Contains(errors, e => e.Contains("First ASN"));
    }

    // ── AsnBlock ────────────────────────────────────────────────────

    [Fact]
    public void AsnBlock_New_RequiresPool()
    {
        var errors = ValidateAsnBlock(
            new AsnBlock { BlockCode = "B", AsnFirst = 100, AsnLast = 110 },
            Mode.New);
        Assert.Contains(errors, e => e.Contains("ASN pool"));
    }

    [Fact]
    public void AsnBlock_EditOmitsPoolCheck()
    {
        var errors = ValidateAsnBlock(
            new AsnBlock { BlockCode = "B", AsnFirst = 100, AsnLast = 110 },
            Mode.Edit);
        Assert.Empty(errors);
    }

    // ── IpPool ──────────────────────────────────────────────────────

    [Fact]
    public void IpPool_HappyPath_OK()
    {
        Assert.Empty(ValidateIpPool(new IpPool
        {
            PoolCode = "IMM-IP", DisplayName = "IP pool", Network = "10.0.0.0/8",
        }));
    }

    [Fact]
    public void IpPool_MissingNetwork_Rejected()
    {
        Assert.Contains(
            ValidateIpPool(new IpPool { PoolCode = "P", DisplayName = "D" }),
            e => e.Contains("Network"));
    }

    // ── VlanPool ────────────────────────────────────────────────────

    [Fact]
    public void VlanPool_ValidFullRange_OK()
    {
        Assert.Empty(ValidateVlanPool(new VlanPool
        {
            PoolCode = "V", DisplayName = "VLAN space", VlanFirst = 1, VlanLast = 4094,
        }));
    }

    [Fact]
    public void VlanPool_OutOfRangeHigh_Rejected()
    {
        // 4095 is reserved, must be rejected.
        var errors = ValidateVlanPool(new VlanPool
        {
            PoolCode = "V", DisplayName = "D", VlanFirst = 1, VlanLast = 4095,
        });
        Assert.Contains(errors, e => e.Contains("Last VLAN"));
    }

    [Fact]
    public void VlanPool_FirstZero_Rejected()
    {
        var errors = ValidateVlanPool(new VlanPool
        {
            PoolCode = "V", DisplayName = "D", VlanFirst = 0, VlanLast = 100,
        });
        Assert.Contains(errors, e => e.Contains("First VLAN"));
    }

    // ── VlanBlock ───────────────────────────────────────────────────

    [Fact]
    public void VlanBlock_New_RequiresPool()
    {
        var errors = ValidateVlanBlock(
            new VlanBlock { BlockCode = "B", VlanFirst = 1, VlanLast = 2048 },
            Mode.New);
        Assert.Contains(errors, e => e.Contains("VLAN pool"));
    }

    // ── VlanTemplate ────────────────────────────────────────────────

    [Fact]
    public void VlanTemplate_MissingRole_Rejected()
    {
        // VlanRole drives config generation — empty = hostname
        // generation later picks a nonsensical default, so catch here.
        var errors = ValidateVlanTemplate(new VlanTemplate
        {
            TemplateCode = "SRV", DisplayName = "Servers",
        });
        Assert.Contains(errors, e => e.Contains("role"));
    }

    [Fact]
    public void VlanTemplate_FullySpecified_OK()
    {
        Assert.Empty(ValidateVlanTemplate(new VlanTemplate
        {
            TemplateCode = "SRV", DisplayName = "Servers", VlanRole = "Servers",
        }));
    }

    // ── MlagPool ────────────────────────────────────────────────────

    [Fact]
    public void MlagPool_ValidRange_OK()
    {
        Assert.Empty(ValidateMlagPool(new MlagDomainPool
        {
            PoolCode = "M", DisplayName = "MLAG", DomainFirst = 1, DomainLast = 4094,
        }));
    }

    [Fact]
    public void MlagPool_FirstAboveLast_Rejected()
    {
        var errors = ValidateMlagPool(new MlagDomainPool
        {
            PoolCode = "M", DisplayName = "M", DomainFirst = 200, DomainLast = 100,
        });
        Assert.Contains(errors, e => e.Contains("First domain"));
    }
}
