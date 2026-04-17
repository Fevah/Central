-- =============================================================================
-- Stage 4: AI Provider Registry — Dual-tier (platform + tenant BYOK)
-- =============================================================================
-- Architecture:
--   1. Platform admin registers providers in central_platform.ai_providers
--      and optionally supplies platform-level keys (shared/subsidized pool).
--   2. Tenant admin configures tenant_ai_providers to EITHER:
--        (a) use platform key (subject to platform quota), or
--        (b) bring their own API key (BYOK) — encrypted with CredentialEncryptor
--   3. At call time: TenantAiProviderResolver resolves
--        tenant override > platform key > error
--      and tracks usage + cost per tenant per provider per model.
-- =============================================================================

-- ─── Global provider registry (managed by platform admins) ──────────────────
CREATE TABLE IF NOT EXISTS central_platform.ai_providers (
    id                  serial PRIMARY KEY,
    provider_code       text NOT NULL UNIQUE,             -- anthropic, openai, azure_openai, vertex, ollama, bedrock, groq
    display_name        text NOT NULL,
    provider_type       text NOT NULL,                    -- cloud_api, self_hosted, local
    -- Endpoint / auth
    base_url            text,                              -- NULL for providers with fixed endpoints (routed via SDK)
    auth_type           text NOT NULL DEFAULT 'api_key',  -- api_key, oauth2, service_account, aws_iam, none
    -- Platform-level shared credentials (optional — tenants can use or override)
    platform_key_enc    text,                              -- AES-256 encrypted via CredentialEncryptor
    platform_key_configured boolean GENERATED ALWAYS AS (platform_key_enc IS NOT NULL AND platform_key_enc <> '') STORED,
    -- Capabilities
    supports_chat       boolean NOT NULL DEFAULT true,
    supports_embeddings boolean NOT NULL DEFAULT false,
    supports_vision     boolean NOT NULL DEFAULT false,
    supports_tool_use   boolean NOT NULL DEFAULT false,
    supports_streaming  boolean NOT NULL DEFAULT true,
    -- Rate limits (platform-level defaults)
    rate_limit_rpm      int,                               -- requests per minute
    rate_limit_tpm      int,                               -- tokens per minute
    -- Docs + UX
    docs_url            text,
    icon_url            text,
    brand_color         text,
    -- Lifecycle
    is_enabled          boolean NOT NULL DEFAULT true,
    is_default          boolean NOT NULL DEFAULT false,
    created_at          timestamptz NOT NULL DEFAULT now(),
    updated_at          timestamptz NOT NULL DEFAULT now()
);

-- Model catalog per provider (pricing + capabilities per model)
CREATE TABLE IF NOT EXISTS central_platform.ai_models (
    id                  serial PRIMARY KEY,
    provider_id         int NOT NULL REFERENCES central_platform.ai_providers(id) ON DELETE CASCADE,
    model_code          text NOT NULL,                     -- claude-opus-4, gpt-5, gemini-2.5-pro
    display_name        text NOT NULL,
    model_family        text,                              -- claude, gpt, gemini, llama
    -- Capabilities
    context_window      int,                               -- tokens
    max_output_tokens   int,
    supports_vision     boolean NOT NULL DEFAULT false,
    supports_tool_use   boolean NOT NULL DEFAULT false,
    -- Pricing (USD per million tokens — denormalized from provider docs)
    input_price_per_m   numeric(10,4),
    output_price_per_m  numeric(10,4),
    -- Tier
    tier                text,                              -- flagship, fast, cheap, embed
    is_recommended      boolean NOT NULL DEFAULT false,
    is_deprecated       boolean NOT NULL DEFAULT false,
    deprecation_date    date,
    is_active           boolean NOT NULL DEFAULT true,
    UNIQUE(provider_id, model_code)
);

-- Seed provider catalog
INSERT INTO central_platform.ai_providers
    (provider_code, display_name, provider_type, base_url, auth_type,
     supports_chat, supports_embeddings, supports_vision, supports_tool_use, is_enabled)
