-- Add LLDP neighbor columns to switch_interfaces
ALTER TABLE switch_interfaces ADD COLUMN IF NOT EXISTS lldp_host TEXT NOT NULL DEFAULT '';
ALTER TABLE switch_interfaces ADD COLUMN IF NOT EXISTS lldp_port TEXT NOT NULL DEFAULT '';
