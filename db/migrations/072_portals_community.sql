-- =============================================================================
-- Stage 5.1-5.3: Customer/Partner Portals + Community + Knowledge Base
-- =============================================================================

-- ─── Portal users (external users, separate from app_users) ─────────────────
CREATE TABLE IF NOT EXISTS portal_users (
    id                  serial PRIMARY KEY,
    tenant_id           uuid,
    email               citext NOT NULL,
    display_name        text,
    portal_type         text NOT NULL DEFAULT 'customer', -- customer, partner
    contact_id          int REFERENCES contacts(id) ON DELETE SET NULL,
    account_id          int REFERENCES crm_accounts(id) ON DELETE SET NULL,
    company_id          int REFERENCES companies(id) ON DELETE SET NULL,
    password_hash       text,
    salt                text,
    last_login_at       timestamptz,
    login_count         int NOT NULL DEFAULT 0,
    is_active           boolean NOT NULL DEFAULT true,
    email_verified_at   timestamptz,
    created_at          timestamptz NOT NULL DEFAULT now(),
    updated_at          timestamptz NOT NULL DEFAULT now(),
    UNIQUE(tenant_id, email)
);

CREATE INDEX IF NOT EXISTS idx_portal_users_email ON portal_users(email);
CREATE INDEX IF NOT EXISTS idx_portal_users_account ON portal_users(account_id);

-- Magic-link login tokens
CREATE TABLE IF NOT EXISTS portal_magic_links (
    id              bigserial PRIMARY KEY,
    user_id         int NOT NULL REFERENCES portal_users(id) ON DELETE CASCADE,
    token_hash      text NOT NULL UNIQUE,
    created_at      timestamptz NOT NULL DEFAULT now(),
    expires_at      timestamptz NOT NULL DEFAULT (now() + interval '30 minutes'),
    used_at         timestamptz,
    ip_address      inet
);

CREATE INDEX IF NOT EXISTS idx_magic_links_user ON portal_magic_links(user_id);

-- Portal sessions
CREATE TABLE IF NOT EXISTS portal_sessions (
    id              serial PRIMARY KEY,
    user_id         int NOT NULL REFERENCES portal_users(id) ON DELETE CASCADE,
    session_token   text NOT NULL UNIQUE,
    ip_address      inet,
    user_agent      text,
    started_at      timestamptz NOT NULL DEFAULT now(),
    last_activity   timestamptz NOT NULL DEFAULT now(),
    expires_at      timestamptz NOT NULL,
    is_active       boolean NOT NULL DEFAULT true
);

CREATE INDEX IF NOT EXISTS idx_portal_sessions_user ON portal_sessions(user_id) WHERE is_active = true;

-- ─── Partner portal: deal registration ──────────────────────────────────────
CREATE TABLE IF NOT EXISTS partner_deal_registrations (
    id                  serial PRIMARY KEY,
    tenant_id           uuid,
    partner_user_id     int NOT NULL REFERENCES portal_users(id),
    customer_company_name text NOT NULL,
    customer_contact_name text,
    customer_contact_email text,
    estimated_value     numeric(14,2),
    currency            char(3) NOT NULL DEFAULT 'GBP',
    products_of_interest text[],
    notes               text,
    status              text NOT NULL DEFAULT 'submitted', -- submitted, approved, rejected, converted
    submitted_at        timestamptz NOT NULL DEFAULT now(),
    reviewed_at         timestamptz,
    reviewed_by         int REFERENCES app_users(id),
    converted_deal_id   int REFERENCES crm_deals(id) ON DELETE SET NULL,
    rejection_reason    text
);

CREATE INDEX IF NOT EXISTS idx_deal_regs_partner ON partner_deal_registrations(partner_user_id);
CREATE INDEX IF NOT EXISTS idx_deal_regs_status ON partner_deal_registrations(status);

-- ─── Knowledge base ─────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS kb_categories (
    id              serial PRIMARY KEY,
    tenant_id       uuid,
    slug            text NOT NULL,
    name            text NOT NULL,
    description     text,
    parent_id       int REFERENCES kb_categories(id) ON DELETE SET NULL,
    sort_order      int NOT NULL DEFAULT 100,
    is_public       boolean NOT NULL DEFAULT true,
    UNIQUE(tenant_id, slug)
);

