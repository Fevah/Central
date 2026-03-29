-- 012_config_versions.sql
-- Add version numbering to running_configs for config sync history

ALTER TABLE running_configs
  ADD COLUMN IF NOT EXISTS version_num INT DEFAULT 1,
  ADD COLUMN IF NOT EXISTS operator    TEXT DEFAULT '';

-- Back-fill existing rows with sequential version numbers per switch
WITH numbered AS (
  SELECT id, ROW_NUMBER() OVER (PARTITION BY switch_id ORDER BY downloaded_at) AS rn
  FROM running_configs
)
UPDATE running_configs rc
SET version_num = n.rn
FROM numbered n
WHERE rc.id = n.id;

-- Index for fast version listing
CREATE INDEX IF NOT EXISTS idx_running_configs_version
  ON running_configs(switch_id, version_num DESC);
