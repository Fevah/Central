namespace Central.Data;

/// <summary>
/// Result of a database write operation. Allows callers to check success
/// and display errors without catching exceptions.
/// </summary>
public class DbResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }

    public static DbResult Ok() => new() { Success = true };
    public static DbResult Fail(string error) => new() { Success = false, Error = error };
    public static DbResult Fail(Exception ex) => new() { Success = false, Error = ex.Message };
}
