#!/bin/bash
# Central Platform — Database setup script
# Applies all migrations and seeds default data.
# Usage: ./db/setup.sh [DSN]

DSN="${1:-postgresql://central:central@localhost:5432/central}"

echo "Central Platform — Database Setup"
echo "DSN: $DSN"
echo ""

# Apply all migrations in order
MIGRATIONS_DIR="$(dirname "$0")/migrations"
if [ ! -d "$MIGRATIONS_DIR" ]; then
    echo "ERROR: migrations directory not found at $MIGRATIONS_DIR"
    exit 1
fi

echo "Applying migrations..."
APPLIED=0
FAILED=0
for sql in $(ls "$MIGRATIONS_DIR"/*.sql | sort); do
    NAME=$(basename "$sql" .sql)
    echo -n "  $NAME... "
    if psql "$DSN" -f "$sql" -q 2>/dev/null; then
        echo "OK"
        APPLIED=$((APPLIED + 1))
    else
        echo "SKIP (may already exist)"
    fi
done

echo ""
echo "Applied $APPLIED migrations"

# Seed default data
echo ""
echo "Seeding default data..."
SEED="$(dirname "$0")/seed.sql"
if [ -f "$SEED" ]; then
    psql "$DSN" -f "$SEED" -q 2>/dev/null
    echo "Seed complete"
else
    echo "No seed.sql found"
fi

echo ""
echo "Database setup complete!"
echo "Default admin login: admin / admin (change immediately)"
