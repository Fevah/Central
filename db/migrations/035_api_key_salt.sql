-- Add per-key salt to api_keys for salted hash storage
ALTER TABLE api_keys ADD COLUMN IF NOT EXISTS key_salt TEXT NOT NULL DEFAULT '';
