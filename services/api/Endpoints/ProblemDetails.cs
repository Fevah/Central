namespace Central.Api.Endpoints;

/// <summary>
/// RFC 7807 Problem Details for HTTP APIs.
/// All error responses should use this format.
/// </summary>
public record ApiProblem(
    string Type,
    string Title,
    int Status,
    string? Detail = null,
    string? Instance = null,
    Dictionary<string, object>? Extensions = null
)
{
    public static IResult ValidationError(string detail, Dictionary<string, string[]>? errors = null)
    {
        var extensions = errors != null
            ? new Dictionary<string, object> { ["errors"] = errors }
            : null;

        return Results.Json(new ApiProblem(
            Type: "https://tools.ietf.org/html/rfc7807#section-3.1",
            Title: "Validation Error",
            Status: 400,
            Detail: detail,
            Extensions: extensions
        ), statusCode: 400, contentType: "application/problem+json");
    }

    public static IResult NotFound(string detail)
        => Results.Json(new ApiProblem(
            Type: "https://tools.ietf.org/html/rfc7807#section-3.1",
            Title: "Not Found",
            Status: 404,
            Detail: detail
        ), statusCode: 404, contentType: "application/problem+json");

    public static IResult Unauthorized(string detail = "Authentication required")
        => Results.Json(new ApiProblem(
            Type: "https://tools.ietf.org/html/rfc7807#section-3.1",
            Title: "Unauthorized",
            Status: 401,
            Detail: detail
        ), statusCode: 401, contentType: "application/problem+json");

    public static IResult Forbidden(string detail = "Insufficient permissions")
        => Results.Json(new ApiProblem(
            Type: "https://tools.ietf.org/html/rfc7807#section-3.1",
            Title: "Forbidden",
            Status: 403,
            Detail: detail
        ), statusCode: 403, contentType: "application/problem+json");

    public static IResult Conflict(string detail)
        => Results.Json(new ApiProblem(
            Type: "https://tools.ietf.org/html/rfc7807#section-3.1",
            Title: "Conflict",
            Status: 409,
            Detail: detail
        ), statusCode: 409, contentType: "application/problem+json");

    public static IResult RateLimited(string detail, int retryAfterSeconds = 60)
        => Results.Json(new ApiProblem(
            Type: "https://tools.ietf.org/html/rfc7807#section-3.1",
            Title: "Too Many Requests",
            Status: 429,
            Detail: detail,
            Extensions: new() { ["retry_after"] = retryAfterSeconds }
        ), statusCode: 429, contentType: "application/problem+json");

    public static IResult ServerError(string detail = "An internal error occurred")
        => Results.Json(new ApiProblem(
            Type: "https://tools.ietf.org/html/rfc7807#section-3.1",
            Title: "Internal Server Error",
            Status: 500,
            Detail: detail
        ), statusCode: 500, contentType: "application/problem+json");
}

/// <summary>Bulk operation result with partial success support.</summary>
public record BulkResult(
    int Succeeded,
    int Failed,
    List<BulkItemResult> Results
);

public record BulkItemResult(
    object? Id,
    bool Success,
    string? Error = null
);
