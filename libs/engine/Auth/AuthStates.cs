namespace Central.Engine.Auth;

public enum AuthStates
{
    NotAuthenticated = 0,
    Windows = 1,
    Offline = 2,
    Password = 3,    // Manual password login
    EntraId = 4,     // Microsoft Entra ID (OIDC)
    Okta = 5,        // Okta (OIDC)
    Saml = 6,        // SAML2 / Duo
    Local = 7,       // Public local (social, magic-link)
    ApiToken = 8     // Authenticated via API JWT forwarding
}
