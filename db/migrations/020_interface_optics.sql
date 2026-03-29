-- Historical optics diagnostics per interface per sync
-- Keeps all readings for trending TX/RX power over time
CREATE TABLE IF NOT EXISTS interface_optics (
    id              SERIAL PRIMARY KEY,
    switch_id       UUID NOT NULL REFERENCES switches(id) ON DELETE CASCADE,
    captured_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    interface_name  TEXT NOT NULL,
    channel         TEXT NOT NULL DEFAULT '',       -- '' for single-channel, 'C1','C2','C3','C4' for breakout/QSFP
    temp_c          NUMERIC(6,2),                   -- Temperature in Celsius
    temp_f          NUMERIC(6,2),                   -- Temperature in Fahrenheit
    voltage         NUMERIC(6,3),                   -- Voltage in Volts
    bias_ma         NUMERIC(8,3),                   -- Bias current in mA
    tx_power_dbm    NUMERIC(8,3),                   -- Transmit power in dBm
    rx_power_dbm    NUMERIC(8,3),                   -- Receive power in dBm
    module_type     TEXT NOT NULL DEFAULT ''         -- e.g. "100G_BASE_AOC", "10G_BASE_SR", "25G_BASE_LR"
);
CREATE INDEX IF NOT EXISTS idx_optics_switch ON interface_optics (switch_id);
CREATE INDEX IF NOT EXISTS idx_optics_captured ON interface_optics (switch_id, captured_at DESC);
CREATE INDEX IF NOT EXISTS idx_optics_iface ON interface_optics (switch_id, interface_name, captured_at DESC);
