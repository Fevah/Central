-- Migration 050: Webhook receiver log
-- Stores inbound webhook payloads from external systems for processing.

CREATE TABLE IF NOT EXISTS webhook_log (
    id              bigserial PRIMARY KEY,
    source          varchar(64) NOT NULL,
    headers         jsonb DEFAULT '{}',
    payload         jsonb NOT NULL DEFAULT '{}',
    received_at     timestamptz NOT NULL DEFAULT now(),
    processed       boolean DEFAULT false,
    processed_at    timestamptz,
    error_message   text
);

CREATE INDEX IF NOT EXISTS idx_webhook_log_source ON webhook_log(source, received_at DESC);

-- pg_notify trigger
DROP TRIGGER IF EXISTS trg_notify_webhook_log ON webhook_log;
CREATE TRIGGER trg_notify_webhook_log AFTER INSERT OR UPDATE OR DELETE ON webhook_log
    FOR EACH ROW EXECUTE FUNCTION notify_data_change();
