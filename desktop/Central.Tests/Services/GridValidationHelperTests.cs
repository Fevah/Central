using Central.Core.Widgets;

namespace Central.Tests.Services;

public class GridValidationHelperTests
{
    private class TestEntity
    {
        public string Name { get; set; } = "";
        public string Building { get; set; } = "";
        public int Count { get; set; }
        public Guid UniqueId { get; set; }
        public int Priority { get; set; }
        public int SortOrder { get; set; }
    }

    [Fact]
    public void Validate_AllValid_ReturnsEmpty()
    {
        var item = new TestEntity { Name = "Switch-01", Building = "B91" };
        var errors = GridValidationHelper.Validate(item,
            ("Name", "Name is required"),
            ("Building", "Building is required"));
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_EmptyString_ReturnsError()
    {
        var item = new TestEntity { Name = "", Building = "B91" };
        var errors = GridValidationHelper.Validate(item,
            ("Name", "Name is required"),
            ("Building", "Building is required"));
        Assert.Single(errors);
        Assert.Equal("Name", errors[0].Field);
        Assert.Equal("Name is required", errors[0].Error);
    }

    [Fact]
    public void Validate_NullValue_ReturnsError()
    {
        var item = new TestEntity { Name = null!, Building = "B91" };
        var errors = GridValidationHelper.Validate(item,
            ("Name", "Name is required"));
        Assert.Single(errors);
    }

    [Fact]
    public void Validate_WhitespaceString_ReturnsError()
    {
        var item = new TestEntity { Name = "   ", Building = "B91" };
        var errors = GridValidationHelper.Validate(item,
            ("Name", "Name is required"));
        Assert.Single(errors);
    }

    [Fact]
    public void Validate_MultipleErrors_ReturnsAll()
    {
        var item = new TestEntity { Name = "", Building = "" };
        var errors = GridValidationHelper.Validate(item,
            ("Name", "Name required"),
            ("Building", "Building required"));
        Assert.Equal(2, errors.Count);
    }

    [Fact]
    public void Validate_EmptyGuid_ReturnsError()
    {
        var item = new TestEntity { Name = "ok", UniqueId = Guid.Empty };
        var errors = GridValidationHelper.Validate(item,
            ("UniqueId", "ID required"));
        Assert.Single(errors);
    }

    [Fact]
    public void Validate_ValidGuid_NoError()
    {
        var item = new TestEntity { Name = "ok", UniqueId = Guid.NewGuid() };
        var errors = GridValidationHelper.Validate(item,
            ("UniqueId", "ID required"));
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_ZeroInt_ReturnsError()
    {
        var item = new TestEntity { Count = 0 };
        var errors = GridValidationHelper.Validate(item,
            ("Count", "Count must be set"));
        Assert.Single(errors);
    }

    [Fact]
    public void Validate_NonZeroInt_NoError()
    {
        var item = new TestEntity { Count = 5 };
        var errors = GridValidationHelper.Validate(item,
            ("Count", "Count must be set"));
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_PriorityZero_Allowed()
    {
        // Priority and SortOrder at zero should not trigger error
        var item = new TestEntity { Priority = 0 };
        var errors = GridValidationHelper.Validate(item,
            ("Priority", "Priority required"));
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_SortOrderZero_Allowed()
    {
        var item = new TestEntity { SortOrder = 0 };
        var errors = GridValidationHelper.Validate(item,
            ("SortOrder", "Sort required"));
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_MissingProperty_Skipped()
    {
        var item = new TestEntity { Name = "ok" };
        var errors = GridValidationHelper.Validate(item,
            ("NonExistentField", "Should not appear"));
        Assert.Empty(errors);
    }

    [Fact]
    public void FormatErrors_FormatsCorrectly()
    {
        var errors = new List<(string Field, string Error)>
        {
            ("Name", "Name is required"),
            ("Building", "Building is required")
        };
        var result = GridValidationHelper.FormatErrors(errors);
        Assert.Contains("• Name is required", result);
        Assert.Contains("• Building is required", result);
    }

    [Fact]
    public void FormatErrors_EmptyList_ReturnsEmpty()
    {
        var result = GridValidationHelper.FormatErrors(new List<(string, string)>());
        Assert.Equal("", result);
    }
}
