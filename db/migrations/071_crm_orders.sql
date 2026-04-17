-- =============================================================================
-- Stage 3.10: Orders (firm commitments post-quote-acceptance)
-- =============================================================================

CREATE TABLE IF NOT EXISTS crm_orders (
    id                  serial PRIMARY KEY,
    tenant_id           uuid,
    order_number        text NOT NULL,
    quote_id            int REFERENCES crm_quotes(id) ON DELETE SET NULL,
    deal_id             int REFERENCES crm_deals(id) ON DELETE SET NULL,
    account_id          int NOT NULL REFERENCES crm_accounts(id),
    contact_id          int REFERENCES contacts(id) ON DELETE SET NULL,
    contract_id         int REFERENCES crm_contracts(id) ON DELETE SET NULL,
    status              text NOT NULL DEFAULT 'draft',-- draft, submitted, approved, fulfilled, invoiced, cancelled
    -- Totals
    subtotal            numeric(14,2) NOT NULL DEFAULT 0,
    discount_amount     numeric(14,2) DEFAULT 0,
    tax_amount          numeric(14,2) DEFAULT 0,
    total               numeric(14,2) NOT NULL DEFAULT 0,
    currency            char(3) NOT NULL DEFAULT 'GBP',
    -- Dates
    order_date          date NOT NULL DEFAULT CURRENT_DATE,
    fulfilled_at        timestamptz,
    invoiced_at         timestamptz,
    -- Addresses
    billing_address_id  int REFERENCES addresses(id),
    shipping_address_id int REFERENCES addresses(id),
    po_number           text,                          -- customer's purchase order reference
    notes               text,
    owner_id            int REFERENCES app_users(id),
    invoice_id          int,                           -- link to central_platform.invoices once issued
    created_at          timestamptz NOT NULL DEFAULT now(),
    updated_at          timestamptz NOT NULL DEFAULT now(),
    UNIQUE(tenant_id, order_number)
);

CREATE INDEX IF NOT EXISTS idx_orders_account ON crm_orders(account_id);
CREATE INDEX IF NOT EXISTS idx_orders_quote ON crm_orders(quote_id);
CREATE INDEX IF NOT EXISTS idx_orders_status ON crm_orders(status);
CREATE INDEX IF NOT EXISTS idx_orders_date ON crm_orders(order_date);

-- Order lines (copied from quote lines at order time)
CREATE TABLE IF NOT EXISTS crm_order_lines (
    id                  serial PRIMARY KEY,
    order_id            int NOT NULL REFERENCES crm_orders(id) ON DELETE CASCADE,
    product_id          int REFERENCES crm_products(id),
    bundle_id           int REFERENCES crm_product_bundles(id),
    subscription_id     int REFERENCES crm_subscriptions(id), -- if this line is a subscription
    sku                 text,
    description         text NOT NULL,
    quantity            numeric(12,2) NOT NULL DEFAULT 1,
    unit_price          numeric(14,2) NOT NULL,
    discount_pct        numeric(5,2) DEFAULT 0,
    line_total          numeric(14,2) NOT NULL,
    tax_pct             numeric(5,2) DEFAULT 0,
    -- Fulfillment
    fulfillment_status  text DEFAULT 'pending',       -- pending, shipped, delivered, delayed
    fulfilled_quantity  numeric(12,2) DEFAULT 0,
    fulfilled_at        timestamptz,
    -- Revenue recognition link
    revenue_schedule_id int REFERENCES crm_revenue_schedules(id),
    sort_order          int NOT NULL DEFAULT 0
);

CREATE INDEX IF NOT EXISTS idx_order_lines_order ON crm_order_lines(order_id);
CREATE INDEX IF NOT EXISTS idx_order_lines_product ON crm_order_lines(product_id);

-- Auto-recalc totals on line change
CREATE OR REPLACE FUNCTION recalc_order_totals() RETURNS trigger AS $$
DECLARE
    o_id int;
