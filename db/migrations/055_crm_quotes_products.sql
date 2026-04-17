-- =============================================================================
-- CRM Extended: Quotes + Products + Price Books
-- Phases 22-23 of the 29-phase CRM buildout.
-- =============================================================================

-- ─── Products catalog ───────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS crm_products (
    id              serial PRIMARY KEY,
    tenant_id       uuid,
    sku             text NOT NULL,
    name            text NOT NULL,
    description     text,
    category        text,
    unit_price      numeric(14,2) NOT NULL DEFAULT 0,
    currency        char(3) NOT NULL DEFAULT 'GBP',
    is_recurring    boolean NOT NULL DEFAULT false,
    billing_period  text,                      -- monthly, annual, one-time
    tax_rate_pct    numeric(5,2) DEFAULT 0,
    cost_price      numeric(14,2),             -- for margin calc
    is_active       boolean NOT NULL DEFAULT true,
    metadata        jsonb DEFAULT '{}',
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now(),
    UNIQUE(tenant_id, sku)
);

CREATE INDEX IF NOT EXISTS idx_crm_products_tenant ON crm_products(tenant_id);
CREATE INDEX IF NOT EXISTS idx_crm_products_category ON crm_products(category);
CREATE INDEX IF NOT EXISTS idx_crm_products_name_trgm ON crm_products USING gin(name gin_trgm_ops);

-- ─── Price books (multi-currency, volume, contract pricing) ─────────────────
CREATE TABLE IF NOT EXISTS crm_price_books (
    id              serial PRIMARY KEY,
    tenant_id       uuid,
    name            text NOT NULL,
    description     text,
    currency        char(3) NOT NULL DEFAULT 'GBP',
    is_default      boolean NOT NULL DEFAULT false,
    valid_from      date,
    valid_to        date,
    is_active       boolean NOT NULL DEFAULT true,
    created_at      timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS crm_price_book_entries (
    id              serial PRIMARY KEY,
    price_book_id   int NOT NULL REFERENCES crm_price_books(id) ON DELETE CASCADE,
    product_id      int NOT NULL REFERENCES crm_products(id) ON DELETE CASCADE,
    unit_price      numeric(14,2) NOT NULL,
    min_quantity    int NOT NULL DEFAULT 1,
    UNIQUE(price_book_id, product_id, min_quantity)
);

CREATE INDEX IF NOT EXISTS idx_price_entries_book ON crm_price_book_entries(price_book_id);
CREATE INDEX IF NOT EXISTS idx_price_entries_product ON crm_price_book_entries(product_id);

-- ─── Quotes / Proposals ─────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS crm_quotes (
    id                      serial PRIMARY KEY,
    tenant_id               uuid,
    deal_id                 int REFERENCES crm_deals(id) ON DELETE SET NULL,
    account_id              int REFERENCES crm_accounts(id) ON DELETE SET NULL,
    contact_id              int REFERENCES contacts(id) ON DELETE SET NULL,
    quote_number            text NOT NULL,
    version                 int NOT NULL DEFAULT 1,
    status                  text NOT NULL DEFAULT 'draft',  -- draft, sent, accepted, rejected, expired, superseded
    billing_address_id      int REFERENCES addresses(id),
    shipping_address_id     int REFERENCES addresses(id),
    currency                char(3) NOT NULL DEFAULT 'GBP',
    subtotal                numeric(14,2) NOT NULL DEFAULT 0,
    discount_pct            numeric(5,2) DEFAULT 0,
    discount_amount         numeric(14,2) DEFAULT 0,
    tax_pct                 numeric(5,2) DEFAULT 0,
    tax_amount              numeric(14,2) DEFAULT 0,
    total                   numeric(14,2) NOT NULL DEFAULT 0,
    valid_until             date,
    notes                   text,
    terms                   text,
    accepted_at             timestamptz,
    sent_at                 timestamptz,
    pdf_url                 text,
    created_by              int REFERENCES app_users(id),
    created_at              timestamptz NOT NULL DEFAULT now(),
    updated_at              timestamptz NOT NULL DEFAULT now(),
    UNIQUE(tenant_id, quote_number, version)
);

CREATE INDEX IF NOT EXISTS idx_crm_quotes_tenant ON crm_quotes(tenant_id);
CREATE INDEX IF NOT EXISTS idx_crm_quotes_deal ON crm_quotes(deal_id);
CREATE INDEX IF NOT EXISTS idx_crm_quotes_account ON crm_quotes(account_id);
CREATE INDEX IF NOT EXISTS idx_crm_quotes_status ON crm_quotes(status);

-- Quote line items
CREATE TABLE IF NOT EXISTS crm_quote_lines (
    id              serial PRIMARY KEY,
    quote_id        int NOT NULL REFERENCES crm_quotes(id) ON DELETE CASCADE,
    product_id      int REFERENCES crm_products(id) ON DELETE SET NULL,
    sku             text,                        -- snapshot at quote time
    description     text NOT NULL,
    quantity        numeric(12,2) NOT NULL DEFAULT 1,
    unit_price      numeric(14,2) NOT NULL,
    discount_pct    numeric(5,2) DEFAULT 0,
    line_total      numeric(14,2) NOT NULL,
    tax_pct         numeric(5,2) DEFAULT 0,
    sort_order      int NOT NULL DEFAULT 0
);

CREATE INDEX IF NOT EXISTS idx_quote_lines_quote ON crm_quote_lines(quote_id);

-- Auto-calculate totals on line change
CREATE OR REPLACE FUNCTION recalc_quote_totals() RETURNS trigger AS $$
DECLARE
    q_id int;
BEGIN
    q_id := COALESCE(NEW.quote_id, OLD.quote_id);
    UPDATE crm_quotes q SET
        subtotal = COALESCE((SELECT SUM(line_total) FROM crm_quote_lines WHERE quote_id = q_id), 0),
        updated_at = NOW()
    WHERE q.id = q_id;
    -- Total = subtotal * (1 - discount_pct/100) * (1 + tax_pct/100)
    UPDATE crm_quotes SET
        discount_amount = subtotal * discount_pct / 100,
        tax_amount = (subtotal - (subtotal * discount_pct / 100)) * tax_pct / 100,
        total = (subtotal - (subtotal * discount_pct / 100)) * (1 + tax_pct / 100)
    WHERE id = q_id;
    RETURN COALESCE(NEW, OLD);
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_quote_lines_totals ON crm_quote_lines;
CREATE TRIGGER trg_quote_lines_totals
    AFTER INSERT OR UPDATE OR DELETE ON crm_quote_lines
    FOR EACH ROW EXECUTE FUNCTION recalc_quote_totals();