VALUES
    ('anthropic',    'Anthropic Claude', 'cloud_api',  'https://api.anthropic.com',            'api_key',
     true, false, true, true, true),
    ('openai',       'OpenAI',           'cloud_api',  'https://api.openai.com/v1',            'api_key',
     true, true,  true, true, true),
    ('azure_openai', 'Azure OpenAI',     'cloud_api',  NULL,                                    'api_key',
     true, true,  true, true, true),
    ('vertex',       'Google Vertex AI', 'cloud_api',  'https://us-central1-aiplatform.googleapis.com', 'service_account',
     true, true,  true, true, true),
    ('bedrock',      'AWS Bedrock',      'cloud_api',  NULL,                                    'aws_iam',
     true, true,  true, true, true),
    ('groq',         'Groq',             'cloud_api',  'https://api.groq.com/openai/v1',        'api_key',
     true, false, false, true, true),
    ('ollama',       'Ollama (local)',   'self_hosted','http://localhost:11434',                'none',
     true, true,  false, false, true),
    ('lmstudio',     'LM Studio (local)','local',      'http://localhost:1234/v1',              'none',
     true, false, false, false, true)
ON CONFLICT (provider_code) DO NOTHING;

-- Seed common models (partial — more added via admin UI)
INSERT INTO central_platform.ai_models
    (provider_id, model_code, display_name, model_family, context_window, max_output_tokens,
     supports_vision, supports_tool_use, input_price_per_m, output_price_per_m, tier, is_recommended)
SELECT p.id, m.model_code, m.display_name, m.model_family, m.context_window, m.max_output_tokens,
       m.supports_vision, m.supports_tool_use, m.input_price_per_m, m.output_price_per_m, m.tier, m.is_recommended
FROM central_platform.ai_providers p
CROSS JOIN (VALUES
    ('anthropic', 'claude-opus-4-7',    'Claude Opus 4.7',    'claude', 1000000, 64000, true,  true, 15.00, 75.00, 'flagship', true),
    ('anthropic', 'claude-sonnet-4-6',  'Claude Sonnet 4.6',  'claude', 1000000, 64000, true,  true,  3.00, 15.00, 'flagship', true),
    ('anthropic', 'claude-haiku-4-5',   'Claude Haiku 4.5',   'claude',  200000,  8192, true,  true,  0.80,  4.00, 'fast',     true),
    ('openai',    'gpt-5',              'GPT-5',              'gpt',    400000,  32000, true,  true,  5.00, 20.00, 'flagship', true),
    ('openai',    'gpt-5-mini',         'GPT-5 Mini',         'gpt',    400000,  32000, true,  true,  0.50,  2.00, 'fast',     true),
    ('openai',    'text-embedding-3-large','Embeddings 3 Large','gpt',  8191,    0,    false, false, 0.13,  0.00, 'embed',    true),
    ('vertex',    'gemini-2.5-pro',     'Gemini 2.5 Pro',     'gemini', 2000000, 65000, true,  true,  2.50, 10.00, 'flagship', true),
    ('groq',      'llama-3.3-70b',      'Llama 3.3 70B',      'llama',  131072,  8192, false, true,  0.59,  0.79, 'fast',     true),
    ('ollama',    'llama3.3',           'Llama 3.3 (local)',  'llama',  131072,  8192, false, false, 0.00,  0.00, 'flagship', false)
) AS m(provider_code, model_code, display_name, model_family, context_window, max_output_tokens,
       supports_vision, supports_tool_use, input_price_per_m, output_price_per_m, tier, is_recommended)
WHERE p.provider_code = m.provider_code
ON CONFLICT (provider_id, model_code) DO NOTHING;

-- ─── Tenant-specific AI provider configuration ──────────────────────────────
-- Lets tenants:
--   - Use platform-provided key (use_platform_key = true, api_key_enc NULL)
--   - Bring their own (use_platform_key = false, api_key_enc encrypted)
--   - Disable provider entirely (is_enabled = false)
-- Both flags set implements a fallback chain: try BYOK first, else platform.
CREATE TABLE IF NOT EXISTS central_platform.tenant_ai_providers (
    id                  serial PRIMARY KEY,
    tenant_id           uuid NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    provider_id         int NOT NULL REFERENCES central_platform.ai_providers(id),
    is_enabled          boolean NOT NULL DEFAULT true,
    is_default_for_tenant boolean NOT NULL DEFAULT false,
    use_platform_key    boolean NOT NULL DEFAULT true,    -- allow falling back to platform key
    api_key_enc         text,                              -- tenant's encrypted key (if BYOK)
    api_key_label       text,                              -- "Production Anthropic key"
    org_id              text,                              -- org ID for providers that need it (OpenAI org, Vertex project)
    default_model_code  text,                              -- tenant's preferred model
    -- Quotas + spend controls
    monthly_token_limit bigint,                            -- NULL = unlimited
    monthly_cost_limit  numeric(10,2),                     -- in USD
    current_month_tokens bigint NOT NULL DEFAULT 0,
    current_month_cost  numeric(10,2) NOT NULL DEFAULT 0,
    current_month_resets_at timestamptz,
    -- Audit
    configured_by       int,                               -- app_users.id of the tenant admin
    configured_at       timestamptz NOT NULL DEFAULT now(),
    last_used_at        timestamptz,
    UNIQUE(tenant_id, provider_id)
);

