# Reference: Inventory Stock Model (legacy TotalLink)

> Captured 2026-04-17 from `source/TIG.TotalLink.Server` before that tree was removed.
> Not a spec for Central's Inventory module — a reference so whoever builds it
> doesn't redesign blind. See [docs/LEGACY_MIGRATION.md](LEGACY_MIGRATION.md) for context.
>
> Legacy stack: DevExpress XPO ORM (XAF-style), partial-class `.Designer.cs` + hand-written `.cs`,
> WCF method services (`InventoryMethodService.svc`), Autofac module registration.
> Domain is apparel (`Style` + `Colour` + `Size` = `Sku`) — expect clothing vocabulary in the model.

---

## 1. Domain vocabulary

| Central term | Legacy entity / field | Notes |
|---|---|---|
| Product family | `Style` | Apparel concept: a garment design. Has `Code`, `Gender`, `Category`, `ProductType`, `Fabric`, `Fit`, `SizeRange`, `Season`, long description, web fields, `Reference` (auto-seq). |
| SKU (stock-keeping unit) | `Sku` | The priced, barcoded thing. Unique combination of `Style` + `Size` + `Colour` + `Season` + `BusinessDivision`. Has `UnitPrice`, `UnitCost`, `CostingMethod`, `Barcode`, `Parent` SKU (optional variants), web imagery, GST/product posting groups. |
| Price break | `PriceRange` | Volume pricing on a SKU: `MinimumQuantity`, `DirectUnitCost`, `UnitPrice`. Multiple rows per SKU. |
| Warehouse | `WarehouseLocation` | `Name`, `Code` (<=10 chars), optional `Address` (CRM). Owns its bins (aggregated). |
| Bin | `BinLocation` | `Name`, `IsleCode`, `BinCode`, FK → `WarehouseLocation`. Aggregated under the warehouse (cascade delete). |
| Physical stock (on-hand row) | `PhysicalStock` | The **quantity ledger**. One row per `(Sku, BinLocation, PhysicalStockType)`. Qty is an `int`. |
| Stock type | `PhysicalStockType` | Lookup (seeded: General / Seasonal / Promotional). Segregates inventory for accounting/allocation. |
| Stock adjustment | `StockAdjustment` | An immutable audit row describing a movement / correction. **Never deleted** (the list VM nulls out DeleteCommand). |
| Adjustment reason | `StockAdjustmentReason` | Lookup with two bool flags that drive propagation: `IsTargetIncrease`, `IsSourceIncrease` (nullable). |
| Reservation / commitment | `PickItem` (sale module) | **There is no Reservation table.** Reservations are derived from open `DeliveryItem`/`PickItem` rows whose `Delivery.Status.IsStockAdjusted = 0`. |
| Pick | `PickItem` | A picker's intent to take qty from a specific `(Sku, BinLocation, PhysicalStockType)` to fulfil a delivery. Cross-module FK (Sale → Inventory). |
| Receiving | (no dedicated entity) | Inbound stock comes in as a `StockAdjustment` with reason "Migrated" or "New" + a vendor reference (con-note). |
| Cycle count / stocktake | **not present** | No `CycleCount`, `StockTake`, or count-variance entity exists. Reconciliation happens by manual `StockAdjustment` rows with reasons "Missing" / "Damaged". |
| Unit of measure | `UnitOfMeasure` | Lookup with `Code` + `Name`. SKU carries **two** UoM refs: `PackUnitOfMeasure` and `ItemUnitOfMeasure`. No conversion factor table — UoM is informational only. |
| Barcode | `Barcode` + `BarcodeType` | One SKU → one barcode (FK on SKU side). Barcode has `Number` + `BarcodeType` lookup. |

---

## 2. Entity model

### `Sku`
The priced, barcoded unit. `TIG.TotalLink.Shared.DataModel.Inventory.Sku`, base `StampedDataObjectBase` (gives `Oid` Guid, `CreatedDate`, `ModifiedDate`, `CreatedBy`, `ModifiedBy`).

