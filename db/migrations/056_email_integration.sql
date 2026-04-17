-- =============================================================================
-- Phase 20: Email Integration
-- =============================================================================
-- SMTP send + IMAP/EWS receive, templates with merge fields, tracking, auto-log to CRM.

-- Email accounts (per-user or shared team inbox)
CREATE TABLE IF NOT EXISTS email_accounts (
    id                  serial PRIMARY KEY,
    tenant_id           uuid,
    user_id             int REFERENCES app_users(id) ON DELETE CASCADE,
    label               text NOT NULL,                 -- "Work", "Sales team", "No-reply"
    address             text NOT NULL,                 -- me@example.com
    provider            text NOT NULL,                 -- smtp, exchange, gmail, outlook365, imap
    smtp_host           text,
    smtp_port           int,
    smtp_user           text,
    smtp_password_enc   text,
    smtp_tls            boolean DEFAULT true,
    imap_host           text,
    imap_port           int,
    imap_user           text,
    imap_password_enc   text,
    oauth_token_enc     text,                           -- for Gmail/Outlook365/Exchange
    oauth_refresh_enc   text,
    oauth_expires_at    timestamptz,
    auto_log_to_crm     boolean NOT NULL DEFAULT true,
    signature_html      text,
    is_shared           boolean NOT NULL DEFAULT false, -- visible to team
    is_active           boolean NOT NULL DEFAULT true,
    last_sync_at        timestamptz,
    created_at          timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_email_accounts_user ON email_accounts(user_id);
CREATE INDEX IF NOT EXISTS idx_email_accounts_tenant ON email_accounts(tenant_id);

-- Email templates with merge fields (e.g., {{contact.first_name}}, {{deal.value}})
CREATE TABLE IF NOT EXISTS email_templates (
    id              serial PRIMARY KEY,
    tenant_id       uuid,
    name            text NOT NULL,
    category        text,                           -- welcome, follow_up, proposal, thank_you
    subject         text NOT NULL,
    body_html       text NOT NULL,
    body_text       text,
    is_default      boolean NOT NULL DEFAULT false,
    is_active       boolean NOT NULL DEFAULT true,
    variables       text[] DEFAULT '{}',            -- list of merge fields used
    created_by      int REFERENCES app_users(id),
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_email_templates_tenant ON email_templates(tenant_id);
CREATE INDEX IF NOT EXISTS idx_email_templates_category ON email_templates(category);

-- Sent/received email messages (tracked for CRM association)
CREATE TABLE IF NOT EXISTS email_messages (
    id                  bigserial PRIMARY KEY,
    tenant_id           uuid,
    account_id          int REFERENCES email_accounts(id) ON DELETE SET NULL,
    message_id          text,                        -- Message-ID header
    thread_id           text,                        -- gmail thread, EWS conversation
    direction           text NOT NULL,               -- inbound, outbound
    status              text NOT NULL DEFAULT 'pending', -- pending, sending, sent, delivered, opened, clicked, bounced, failed
    from_address        text NOT NULL,
    from_name           text,
    to_addresses        text[] NOT NULL,
    cc_addresses        text[] DEFAULT '{}',
    bcc_addresses       text[] DEFAULT '{}',
    reply_to            text,
    subject             text,
    body_html           text,
    body_text           text,
    has_attachments     boolean DEFAULT false,
    -- CRM linking (nullable — only set when auto-log finds a match)
    linked_contact_id   int REFERENCES contacts(id) ON DELETE SET NULL,
    linked_account_id   int REFERENCES crm_accounts(id) ON DELETE SET NULL,
    linked_deal_id      int REFERENCES crm_deals(id) ON DELETE SET NULL,
    linked_lead_id      int REFERENCES crm_leads(id) ON DELETE SET NULL,
    -- Tracking
    opened_at           timestamptz,
    open_count          int NOT NULL DEFAULT 0,
    clicked_at          timestamptz,
    click_count         int NOT NULL DEFAULT 0,
    bounced_at          timestamptz,
    bounce_reason       text,
    -- Metadata
    template_id         int REFERENCES email_templates(id) ON DELETE SET NULL,
    sent_by             int REFERENCES app_users(id),
    sent_at             timestamptz,
    received_at         timestamptz,
    raw_headers         jsonb DEFAULT '{}',
    error_message       text
);

CREATE INDEX IF NOT EXISTS idx_email_messages_tenant ON email_messages(tenant_id);
CREATE INDEX IF NOT EXISTS idx_email_messages_account ON email_messages(account_id);
CREATE INDEX IF NOT EXISTS idx_email_messages_contact ON email_messages(linked_contact_id);
CREATE INDEX IF NOT EXISTS idx_email_messages_deal ON email_messages(linked_deal_id);
CREATE INDEX IF NOT EXISTS idx_email_messages_thread ON email_messages(thread_id);
CREATE INDEX IF NOT EXISTS idx_email_messages_sent ON email_messages(sent_at DESC);
CREATE INDEX IF NOT EXISTS idx_email_messages_status ON email_messages(status);

-- Tracking pixel + click redirect tokens
CREATE TABLE IF NOT EXISTS email_tracking_events (
    id              bigserial PRIMARY KEY,
    message_id      bigint NOT NULL REFERENCES email_messages(id) ON DELETE CASCADE,
    event_type      text NOT NULL,                    -- open, click, bounce, unsubscribe
    url             text,                             -- for clicks
    ip_address      inet,
    user_agent      text,
    occurred_at     timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_tracking_events_message ON email_tracking_events(message_id);
CREATE INDEX IF NOT EXISTS idx_tracking_events_type ON email_tracking_events(event_type);

-- Attachments (file_id references the files table)
CREATE TABLE IF NOT EXISTS email_attachments (
    id              serial PRIMARY KEY,
    message_id      bigint NOT NULL REFERENCES email_messages(id) ON DELETE CASCADE,
    filename        text NOT NULL,
    content_type    text,
    size_bytes      bigint,
    file_path       text,
    content_id      text                               -- for inline images
);

CREATE INDEX IF NOT EXISTS idx_email_attachments_message ON email_attachments(message_id);

-- Seed default templates
INSERT INTO email_templates (name, category, subject, body_html, body_text, is_default)
VALUES
    ('Welcome', 'welcome', 'Welcome to {{company.name}}!',
     '<p>Hi {{contact.first_name}},</p><p>Thank you for choosing us. Your account is active.</p>',
     'Hi {{contact.first_name}}, Thank you for choosing us. Your account is active.', true),
    ('Follow-up', 'follow_up', 'Following up on {{deal.title}}',
     '<p>Hi {{contact.first_name}},</p><p>Just checking in on {{deal.title}}. Is there anything else you need?</p>',
     'Hi {{contact.first_name}}, Just checking in on {{deal.title}}. Is there anything else you need?', true),
    ('Proposal sent', 'proposal', 'Your proposal for {{deal.title}}',
     '<p>Hi {{contact.first_name}},</p><p>Please find attached our proposal for {{deal.title}}. The total value is {{deal.value}} {{deal.currency}}.</p>',
     'Hi {{contact.first_name}}, Please find attached our proposal for {{deal.title}}. Total: {{deal.value}} {{deal.currency}}.', true),
    ('Thank you', 'thank_you', 'Thank you, {{contact.first_name}}',
     '<p>Hi {{contact.first_name}},</p><p>Thanks for your business. We appreciate it.</p>',
     'Hi {{contact.first_name}}, Thanks for your business. We appreciate it.', true)
ON CONFLICT DO NOTHING;
