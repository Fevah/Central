using Central.Api.Endpoints;

namespace Central.Tests.Api;

/// <summary>
/// Tests for SQL injection prevention via column whitelist validation.
/// Ensures dynamic column names from client requests cannot inject SQL.
/// </summary>
public class ColumnValidationTests
{
    private static readonly HashSet<string> AllowedColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "switch_name", "building", "status", "device_type", "primary_ip"
    };

    [Fact]
    public void ValidateColumns_AllAllowed_ReturnsNull()
    {
        var result = EndpointHelpers.ValidateColumns(
            new[] { "switch_name", "building", "status" }, AllowedColumns);
        Assert.Null(result);
    }

    [Fact]
    public void ValidateColumns_UnknownColumn_ReturnsBadRequest()
    {
        var result = EndpointHelpers.ValidateColumns(
            new[] { "switch_name", "is_deleted" }, AllowedColumns);
        Assert.NotNull(result);
    }

    [Fact]
    public void ValidateColumns_SqlInjection_Blocked()
    {
        // Attempt to inject SQL via column name
        var result = EndpointHelpers.ValidateColumns(
            new[] { "status; DROP TABLE switches--" }, AllowedColumns);
        Assert.NotNull(result);
    }

    [Fact]
    public void ValidateColumns_EmptyList_ReturnsNull()
    {
        var result = EndpointHelpers.ValidateColumns(
            Array.Empty<string>(), AllowedColumns);
        Assert.Null(result);
    }

    [Fact]
    public void ValidateColumns_CaseInsensitive_Allowed()
    {
        var result = EndpointHelpers.ValidateColumns(
            new[] { "SWITCH_NAME", "Building" }, AllowedColumns);
        Assert.Null(result);
    }

    [Fact]
    public void ValidateColumns_SystemColumn_Blocked()
    {
        // Attacker tries to modify system columns
        var result = EndpointHelpers.ValidateColumns(
            new[] { "password_hash" }, AllowedColumns);
        Assert.NotNull(result);
    }

    [Fact]
    public void ValidateColumns_SpecialCharsInColumn_Blocked()
    {
        var result = EndpointHelpers.ValidateColumns(
            new[] { "col=1" }, AllowedColumns);
        Assert.NotNull(result);
    }
}
