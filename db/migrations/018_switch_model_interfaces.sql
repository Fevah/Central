-- Maps switch models to their interface layout
-- Different switch models have different port counts and types
CREATE TABLE IF NOT EXISTS switch_model_interfaces (
    id          SERIAL PRIMARY KEY,
    model       TEXT NOT NULL,              -- e.g. "S5860-20SQ", "S5860-48SC"
    interface_name TEXT NOT NULL,            -- e.g. "xe-1/1/1", "ge-1/1/1"
    interface_type TEXT NOT NULL DEFAULT '', -- e.g. "SFP+", "SFP28", "QSFP28", "RJ45"
    speed       TEXT NOT NULL DEFAULT '',    -- e.g. "10G", "25G", "100G", "1G"
    breakout    BOOLEAN NOT NULL DEFAULT FALSE,
    sort_order  INT NOT NULL DEFAULT 0,
    UNIQUE (model, interface_name)
);
CREATE INDEX IF NOT EXISTS idx_model_iface_model ON switch_model_interfaces (model);

-- Seed common FS/PicOS switch models
-- S5860-20SQ: 20x 25G SFP28 + 4x 100G QSFP28
INSERT INTO switch_model_interfaces (model, interface_name, interface_type, speed, sort_order)
SELECT 'S5860-20SQ', 'xe-1/1/' || n, 'SFP28', '25G', n
FROM generate_series(1, 20) AS n
ON CONFLICT DO NOTHING;

INSERT INTO switch_model_interfaces (model, interface_name, interface_type, speed, breakout, sort_order)
SELECT 'S5860-20SQ', 'xe-1/1/' || n, 'QSFP28', '100G', TRUE, n
FROM generate_series(21, 24) AS n
ON CONFLICT DO NOTHING;

-- S5860-48SC: 48x 25G SFP28 + 8x 100G QSFP28
INSERT INTO switch_model_interfaces (model, interface_name, interface_type, speed, sort_order)
SELECT 'S5860-48SC', 'xe-1/1/' || n, 'SFP28', '25G', n
FROM generate_series(1, 48) AS n
ON CONFLICT DO NOTHING;

INSERT INTO switch_model_interfaces (model, interface_name, interface_type, speed, breakout, sort_order)
SELECT 'S5860-48SC', 'xe-1/1/' || n, 'QSFP28', '100G', TRUE, n
FROM generate_series(49, 56) AS n
ON CONFLICT DO NOTHING;

-- S5860-48T4Q: 48x 1G RJ45 + 4x 40G QSFP+
INSERT INTO switch_model_interfaces (model, interface_name, interface_type, speed, sort_order)
SELECT 'S5860-48T4Q', 'ge-1/1/' || n, 'RJ45', '1G', n
FROM generate_series(1, 48) AS n
ON CONFLICT DO NOTHING;

INSERT INTO switch_model_interfaces (model, interface_name, interface_type, speed, breakout, sort_order)
SELECT 'S5860-48T4Q', 'xe-1/1/' || n, 'QSFP+', '40G', TRUE, n
FROM generate_series(49, 52) AS n
ON CONFLICT DO NOTHING;

-- N8560-32C: 32x 100G QSFP28 (spine/core)
INSERT INTO switch_model_interfaces (model, interface_name, interface_type, speed, breakout, sort_order)
SELECT 'N8560-32C', 'xe-1/1/' || n, 'QSFP28', '100G', TRUE, n
FROM generate_series(1, 32) AS n
ON CONFLICT DO NOTHING;

-- N8560-48BC: 48x 25G SFP28 + 8x 100G QSFP28
INSERT INTO switch_model_interfaces (model, interface_name, interface_type, speed, sort_order)
SELECT 'N8560-48BC', 'xe-1/1/' || n, 'SFP28', '25G', n
FROM generate_series(1, 48) AS n
ON CONFLICT DO NOTHING;

INSERT INTO switch_model_interfaces (model, interface_name, interface_type, speed, breakout, sort_order)
SELECT 'N8560-48BC', 'xe-1/1/' || n, 'QSFP28', '100G', TRUE, n
FROM generate_series(49, 56) AS n
ON CONFLICT DO NOTHING;
