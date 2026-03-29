-- Add BGP sync tracking and extra config columns
ALTER TABLE bgp_config ADD COLUMN IF NOT EXISTS fast_external_failover BOOLEAN DEFAULT FALSE;
ALTER TABLE bgp_config ADD COLUMN IF NOT EXISTS bestpath_multipath_relax BOOLEAN DEFAULT FALSE;
ALTER TABLE bgp_config ADD COLUMN IF NOT EXISTS redistribute_connected BOOLEAN DEFAULT TRUE;
ALTER TABLE bgp_config ADD COLUMN IF NOT EXISTS last_synced TIMESTAMPTZ;
