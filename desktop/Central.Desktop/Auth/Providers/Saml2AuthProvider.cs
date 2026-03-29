using System.Diagnostics;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Central.Core.Auth;

namespace Central.Desktop.Auth.Providers;

/// <summary>
/// SAML2 SP-initiated SSO + optional Duo MFA provider.
/// Uses system browser for IdP authentication, localhost HTTP listener for ACS (POST binding).
/// Duo MFA is handled as a second browser redirect after SAML assertion validation.
///
/// Note: For production, consider routing SAML through the API server (proper ACS endpoint)
/// and having the desktop just receive a Central JWT back.
/// </summary>
public class Saml2AuthProvider : IAuthenticationProvider
{
    private string _idpSsoUrl = "";
    private string _spEntityId = "";
    private bool _duoEnabled;
    private string _duoClientId = "";
    private string _duoApiHostname = "";
    private static readonly HttpClient Http = new();

    public string ProviderType => "saml2";
    public string DisplayName => "SAML SSO";
    public bool SupportsRefresh => false;
    public bool RequiresMfa => false;  // Duo MFA handled inline

    public Task InitializeAsync(IdentityProviderConfig config)
    {
        var cfg = ParseConfig(config.ConfigJson);
        _idpSsoUrl = cfg.GetValueOrDefault("idp_sso_url", "");
        _spEntityId = cfg.GetValueOrDefault("sp_entity_id", "https://central.example.com/saml/sp");
        _duoEnabled = cfg.GetValueOrDefault("duo_enabled", "false") == "true";
        _duoClientId = cfg.GetValueOrDefault("duo_client_id", "");
        _duoApiHostname = cfg.GetValueOrDefault("duo_api_hostname", "");
        return Task.CompletedTask;
    }