CREATE INDEX IF NOT EXISTS idx_tenant_ai_tenant ON central_platform.tenant_ai_providers(tenant_id);
CREATE INDEX IF NOT EXISTS idx_tenant_ai_default ON central_platform.tenant_ai_providers(tenant_id) WHERE is_default_for_tenant = true;

-- ─── AI feature configuration (which provider for which feature) ────────────
-- Allows tenant to use different providers for different features:
--   - Chat assistant → Claude Opus
--   - Lead scoring → GPT-5 Mini (cheaper)
--   - Embeddings → OpenAI text-embedding-3-large
--   - Transcription → Groq (fast + cheap)
CREATE TABLE IF NOT EXISTS central_platform.tenant_ai_features (
    id              serial PRIMARY KEY,
    tenant_id       uuid NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    feature_code    text NOT NULL,                        -- assistant, lead_scoring, opp_scoring, summarize, draft_email, embed, transcribe, next_best_action, dedup
    provider_id     int REFERENCES central_platform.ai_providers(id),
    model_code      text,
    is_enabled      boolean NOT NULL DEFAULT true,
    custom_system_prompt text,
    temperature     numeric(3,2),                          -- 0.0 - 2.0
    max_tokens      int,
    configured_at   timestamptz NOT NULL DEFAULT now(),
    UNIQUE(tenant_id, feature_code)
);

CREATE INDEX IF NOT EXISTS idx_tenant_ai_features_tenant ON central_platform.tenant_ai_features(tenant_id);

-- ─── Usage log (per-call tracking for billing + audit) ──────────────────────
CREATE TABLE IF NOT EXISTS central_platform.ai_usage_log (
    id              bigserial PRIMARY KEY,
    tenant_id       uuid NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    user_id         int,                                   -- app_users.id (nullable for system-initiated)
    provider_id     int NOT NULL REFERENCES central_platform.ai_providers(id),
    model_code      text NOT NULL,
    feature_code    text,                                  -- which feature triggered the call
    key_source      text NOT NULL,                         -- tenant_byok, platform, fallback
    -- Request/response metrics
    input_tokens    int NOT NULL DEFAULT 0,
    output_tokens   int NOT NULL DEFAULT 0,
    total_tokens    int GENERATED ALWAYS AS (input_tokens + output_tokens) STORED,
    cost_usd        numeric(12,6) NOT NULL DEFAULT 0,
    latency_ms      int,
    -- Status
    success         boolean NOT NULL DEFAULT true,
    error_code      text,
    error_message   text,
    -- Context (for audit/debug)
    entity_type     text,                                  -- account, deal, lead, contact (what the AI was called about)
    entity_id       int,
    prompt_preview  text,                                  -- first 200 chars of prompt (for audit, NOT full prompt — privacy)
    response_preview text,                                 -- first 200 chars of response
    called_at       timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_ai_usage_tenant ON central_platform.ai_usage_log(tenant_id, called_at DESC);
CREATE INDEX IF NOT EXISTS idx_ai_usage_provider ON central_platform.ai_usage_log(provider_id, called_at DESC);
CREATE INDEX IF NOT EXISTS idx_ai_usage_feature ON central_platform.ai_usage_log(feature_code, called_at DESC);
CREATE INDEX IF NOT EXISTS idx_ai_usage_entity ON central_platform.ai_usage_log(entity_type, entity_id);

-- Auto-increment tenant month-to-date usage + trigger quota alerts
CREATE OR REPLACE FUNCTION update_tenant_ai_usage() RETURNS trigger AS $$
BEGIN
    UPDATE central_platform.tenant_ai_providers
    SET current_month_tokens = current_month_tokens + NEW.total_tokens,
        current_month_cost = current_month_cost + NEW.cost_usd,
        last_used_at = NEW.called_at
    WHERE tenant_id = NEW.tenant_id AND provider_id = NEW.provider_id;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_ai_usage_aggregate ON central_platform.ai_usage_log;
CREATE TRIGGER trg_ai_usage_aggregate
    AFTER INSERT ON central_platform.ai_usage_log
    FOR EACH ROW EXECUTE FUNCTION update_tenant_ai_usage();

-- Monthly reset job — called by scheduler
CREATE OR REPLACE FUNCTION reset_tenant_ai_monthly_counters() RETURNS void AS $$
BEGIN
    UPDATE central_platform.tenant_ai_providers
    SET current_month_tokens = 0,
        current_month_cost = 0,
        current_month_resets_at = NOW()
    WHERE current_month_resets_at IS NULL
       OR current_month_resets_at < date_trunc('month', NOW());
END;
$$ LANGUAGE plpgsql;

-- Schedule first-of-month reset
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'job_schedules') THEN
        INSERT INTO job_schedules (job_type, name, schedule_cron, is_enabled)
        VALUES ('ai_monthly_reset', 'Reset AI Usage Counters', '0 0 1 * *', true)
        ON CONFLICT DO NOTHING;
    END IF;
