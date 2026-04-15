---
name: Enterprise authentication providers — SAML2/Duo, Entra ID, Okta, Public/Local
description: Full feature specs for 4 auth providers + cross-cutting auth layer. User provided detailed requirements on 2026-03-27.
type: project
---

Central needs enterprise-grade multi-provider authentication. Four providers plus a shared auth layer.

**Why:** Enterprise deployment requires SSO integration with corporate IdPs (Entra, Okta), MFA enforcement (Duo), and fallback local auth for non-federated users.

**How to apply:** All auth flows must converge to a unified session token. IdP discovery by email domain. RBAC claims mapping from any provider. Build as pluggable provider architecture so new IdPs can be added without touching core.

## Provider 1: SAML2 / Duo
- SP-initiated and IdP-initiated SSO
- SAML assertion signing and encryption
- JIT user provisioning from SAML attributes
- Duo Universal Prompt for inline MFA (push, SMS, hardware token, biometric)
- Device trust and health checks (Duo Trusted Endpoints)
- Adaptive access policies (geo, network, device posture)
- Self-service device management portal
- Admin-configurable bypass/deny rules per application

## Provider 2: Microsoft Entra ID (Azure AD)
- OIDC / OAuth 2.0 primary, optional SAML 2.0
- Single-tenant or multi-tenant app registrations
- Conditional Access policies (device compliance, risk-based, location, session)
- Entra ID Protection (sign-in and user risk scoring)
- Group and role claims in tokens for RBAC
- SCIM 2.0 provisioning
- Privileged Identity Management integration
- Continuous Access Evaluation (CAE)
- Cross-tenant B2B/B2X collaboration
- Microsoft Authenticator / FIDO2 / passkey MFA

## Provider 3: Okta
- OIDC / OAuth 2.0 with SAML 2.0 support
- Universal Directory (unified user store, attribute mappings, profile mastering)
- Okta Expression Language for attribute transforms
- Org-level and app-level sign-on policies
- Adaptive MFA (Okta Verify, WebAuthn, email, SMS, hardware tokens)
- ThreatInsight (pre-auth threat detection)
- Lifecycle management and HR-driven provisioning via SCIM
- Inline and event hooks for custom logic
- DPoP token binding
- Admin API for automation

## Provider 4: Public / Local Auth
- Email/password registration with configurable password policy
- Email verification and magic-link passwordless login
- Social IdP federation (Google, GitHub, Apple, Microsoft personal)
- TOTP and WebAuthn/passkey MFA enrolment
- Account recovery flows (reset link, backup codes)
- Rate limiting and brute-force lockout
- Refresh token rotation and revocation
- Optional guest/anonymous access

## Cross-Cutting Concerns (all providers)
- IdP discovery (email-domain → IdP routing)
- Unified session model (single session token irrespective of upstream IdP)
- Centralised RBAC/ABAC claims mapping
- Token refresh and silent re-auth
- Single logout / session revocation across IdPs
- Audit logging of every authn/authz event
- Fallback flow if external IdP unavailable