Key fields: `Name` (150), `UnitPrice`, `UnitCost`, `CostingMethod` enum, `ReplenishmentSystem` enum, `ReorderingPolicy` enum, `IncludeInventory` bool, `ReschedulingPeriod`/`LotAccumulationPeriod` (stringly-typed period expressions — DX pattern, e.g. "1M"), `PriceIncludesGST`, `AllowLineDiscount`, `Web_*` e-commerce fields, `Reference` long (auto-seq code 31), `LegacyReference` string.

Relationships (all 1:N from Sku unless noted):
- `Style` (N:1) — mandatory product family
- `Size`, `Colour`, `BusinessDivision`, `Season` — N:1 lookups
- `PackUnitOfMeasure`, `ItemUnitOfMeasure` — N:1 × 2
- `Barcode` — 1:1 (FK on Sku side)
- `Country` — N:1 (country of origin)
- `InventoryPostingGroup`, `GeneralProductPostingGroup`, `GSTProductPostingGroup` — N:1 accounting categorisation (`PostingGroup` lives in `Admin` module)
- `Parent` Sku + `Children` — self-referencing tree for variants
- `PriceRanges` 1:N, `PhysicalStocks` 1:N

Computed (non-persisted aggregates):
```csharp
[PersistentAlias("Iif(PhysicalStocks.Exists, PhysicalStocks.Sum(Quantity), 0)")]
public int PhysicalStock { get; }

// CommittedStock at the SKU level is a cross-module join to open DeliveryItems:
[PersistentAlias("Iif([<DeliveryItem>][Delivery.Status.IsStockAdjusted = 0 AND ^.Oid = Sku].Exists,
                     [<DeliveryItem>][Delivery.Status.IsStockAdjusted = 0 AND ^.Oid = Sku].Sum(Quantity), 0)")]
public int CommittedStock { get; }

[PersistentAlias("PhysicalStock - CommittedStock")]
public int StockOnHand { get; }

[PersistentAlias("Max(StockOnHand, 0)")]
public int AvailableStock { get; }
```

**In a PG rebuild**: `skus` table, BIGSERIAL `reference` as the user-facing code, UUID PK. Keep `parent_sku_id` self-FK. Posting groups and most lookups become small reference tables. Computed columns become views or generated columns. `Reference` should be a PG sequence or IDENTITY, not hand-rolled like the XML seed file.

### `PhysicalStock`
The on-hand ledger. **This is the row that matters.**

```csharp
public partial class PhysicalStock : StampedDataObjectBase {
    [Indexed("PhysicalStockType;BinLocation", Name = "IX_PhysicalStock")]
    public Sku Sku;
    public PhysicalStockType PhysicalStockType;
    public int Quantity;
    public BinLocation BinLocation;
}
```

Cardinality: **one row per unique `(Sku, BinLocation, PhysicalStockType)` triple** — enforced by business logic (see §4), not a DB unique constraint. The index is `(Sku, PhysicalStockType, BinLocation)`.

Computed:
- `CommittedStock` — sum of `PickItem.Quantity` for open deliveries (`IsStockAdjusted = 0`) matching this exact (Sku, Bin, StockType)
- `StockOnHand = Quantity - CommittedStock`
- `AvailableStock = Max(StockOnHand, 0)` (never shows negative on the UI)

Relationship to `BinLocation` is `[Aggregated]` — deleting a bin cascades its `PhysicalStock` rows. **This is dangerous** (see §9).

**In a PG rebuild**:
```sql
CREATE TABLE physical_stock (
  id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  sku_id        uuid NOT NULL REFERENCES skus(id),
  bin_id        uuid NOT NULL REFERENCES bin_locations(id),
  stock_type_id uuid NOT NULL REFERENCES physical_stock_types(id),
  quantity      integer NOT NULL CHECK (quantity >= 0),
  created_at timestamptz, modified_at timestamptz,
  created_by, modified_by,
  UNIQUE (sku_id, bin_id, stock_type_id)
);
CREATE INDEX ix_physical_stock_sku_type_bin ON physical_stock (sku_id, stock_type_id, bin_id);
```
Add the unique constraint the legacy code *assumed but never enforced*. Keep qty as integer — legacy has no decimal quantities anywhere.

### `BinLocation`
Physical location inside a warehouse.

