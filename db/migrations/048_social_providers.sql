-- Social OAuth providers (Google, Microsoft, GitHub, etc.)

CREATE TABLE IF NOT EXISTS social_providers (
    id              serial PRIMARY KEY,
    provider        text NOT NULL UNIQUE,  -- google, microsoft, github, facebook, apple
    display_name    text NOT NULL,
    client_id       text,
    client_secret_enc text,
    authorize_url   text NOT NULL,
    token_url       text NOT NULL,
    userinfo_url    text NOT NULL,
    scope           text NOT NULL DEFAULT 'openid email profile',
    is_enabled      boolean NOT NULL DEFAULT false,
    icon_url        text,
    button_color    text,
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now()
);

-- Pre-seeded with well-known provider URLs
INSERT INTO social_providers (provider, display_name, authorize_url, token_url, userinfo_url, scope, is_enabled, button_color) VALUES
    ('google', 'Google',
     'https://accounts.google.com/o/oauth2/v2/auth',
     'https://oauth2.googleapis.com/token',
     'https://www.googleapis.com/oauth2/v3/userinfo',
     'openid email profile',
     false, '#4285F4'),
    ('microsoft', 'Microsoft',
     'https://login.microsoftonline.com/common/oauth2/v2.0/authorize',
     'https://login.microsoftonline.com/common/oauth2/v2.0/token',
     'https://graph.microsoft.com/oidc/userinfo',
     'openid email profile User.Read',
     false, '#2F2F2F'),
    ('github', 'GitHub',
     'https://github.com/login/oauth/authorize',
     'https://github.com/login/oauth/access_token',
     'https://api.github.com/user',
     'read:user user:email',
     false, '#24292E')
ON CONFLICT (provider) DO NOTHING;

-- Link users to their social logins (user_external_identities already covers SAML/OIDC, this is for OAuth2 social)
CREATE TABLE IF NOT EXISTS user_social_logins (
    id              serial PRIMARY KEY,
    user_id         int NOT NULL REFERENCES app_users(id) ON DELETE CASCADE,
    provider        text NOT NULL REFERENCES social_providers(provider),
    external_id     text NOT NULL,
    external_email  text,
    display_name    text,
    avatar_url      text,
    access_token_enc text,
    refresh_token_enc text,
    linked_at       timestamptz NOT NULL DEFAULT now(),
    last_login_at   timestamptz,
    UNIQUE(provider, external_id)
);

CREATE INDEX IF NOT EXISTS idx_social_logins_user ON user_social_logins(user_id);

-- OAuth state table for CSRF protection during OAuth flow
CREATE TABLE IF NOT EXISTS oauth_states (
    state           text PRIMARY KEY,
    provider        text NOT NULL,
    nonce           text,
    redirect_uri    text,
    created_at      timestamptz NOT NULL DEFAULT now(),
    expires_at      timestamptz NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_oauth_states_expires ON oauth_states(expires_at);