END $$;

-- ─── Resolver function — single source of truth for provider selection ──────
-- Returns the effective provider + key source for a tenant + feature.
-- Resolution order:
--   1. Tenant BYOK for the feature's preferred provider
--   2. Platform key for the feature's preferred provider (if use_platform_key)
--   3. Tenant default provider BYOK
--   4. Platform default provider
--   5. NULL (error — no provider available)
CREATE OR REPLACE FUNCTION central_platform.resolve_ai_provider(
    p_tenant_id uuid,
    p_feature_code text
) RETURNS TABLE (
    provider_id int,
    provider_code text,
    model_code text,
    key_source text,
    has_byok boolean,
    quota_remaining bigint,
    cost_remaining numeric
) AS $$
DECLARE
    feature_provider_id int;
    feature_model text;
BEGIN
    -- 1. Look up feature-specific config
    SELECT f.provider_id, f.model_code INTO feature_provider_id, feature_model
    FROM central_platform.tenant_ai_features f
    WHERE f.tenant_id = p_tenant_id AND f.feature_code = p_feature_code AND f.is_enabled = true
    LIMIT 1;

    -- 2. If not set, use tenant default
    IF feature_provider_id IS NULL THEN
        SELECT tap.provider_id INTO feature_provider_id
        FROM central_platform.tenant_ai_providers tap
        WHERE tap.tenant_id = p_tenant_id AND tap.is_default_for_tenant = true AND tap.is_enabled = true
        LIMIT 1;
    END IF;

    -- 3. If still null, use platform default
    IF feature_provider_id IS NULL THEN
        SELECT id INTO feature_provider_id
        FROM central_platform.ai_providers
        WHERE is_default = true AND is_enabled = true
        LIMIT 1;
    END IF;

    IF feature_provider_id IS NULL THEN
        RETURN;   -- no provider available
    END IF;

    RETURN QUERY
    SELECT p.id, p.provider_code,
           COALESCE(feature_model, tap.default_model_code,
                    (SELECT model_code FROM central_platform.ai_models m
                     WHERE m.provider_id = p.id AND m.is_recommended = true
                     ORDER BY m.tier DESC LIMIT 1)),
           CASE
               WHEN tap.api_key_enc IS NOT NULL AND tap.api_key_enc <> '' THEN 'tenant_byok'
               WHEN p.platform_key_configured AND COALESCE(tap.use_platform_key, true) THEN 'platform'
               ELSE 'none'
           END AS key_source,
           (tap.api_key_enc IS NOT NULL AND tap.api_key_enc <> '') AS has_byok,
           GREATEST(0, COALESCE(tap.monthly_token_limit - tap.current_month_tokens, 999999999)) AS quota_remaining,
           GREATEST(0, COALESCE(tap.monthly_cost_limit - tap.current_month_cost, 999999)) AS cost_remaining
    FROM central_platform.ai_providers p
    LEFT JOIN central_platform.tenant_ai_providers tap
        ON tap.tenant_id = p_tenant_id AND tap.provider_id = p.id AND tap.is_enabled = true
    WHERE p.id = feature_provider_id AND p.is_enabled = true;
END;
$$ LANGUAGE plpgsql STABLE;