BEGIN
    o_id := COALESCE(NEW.order_id, OLD.order_id);
    UPDATE crm_orders SET
        subtotal = COALESCE((SELECT SUM(line_total) FROM crm_order_lines WHERE order_id = o_id), 0),
        total = COALESCE((SELECT SUM(line_total * (1 + COALESCE(tax_pct,0)/100)) FROM crm_order_lines WHERE order_id = o_id), 0)
              - COALESCE(discount_amount, 0),
        updated_at = NOW()
    WHERE id = o_id;
    RETURN COALESCE(NEW, OLD);
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_order_line_recalc ON crm_order_lines;
CREATE TRIGGER trg_order_line_recalc
    AFTER INSERT OR UPDATE OR DELETE ON crm_order_lines
    FOR EACH ROW EXECUTE FUNCTION recalc_order_totals();

-- Auto-generate subscription from order line when product is_recurring
CREATE OR REPLACE FUNCTION create_subscription_from_order_line() RETURNS trigger AS $$
DECLARE
    prod_recurring boolean;
    prod_billing text;
    new_sub_id int;
    ord_account_id int;
    ord_contract_id int;
BEGIN
    IF NEW.product_id IS NULL OR NEW.subscription_id IS NOT NULL THEN RETURN NEW; END IF;

    SELECT p.is_recurring, p.billing_period INTO prod_recurring, prod_billing
    FROM crm_products p WHERE p.id = NEW.product_id;

    IF NOT COALESCE(prod_recurring, false) THEN RETURN NEW; END IF;

    SELECT account_id, contract_id INTO ord_account_id, ord_contract_id
    FROM crm_orders WHERE id = NEW.order_id;

    INSERT INTO crm_subscriptions
        (account_id, contract_id, product_id, subscription_number, name,
         quantity, unit_price, mrr, arr, billing_period, start_date)
    VALUES (ord_account_id, ord_contract_id, NEW.product_id,
            'SUB-' || NEW.order_id || '-' || NEW.id,
            NEW.description,
            NEW.quantity, NEW.unit_price,
            CASE prod_billing
                WHEN 'monthly' THEN NEW.line_total
                WHEN 'annual'  THEN NEW.line_total / 12.0
                WHEN 'quarterly' THEN NEW.line_total / 3.0
                ELSE NEW.line_total END,
            CASE prod_billing
                WHEN 'monthly' THEN NEW.line_total * 12
                WHEN 'annual'  THEN NEW.line_total
                WHEN 'quarterly' THEN NEW.line_total * 4
                ELSE NEW.line_total * 12 END,
            COALESCE(prod_billing, 'monthly'), CURRENT_DATE)
    RETURNING id INTO new_sub_id;

    UPDATE crm_order_lines SET subscription_id = new_sub_id WHERE id = NEW.id;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_order_line_subscription ON crm_order_lines;
CREATE TRIGGER trg_order_line_subscription
    AFTER INSERT ON crm_order_lines
    FOR EACH ROW EXECUTE FUNCTION create_subscription_from_order_line();

-- Webhook events for new entities
INSERT INTO webhook_event_types (event_type, category, description) VALUES
    ('crm.contract.signed',          'crm', 'Contract has been signed'),
    ('crm.contract.renewing',        'crm', 'Contract entered renewal window'),
    ('crm.contract.expired',         'crm', 'Contract expired without renewal'),
    ('crm.subscription.upgraded',    'crm', 'Subscription upgraded (MRR increase)'),
    ('crm.subscription.downgraded',  'crm', 'Subscription downgraded'),
    ('crm.subscription.cancelled',   'crm', 'Subscription cancelled (churn)'),
    ('crm.order.submitted',          'crm', 'Order submitted'),
    ('crm.order.fulfilled',          'crm', 'Order fulfilled'),
    ('crm.approval.requested',       'crm', 'Approval requested'),
    ('crm.approval.approved',        'crm', 'Approval granted'),
    ('crm.approval.rejected',        'crm', 'Approval rejected'),
    ('crm.campaign.launched',        'crm', 'Campaign activated'),
    ('crm.lead.from_form',           'crm', 'Lead created from public form submission'),
    ('crm.quota.attained',           'crm', 'Quota attained for period'),
    ('crm.deal.stalled',             'crm', 'Deal flagged as stalled'),
    ('crm.deal.slipping',            'crm', 'Deal close date has slipped')
ON CONFLICT (event_type) DO NOTHING;
