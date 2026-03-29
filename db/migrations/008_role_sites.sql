-- =============================================================================
-- 008_role_sites.sql — Per-role site/building access control
-- =============================================================================

CREATE TABLE IF NOT EXISTS role_sites (
    id          SERIAL PRIMARY KEY,
    role        VARCHAR(50) NOT NULL,
    building    VARCHAR(100) NOT NULL,
    allowed     BOOLEAN NOT NULL DEFAULT TRUE,
    UNIQUE (role, building)
);

-- Seed: Admin gets access to all existing buildings
INSERT INTO role_sites (role, building, allowed)
SELECT 'Admin', DISTINCT_BUILDINGS.building, TRUE
FROM (SELECT DISTINCT building FROM switch_guide ORDER BY building) AS DISTINCT_BUILDINGS
ON CONFLICT (role, building) DO NOTHING;

-- Seed: Operator and Viewer also get all buildings by default
INSERT INTO role_sites (role, building, allowed)
SELECT r.name, b.building, TRUE
FROM roles r
CROSS JOIN (SELECT DISTINCT building FROM switch_guide) b
WHERE r.name IN ('Operator', 'Viewer')
ON CONFLICT (role, building) DO NOTHING;
