namespace Central.Engine.Validation;

/// <summary>Mark a property as required. Engine validates before save.</summary>
[AttributeUsage(AttributeTargets.Property)]
public class RequiredFieldAttribute : Attribute
{
    public string? ErrorMessage { get; set; }
}

/// <summary>Max length constraint on string properties.</summary>
[AttributeUsage(AttributeTargets.Property)]
public class MaxLengthFieldAttribute : Attribute
{
    public int Length { get; }
    public string? ErrorMessage { get; set; }
    public MaxLengthFieldAttribute(int length) => Length = length;
}

/// <summary>Regex pattern validation.</summary>
[AttributeUsage(AttributeTargets.Property)]
public class PatternFieldAttribute : Attribute
{
    public string Pattern { get; }
    public string? ErrorMessage { get; set; }
    public PatternFieldAttribute(string pattern) => Pattern = pattern;
}

/// <summary>Range validation for numeric properties.</summary>
[AttributeUsage(AttributeTargets.Property)]
public class RangeFieldAttribute : Attribute
{
    public double Min { get; }
    public double Max { get; }
    public string? ErrorMessage { get; set; }
    public RangeFieldAttribute(double min, double max) { Min = min; Max = max; }
}

/// <summary>
/// Engine validation helper — validates an object using its field attributes.
/// Call this from ValidateRow handlers or before save.
/// </summary>
public static class FieldValidator
{
    public static List<string> Validate(object entity)
    {
        var errors = new List<string>();
        foreach (var prop in entity.GetType().GetProperties())
        {
            var val = prop.GetValue(entity);

            // Required
            var req = prop.GetCustomAttributes(typeof(RequiredFieldAttribute), true).FirstOrDefault() as RequiredFieldAttribute;
            if (req != null && (val == null || (val is string s && string.IsNullOrWhiteSpace(s))))
                errors.Add(req.ErrorMessage ?? $"{prop.Name} is required");

            // MaxLength
            var maxLen = prop.GetCustomAttributes(typeof(MaxLengthFieldAttribute), true).FirstOrDefault() as MaxLengthFieldAttribute;
            if (maxLen != null && val is string str && str.Length > maxLen.Length)
                errors.Add(maxLen.ErrorMessage ?? $"{prop.Name} exceeds {maxLen.Length} characters");

            // Pattern
            var pattern = prop.GetCustomAttributes(typeof(PatternFieldAttribute), true).FirstOrDefault() as PatternFieldAttribute;
            if (pattern != null && val is string pStr && !string.IsNullOrEmpty(pStr)
                && !System.Text.RegularExpressions.Regex.IsMatch(pStr, pattern.Pattern))
                errors.Add(pattern.ErrorMessage ?? $"{prop.Name} format is invalid");

            // Range
            var range = prop.GetCustomAttributes(typeof(RangeFieldAttribute), true).FirstOrDefault() as RangeFieldAttribute;
            if (range != null && val != null)
            {
                var num = Convert.ToDouble(val);
                if (num < range.Min || num > range.Max)
                    errors.Add(range.ErrorMessage ?? $"{prop.Name} must be between {range.Min} and {range.Max}");
            }
        }
        return errors;
    }
}