CREATE TABLE IF NOT EXISTS kb_articles (
    id              serial PRIMARY KEY,
    tenant_id       uuid,
    slug            text NOT NULL,
    category_id     int REFERENCES kb_categories(id) ON DELETE SET NULL,
    title           text NOT NULL,
    content_html    text NOT NULL,
    content_markdown text,
    status          text NOT NULL DEFAULT 'draft',    -- draft, published, archived
    visibility      text NOT NULL DEFAULT 'public',  -- public, customers, partners, internal
    view_count      int NOT NULL DEFAULT 0,
    helpful_count   int NOT NULL DEFAULT 0,
    not_helpful_count int NOT NULL DEFAULT 0,
    search_vector   tsvector,
    tags            text[] DEFAULT '{}',
    author_id       int REFERENCES app_users(id),
    published_at    timestamptz,
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now(),
    UNIQUE(tenant_id, slug)
);

CREATE INDEX IF NOT EXISTS idx_kb_articles_status ON kb_articles(status, visibility);
CREATE INDEX IF NOT EXISTS idx_kb_articles_category ON kb_articles(category_id);
CREATE INDEX IF NOT EXISTS idx_kb_articles_search ON kb_articles USING gin(search_vector);

-- Auto-update search vector
CREATE OR REPLACE FUNCTION kb_article_search_update() RETURNS trigger AS $$
BEGIN
    NEW.search_vector := to_tsvector('english',
        COALESCE(NEW.title, '') || ' ' ||
        COALESCE(NEW.content_markdown, '') || ' ' ||
        array_to_string(COALESCE(NEW.tags, '{}'), ' '));
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_kb_article_search ON kb_articles;
CREATE TRIGGER trg_kb_article_search
    BEFORE INSERT OR UPDATE OF title, content_markdown, tags ON kb_articles
    FOR EACH ROW EXECUTE FUNCTION kb_article_search_update();

-- ─── Community: threads + posts + comments ─────────────────────────────────
CREATE TABLE IF NOT EXISTS community_threads (
    id              serial PRIMARY KEY,
    tenant_id       uuid,
    category        text,                              -- general, product_feedback, support, announcements
    title           text NOT NULL,
    body_markdown   text NOT NULL,
    author_user_id  int REFERENCES app_users(id),
    author_portal_user_id int REFERENCES portal_users(id),
    is_pinned       boolean NOT NULL DEFAULT false,
    is_locked       boolean NOT NULL DEFAULT false,
    is_resolved     boolean NOT NULL DEFAULT false,
    view_count      int NOT NULL DEFAULT 0,
    reply_count     int NOT NULL DEFAULT 0,
    vote_score      int NOT NULL DEFAULT 0,
    last_reply_at   timestamptz,
    tags            text[] DEFAULT '{}',
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_threads_category ON community_threads(category);
CREATE INDEX IF NOT EXISTS idx_threads_last_reply ON community_threads(last_reply_at DESC NULLS LAST);

CREATE TABLE IF NOT EXISTS community_posts (
    id              serial PRIMARY KEY,
    thread_id       int NOT NULL REFERENCES community_threads(id) ON DELETE CASCADE,
    parent_post_id  int REFERENCES community_posts(id) ON DELETE CASCADE, -- for nested replies
    body_markdown   text NOT NULL,
    author_user_id  int REFERENCES app_users(id),
    author_portal_user_id int REFERENCES portal_users(id),
    is_marked_answer boolean NOT NULL DEFAULT false,
    vote_score      int NOT NULL DEFAULT 0,
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_posts_thread ON community_posts(thread_id);
CREATE INDEX IF NOT EXISTS idx_posts_parent ON community_posts(parent_post_id);

-- Auto-update thread reply count + last_reply_at
CREATE OR REPLACE FUNCTION update_thread_on_post() RETURNS trigger AS $$
BEGIN
    IF TG_OP = 'INSERT' THEN
        UPDATE community_threads
        SET reply_count = reply_count + 1, last_reply_at = NOW(), updated_at = NOW()
        WHERE id = NEW.thread_id;
    ELSIF TG_OP = 'DELETE' THEN
        UPDATE community_threads
        SET reply_count = GREATEST(0, reply_count - 1)
        WHERE id = OLD.thread_id;
    END IF;
    RETURN COALESCE(NEW, OLD);
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_post_thread_update ON community_posts;
CREATE TRIGGER trg_post_thread_update
    AFTER INSERT OR DELETE ON community_posts
    FOR EACH ROW EXECUTE FUNCTION update_thread_on_post();

-- Votes (one per user per post/thread)
CREATE TABLE IF NOT EXISTS community_votes (
    id              bigserial PRIMARY KEY,
    entity_type     text NOT NULL,                     -- thread, post
    entity_id       int NOT NULL,
    user_id         int REFERENCES app_users(id),
    portal_user_id  int REFERENCES portal_users(id),
    vote_value      int NOT NULL CHECK (vote_value IN (-1, 1)),
    voted_at        timestamptz NOT NULL DEFAULT now(),
    UNIQUE(entity_type, entity_id, user_id),
    UNIQUE(entity_type, entity_id, portal_user_id)
);
