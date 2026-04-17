-- Self-service password reset + email verification

-- Password reset tokens
CREATE TABLE IF NOT EXISTS password_reset_tokens (
    id              serial PRIMARY KEY,
    user_id         int NOT NULL REFERENCES app_users(id) ON DELETE CASCADE,
    token_hash      text NOT NULL UNIQUE,
    created_at      timestamptz NOT NULL DEFAULT now(),
    expires_at      timestamptz NOT NULL,
    used_at         timestamptz,
    ip_address      inet
);

CREATE INDEX IF NOT EXISTS idx_password_reset_user ON password_reset_tokens(user_id);

-- Email verification tokens
CREATE TABLE IF NOT EXISTS email_verification_tokens (
    id              serial PRIMARY KEY,
    user_id         int NOT NULL REFERENCES app_users(id) ON DELETE CASCADE,
    email           text NOT NULL,
    token_hash      text NOT NULL UNIQUE,
    created_at      timestamptz NOT NULL DEFAULT now(),
    expires_at      timestamptz NOT NULL,
    verified_at     timestamptz
);

CREATE INDEX IF NOT EXISTS idx_email_verification_user ON email_verification_tokens(user_id);

ALTER TABLE app_users ADD COLUMN IF NOT EXISTS email_verified_at timestamptz;
ALTER TABLE app_users ADD COLUMN IF NOT EXISTS must_change_password boolean NOT NULL DEFAULT false;
