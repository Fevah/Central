-- =============================================================================
-- Stage 4: AI Assistant — Conversational interface (Claude/GPT-style)
-- =============================================================================

-- Conversations (a single thread of messages between user and AI)
CREATE TABLE IF NOT EXISTS ai_conversations (
    id              serial PRIMARY KEY,
    tenant_id       uuid,
    user_id         int,                                   -- app_users.id
    title           text,                                  -- auto-generated from first message
    -- Context scope: conversation can be "about" a CRM entity
    context_entity_type text,                              -- account, deal, contact, lead, quote, contract, null (general)
    context_entity_id   int,
    -- Provider used (locked per conversation for consistency)
    provider_id     int REFERENCES central_platform.ai_providers(id),
    model_code      text,
    -- System prompt template used
    prompt_template text,
    -- State
    status          text NOT NULL DEFAULT 'active',       -- active, archived, deleted
    message_count   int NOT NULL DEFAULT 0,
    total_input_tokens  bigint NOT NULL DEFAULT 0,
    total_output_tokens bigint NOT NULL DEFAULT 0,
    total_cost_usd  numeric(10,4) NOT NULL DEFAULT 0,
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now(),
    last_message_at timestamptz
);

CREATE INDEX IF NOT EXISTS idx_ai_convs_user ON ai_conversations(user_id, updated_at DESC);
CREATE INDEX IF NOT EXISTS idx_ai_convs_entity ON ai_conversations(context_entity_type, context_entity_id);
CREATE INDEX IF NOT EXISTS idx_ai_convs_tenant ON ai_conversations(tenant_id);

