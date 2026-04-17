# Reference: Warehousing (legacy TotalLink)

> Captured 2026-04-17 from `source/TIG.TotalLink.Server` before that tree was removed.
> Companion to [REFERENCE_INVENTORY_STOCK_MODEL.md](REFERENCE_INVENTORY_STOCK_MODEL.md) —
> that doc covers stock; this one covers the warehouse/bin hierarchy and what's
> *missing* from the legacy model if Central ever adds a proper WMS.
> See [LEGACY_MIGRATION.md](LEGACY_MIGRATION.md) for context.

## 1. TL;DR

The legacy code has **warehouses and bins as first-class entities, but not much else of what you'd call a WMS.** Everything above the bin (warehouse-level on-hand, reservations, cycle counts, transfers, zones, pick routes, put-away rules, UOM conversions) is either derived at read time from the stock ledger or missing entirely.

If Central builds a warehousing module later, this doc tells you what the legacy shape is, what you can take as-is, and what you should design from scratch.

## 2. Hierarchy

```
WarehouseLocation       (the warehouse — Name, 10-char Code, optional CRM Address)
   │ 1:N
   ▼
BinLocation             (the slot inside a warehouse — IsleCode, BinCode)
   │ 1:N
   ▼
PhysicalStock           (one row per (Sku, Bin, StockType) — the quantity ledger)
```

**Everything above `PhysicalStock` is derived, not stored.** "On hand by warehouse" is `SUM(PhysicalStock.Quantity) GROUP BY Bin.Warehouse` at query time.

## 3. Entities present

### `WarehouseLocation`
```csharp
public string Name;
public string Code;          // <= 10 chars — used in external-system contracts
public Address Address;      // cross-module FK to Crm.Address (optional)
// BinLocations [Aggregated] — cascade delete (hazard — see §7)
```

### `BinLocation`
```csharp
public string Name;
public string IsleCode;      // note spelling — legacy field name
public string BinCode;
public WarehouseLocation WarehouseLocation;
// PhysicalStocks [Aggregated] — cascade delete (hazard — see §7)
```

No uniqueness constraint on `(WarehouseLocation, IsleCode, BinCode)` in the legacy schema — the UI enforces it, the DB doesn't.

### `PhysicalStock`
Covered in [REFERENCE_INVENTORY_STOCK_MODEL.md §3](REFERENCE_INVENTORY_STOCK_MODEL.md). Relevant here: the bin is the finest-grained location — there is no sub-bin concept (no "location within bin"), no serial or lot tracking, no pallet/LPN.

## 4. What's *not* in the legacy model

Things a real WMS has that TotalLink doesn't:

| Feature | Legacy state | Notes |
|---------|--------------|-------|
| Warehouse-level on-hand aggregate | derived at query time | No `SkuWarehouse` table despite the task hypothesising one. Fine at small scale; doesn't scale. |
| Reservations | derived at query time | No `Reservation` table. `CommittedStock = Σ PickItem.Quantity` over open deliveries. Over-allocation is *hidden*, not prevented: `AvailableStock = Max(Quantity - CommittedStock, 0)`. |
| Cycle counts / stocktake | absent | "Missing" and "Damaged" are just adjustment reason codes applied row-by-row. No count session, no variance report, no blind-count workflow. |
| Transfers (warehouse → warehouse) | absent as entity | A transfer is two `StockAdjustment` rows with opposing signs. No transfer order, no in-transit state. |
| Zones / areas inside a warehouse | absent | `IsleCode` is just a string on the bin. No zone entity, no per-zone rules. |
| Pick routes / pick sequence | absent | Bins have no ordinal within the warehouse; the picker decides the path. |
| Put-away rules | absent | Receiving picks the target bin manually. No rule engine for "SKU X always goes to zone Y". |
| Unit-of-measure conversion | absent | `PhysicalStock.Quantity` is an `int`. No eaches-vs-cases, no pack size. |
| Serial / lot / batch tracking | absent | No way to trace a specific unit. |
| Pallets / LPNs / licence plates | absent | |
| Temperature zones / hazard classes | absent | |
| Dock / loading-bay management | absent | |
| Receiving vs put-away split | absent | Receiving creates `PhysicalStock` directly into the final bin; no "receiving bay" intermediate state. |
| Cross-docking | absent | |
| Wave picking | absent | Picks are per-delivery. |

**If Central adds warehousing and any of these matter, design them into the schema on day one** — retrofitting serial tracking or UOM conversion onto an `int` quantity ledger is painful.

## 5. Opinionated PG rebuild sketch

If Central eventually needs real warehousing, this is the shape I'd start from. Use it as the skeleton and layer in the WMS features from §4 that actually matter for the use case.