```csharp
public string Name, IsleCode, BinCode;
public WarehouseLocation WarehouseLocation;
// PhysicalStocks aggregated (cascades on bin delete)
```
Cardinality: `WarehouseLocation` 1 → N `BinLocation` 1 → N `PhysicalStock`.

**In a PG rebuild**: add unique `(warehouse_id, isle_code, bin_code)`. Drop aggregation cascade — delete should be blocked or soft-delete if non-zero stock exists (see §9).

### `WarehouseLocation`
```csharp
public string Name, Code;       // Code <= 10 chars
public Address Address;         // Cross-module to Crm.Address
// BinLocations aggregated
```
**No `SkuWarehouse` table exists** (the task description hypothesised one). Per-warehouse SKU-level aggregation is purely derived at query time — see §8. That's worth keeping in mind: "on hand by warehouse" was always `SUM(PhysicalStock.Quantity) GROUP BY Bin.Warehouse` in legacy.

### `PhysicalStockType`
Lookup: just `Name`. Seeded (`Data/PhysicalStockType.xml`):
```xml
<PhysicalStockType Name="General" />
<PhysicalStockType Name="Seasonal Stock" />
<PhysicalStockType Name="Promotional Stock" />
```
Referenced from `PhysicalStock`, `StockAdjustment` (target + source), and `PickItem`.

### `StockAdjustment`
The only mutation entity — **append-only audit log** of every stock movement.

```csharp
public Sku Sku;
public int Quantity;            // always positive; sign is derived from Reason
public string Notes;            // unlimited
public BinLocation TargetBinLocation;
public BinLocation SourceBinLocation;       // nullable
public StockAdjustmentReason Reason;
public PhysicalStockType TargetPhysicalStockType;
public PhysicalStockType SourcePhysicalStockType;   // nullable
public Contact Vendor;          // CRM link, nullable — used when receiving
public string VendorReference;  // e.g. courier consignment note
```
Cardinality: M:1 to Sku, Reason, both bins, both stock types, and Vendor. No child entities — each adjustment is a single movement.

**In a PG rebuild**: `stock_adjustments` append-only, no UPDATE grants. Write tests that confirm the "never delete" invariant.

### `StockAdjustmentReason`
```csharp
public string Name;
public bool   IsTargetIncrease;    // REQUIRED
public bool?  IsSourceIncrease;    // NULL = no source leg (single-sided adjustment)
```

Seeded (`Data/StockAdjustmentReason.xml`):
```xml
<StockAdjustmentReason Name="Migrated"   IsTargetIncrease="True"  />
<StockAdjustmentReason Name="New"        IsTargetIncrease="True"  />
<StockAdjustmentReason Name="Missing"    IsTargetIncrease="False" />
<StockAdjustmentReason Name="Damaged"    IsTargetIncrease="False" />
<StockAdjustmentReason Name="Transfer"   IsTargetIncrease="True"  IsSourceIncrease="False" />
<StockAdjustmentReason Name="Dispatched" IsTargetIncrease="False" />
```
The two-flag design is non-obvious but elegant — see §4.

### `Style` (not stock, but load-bearing context)
Garment-level attributes (Gender, Category, Fabric, Fit, SizeRange, Department, Season, web content). Owns the `Skus` collection. `Reference` long is the Style code (auto-seq 30).

### Lookups (all trivial — `Name` + sometimes `Code`)
`Barcode`, `BarcodeType`, `BusinessDivision`, `Colour`, `ColourCategory`, `Fabric`, `Fit`, `PriceRange`, `ProductCategory`, `ProductType`, `Season`, `Size`, `SizeRange`, `StyleCategory`, `StyleClass`, `StyleDepartment`, `StyleGender`, `UnitOfMeasure`.

**In a PG rebuild**: collapse most of these into a single `lookups(category, code, name)` table unless they have distinguishing fields. `BarcodeType`, `ColourCategory`, `SizeRange` (parent-of-Sizes) are the only ones with real structure.

---

## 3. Stock-type taxonomy

Stock types are implemented as **rows in `PhysicalStockType`**, not as an enum — new types can be added without schema changes. They partition the on-hand ledger: `PhysicalStock` rows with different types but the same `(Sku, Bin)` are independent balances.

