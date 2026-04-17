namespace Central.Engine.Auth;

/// <summary>
/// Configurable password policy for enterprise security.
/// Validates passwords against complexity, length, and history requirements.
/// </summary>
public class PasswordPolicy
{
    public int MinLength { get; set; } = 8;
    public int MaxLength { get; set; } = 128;
    public bool RequireUppercase { get; set; } = true;
    public bool RequireLowercase { get; set; } = true;
    public bool RequireDigit { get; set; } = true;
    public bool RequireSpecialChar { get; set; } = true;
    public int PasswordHistoryCount { get; set; } = 5;  // prevent reuse of last N passwords
    public int ExpiryDays { get; set; } = 90;  // 0 = no expiry
    public int MinAgeDays { get; set; } = 1;  // prevent immediate re-change

    /// <summary>Default enterprise policy.</summary>
    public static PasswordPolicy Default => new();

    /// <summary>Relaxed policy for development/testing.</summary>
    public static PasswordPolicy Relaxed => new()
    {
        MinLength = 4, RequireUppercase = false, RequireLowercase = false,
        RequireDigit = false, RequireSpecialChar = false,
        PasswordHistoryCount = 0, ExpiryDays = 0
    };

    /// <summary>Validate a password against this policy.</summary>
    public PasswordValidationResult Validate(string password, IEnumerable<string>? previousHashes = null, string? salt = null)
    {
        var errors = new List<string>();

        if (string.IsNullOrEmpty(password))
            return new PasswordValidationResult(false, ["Password is required"]);

        if (password.Length < MinLength)
            errors.Add($"Minimum {MinLength} characters required");
        if (password.Length > MaxLength)
            errors.Add($"Maximum {MaxLength} characters allowed");
        if (RequireUppercase && !password.Any(char.IsUpper))
            errors.Add("At least one uppercase letter required");
        if (RequireLowercase && !password.Any(char.IsLower))
            errors.Add("At least one lowercase letter required");
        if (RequireDigit && !password.Any(char.IsDigit))
            errors.Add("At least one digit required");
        if (RequireSpecialChar && password.All(c => char.IsLetterOrDigit(c)))
            errors.Add("At least one special character required");

        // Check password history
        if (PasswordHistoryCount > 0 && previousHashes != null && !string.IsNullOrEmpty(salt))
        {
            var newHash = PasswordHasher.Hash(password, salt);
            if (previousHashes.Take(PasswordHistoryCount).Contains(newHash))
                errors.Add($"Cannot reuse one of your last {PasswordHistoryCount} passwords");
        }

        return new PasswordValidationResult(errors.Count == 0, errors);
    }

    /// <summary>Check if a password has expired.</summary>
    public bool IsExpired(DateTime? passwordChangedAt)
    {
        if (ExpiryDays <= 0 || !passwordChangedAt.HasValue) return false;
        return (DateTime.UtcNow - passwordChangedAt.Value).TotalDays > ExpiryDays;
    }

    /// <summary>Check if password was changed too recently to change again.</summary>
    public bool IsTooRecent(DateTime? passwordChangedAt)
    {
        if (MinAgeDays <= 0 || !passwordChangedAt.HasValue) return false;
        return (DateTime.UtcNow - passwordChangedAt.Value).TotalDays < MinAgeDays;
    }

    /// <summary>Get a human-readable description of the policy.</summary>
    public string Description
    {
        get
        {
            var parts = new List<string> { $"Min {MinLength} chars" };
            if (RequireUppercase) parts.Add("uppercase");
            if (RequireLowercase) parts.Add("lowercase");
            if (RequireDigit) parts.Add("digit");
            if (RequireSpecialChar) parts.Add("special char");
            if (ExpiryDays > 0) parts.Add($"expires after {ExpiryDays} days");
            return string.Join(", ", parts);
        }
    }
}

/// <summary>Result of password validation.</summary>
public class PasswordValidationResult
{
    public bool IsValid { get; }
    public IReadOnlyList<string> Errors { get; }

    public PasswordValidationResult(bool isValid, List<string> errors)
    {
        IsValid = isValid;
        Errors = errors;
    }

    public string ErrorSummary => string.Join("; ", Errors);
}