```sql
-- Warehouses
CREATE TABLE warehouses (
    id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id     uuid NOT NULL,
    code          text NOT NULL,                     -- short code for external systems
    name          text NOT NULL,
    address_id    int REFERENCES addresses(id),      -- polymorphic address system
    is_active     boolean NOT NULL DEFAULT true,
    created_at    timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, code)
);

-- Zones (optional — add when you actually need them, not preemptively)
CREATE TABLE warehouse_zones (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    warehouse_id    uuid NOT NULL REFERENCES warehouses(id) ON DELETE CASCADE,
    code            text NOT NULL,
    name            text NOT NULL,
    zone_type       text,                            -- receiving, putaway, pick, ship, quarantine, returns
    UNIQUE (warehouse_id, code)
);

-- Bins
CREATE TABLE bin_locations (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    warehouse_id    uuid NOT NULL REFERENCES warehouses(id),    -- no cascade
    zone_id         uuid REFERENCES warehouse_zones(id),
    aisle_code      text NOT NULL,                   -- fixed spelling (legacy: IsleCode)
    bin_code        text NOT NULL,
    pick_sequence   int,                             -- for pick-route ordering
    is_active       boolean NOT NULL DEFAULT true,
    UNIQUE (warehouse_id, aisle_code, bin_code)
);

-- Stock ledger (see REFERENCE_INVENTORY_STOCK_MODEL for full treatment)
CREATE TABLE physical_stock (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    sku_id          uuid NOT NULL REFERENCES skus(id),
    bin_id          uuid NOT NULL REFERENCES bin_locations(id),
    stock_type_id   int NOT NULL REFERENCES physical_stock_types(id),
    quantity        numeric(14,4) NOT NULL DEFAULT 0,  -- numeric, not int — leaves room for UOM
    updated_at      timestamptz NOT NULL DEFAULT now(),
    UNIQUE (sku_id, bin_id, stock_type_id),           -- legacy missed this — enforce it
    CHECK (quantity >= 0)                             -- prevent negative — legacy missed this too
);

CREATE INDEX ix_physical_stock_bin ON physical_stock (bin_id);
CREATE INDEX ix_physical_stock_sku ON physical_stock (sku_id);

-- Warehouse-level aggregate as a materialised view if query-time SUM gets slow
CREATE MATERIALIZED VIEW mv_sku_warehouse_stock AS
SELECT
    ps.sku_id,
    bl.warehouse_id,
    ps.stock_type_id,
    SUM(ps.quantity) AS on_hand_qty
FROM physical_stock ps
JOIN bin_locations bl ON bl.id = ps.bin_id
GROUP BY ps.sku_id, bl.warehouse_id, ps.stock_type_id;

CREATE UNIQUE INDEX ix_mv_sku_warehouse_stock
    ON mv_sku_warehouse_stock (sku_id, warehouse_id, stock_type_id);
```

Reservations and transfer orders get their own tables when you actually need them — don't preemptively model them off this reference. They're where real WMS complexity lives and the schema choices depend heavily on what your fulfilment flow looks like.

## 6. Locking during adjustment / transfer

Legacy does its adjustment in an XPO unit of work with a 10-attempt optimistic retry loop ([REFERENCE_SALES_ORDER_RELEASE.md §7](REFERENCE_SALES_ORDER_RELEASE.md) covers the same pattern).

For PG, prefer:

```sql
BEGIN;
-- Lock the two affected stock rows in a stable order (by id) to avoid deadlock
SELECT id, quantity FROM physical_stock
WHERE id IN ($source_ps_id, $target_ps_id)
ORDER BY id
FOR UPDATE;

UPDATE physical_stock SET quantity = quantity - $qty WHERE id = $source_ps_id;
UPDATE physical_stock SET quantity = quantity + $qty WHERE id = $target_ps_id;

INSERT INTO stock_adjustments (...) VALUES (...);
COMMIT;
```

No retry loop needed — `FOR UPDATE` queues on conflict instead of throwing. For hot bins (lots of concurrent writers) a `pg_advisory_xact_lock(hashtext(bin_code))` keyed to the bin is cheaper than locking the ledger rows themselves.

## 7. Things to specifically NOT carry forward

- **`[Aggregated]` cascade from `WarehouseLocation → BinLocation → PhysicalStock`.** In legacy, deleting a warehouse silently deletes every stock row in it. Replace with `ON DELETE RESTRICT` or soft-delete with a `deleted_at` column.
- **No unique constraint on `(warehouse, aisle, bin)`.** The UI enforces it; the DB shrugs. Add the constraint.
- **No unique constraint on `(sku, bin, stock_type)`.** Combined with no row locking, this is a real race hazard — two concurrent adjustments can create two rows for the same triple and leave the totals wrong. Add the constraint.
- **Integer quantities.** `numeric(14,4)` costs nothing and makes UOM conversions and weight-based items tractable later.
- **`Max(qty - committed, 0)` hiding over-allocation.** If `committed > qty` something has already gone wrong; expose it, don't paper over it.
- **`IsleCode` field name.** Spell it `aisle_code` in the rebuild.
- **Code ≤ 10 chars.** That limit was Navision-compat. Drop it unless we have an external system still demanding it — use `text` like everywhere else.
- **Zero-qty `PhysicalStock` rows never cleaned up.** Either delete on `quantity = 0` or ignore them in reads with a partial index: `CREATE INDEX ix_ps_nonzero ON physical_stock (sku_id, bin_id) WHERE quantity > 0`.

## 8. When you actually build this

Before any code, answer these:

1. **Do we need serial/lot/batch tracking?** If yes, the quantity ledger changes shape — you track individual units, not sums.
2. **Do we need UOM conversion?** If yes, `numeric` quantity + a `uom_id` column + a conversion table.
3. **What's our receiving flow?** Single-step (straight into pick bin) or two-step (receiving bay → put-away)?
4. **Are reservations soft (warn on over-allocation) or hard (reject)?** Legacy was neither — it just hid them. Pick a side.
5. **Do we need transfer orders as first-class entities** (with in-transit state, tracking, dates), or are adjustment pairs enough?
6. **Warehouse-level on-hand: query-time SUM or materialised?** Depends on row counts. Start with a view; promote to materialised view with triggers if hot.
7. **Cycle counts: blind or informed?** Blind counts (the counter doesn't see the expected qty) catch more errors but are slower.

None of the answers are in the legacy code — they're product decisions. Make them before the schema goes in.
