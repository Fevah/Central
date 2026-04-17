using Central.Engine.Services;

namespace Central.Tests.Services;

/// <summary>
/// Extended tests for DataValidationService covering edge cases not in the existing test file.
/// </summary>
public class DataValidationEdgeCaseTests
{
    private class TestModel
    {
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public int Age { get; set; }
        public string Code { get; set; } = "";
    }

    [Fact]
    public void Validate_UnregisteredType_ReturnsOk()
    {
        var svc = new DataValidationService();
        var result = svc.Validate("NonExistent", new TestModel());
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_RequiredNull_Fails()
    {
        var svc = new DataValidationService();
        svc.Register("Test", ValidationRule.Required("Name", "Name required"));
        var model = new TestModel { Name = null! };
        var result = svc.Validate("Test", model);
        Assert.False(result.IsValid);
        Assert.Contains("Name required", result.Errors);
    }

    [Fact]
    public void Validate_RequiredWhitespace_Fails()
    {
        var svc = new DataValidationService();
        svc.Register("Test", ValidationRule.Required("Name", "Name required"));
        var model = new TestModel { Name = "   " };
        var result = svc.Validate("Test", model);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_RequiredWithValue_Passes()
    {
        var svc = new DataValidationService();
        svc.Register("Test", ValidationRule.Required("Name", "Name required"));
        var model = new TestModel { Name = "Valid" };
        var result = svc.Validate("Test", model);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_MinLength_TooShort_Fails()
    {
        var svc = new DataValidationService();
        svc.Register("Test", ValidationRule.MinLength("Name", 5, "Too short"));
        var model = new TestModel { Name = "ab" };
        var result = svc.Validate("Test", model);
        Assert.False(result.IsValid);
        Assert.Contains("Too short", result.Errors);
    }

    [Fact]
    public void Validate_MinLength_ExactLength_Passes()
    {
        var svc = new DataValidationService();
        svc.Register("Test", ValidationRule.MinLength("Name", 3));
        var model = new TestModel { Name = "abc" };
        var result = svc.Validate("Test", model);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_MaxLength_TooLong_Fails()
    {
        var svc = new DataValidationService();
        svc.Register("Test", ValidationRule.MaxLength("Code", 3, "Code too long"));
        var model = new TestModel { Code = "ABCDE" };
        var result = svc.Validate("Test", model);
        Assert.False(result.IsValid);
        Assert.Contains("Code too long", result.Errors);
    }

    [Fact]
    public void Validate_MaxLength_ExactLength_Passes()
    {
        var svc = new DataValidationService();
        svc.Register("Test", ValidationRule.MaxLength("Code", 3));
        var model = new TestModel { Code = "ABC" };
        var result = svc.Validate("Test", model);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_Regex_Valid_Passes()
    {
        var svc = new DataValidationService();
        svc.Register("Test", ValidationRule.Regex("Email", @"^[^@]+@[^@]+\.[^@]+$", "Invalid email"));
        var model = new TestModel { Email = "user@example.com" };
        var result = svc.Validate("Test", model);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_Regex_Invalid_Fails()
    {
        var svc = new DataValidationService();
        svc.Register("Test", ValidationRule.Regex("Email", @"^[^@]+@[^@]+\.[^@]+$", "Invalid email"));
        var model = new TestModel { Email = "not-an-email" };
        var result = svc.Validate("Test", model);
        Assert.False(result.IsValid);
        Assert.Contains("Invalid email", result.Errors);
    }

    [Fact]
    public void Validate_Custom_PassesWhenTrue()
    {
        var svc = new DataValidationService();
        svc.Register("Test", ValidationRule.Custom("Name", v => v is string s && s.StartsWith("X"), "Must start with X"));
        var model = new TestModel { Name = "Xray" };
        var result = svc.Validate("Test", model);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_Custom_FailsWhenFalse()
    {
        var svc = new DataValidationService();
        svc.Register("Test", ValidationRule.Custom("Name", v => v is string s && s.StartsWith("X"), "Must start with X"));
        var model = new TestModel { Name = "Alpha" };
        var result = svc.Validate("Test", model);
        Assert.False(result.IsValid);
        Assert.Contains("Must start with X", result.Errors);
    }

    [Fact]
    public void Validate_Range_IntInRange_Passes()
    {
        var svc = new DataValidationService();
        svc.Register("Test", ValidationRule.Range("Age", 1, 150, "Invalid age"));
        var model = new TestModel { Age = 30 };
        var result = svc.Validate("Test", model);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_Range_IntBelowMin_Fails()
    {
        var svc = new DataValidationService();
        svc.Register("Test", ValidationRule.Range("Age", 1, 150, "Invalid age"));
        var model = new TestModel { Age = 0 };
        var result = svc.Validate("Test", model);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_Range_IntAboveMax_Fails()
    {
        var svc = new DataValidationService();
        svc.Register("Test", ValidationRule.Range("Age", 1, 150, "Invalid age"));
        var model = new TestModel { Age = 200 };
        var result = svc.Validate("Test", model);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_MultipleRules_AllChecked()
    {
        var svc = new DataValidationService();
        svc.Register("Test",
            ValidationRule.Required("Name"),
            ValidationRule.Required("Email"),
            ValidationRule.MinLength("Code", 2));
        var model = new TestModel { Name = "", Email = "", Code = "A" };
        var result = svc.Validate("Test", model);
        Assert.False(result.IsValid);
        Assert.Equal(3, result.Errors.Count);
    }

    [Fact]
    public void ValidationResult_ErrorSummary_JoinsSemicolon()
    {
        var result = ValidationResult.Fail(new List<string> { "Error A", "Error B" });
        Assert.Equal("Error A; Error B", result.ErrorSummary);
    }

    [Fact]
    public void ValidationResult_Ok_HasNoErrors()
    {
        var result = ValidationResult.Ok();
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Equal("", result.ErrorSummary);
    }

    [Fact]
    public void ValidationResult_Fail_SingleError()
    {
        var result = ValidationResult.Fail("Single error");
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal("Single error", result.Errors[0]);
    }

    [Fact]
    public void Validate_MissingProperty_Skipped()
    {
        var svc = new DataValidationService();
        svc.Register("Test", ValidationRule.Required("NonExistent", "Should not fail"));
        var model = new TestModel();
        var result = svc.Validate("Test", model);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Register_MultipleCalls_Additive()
    {
        var svc = new DataValidationService();
        svc.Register("Test", ValidationRule.Required("Name", "Name needed"));
        svc.Register("Test", ValidationRule.Required("Email", "Email needed"));
        var model = new TestModel { Name = "", Email = "" };
        var result = svc.Validate("Test", model);
        Assert.Equal(2, result.Errors.Count);
    }

    [Fact]
    public void RegisterDefaults_RegistersKnownTypes()
    {
        var svc = new DataValidationService();
        svc.RegisterDefaults();

        // Device should require SwitchName
        var deviceModel = new { SwitchName = "", Building = "" };
        var result = svc.Validate("Device", deviceModel);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidationRule_StaticFactories_SetProperties()
    {
        var required = ValidationRule.Required("Field1", "msg1");
        Assert.Equal("Field1", required.PropertyName);
        Assert.Equal("required", required.RuleType);
        Assert.Equal("msg1", required.ErrorMessage);

        var minLen = ValidationRule.MinLength("Field2", 5, "msg2");
        Assert.Equal("min_length", minLen.RuleType);
        Assert.Equal(5, minLen.MinValue);

        var maxLen = ValidationRule.MaxLength("Field3", 10, "msg3");
        Assert.Equal("max_length", maxLen.RuleType);
        Assert.Equal(10, maxLen.MaxValue);

        var range = ValidationRule.Range("Field4", 1, 100, "msg4");
        Assert.Equal("range", range.RuleType);
        Assert.Equal(1, range.MinValue);
        Assert.Equal(100, range.MaxValue);

        var regex = ValidationRule.Regex("Field5", @"\d+", "msg5");
        Assert.Equal("regex", regex.RuleType);
        Assert.Equal(@"\d+", regex.Pattern);

        var custom = ValidationRule.Custom("Field6", _ => true, "msg6");
        Assert.Equal("custom", custom.RuleType);
        Assert.NotNull(custom.CustomValidator);
    }
}
