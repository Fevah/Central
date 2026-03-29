namespace Central.Core.Widgets;

/// <summary>
/// Engine validation helper. Modules declare required fields;
/// the engine highlights invalid cells during ValidateRow.
///
/// Usage:
///   var errors = GridValidationHelper.Validate(device,
///       ("SwitchName", "Device name is required"),
///       ("Building", "Building is required"));
/// </summary>
public static class GridValidationHelper
{
    /// <summary>
    /// Validate an item's required fields. Returns list of (fieldName, errorMessage) pairs.
    /// Empty list = valid.
    /// </summary>
    public static List<(string Field, string Error)> Validate(object item, params (string Field, string Error)[] rules)
    {
        var errors = new List<(string, string)>();
        var type = item.GetType();

        foreach (var (field, error) in rules)
        {
            var prop = type.GetProperty(field);
            if (prop == null) continue;

            var value = prop.GetValue(item);
            bool invalid = value switch
            {
                null => true,
                string s => string.IsNullOrWhiteSpace(s),
                int i => i == 0 && field != "Priority" && field != "SortOrder",
                Guid g => g == Guid.Empty,
                _ => false
            };

            if (invalid) errors.Add((field, error));
        }

        return errors;
    }

    /// <summary>Format validation errors for display in a grid row validation event.</summary>
    public static string FormatErrors(List<(string Field, string Error)> errors)
        => string.Join("\n", errors.Select(e => $"• {e.Error}"));
}
