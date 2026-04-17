namespace Central.Engine.Extensions;

public static class StringExtensions
{
    /// <summary>
    /// Strip -L1/-L2 tier suffix from building name to get base building.
    /// "MEP-93-L1" → "MEP-93", "MEP-96-L2" → "MEP-96", "MEP-91" → "MEP-91"
    /// </summary>
    public static string StripBuildingTier(this string? building)
    {
        if (string.IsNullOrEmpty(building)) return "";
        var idx = building.LastIndexOf("-L", StringComparison.OrdinalIgnoreCase);
        if (idx > 0 && idx < building.Length - 2 && char.IsDigit(building[idx + 2]))
            return building[..idx];
        return building;
    }
}