-- Messages
CREATE TABLE IF NOT EXISTS ai_messages (
    id              bigserial PRIMARY KEY,
    conversation_id int NOT NULL REFERENCES ai_conversations(id) ON DELETE CASCADE,
    role            text NOT NULL,                        -- system, user, assistant, tool
    content         text NOT NULL,
    tool_calls      jsonb,                                 -- for assistant messages that invoke tools
    tool_call_id    text,                                  -- for tool-role responses
    -- Token metrics (per-message granularity)
    input_tokens    int,
    output_tokens   int,
    latency_ms      int,
    -- Attachments (files, images)
    attachments     jsonb DEFAULT '[]',
    -- Moderation + safety
    moderation_flagged boolean NOT NULL DEFAULT false,
    moderation_reason text,
    -- Feedback
    thumbs          int,                                   -- 1 = up, -1 = down, NULL = no rating
    feedback_text   text,
    created_at      timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_ai_messages_conv ON ai_messages(conversation_id, created_at);

-- Auto-update conversation counters on message insert
CREATE OR REPLACE FUNCTION update_conv_on_message() RETURNS trigger AS $$
BEGIN
    UPDATE ai_conversations SET
        message_count = message_count + 1,
        total_input_tokens = total_input_tokens + COALESCE(NEW.input_tokens, 0),
        total_output_tokens = total_output_tokens + COALESCE(NEW.output_tokens, 0),
        last_message_at = NEW.created_at,
        updated_at = NEW.created_at
    WHERE id = NEW.conversation_id;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_conv_on_message ON ai_messages;
CREATE TRIGGER trg_conv_on_message
    AFTER INSERT ON ai_messages
    FOR EACH ROW EXECUTE FUNCTION update_conv_on_message();

-- ─── Prompt templates (per-tenant, reusable) ────────────────────────────────
CREATE TABLE IF NOT EXISTS ai_prompt_templates (
    id              serial PRIMARY KEY,
    tenant_id       uuid,
    name            text NOT NULL,
    category        text,                                  -- summarize, draft, analyze, recommend
    system_prompt   text NOT NULL,
    user_prompt_template text,
    variables       text[] DEFAULT '{}',                   -- {{account.name}}, {{deal.value}}
    -- Preferred settings
    suggested_model text,
    temperature     numeric(3,2) DEFAULT 0.7,
    max_tokens      int,
    is_public       boolean NOT NULL DEFAULT false,       -- visible to all tenants (platform template)
    is_active       boolean NOT NULL DEFAULT true,
    use_count       int NOT NULL DEFAULT 0,
    created_by      int,
    created_at      timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_prompt_templates_tenant ON ai_prompt_templates(tenant_id);
CREATE INDEX IF NOT EXISTS idx_prompt_templates_category ON ai_prompt_templates(category);

-- Seed common platform templates (tenant_id = NULL → shared across tenants)
INSERT INTO ai_prompt_templates
    (tenant_id, name, category, system_prompt, user_prompt_template, variables, temperature, is_public, is_active)
VALUES
    (NULL, 'Summarize Account', 'summarize',
     'You are a concise CRM assistant. Summarize accounts focusing on: open deals, recent activities, risks, and next steps. Output 4-6 bullet points.',
     'Summarize account {{account.name}} (industry: {{account.industry}}, rating: {{account.rating}}). Open deals value: {{account.open_pipeline}}. Last activity: {{account.last_activity_at}}.',
     ARRAY['account.name', 'account.industry', 'account.rating', 'account.open_pipeline', 'account.last_activity_at'],
     0.3, true, true),

    (NULL, 'Draft Follow-Up Email', 'draft',
     'You are an experienced B2B sales rep. Write professional, concise emails. Match the tone of previous communications.',
     'Draft a follow-up email to {{contact.first_name}} at {{account.name}}. Deal: {{deal.title}}, value {{deal.value}}. Last touchpoint: {{deal.last_activity}}. Next step: {{deal.next_step}}.',
     ARRAY['contact.first_name','account.name','deal.title','deal.value','deal.last_activity','deal.next_step'],
     0.7, true, true),

    (NULL, 'Analyze Deal Risk', 'analyze',
     'You are a deal coach. Identify specific risks and suggest concrete mitigations. Cite the signals you see in the data.',
     'Assess risk on deal {{deal.title}} (stage: {{deal.stage}}, probability: {{deal.probability}}%, expected close: {{deal.expected_close}}). Activities in last 14 days: {{deal.recent_activities}}.',
     ARRAY['deal.title','deal.stage','deal.probability','deal.expected_close','deal.recent_activities'],
     0.3, true, true),

    (NULL, 'Suggest Next Actions', 'recommend',
     'You are a proactive sales assistant. Recommend 3 specific next actions with expected value/urgency.',
     'For {{entity_type}} "{{entity_name}}" — history: {{entity_history}}. Suggest 3 next best actions.',
     ARRAY['entity_type','entity_name','entity_history'],
     0.5, true, true)
ON CONFLICT DO NOTHING;

-- ─── Tool registry (for tool-use/function-calling) ─────────────────────────
-- Declares which tools the AI assistant can invoke on the user's behalf
CREATE TABLE IF NOT EXISTS ai_tools (
    id              serial PRIMARY KEY,
    tool_name       text NOT NULL UNIQUE,
    display_name    text NOT NULL,
    description     text NOT NULL,
    input_schema    jsonb NOT NULL,                       -- JSON Schema for tool arguments
    http_endpoint   text,                                   -- optional: call via HTTP
    requires_confirmation boolean NOT NULL DEFAULT false, -- require user OK before executing
    is_enabled      boolean NOT NULL DEFAULT true,
    category        text
);

INSERT INTO ai_tools (tool_name, display_name, description, input_schema, requires_confirmation, category) VALUES
    ('get_account',   'Get Account',   'Retrieve full account details by ID',
     '{"type":"object","properties":{"account_id":{"type":"integer"}},"required":["account_id"]}',
     false, 'read'),
    ('list_open_deals','List Open Deals','List all open deals, optionally filtered by owner or account',
     '{"type":"object","properties":{"account_id":{"type":"integer"},"owner_id":{"type":"integer"},"limit":{"type":"integer","default":20}}}',
     false, 'read'),
    ('create_activity','Create Activity','Log a new call/email/meeting/note on an entity',
     '{"type":"object","properties":{"entity_type":{"type":"string","enum":["account","contact","deal","lead"]},"entity_id":{"type":"integer"},"activity_type":{"type":"string","enum":["call","email","meeting","note","task"]},"subject":{"type":"string"},"body":{"type":"string"},"due_at":{"type":"string","format":"date-time"}},"required":["entity_type","entity_id","activity_type","subject"]}',
     true, 'write'),
    ('update_deal_stage','Update Deal Stage','Move a deal to a new stage',
     '{"type":"object","properties":{"deal_id":{"type":"integer"},"new_stage":{"type":"string"},"reason":{"type":"string"}},"required":["deal_id","new_stage"]}',
     true, 'write'),
    ('draft_email',   'Draft Email',   'Compose an email draft (does not send)',
     '{"type":"object","properties":{"to":{"type":"string"},"subject":{"type":"string"},"body":{"type":"string"}},"required":["to","subject","body"]}',
     false, 'write'),
    ('search_kb',     'Search Knowledge Base','Full-text search the knowledge base',
     '{"type":"object","properties":{"query":{"type":"string"}},"required":["query"]}',
     false, 'read')
ON CONFLICT (tool_name) DO NOTHING;
