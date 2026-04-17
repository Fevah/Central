using Central.Core.Services;

namespace Central.Tests.Services;

public class DataValidationServiceTests
{
    [Fact]
    public void Validate_NoRules_ReturnsOk()
    {
        var svc = new DataValidationService();
        var result = svc.Validate("UnknownType", new { Name = "test" });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_RequiredField_Empty_Fails()
    {
        var svc = new DataValidationService();
        svc.Register("TestEntity", ValidationRule.Required("Name", "Name is required"));

        var result = svc.Validate("TestEntity", new TestEntity { Name = "" });
        Assert.False(result.IsValid);
        Assert.Contains("Name is required", result.Errors);
    }

    [Fact]
    public void Validate_RequiredField_HasValue_Passes()
    {
        var svc = new DataValidationService();
        svc.Register("TestEntity", ValidationRule.Required("Name"));

        var result = svc.Validate("TestEntity", new TestEntity { Name = "hello" });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_MinLength_TooShort_Fails()
    {
        var svc = new DataValidationService();
        svc.Register("TestEntity", ValidationRule.MinLength("Name", 5, "Too short"));

        var result = svc.Validate("TestEntity", new TestEntity { Name = "ab" });
        Assert.False(result.IsValid);
        Assert.Contains("Too short", result.Errors);
    }

    [Fact]
    public void Validate_MinLength_LongEnough_Passes()
    {
        var svc = new DataValidationService();
        svc.Register("TestEntity", ValidationRule.MinLength("Name", 3));

        var result = svc.Validate("TestEntity", new TestEntity { Name = "hello" });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_MaxLength_TooLong_Fails()
    {
        var svc = new DataValidationService();
        svc.Register("TestEntity", ValidationRule.MaxLength("Name", 5));

        var result = svc.Validate("TestEntity", new TestEntity { Name = "toolongvalue" });
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_Regex_NoMatch_Fails()
    {
        var svc = new DataValidationService();
        svc.Register("TestEntity", ValidationRule.Regex("Name", @"^\d+$", "Must be numeric"));

        var result = svc.Validate("TestEntity", new TestEntity { Name = "abc" });
        Assert.False(result.IsValid);
        Assert.Contains("Must be numeric", result.Errors);
    }

    [Fact]
    public void Validate_Regex_Matches_Passes()
    {
        var svc = new DataValidationService();
        svc.Register("TestEntity", ValidationRule.Regex("Name", @"^\d+$"));

        var result = svc.Validate("TestEntity", new TestEntity { Name = "12345" });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_Custom_ReturnsFalse_Fails()
    {
        var svc = new DataValidationService();
        svc.Register("TestEntity", ValidationRule.Custom("Name",
            val => val?.ToString()?.Contains("bad") != true, "Cannot contain 'bad'"));

        var result = svc.Validate("TestEntity", new TestEntity { Name = "this is bad" });
        Assert.False(result.IsValid);
        Assert.Contains("Cannot contain 'bad'", result.Errors);
    }

    [Fact]
    public void Validate_MultipleRules_AllErrorsReported()
    {
        var svc = new DataValidationService();
        svc.Register("TestEntity",
            ValidationRule.Required("Name"),
            ValidationRule.Required("Code", "Code required"));

        var result = svc.Validate("TestEntity", new TestEntity { Name = "", Code = "" });
        Assert.False(result.IsValid);
        Assert.True(result.Errors.Count >= 2);
    }

    [Fact]
    public void RegisterDefaults_DoesNotThrow()
    {
        var svc = new DataValidationService();
        svc.RegisterDefaults();
        // Should register rules without error
    }

    [Fact]
    public void ValidationResult_ErrorSummary()
    {
        var result = ValidationResult.Fail(new List<string> { "Error 1", "Error 2" });
        Assert.Equal("Error 1; Error 2", result.ErrorSummary);
    }

    private class TestEntity
    {
        public string Name { get; set; } = "";
        public string Code { get; set; } = "";
    }
}
