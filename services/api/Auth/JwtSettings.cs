namespace Central.Api.Auth;

public class JwtSettings
{
    /// <summary>HMAC-SHA256 secret key. Set via CENTRAL_JWT_SECRET or CENTRAL_JWT_SECRET env var.</summary>
    public string Secret { get; set; } = "";

    /// <summary>Token issuer.</summary>
    public string Issuer { get; set; } = "Central.Api";

    /// <summary>Token audience.</summary>
    public string Audience { get; set; } = "Central.Desktop";

    /// <summary>Token lifetime in hours.</summary>
    public int ExpiryHours { get; set; } = 24;
}
