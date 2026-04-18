using Central.Engine.Net.Pools;

namespace Central.Engine.Net.Dialogs;

/// <summary>
/// Pure validation rules for the pool detail dialog — per-type CRUD
/// on ASN / IP / VLAN / MLAG pools, plus the VLAN template.
/// Counterpart to <see cref="HierarchyValidation"/>.
/// </summary>
public static class PoolValidation
{
    public enum Mode { New, Edit }

    public static IReadOnlyList<string> ValidateAsnPool(AsnPool p)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(p.PoolCode))    errors.Add("Code is required.");
        if (string.IsNullOrWhiteSpace(p.DisplayName)) errors.Add("Display name is required.");
        if (p.AsnFirst > p.AsnLast)                   errors.Add("First ASN must be <= last ASN.");
        return errors;
    }

    public static IReadOnlyList<string> ValidateAsnBlock(AsnBlock b, Mode mode)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(b.BlockCode)) errors.Add("Code is required.");
        if (b.AsnFirst > b.AsnLast)                 errors.Add("First ASN must be <= last ASN.");
        if (mode == Mode.New && b.PoolId == Guid.Empty)
            errors.Add("ASN pool is required.");
        return errors;
    }

    public static IReadOnlyList<string> ValidateIpPool(IpPool p)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(p.PoolCode))    errors.Add("Code is required.");
        if (string.IsNullOrWhiteSpace(p.DisplayName)) errors.Add("Display name is required.");
        if (string.IsNullOrWhiteSpace(p.Network))     errors.Add("Network (CIDR) is required.");
        return errors;
    }

    public static IReadOnlyList<string> ValidateVlanPool(VlanPool p)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(p.PoolCode))    errors.Add("Code is required.");
        if (string.IsNullOrWhiteSpace(p.DisplayName)) errors.Add("Display name is required.");
        if (p.VlanFirst > p.VlanLast)                 errors.Add("First VLAN must be <= last VLAN.");
        if (p.VlanFirst is < 1 or > 4094)             errors.Add("First VLAN must be 1..4094.");
        if (p.VlanLast  is < 1 or > 4094)             errors.Add("Last VLAN must be 1..4094.");
        return errors;
    }

    public static IReadOnlyList<string> ValidateVlanBlock(VlanBlock b, Mode mode)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(b.BlockCode)) errors.Add("Code is required.");
        if (b.VlanFirst > b.VlanLast)               errors.Add("First VLAN must be <= last VLAN.");
        if (b.VlanFirst is < 1 or > 4094)           errors.Add("First VLAN must be 1..4094.");
        if (b.VlanLast  is < 1 or > 4094)           errors.Add("Last VLAN must be 1..4094.");
        if (mode == Mode.New && b.PoolId == Guid.Empty)
            errors.Add("VLAN pool is required.");
        return errors;
    }

    public static IReadOnlyList<string> ValidateVlanTemplate(VlanTemplate t)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(t.TemplateCode)) errors.Add("Code is required.");
        if (string.IsNullOrWhiteSpace(t.DisplayName))  errors.Add("Display name is required.");
        if (string.IsNullOrWhiteSpace(t.VlanRole))
            errors.Add("VLAN role is required (drives config generation).");
        return errors;
    }

    public static IReadOnlyList<string> ValidateMlagPool(MlagDomainPool p)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(p.PoolCode))    errors.Add("Code is required.");
        if (string.IsNullOrWhiteSpace(p.DisplayName)) errors.Add("Display name is required.");
        if (p.DomainFirst > p.DomainLast)             errors.Add("First domain must be <= last domain.");
        if (p.DomainFirst is < 1 or > 4094)           errors.Add("First domain must be 1..4094.");
        if (p.DomainLast  is < 1 or > 4094)           errors.Add("Last domain must be 1..4094.");
        return errors;
    }
}
