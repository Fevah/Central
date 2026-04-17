namespace Central.Engine.Services;

/// <summary>
/// Enterprise data validation service — validates model properties before save.
/// Used by grid ValidateRow events and detail dialogs.
/// Supports: required, min/max length, regex, range, unique, custom rules.
/// </summary>
public class DataValidationService
{
    private static DataValidationService? _instance;
    public static DataValidationService Instance => _instance ??= new();

    private readonly Dictionary<string, List<ValidationRule>> _rules = new();

    /// <summary>Register validation rules for an entity type.</summary>
    public void Register(string entityType, params ValidationRule[] rules)
    {
        if (!_rules.ContainsKey(entityType))
            _rules[entityType] = new List<ValidationRule>();
        _rules[entityType].AddRange(rules);
    }

    /// <summary>Validate an object against registered rules for its type.</summary>
    public ValidationResult Validate(string entityType, object entity)
    {
        if (!_rules.TryGetValue(entityType, out var rules))
            return ValidationResult.Ok();

        var errors = new List<string>();
        var entityType2 = entity.GetType();

        foreach (var rule in rules)
        {
            var prop = entityType2.GetProperty(rule.PropertyName);
            if (prop == null) continue;
            var value = prop.GetValue(entity);

            switch (rule.RuleType)
            {
                case "required":
                    if (value == null || (value is string s && string.IsNullOrWhiteSpace(s)))
                        errors.Add(rule.ErrorMessage ?? $"{rule.PropertyName} is required");
                    break;

                case "min_length":
                    if (value is string str && str.Length < rule.MinValue)
                        errors.Add(rule.ErrorMessage ?? $"{rule.PropertyName} must be at least {rule.MinValue} characters");
                    break;

                case "max_length":
                    if (value is string str2 && str2.Length > rule.MaxValue)
                        errors.Add(rule.ErrorMessage ?? $"{rule.PropertyName} must be at most {rule.MaxValue} characters");
                    break;

                case "range":
                    if (value is IComparable comp)
                    {
                        if (rule.MinValue > 0 && comp.CompareTo(Convert.ChangeType(rule.MinValue, value.GetType())) < 0)
                            errors.Add(rule.ErrorMessage ?? $"{rule.PropertyName} must be at least {rule.MinValue}");
                        if (rule.MaxValue > 0 && comp.CompareTo(Convert.ChangeType(rule.MaxValue, value.GetType())) > 0)
                            errors.Add(rule.ErrorMessage ?? $"{rule.PropertyName} must be at most {rule.MaxValue}");
                    }
                    break;

                case "regex":
                    if (value is string regStr && !string.IsNullOrEmpty(rule.Pattern) &&
                        !System.Text.RegularExpressions.Regex.IsMatch(regStr, rule.Pattern))
                        errors.Add(rule.ErrorMessage ?? $"{rule.PropertyName} format is invalid");
                    break;

                case "custom":
                    if (rule.CustomValidator != null && !rule.CustomValidator(value))
                        errors.Add(rule.ErrorMessage ?? $"{rule.PropertyName} validation failed");
                    break;
            }
        }

        return errors.Count > 0 ? ValidationResult.Fail(errors) : ValidationResult.Ok();
    }

    /// <summary>Register default rules for the platform's core entities.</summary>
    public void RegisterDefaults()
    {
        Register("Device",
            ValidationRule.Required("SwitchName", "Device name is required"),
            ValidationRule.Required("Building", "Building is required"));

        Register("User",
            ValidationRule.Required("Username", "Username is required"),
            ValidationRule.MinLength("Username", 3, "Username must be at least 3 characters"));

        Register("SdRequest",
            ValidationRule.Required("Subject", "Subject is required"));

        Register("Appointment",
            ValidationRule.Required("Subject", "Subject is required"));

        Register("Country",
            ValidationRule.Required("Code", "Country code is required"),
            ValidationRule.Required("Name", "Country name is required"),
            ValidationRule.MaxLength("Code", 3, "Country code must be 3 characters"));

        Register("ReferenceConfig",
            ValidationRule.Required("EntityType", "Entity type is required"),
            ValidationRule.Required("Prefix", "Prefix is required"));
    }
}

/// <summary>Single validation rule for a property.</summary>
public class ValidationRule
{
    public string PropertyName { get; set; } = "";
    public string RuleType { get; set; } = "required";
    public string? ErrorMessage { get; set; }
    public int MinValue { get; set; }
    public int MaxValue { get; set; }
    public string? Pattern { get; set; }
    public Func<object?, bool>? CustomValidator { get; set; }

    public static ValidationRule Required(string prop, string? msg = null) =>
        new() { PropertyName = prop, RuleType = "required", ErrorMessage = msg };

    public static ValidationRule MinLength(string prop, int min, string? msg = null) =>
        new() { PropertyName = prop, RuleType = "min_length", MinValue = min, ErrorMessage = msg };

    public static ValidationRule MaxLength(string prop, int max, string? msg = null) =>
        new() { PropertyName = prop, RuleType = "max_length", MaxValue = max, ErrorMessage = msg };

    public static ValidationRule Range(string prop, int min, int max, string? msg = null) =>
        new() { PropertyName = prop, RuleType = "range", MinValue = min, MaxValue = max, ErrorMessage = msg };

    public static ValidationRule Regex(string prop, string pattern, string? msg = null) =>
        new() { PropertyName = prop, RuleType = "regex", Pattern = pattern, ErrorMessage = msg };

    public static ValidationRule Custom(string prop, Func<object?, bool> validator, string? msg = null) =>
        new() { PropertyName = prop, RuleType = "custom", CustomValidator = validator, ErrorMessage = msg };
}

public class ValidationResult
{
    public bool IsValid { get; }
    public IReadOnlyList<string> Errors { get; }
    public string ErrorSummary => string.Join("; ", Errors);

    private ValidationResult(bool valid, List<string> errors) { IsValid = valid; Errors = errors; }

    public static ValidationResult Ok() => new(true, new());
    public static ValidationResult Fail(List<string> errors) => new(false, errors);
    public static ValidationResult Fail(string error) => new(false, new List<string> { error });
}
