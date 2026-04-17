-- =============================================================================
-- Stage 5.9-5.10: Import wizard + Commerce (cart + checkout + payments)
-- =============================================================================

-- ─── Import Jobs ────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS import_jobs (
    id              serial PRIMARY KEY,
    tenant_id       uuid,
    entity_type     text NOT NULL,                    -- accounts, contacts, leads, deals, custom_*
    source_type     text NOT NULL DEFAULT 'csv',     -- csv, excel, json
    file_path       text,
    filename        text,
    file_size_bytes bigint,
    row_count       int NOT NULL DEFAULT 0,
    status          text NOT NULL DEFAULT 'pending',-- pending, validating, previewing, running, completed, failed, cancelled
    field_mappings  jsonb DEFAULT '{}',               -- {csv_column: db_field}
    dedup_strategy  text NOT NULL DEFAULT 'create_new', -- create_new, update_existing, skip_duplicates, merge
    dedup_match_field text,                            -- which field is the match key
    dry_run         boolean NOT NULL DEFAULT true,    -- preview only
    rows_processed  int NOT NULL DEFAULT 0,
    rows_created    int NOT NULL DEFAULT 0,
    rows_updated    int NOT NULL DEFAULT 0,
    rows_skipped    int NOT NULL DEFAULT 0,
    rows_failed     int NOT NULL DEFAULT 0,
    started_at      timestamptz,
    completed_at    timestamptz,
    error_message   text,
    created_by      int REFERENCES app_users(id),
    created_at      timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_import_jobs_status ON import_jobs(status);
CREATE INDEX IF NOT EXISTS idx_import_jobs_tenant ON import_jobs(tenant_id);

-- Individual row results (for error reporting + rollback)
CREATE TABLE IF NOT EXISTS import_job_rows (
    id              bigserial PRIMARY KEY,
    job_id          int NOT NULL REFERENCES import_jobs(id) ON DELETE CASCADE,
    row_number      int NOT NULL,
    raw_data        jsonb NOT NULL,
    status          text NOT NULL,                    -- valid, invalid, created, updated, skipped, failed
    created_entity_id int,                             -- id of created/updated record
    errors          text[],
    warnings        text[]
);

CREATE INDEX IF NOT EXISTS idx_import_rows_job ON import_job_rows(job_id, row_number);
CREATE INDEX IF NOT EXISTS idx_import_rows_status ON import_job_rows(job_id, status);

-- ─── Commerce: Shopping cart + checkout ─────────────────────────────────────
CREATE TABLE IF NOT EXISTS shopping_carts (
    id              serial PRIMARY KEY,
    tenant_id       uuid,
    portal_user_id  int REFERENCES portal_users(id) ON DELETE CASCADE,
    session_token   text,                              -- anonymous carts
    account_id      int REFERENCES crm_accounts(id) ON DELETE SET NULL,
    currency        char(3) NOT NULL DEFAULT 'GBP',
    subtotal        numeric(14,2) NOT NULL DEFAULT 0,
    discount_amount numeric(14,2) DEFAULT 0,
    tax_amount      numeric(14,2) DEFAULT 0,
    shipping_amount numeric(14,2) DEFAULT 0,
    total           numeric(14,2) NOT NULL DEFAULT 0,
    promo_code      text,
    status          text NOT NULL DEFAULT 'active',   -- active, abandoned, converted, expired
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now(),
    expires_at      timestamptz
);

CREATE INDEX IF NOT EXISTS idx_carts_user ON shopping_carts(portal_user_id);
CREATE INDEX IF NOT EXISTS idx_carts_session ON shopping_carts(session_token);
CREATE INDEX IF NOT EXISTS idx_carts_status ON shopping_carts(status);

CREATE TABLE IF NOT EXISTS cart_items (
    id              serial PRIMARY KEY,
    cart_id         int NOT NULL REFERENCES shopping_carts(id) ON DELETE CASCADE,
    product_id      int REFERENCES crm_products(id),
    bundle_id       int REFERENCES crm_product_bundles(id),
    sku             text,
    name            text NOT NULL,
    quantity        numeric(10,2) NOT NULL DEFAULT 1,
    unit_price      numeric(14,2) NOT NULL,
    discount_pct    numeric(5,2) DEFAULT 0,
    line_total      numeric(14,2) NOT NULL,
    metadata        jsonb DEFAULT '{}',
    added_at        timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_cart_items_cart ON cart_items(cart_id);

-- Auto-recalc cart totals
CREATE OR REPLACE FUNCTION recalc_cart_totals() RETURNS trigger AS $$
DECLARE
    c_id int;
BEGIN
    c_id := COALESCE(NEW.cart_id, OLD.cart_id);
    UPDATE shopping_carts SET
        subtotal = COALESCE((SELECT SUM(line_total) FROM cart_items WHERE cart_id = c_id), 0),
        total = COALESCE((SELECT SUM(line_total) FROM cart_items WHERE cart_id = c_id), 0)
              + COALESCE(tax_amount, 0) + COALESCE(shipping_amount, 0)
              - COALESCE(discount_amount, 0),
        updated_at = NOW()
    WHERE id = c_id;
    RETURN COALESCE(NEW, OLD);
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_cart_items_totals ON cart_items;
CREATE TRIGGER trg_cart_items_totals
    AFTER INSERT OR UPDATE OR DELETE ON cart_items
    FOR EACH ROW EXECUTE FUNCTION recalc_cart_totals();

-- ─── Payments ──────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS payments (
    id              serial PRIMARY KEY,
    tenant_id       uuid,
    payment_number  text NOT NULL,
    -- Link to one of: order, invoice, cart
    order_id        int REFERENCES crm_orders(id),
    invoice_id      int,                               -- central_platform.invoices
    cart_id         int REFERENCES shopping_carts(id),
    account_id      int REFERENCES crm_accounts(id),
    portal_user_id  int REFERENCES portal_users(id),
    amount          numeric(14,2) NOT NULL,
    currency        char(3) NOT NULL DEFAULT 'GBP',
    status          text NOT NULL DEFAULT 'pending', -- pending, processing, succeeded, failed, refunded, disputed
    payment_method  text NOT NULL,                    -- card, bank, po, wire, manual
    -- Stripe-compatible fields
    stripe_payment_intent_id text,
    stripe_charge_id text,
    last4           text,
    brand           text,
    -- Failure info
    failure_code    text,
    failure_message text,
    -- Timing
    authorized_at   timestamptz,
    captured_at     timestamptz,
    refunded_at     timestamptz,
    created_at      timestamptz NOT NULL DEFAULT now(),
    UNIQUE(tenant_id, payment_number)
);

CREATE INDEX IF NOT EXISTS idx_payments_order ON payments(order_id);
CREATE INDEX IF NOT EXISTS idx_payments_cart ON payments(cart_id);
CREATE INDEX IF NOT EXISTS idx_payments_status ON payments(status);
CREATE INDEX IF NOT EXISTS idx_payments_stripe ON payments(stripe_payment_intent_id);

-- Record seed migration
INSERT INTO schema_versions (version_number, description)
VALUES ('075_import_commerce', 'Import wizard + commerce cart + payments')
ON CONFLICT (version_number) DO NOTHING;
