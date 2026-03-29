using System.Text.RegularExpressions;

namespace Central.Core.Extensions;

/// <summary>
/// Natural sort for interface names: xe-1/1/2 before xe-1/1/10.
/// </summary>
public static class NaturalSortExtensions
{
    private static readonly Regex NumericParts = new(@"(\d+)", RegexOptions.Compiled);

    public static string NaturalSortKey(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return NumericParts.Replace(value, m => m.Value.PadLeft(6, '0'));
    }

    public static IOrderedEnumerable<T> OrderByNatural<T>(this IEnumerable<T> source,
        Func<T, string?> selector)
        => source.OrderBy(x => NaturalSortKey(selector(x)));
}