| Seed value | Operational meaning (inferred — no docs in legacy) |
|---|---|
| **General** | Default sellable inventory. What "available" normally means. |
| **Seasonal Stock** | Ring-fenced for a season release. Not freely allocatable. |
| **Promotional Stock** | Ring-fenced for a promo campaign. |

### Key facts about stock types
- They are **not enums in code** — a `PhysicalStockType.Oid` FK on `PhysicalStock`. Runtime-extensible.
- There is no "reserved", "quarantined", "in-transit", or "damaged" type. Those concepts are modelled differently:
  - **Reserved** = derived from `PickItem` rows against open deliveries (see §7). Not a stock type.
  - **In-transit** = a `Delivery.Status` flag (`InTransit = True`) — a delivery state, not a stock-type state.
  - **Damaged/Missing** = not tracked as a stock type — the stock is simply removed via a `StockAdjustment` with reason "Damaged" or "Missing". No "damaged inventory awaiting disposal" bucket exists.
- Allowed transitions: legacy enforces **none**. Any reason can move any type to any other type by filling in Target and Source fields. The only hard rule is "quantity can't go below zero" (§4).
- Commitment (`CommittedStock` computed) is scoped to the stock type — a `PickItem` points at one specific `PhysicalStockType`, so Promotional picks don't draw down General availability.
- For order allocation, a pick must target a specific type. There is no configured rule like "prefer General over Seasonal" — the picker chooses the row.

### Delivery-status flags (from `Sale.DeliveryStatus`)
These drive the commitment computation. Seeded (`DeliveryStatus.xml`):

| Status | InTransit | IsStockAdjusted | CanBePicked | CanBeDispatched |
|---|---|---|---|---|
| Generating | F | F | F | F |
| Awaiting Picking | F | F | T | F |
| Awaiting Picking and Dispatch | F | F | T | T |
| Awaiting Payment | F | F | F | F |
| Awaiting Dispatch | F | F | F | T |
| Dispatched | **T** | **T** | F | F |
| Completed | F | **T** | F | F |
| Cancelled | F | **T** | F | F |

`IsStockAdjusted = True` means "the PhysicalStock rows have been decremented, stop counting this delivery as a commitment". Until then, pick quantities count against availability.

---

## 4. Stock adjustment flow (`AddStockAdjustment`)

Source: `InventoryMethodService.svc.cs` lines 38–100 (server-side, WCF). Full flow:

1. Client builds an unsaved `StockAdjustment` in a throwaway UoW, shows the DetailDialog, sends to server as JSON.
2. Server deserialises and opens a new UoW impersonating the client user.
3. Creates a real `StockAdjustment` in that UoW, copying scalars and resolving all FKs via `uow.GetDataObject(...)`.
4. **Target leg** (always runs):
   ```csharp
   var sign = reason.IsTargetIncrease ? +1 : -1;
   AddOrUpdatePhysicalStock(uow, sku, sign * qty, TargetBinLocation, TargetPhysicalStockType);
   ```
5. **Source leg** (runs only if `Reason.IsSourceIncrease` has a value):
   ```csharp
   if (reason.IsSourceIncrease.HasValue) {
       var sign = reason.IsSourceIncrease.Value ? +1 : -1;
       AddOrUpdatePhysicalStock(uow, sku, sign * qty, SourceBinLocation, SourcePhysicalStockType);
   }
   ```
6. `uow.CommitChanges()` — one transaction for the adjustment + both legs.
7. Returns `EntityChange[]` so the client cache can invalidate.

### `AddOrUpdatePhysicalStock` (the upsert, lines 137–166)

```csharp
var ps = uow.Query<PhysicalStock>()
            .FirstOrDefault(p => p.Sku.Oid == sku.Oid
                              && p.BinLocation.Oid == binLocation.Oid
                              && p.PhysicalStockType.Oid == physicalStockType.Oid);

if (ps != null) {
    if (ps.Quantity + quantity < 0)
        throw new ServiceMethodException("This adjustment would reduce the Physical Stock below zero!");
    ps.Quantity += quantity;
    return ps;
}

// No existing row — reject negative on first-touch
if (quantity < 0)
    throw new ServiceMethodException("This adjustment would reduce the Physical Stock below zero!");

return new PhysicalStock(uow) { Sku = sku, BinLocation = binLocation,
                                PhysicalStockType = physicalStockType, Quantity = quantity };
```