    public async Task<AuthenticationResult> AuthenticateAsync(AuthenticationRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_idpSsoUrl))
            return AuthenticationResult.Fail("SAML IdP not configured (missing idp_sso_url)");

        using var listener = new OAuthCallbackListener("/saml/acs");
        var acsUrl = listener.RedirectUri;
        var requestId = "_" + Guid.NewGuid().ToString("N");

        // Build minimal SAML AuthnRequest
        var authnRequest = BuildAuthnRequest(requestId, acsUrl);
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(authnRequest));
        var deflated = Uri.EscapeDataString(encoded);

        // SP-initiated: redirect to IdP with AuthnRequest
        var ssoUrl = $"{_idpSsoUrl}?SAMLRequest={deflated}&RelayState={requestId}";
        Process.Start(new ProcessStartInfo(ssoUrl) { UseShellExecute = true });

        // Wait for ACS callback (IdP POSTs SAML Response)
        var callbackParams = await listener.WaitForCallbackAsync(TimeSpan.FromMinutes(5), ct);

        if (!callbackParams.TryGetValue("SAMLResponse", out var samlResponse))
            return AuthenticationResult.Fail("No SAML response received");

        // Parse SAML response (basic XML extraction — production should use ITfoxtec.Identity.Saml2)
        var samlXml = Encoding.UTF8.GetString(Convert.FromBase64String(samlResponse));
        var claims = ExtractSamlClaims(samlXml);

        if (claims.Count == 0)
            return AuthenticationResult.Fail("Could not parse SAML assertion");

        var nameId = claims.GetValueOrDefault("NameID", new()).FirstOrDefault() ?? "";
        var email = claims.GetValueOrDefault("email", new()).FirstOrDefault() ??
                    claims.GetValueOrDefault("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress", new()).FirstOrDefault() ?? "";
        var displayName = claims.GetValueOrDefault("displayName", new()).FirstOrDefault() ??
                          claims.GetValueOrDefault("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name", new()).FirstOrDefault() ?? "";

        // Optional Duo MFA step
        if (_duoEnabled && !string.IsNullOrEmpty(_duoClientId))
        {
            var duoResult = await PerformDuoMfaAsync(email, ct);
            if (!duoResult)
                return AuthenticationResult.Fail("Duo MFA verification failed");
        }

        var username = !string.IsNullOrEmpty(email) && email.Contains('@') ? email.Split('@')[0] : nameId;
        return new AuthenticationResult
        {
            Success = true,
            Username = username,
            DisplayName = displayName,
            Email = email,
            ExternalId = nameId,
            Claims = claims,
            ProviderType = "saml2"
        };
    }

    public Task<AuthenticationResult?> TryRefreshAsync(string refreshToken, CancellationToken ct = default)
        => Task.FromResult<AuthenticationResult?>(null);

    public Task LogoutAsync(string? accessToken = null) => Task.CompletedTask;

    // ── SAML Helpers ──────────────────────────────────────────────────────

    private string BuildAuthnRequest(string id, string acsUrl)
    {
        var issueInstant = DateTime.UtcNow.ToString("o");
        return $@"<samlp:AuthnRequest xmlns:samlp=""urn:oasis:names:tc:SAML:2.0:protocol""
            ID=""{id}"" Version=""2.0"" IssueInstant=""{issueInstant}""
            Destination=""{_idpSsoUrl}""
            AssertionConsumerServiceURL=""{acsUrl}""
            ProtocolBinding=""urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST"">
            <saml:Issuer xmlns:saml=""urn:oasis:names:tc:SAML:2.0:assertion"">{_spEntityId}</saml:Issuer>
        </samlp:AuthnRequest>";
    }

    private static Dictionary<string, List<string>> ExtractSamlClaims(string samlXml)
    {
        var claims = new Dictionary<string, List<string>>();
        try
        {
            var doc = new System.Xml.XmlDocument();
            doc.LoadXml(samlXml);
            var nsmgr = new System.Xml.XmlNamespaceManager(doc.NameTable);
            nsmgr.AddNamespace("saml", "urn:oasis:names:tc:SAML:2.0:assertion");
            nsmgr.AddNamespace("samlp", "urn:oasis:names:tc:SAML:2.0:protocol");

            // Extract NameID
            var nameId = doc.SelectSingleNode("//saml:Subject/saml:NameID", nsmgr);
            if (nameId != null)
                claims["NameID"] = [nameId.InnerText];

            // Extract attribute statements
            var attrs = doc.SelectNodes("//saml:AttributeStatement/saml:Attribute", nsmgr);
            if (attrs != null)
            {
                foreach (System.Xml.XmlNode attr in attrs)
                {
                    var name = attr.Attributes?["Name"]?.Value ?? "";
                    if (string.IsNullOrEmpty(name)) continue;
                    var values = new List<string>();
                    var valueNodes = attr.SelectNodes("saml:AttributeValue", nsmgr);
                    if (valueNodes != null)
                        foreach (System.Xml.XmlNode val in valueNodes)
                            values.Add(val.InnerText);
                    claims[name] = values;
                }
            }
        }
        catch { }
        return claims;
    }

    // ── Duo MFA ───────────────────────────────────────────────────────────

    private async Task<bool> PerformDuoMfaAsync(string username, CancellationToken ct)
    {
        try
        {
            // Duo Universal Prompt flow: redirect user to Duo, wait for callback
            using var listener = new OAuthCallbackListener("/auth/duo");
            var state = Guid.NewGuid().ToString("N");

            // In production, use DuoUniversal NuGet client for proper JWT signing
            // This is a simplified flow that redirects to Duo hosted prompt
            var duoUrl = $"https://{_duoApiHostname}/oauth/v1/authorize" +
                $"?response_type=code" +
                $"&client_id={Uri.EscapeDataString(_duoClientId)}" +
                $"&redirect_uri={Uri.EscapeDataString(listener.RedirectUri)}" +
                $"&state={state}" +
                $"&duo_uname={Uri.EscapeDataString(username)}";

            Process.Start(new ProcessStartInfo(duoUrl) { UseShellExecute = true });
            var callbackParams = await listener.WaitForCallbackAsync(TimeSpan.FromMinutes(2), ct);

            return callbackParams.TryGetValue("duo_code", out _) ||
                   callbackParams.TryGetValue("code", out _);
        }
        catch { return false; }
    }

    private static Dictionary<string, string> ParseConfig(string json)
    {
        try
        {
            var doc = JsonDocument.Parse(json);
            var result = new Dictionary<string, string>();
            foreach (var prop in doc.RootElement.EnumerateObject()) result[prop.Name] = prop.Value.ToString();
            return result;
        }
        catch { return new(); }
    }
}
