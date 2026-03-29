-- Migration 035: Add resolved_at column to sd_requests
-- Captures ManageEngine's completed_time — the ACTUAL resolution timestamp.
-- Previously synced_at was used which is wrong (it's when WE synced, not when ME resolved).

BEGIN;

ALTER TABLE sd_requests ADD COLUMN IF NOT EXISTS resolved_at TIMESTAMPTZ;
ALTER TABLE sd_requests ADD COLUMN IF NOT EXISTS me_completed_time BIGINT;

-- Backfill: for already-synced resolved/closed tickets, estimate resolved_at from me_updated_time
-- (best approximation — the update that set status to Resolved/Closed)
UPDATE sd_requests
SET resolved_at = to_timestamp(me_updated_time / 1000.0),
    me_completed_time = me_updated_time
WHERE status IN ('Resolved', 'Closed')
  AND resolved_at IS NULL
  AND me_updated_time IS NOT NULL;

CREATE INDEX IF NOT EXISTS idx_sd_requests_resolved_at ON sd_requests (resolved_at DESC);

COMMIT;