Notes on intent:
- **The upsert key is the (Sku, Bin, StockType) triple.** No unique constraint enforces it — a race could produce two rows. XPO's UoW is not row-locking.
- `Quantity` is signed `int` storage but the business rule is "never negative". A pre-existing row with qty 0 is allowed.
- `Quantity + quantity < 0` runs **before** adding — fail fast with no state change.
- **The "no source increase" case encodes a destroy/create.** `Missing`/`Damaged` simply decrement Target with no Source leg. `Migrated`/`New` increment Target with no Source leg. `Transfer` decrements Source + increments Target — the only two-leg reason.
- `Dispatched` (IsTargetIncrease=F, no Source) is used by the sales dispatch flow to remove stock — expect the sale module to create one of these per delivered line.

### Propagation rules
- **PhysicalStock is the only stored aggregate.** There is no Bin-level or Warehouse-level cached total. "Qty per bin" / "qty per warehouse" / "qty per SKU" are all query-time `SUM` aggregations (see §8).
- The `StockAdjustment` row itself is not referenced by future stock queries — it's pure audit. Deleting it would not reverse the physical stock change.

### In a PG rebuild
- Wrap the upsert in `SELECT ... FOR UPDATE` on the `(sku, bin, stock_type)` row, or use `INSERT ... ON CONFLICT (sku_id, bin_id, stock_type_id) DO UPDATE SET quantity = physical_stock.quantity + EXCLUDED.quantity RETURNING quantity` and re-check `>= 0` after.
- Better: put it in a PL/pgSQL function so the client only round-trips once and the transaction is guaranteed atomic.
- Add a partial unique index `(sku_id, bin_id, stock_type_id)` to eliminate the race.

---

## 5. Receiving flow

**There is no dedicated Receiving entity.** Stock enters the system via either:

1. **Manual stock adjustment** — user opens Add dialog on the StockAdjustment grid, picks reason "New" or "Migrated", enters qty + target bin + target stock type (+ optional vendor + con-note). `AddStockAdjustment` creates the `PhysicalStock` row.

2. **Bulk Excel import** — the `StockAdjustmentImporter` / `StockAdjustmentUploader` pair. User uploads a spreadsheet with these columns (defined in `StockAdjustmentImporterViewModel`):

   Header cells (fixed positions): `DateReceived` (C2), `AdjustmentReason` (C3), `Vendor` (F2), `ConNote` (F3).
   Named columns (from row 7 onward): `LegacyReference`/`Nav Id`, `Style Code`, `Colour`, `Size`, `Product Description`, `Quantity`, `Target Warehouse`, `Target Bin`, `Target Stock Type`, `Source Warehouse`, `Source Bin`, `Source Stock Type`, `Notes`.

   Each row creates one `StockAdjustment`. The uploader sets `UploadBatchSize = 1` because the server routes the write through `AddStockAdjustment` — batching would defeat the transactional upsert.

### Put-away rules: **none.**
The user picks the target bin explicitly. No bin capacity, no "first available bin", no ABC classification, no directed put-away. This is a gap worth filling in Central.

### In a PG rebuild
- Keep stock adjustments as the audit source of truth, but consider a separate `receipts` table (with vendor + con-note + arrival date) that *generates* adjustments — cleaner than overloading StockAdjustment.
- Add a lightweight "suggested bin" service before committing to anything complex. Even `bin_locations.preferred_for_sku_id` or `bins.max_volume` would unlock directed put-away.

---

## 6. Cycle counts / inventory adjustments

**Not found in legacy code — design from scratch.**

The legacy system has no `CycleCount`, `StockTake`, `CountSession`, `CountLine`, or variance-approval entity. The `Missing` and `Damaged` adjustment reasons are the entire reconciliation vocabulary, and they apply row-by-row via the same `StockAdjustment` flow. There's no concept of a count *session* (open → scan → reconcile → close).

For Central: this is an open canvas. A reasonable minimum is `count_sessions` + `count_lines(expected_qty, counted_qty, variance_qty, status)`, with a "post" action that emits adjustments for non-zero variances and references a reason code.

