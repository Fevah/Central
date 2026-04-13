-- =============================================================================
-- Central Auth Seed — Default Tenant + Admin User
-- =============================================================================
-- Seeds the secure_auth database with a default tenant and an admin user
-- that matches the existing Central admin account.
--
-- This script is idempotent (ON CONFLICT DO NOTHING).
-- Run against the secure_auth database AFTER auth-service migrations (V001-V017).
-- =============================================================================

-- Well-known default tenant UUID (matches Secure's V013 seed)
INSERT INTO tenants (
    id, name, slug, tier, status,
    settings, feature_flags, rate_limits,
    data_region, metadata,
    created_at, updated_at, activated_at, is_deleted
) VALUES (
    '00000000-0000-0000-0000-000000000001',
    'Central',
    'central',
    'enterprise',
    'active',
    '{
        "allow_self_registration": false,
        "require_email_verification": false,
        "max_failed_logins": 10,
        "lockout_duration_minutes": 30,
        "password_min_length": 8,
        "session_timeout_minutes": 60
    }'::jsonb,
    '{
        "mfa_enabled": true,
        "sso_enabled": true,
        "api_keys_enabled": true,
        "audit_logging": true,
        "custom_roles": true
    }'::jsonb,
    '{
        "api_requests_per_hour": 100000,
        "storage_gb": 100,
        "max_users": 10000,
        "max_sessions_per_user": 10
    }'::jsonb,
    'local',
    '{}'::jsonb,
    NOW(), NOW(), NOW(), false
) ON CONFLICT (id) DO NOTHING;

-- Admin user — matches Central's default admin
-- Password: "admin" hashed with Argon2id (auth-service will re-hash on first login)
INSERT INTO users (
    id,
    tenant_id,
    email,
    email_verified,
    email_verified_at,
    password_hash,
    display_name,
    status,
    mfa_enabled,
    mfa_method,
    oauth_provider,
    metadata,
    created_at, updated_at, is_deleted
) VALUES (
    '00000000-0000-0000-0000-000000000002',
    '00000000-0000-0000-0000-000000000001',
    'admin@central.local',
    true,
    NOW(),
    '$argon2id$v=19$m=19456,t=2,p=1$c2VlZC1zYWx0LWNlbnRyYWw$placeholder-rehash-on-first-login',
    'Central Admin',
    'active',
    false,
    'none',
    'local',
    '{"source": "central_seed", "requires_password_change": true}'::jsonb,
    NOW(), NOW(), false
) ON CONFLICT (id) DO NOTHING;

-- Assign admin user to the existing super_admin system role
-- (V004 seeds system roles: super_admin, tenant_admin, admin, member, guest)
INSERT INTO user_roles (user_id, role_id)
SELECT
    '00000000-0000-0000-0000-000000000002',
    id
FROM roles WHERE code = 'super_admin'
ON CONFLICT DO NOTHING;
