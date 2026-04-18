namespace Central.Engine.Net.Dialogs;

/// <summary>
/// Pure validation rules for the allocate dialog. Five allocation
/// modes: ASN / VLAN / MLAG / IP / subnet-carve. Each input is a
/// DTO populated from the dialog's text boxes; the validator returns
/// the list of error messages to show before calling the allocation
/// service.
///
/// <para>These rules mirror the defensive checks baked into
/// <c>AllocationService</c> / <c>IpAllocationService</c>, but run
/// at the UI boundary so the user gets feedback without round-
/// tripping through the DB.</para>
/// </summary>
public static class AllocationValidation
{
    public static IReadOnlyList<string> ValidateAsn(AllocateAsnInput input)
    {
        var errors = new List<string>();
        if (input.BlockId == Guid.Empty)
            errors.Add("Select an ASN block first.");
        if (string.IsNullOrWhiteSpace(input.ConsumerType))
            errors.Add("Consumer type is required.");
        if (!Guid.TryParse(input.ConsumerIdText, out _))
            errors.Add("Consumer ID must be a valid GUID.");
        return errors;
    }

    public static IReadOnlyList<string> ValidateVlan(AllocateVlanInput input)
    {
        var errors = new List<string>();
        if (input.BlockId == Guid.Empty)
            errors.Add("Select a VLAN block first.");
        if (string.IsNullOrWhiteSpace(input.DisplayName))
            errors.Add("Display name is required.");
        return errors;
    }

    public static IReadOnlyList<string> ValidateMlag(AllocateMlagInput input)
    {
        var errors = new List<string>();
        if (input.PoolId == Guid.Empty)
            errors.Add("Select an MLAG pool first.");
        if (string.IsNullOrWhiteSpace(input.DisplayName))
            errors.Add("Display name is required.");
        return errors;
    }

    public static IReadOnlyList<string> ValidateIp(AllocateIpInput input)
    {
        var errors = new List<string>();
        if (input.SubnetId == Guid.Empty)
            errors.Add("Select a subnet first.");
        if (!string.IsNullOrWhiteSpace(input.AssignedIdText)
            && !Guid.TryParse(input.AssignedIdText, out _))
            errors.Add("Assigned-to ID must be a valid GUID (or leave blank).");
        return errors;
    }

    public static IReadOnlyList<string> ValidateSubnetCarve(CarveSubnetInput input)
    {
        var errors = new List<string>();
        if (input.PoolId == Guid.Empty)
            errors.Add("Select an IP pool first.");
        if (string.IsNullOrWhiteSpace(input.SubnetCode))
            errors.Add("Subnet code is required.");
        if (string.IsNullOrWhiteSpace(input.DisplayName))
            errors.Add("Display name is required.");
        if (input.PrefixLength is < 1 or > 128)
            errors.Add("Prefix length must be 1..128.");
        return errors;
    }
}

// ── Input DTOs ───────────────────────────────────────────────────────────
// Plain records so the dialog can build one from its text-box state
// and hand it to the validator. No WPF dependencies.

public record AllocateAsnInput(Guid BlockId, string ConsumerType, string ConsumerIdText);
public record AllocateVlanInput(Guid BlockId, string DisplayName);
public record AllocateMlagInput(Guid PoolId, string DisplayName);
public record AllocateIpInput(Guid SubnetId, string AssignedIdText);
public record CarveSubnetInput(Guid PoolId, string SubnetCode, string DisplayName, int PrefixLength);