---

## 7. Reservations

Reservations are **not a stock type and not a table** — they're **derived from open picks** in the Sale module.

### How it works
- `Delivery` (sale module) has a `Status` with `IsStockAdjusted` boolean.
- `Delivery` has many `DeliveryItem` (one per SKU + qty released).
- Each `DeliveryItem` has many `PickItem` — a `PickItem` says "take `Quantity` of this SKU from this (BinLocation, PhysicalStockType)".
- Commitment is derived:

```csharp
// on PhysicalStock (PhysicalStock.cs)
[PersistentAlias("Iif([<PickItem>][DeliveryItem.Delivery.Status.IsStockAdjusted = 0
                                   AND ^.Sku = DeliveryItem.Sku
                                   AND ^.BinLocation = BinLocation
                                   AND ^.PhysicalStockType = PhysicalStockType].Exists,
                    [<PickItem>][...].Sum(Quantity), 0)")]
public int CommittedStock { get; }
```

Translation: "for this specific `(Sku, Bin, StockType)` row, sum `PickItem.Quantity` across all PickItems whose parent DeliveryItem belongs to a Delivery whose Status has `IsStockAdjusted = 0`."

- `StockOnHand = Quantity - CommittedStock` (can go negative if over-picked)
- `AvailableStock = Max(StockOnHand, 0)` (the UI-friendly one)

The SKU-level `CommittedStock` is a coarser version: it counts any open `DeliveryItem.Quantity` for the SKU regardless of which bin.

### Protection
There is **no locking** — availability is eventually consistent via the computed alias. A picker creating a `PickItem` does not check availability at write time. Two simultaneous picks against the same row can both succeed and drive `AvailableStock` negative. The only hard-stop is when `AddStockAdjustment` runs at dispatch time and blocks the decrement.

### In a PG rebuild
- Keep reservations virtual (derived) **or** promote them to real rows — pick one.
- If virtual: build a `v_physical_stock_available` view: `quantity - COALESCE((SELECT SUM(qty) FROM pick_items ... WHERE delivery_is_stock_adjusted = false), 0)`. Index the Delivery status boolean.
- If real: add a `reservations (sku_id, bin_id, stock_type_id, qty, reserved_by, expires_at, delivery_id)` table + a trigger that enforces `available >= 0` at reservation time. This is strictly safer but fights the "no reservation table" legacy.
- Recommend: virtual reservations for parity + a `SELECT FOR UPDATE` on `physical_stock` during pick creation to close the race.

---

## 8. Aggregates & reporting

| Report | How legacy computes it |
|---|---|
| On-hand by SKU | `Sku.PhysicalStock` alias: `Sum(PhysicalStocks.Quantity)` |
| Available by SKU | `Sku.AvailableStock = Max(PhysicalStock - CommittedStock, 0)` |
| On-hand by SKU × Warehouse | Query-time: `SUM(ps.Quantity) WHERE ps.Bin.Warehouse = ?` |
| On-hand by SKU × Bin × Stock type | Direct read of `PhysicalStock` rows |
| Committed by SKU | Cross-module alias joining to `DeliveryItem` |
| Available by Bin/Type | `PhysicalStock.AvailableStock` per row |

**Nothing is materialised.** No triggers maintain aggregate totals, no summary tables, no matviews. Every "on hand" display is a fresh aggregate query. At small scale (thousands of SKUs × dozens of bins) this is fine; at >1M stock rows it will need help.

### In a PG rebuild
- Start with views (`v_sku_onhand`, `v_sku_warehouse_onhand`) — don't prematurely materialise.
- If performance demands it, use TimescaleDB continuous aggregates over `stock_adjustments` (append-only!) rather than triggers on `physical_stock` — the append-only source is more reliable.
- Consider a `sku_warehouse_summary(sku_id, warehouse_id, on_hand, committed, available, last_refreshed)` matview refreshed every N minutes rather than trigger-maintained.
- Index `physical_stock (sku_id) INCLUDE (quantity)` for the "total on hand by SKU" common case.

---

## 9. Edge cases the legacy code handles (and doesn't)

**Handled:**
- Negative stock — blocked in `AddOrUpdatePhysicalStock` both for the pre-existing-row case (`qty + delta < 0`) and the new-row case (`delta < 0`).
- Transfer atomicity — source and target legs committed in one UoW transaction.
- Reason drives sign — user never enters a negative `Quantity` (SpinEditor `MinValue = 1`); the sign comes from `IsTargetIncrease` / `IsSourceIncrease`.
- Stock-adjustment delete is hidden — `StockAdjustmentListViewModel.DeleteCommand` returns null; audit integrity preserved.
- Source/Target editor enablement — `Reason.IsSourceIncrease != null` drives UI enablement of the Source group (see `Metadata/StockAdjustment.cs` `builder.Condition`).
- Cross-module commitment — SKUs committed to unshipped deliveries are reflected in computed `AvailableStock`.

**Not handled (real gaps — keep a list):**
- **Race conditions**: no SELECT FOR UPDATE, no unique constraint on `(sku, bin, stock_type)` — two concurrent adjustments can split into two rows or both pass the `qty + delta >= 0` check.
- **Bin deletion with stock**: `PhysicalStocks` is `[Aggregated]` on `BinLocation`, so deleting a bin **silently cascades the stock rows**. There's no "can't delete non-empty bin" guard. This is a landmine.
- **Zero-quantity PhysicalStock rows**: never cleaned up. A bin that once held stock keeps a zero row forever.
- **SKU moves between bins**: only via a Transfer adjustment. No "move" primitive.
- **Negative computed `StockOnHand`**: over-picks produce negative values, which the UI hides behind `AvailableStock = Max(…, 0)` but the underlying ledger still shows the pathological state. No alerting.
- **Unit-of-measure conversion**: SKU has Pack and Item UoM but no conversion factor — adjustments are always in a single implicit unit. Receiving "1 box of 24" requires the user to enter 24.
- **Warehouse-level reservations**: don't exist — pick always targets a specific bin+type.
- **Partial picks**: `PickItem.QuantityPicked` vs `Quantity` — tracked but not enforced against physical stock.
- **Reservation expiry**: no `expires_at`; a Delivery stuck in `Awaiting Picking` holds the commitment indefinitely.
- **Multi-SKU barcode (case/pack)**: one SKU → one barcode. No case barcode flowing to component SKUs.

---

## 10. Notes for a PG rebuild

### Translates cleanly
- Lookup tables (`PhysicalStockType`, `StockAdjustmentReason`, `UnitOfMeasure`, `BarcodeType`, etc.) — small reference tables with `id uuid PK, code text UNIQUE, name text`.
- Seed data from the `Data/*.xml` files — convert to `INSERT ... ON CONFLICT DO NOTHING` in a migration.
- Enum types (`CostingMethod`, `ReplenishmentSystem`, `ReorderingPolicy`) — PG enums or small lookup tables; enums are fine since they're code-level.
- The immutable audit nature of `StockAdjustment` — REVOKE UPDATE/DELETE on the table.

### Needs redesign
- **XPO `StampedDataObjectBase` audit fields** — replace with explicit `created_at timestamptz`, `created_by uuid`, `modified_at`, `modified_by`. Use a trigger to populate on INSERT/UPDATE.
- **`PersistentAlias` computed properties** — become SQL views or generated columns. `CommittedStock`/`AvailableStock` are classic candidates for a view.
- **`[Aggregated]` cascade on bins** — drop it. Use `ON DELETE RESTRICT` and a pre-check procedure.
- **`RuntimeAssociation`** (XPO cross-assembly FK) — plain FK columns.
- **Sequence table in XML** — replace with PG `SEQUENCE` or `GENERATED BY DEFAULT AS IDENTITY`.

### Recommended constraints legacy lacked
```sql
ALTER TABLE physical_stock
  ADD CONSTRAINT uq_physical_stock UNIQUE (sku_id, bin_id, stock_type_id),
  ADD CONSTRAINT chk_qty_nonneg CHECK (quantity >= 0);

ALTER TABLE stock_adjustments
  ADD CONSTRAINT chk_adj_qty_positive CHECK (quantity > 0),
  ADD CONSTRAINT chk_source_consistency
    CHECK ((source_bin_id IS NULL) = (source_stock_type_id IS NULL));
```

### Locking / concurrency
- Wrap stock adjustments in a PL/pgSQL function doing `SELECT ... FOR UPDATE` on the candidate `physical_stock` row (or no row → INSERT) to serialise concurrent updates for the same (sku, bin, type).
- Alternative: advisory lock keyed on `hashtext(sku_id::text || bin_id::text || stock_type_id::text)` — lighter than row lock but coarser.
- `INSERT ... ON CONFLICT DO UPDATE` with a `CHECK (quantity >= 0)` constraint gives you atomicity; combine with a `RETURNING` to see the result.

### Indexes that matter
- `physical_stock (sku_id)` INCLUDE (quantity) — for SKU total
- `physical_stock (sku_id, bin_id, stock_type_id)` UNIQUE — upsert key
- `physical_stock (bin_id)` — "what's in this bin" queries
- `stock_adjustments (sku_id, created_at DESC)` — audit by SKU
- `stock_adjustments (target_bin_id, created_at DESC)` — audit by bin
- Partial index on `physical_stock (sku_id, stock_type_id) WHERE quantity > 0` to skip zero rows

### Materialisation strategy
- Default: **views** (`v_sku_onhand`, `v_bin_contents`, `v_warehouse_summary`).
- If needed: **matviews** refreshed on a cron (pg_notify from `stock_adjustments` insert trigger → refresh queue).
- Don't try to maintain running totals via triggers on `physical_stock` — the legacy has taught us qty updates are the hot path and triggers there would hurt.

### API shape (Central style)
- `POST /api/inventory/adjustments` — body mirrors `StockAdjustment` minus derived fields; server performs the upsert.
- `GET /api/inventory/physical-stock?sku_id=&warehouse_id=&bin_id=&stock_type_id=&include_zero=false` — paginated.
- `GET /api/inventory/skus/{id}/availability?warehouse_id=` — returns `{on_hand, committed, available}` derived from the view.
- `POST /api/inventory/transfers` — sugar over a Transfer-reason adjustment.
- Emit `pg_notify('inventory_changed', sku_id)` on adjustment commit → SignalR fan-out to any grid showing that SKU.

---

## 11. What we deliberately didn't copy

- **DevExpress XPO** — XPO metadata/aliases/Association attributes don't translate to Npgsql; use plain SQL + EF Core or Dapper.
- **Apparel-specific hierarchy** (`Style`, `Colour`, `Size`, `Fabric`, `Fit`, `Gender`, `SizeRange`, `Season`) — Central is a generic inventory module; these are domain concerns for a single tenant vertical, not platform concepts. Expose via custom fields / custom entities (075_custom_objects).
- **`Web_*` fields on `Sku`/`Style`** — e-commerce shopfront concerns bleed into the SKU master. Keep imagery/SEO in a separate `sku_ecommerce` sidecar table.
- **`LegacyReference` string FK matching during import** — replace with proper external-ID mapping (see `crm_external_ids` pattern in migration 058).
- **`ReschedulingPeriod` / `LotAccumulationPeriod` as free-text strings** — parse into `interval` or a small structured type.
- **Three separate posting-group FKs on SKU** (Inventory, General Product, GST) — finance postings belong in the accounting/posting module; SKU should carry at most one "posting profile" reference.
- **The `[Aggregated]` cascade on BinLocation → PhysicalStock** — see §9, silently deleting stock is dangerous. Use `ON DELETE RESTRICT` + an explicit "archive bin" flow.
- **Hand-seeded sequence codes in `Sequence.xml`** — use PG identity columns.
- **Sign-via-Reason** as the only input convention — keep the Reason flag design (it's elegant) **but** also accept signed `delta_quantity` in the API for programmatic callers who don't want a reason lookup round-trip.
- **No locking on adjustment** — explicitly disallowed; always use FOR UPDATE / ON CONFLICT in the rebuild.
- **Delivery-status boolean `IsStockAdjusted` driving commitment** — the logic is fine, but pull commitments out of the aliased PersistentAlias into a plain SQL view so the boundary between Sales and Inventory modules is explicit.
