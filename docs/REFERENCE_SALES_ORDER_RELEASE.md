# Reference: Sales Order Release (legacy TotalLink)

> Captured 2026-04-17 from `source/TIG.TotalLink.Server` before that tree was removed.
> Not a spec for Central's Fulfilment module — a reference so whoever builds it
> doesn't redesign blind. See `docs/LEGACY_MIGRATION.md` for context.
>
> Primary source: `TIG.TotalLink.Server.MethodService.Sale/SaleMethodService.svc.cs`
> (656 lines, two public methods: `ReleaseSalesOrder` and `ReleaseDelivery`).

---

## 1. Domain vocabulary

| Term | Legacy entity | Definition |
|---|---|---|
| Sales Order (SO) | `SalesOrder` | Customer order header. Lines are `SalesOrderItem`. Has an `AllowPartialDelivery` flag. |
| SO Line | `SalesOrderItem` | One line per SKU. Tracks `Quantity`, `QuantityReleased`, `QuantityCancelled`. |
| Release | `SalesOrderRelease` | A **request** to release stock against one or more SOs. Parent of many `Delivery` records over its lifetime. Scoped to a set of bins + physical stock types. |
| Release Line | `SalesOrderReleaseItem` | Result row per attempted SO line on a release. Carries status `Released` / `PartReleased` / `Failed` and (if failed) an `ErrorMessage`. |
| Delivery | `Delivery` | One shipment derived from a release. Has a `DeliveryStatus` FSM, a `ConsignmentNote` when dispatched, and many `DeliveryItem`. |
| Delivery Line | `DeliveryItem` | One SKU row on a delivery, linked back to the `SalesOrderReleaseItem` that spawned it. |
| Pick Item | `PickItem` | Physical pick instruction: "take X of SKU from bin B of stock-type T". Children of a `DeliveryItem`. One DeliveryItem can have many PickItems (if qty spans bins). |
| SKU | `Sku` | Product identifier (inventory module). |
| Bin Location | `BinLocation` | Physical location within a warehouse. |
| Physical Stock Type | `PhysicalStockType` | Stock classification — e.g. `Available`, `Quarantined`, `Reserved`, `Damaged`. Controls which stock is eligible for release. |
| Physical Stock | `PhysicalStock` | The actual quantity on hand, keyed by (SKU, BinLocation, PhysicalStockType). This is the row that gets decremented on dispatch. |
| Stock Adjustment | `StockAdjustment` | Audit record for any change to `PhysicalStock.Quantity`. |
| Short ship | — | Dispatching a delivery without picking everything. Remainder is written off for that delivery. |

## 2. Data model

### 2.1 `SalesOrder`
Customer order header.

| Field | Type | Meaning |
|---|---|---|
| `Oid` | Guid | PK |
| `Reference` | long | Human reference number (via sequence service) |
| `Contact` | FK → `Contact` | Customer |
| `QuoteVersion` | FK → `QuoteVersion` | Originating quote, nullable |
| `Status` | FK → `SalesOrderStatus` | Lookup (see §3) |
| `AllowPartialDelivery` | bool | Can this SO be released in pieces? |
| `TotalQuantity` | computed | `SUM(items.Quantity) - SUM(items.QuantityCancelled)` |
| `TotalQuantityReleased` | computed | `SUM(items.QuantityReleased)` |
| `TotalQuantityCancelled` | computed | `SUM(items.QuantityCancelled)` |
| `SalesOrderItems` | 1:N → `SalesOrderItem` | Aggregated (cascade delete) |
| `SalesOrderReleases` | N:M → `SalesOrderRelease` | A release can cover many SOs and vice versa |
| `Invoices` | 1:N → `Invoice` | |

**In a PG rebuild:** `sales_orders` table with `status` either a text enum or FK to `sales_order_statuses` lookup. Keep `allow_partial_delivery` as a plain `bool NOT NULL DEFAULT true`. Totals are cheap to compute on read — don't denormalise until you measure.

### 2.2 `SalesOrderItem`
One line per SKU on an SO.

| Field | Type | Meaning |
|---|---|---|
| `Sku` | FK → `Sku` | |
| `Quantity` | int | Ordered qty |
| `QuantityReleased` | int | Sum of releases against this line (increments as PickItems are created) |
| `QuantityCancelled` | int | Cancelled qty (lifecycle independent of release) |
| `CostPrice` / `SellPrice` | decimal | Snapshot at order time |
| `QuoteItem` | FK, nullable | Source quote line |
| `SalesOrder` | FK | Parent |
| `SalesOrderReleaseItems` | 1:N | Release attempts for this line |

**Remaining-to-release** = `Quantity - QuantityCancelled - QuantityReleased`. This expression appears in five places in the legacy code — canonicalise it as a column/view in PG.

**In a PG rebuild:** `sales_order_items`. `quantity_released` is a running total maintained by the release flow; prefer either (a) a SUM over release_items with a generated column, or (b) update-in-transaction like the legacy does. Legacy does (b) because XPO persists the integer directly.

### 2.3 `SalesOrderRelease`
Represents the *intent* to release, and the source constraints.

| Field | Type | Meaning |
|---|---|---|
| `Reference` | long | Human reference (from sequence service) |
| `SalesOrders` | N:M → `SalesOrder` | One release can batch multiple SOs |
| `SalesOrderReleaseItems` | 1:N → `SalesOrderReleaseItem` | Per-line results (incl. failures) |
| `BinLocations` | 1:N via `SalesOrderRelease_BinLocation` | **Whitelist** of bins stock may come from |
| `PhysicalStockTypes` | 1:N via `SalesOrderRelease_PhysicalStockType` | **Whitelist** of stock types |
| `Deliveries` | 1:N → `Delivery` | Each physical release attempt creates a new Delivery under the same Release |

Note the two link tables are *explicit*, not N:M. They have `ModifiesParent("SalesOrderRelease")`, meaning changes touch the parent aggregate.

**In a PG rebuild:** `sales_order_releases`, plus junction tables `sales_order_release_bin_locations` and `sales_order_release_physical_stock_types`. The `SalesOrders` N:M is a separate junction `sales_order_release_orders`.

### 2.4 `SalesOrderReleaseItem`
Per-line outcome row. Always written — including on failure, which is why failures have a persistent audit trail.

| Field | Type | Meaning |
|---|---|---|
| `SalesOrderRelease` | FK | |
| `SalesOrderItem` | FK | |
| `QuantityToRelease` | int | Qty **requested** (not necessarily achieved) |
| `QuantityReleased` | computed | Actual: `SUM(DeliveryItems.Quantity)` |
| `Status` | enum | `Released` \| `PartReleased` \| `Failed` (see `SalesOrderReleaseItemStatus.cs`) |
| `ErrorMessage` | text, nullable | Populated on Failed |
| `DeliveryItems` | 1:N → `DeliveryItem` | |

**In a PG rebuild:** `sales_order_release_items`. Keep the enum as text (`CHECK (status IN ('released','part_released','failed'))`). Keep `error_message TEXT` — it's user-facing.

### 2.5 `Delivery`
A physical shipment derived from (part of) a Release.

| Field | Type | Meaning |
|---|---|---|
| `Reference` | long | Human reference |
| `SalesOrderRelease` | FK | Parent |
| `Contact` | FK | Consignee (copied from SO) |
| `Status` | FK → `DeliveryStatus` | FSM (see §3) |
| `ConsignmentNote` | string | Set when dispatched; its presence is the signal |
| `Invoice` | FK, nullable | Linked invoice if billed |
| `DeliveryItems` | 1:N, aggregated | |

**In a PG rebuild:** `deliveries`. `consignment_note TEXT` — presence == dispatched.

### 2.6 `DeliveryItem`
| Field | Type | Meaning |
|---|---|---|
| `Sku` | FK | |
| `Delivery` | FK | |
| `SalesOrderReleaseItem` | FK | Back-link to the release decision |
| `QuantityReleased` | int | Incremented when PickItems are picked |
| `CostPrice` / `SellPrice` | decimal | Snapshot from SO line |
| `InvoiceItem` | FK, nullable | |
| `Quantity` | computed | `SUM(PickItems.Quantity)` — i.e. allocation, not yet picked |
| `PickItems` | 1:N, aggregated | |

Note: **`Quantity` (allocated) and `QuantityReleased` (picked) are different numbers** — a DeliveryItem can be allocated 10 but only 7 picked when short-shipped.

### 2.7 `PickItem`
| Field | Type | Meaning |
|---|---|---|
| `DeliveryItem` | FK | |
| `BinLocation` | FK | Where to pick from |
| `PhysicalStockType` | FK | Which pool |
| `Quantity` | int | Allocated qty (reservation) |
| `QuantityPicked` | int | Actually picked so far |
| `CanBePicked` | computed | `DeliveryItem.Delivery.Status.CanBePicked` |
| `CanBeDispatched` | computed | `DeliveryItem.Delivery.Status.CanBeDispatched` |

**In a PG rebuild:** `pick_items`. This is the soft-reservation row — creating it is how stock is "held" without yet being decremented from `physical_stock`.

### 2.8 `PhysicalStock` (inventory module, referenced here)
```csharp
// PhysicalStock.cs
[PersistentAlias("Iif([<PickItem>][DeliveryItem.Delivery.Status.IsStockAdjusted = 0 AND ...].Exists, ...Sum(Quantity), 0)")]
public int CommittedStock { get; }    // sum of PickItem.Quantity on non-dispatched deliveries

[PersistentAlias("Quantity - CommittedStock")]
public int StockOnHand { get; }

[PersistentAlias("Max(StockOnHand, 0)")]
public int AvailableStock { get; }
```
Keyed by `(Sku, BinLocation, PhysicalStockType)`. **Holds an `OptimisticLockField`** — the lynchpin of the concurrency story (see §7).

**In a PG rebuild:** `physical_stock` table with `UNIQUE (sku_id, bin_location_id, physical_stock_type_id)` and either an `xmin`-based optimistic lock or an explicit `version int`. `available_stock` is a view/generated column over a subquery for committed stock from unshipped PickItems.

## 3. State machine

### 3.1 `SalesOrderStatus` (seeded in `SalesOrderStatus.xml`)

```text
Awaiting Release  (IsReleased=false, IsCompleted=false)   [initial]
     │ ReleaseSalesOrder() partial success
     ▼
Part Released     (IsReleased=true,  IsCompleted=false)
     │ ReleaseSalesOrder() until all items covered
     ▼
Released          (IsReleased=true,  IsCompleted=false)
     │ (downstream invoicing flow — not in this file)
     ▼
Completed         (IsReleased=true,  IsCompleted=true)
```

Transition logic (from `ReleaseSalesOrder`, line 237-239):

```csharp
salesOrder.Status = salesOrder.SalesOrderItems.Sum(s => s.QuantityReleased + s.QuantityCancelled)
                        >= salesOrder.SalesOrderItems.Sum(s => s.Quantity)
    ? uow.Query<SalesOrderStatus>().FirstOrDefault(s => s.Name == "Released")
    : uow.Query<SalesOrderStatus>().FirstOrDefault(s => s.Name == "Part Released");
```

Cancellation is not a status — it's tracked per-line via `QuantityCancelled`. A fully cancelled SO still sits in `Released`/`Completed` once everything remaining is released.

### 3.2 `DeliveryStatus` (seeded in `DeliveryStatus.xml`)

| Name | InTransit | IsStockAdjusted | CanBePicked | CanBeDispatched |
|---|---|---|---|---|
| Generating | F | F | F | F |
| Awaiting Picking | F | F | T | F |
| Awaiting Picking and Dispatch | F | F | T | **T** |
| Awaiting Payment | F | F | F | F |
| Awaiting Dispatch | F | F | F | T |
| Dispatched | **T** | **T** | F | F |
| Completed | F | T | F | F |
| Cancelled | F | T | F | F |

Flow observed in the code:
- Created as **Generating** (line 171)
- If any items were released, promoted to **Awaiting Picking and Dispatch** (line 229)
- If nothing released (all failed, or empty), **deleted** (line 221)
- On `ReleaseDelivery()` with a consignment note: **Dispatched** (line 327). At this point `IsStockAdjusted=true` — `physical_stock` is decremented in the same transaction.

Notable: statuses with `IsStockAdjusted=true` are the signal to the `PhysicalStock.CommittedStock` alias that those PickItems no longer count as committed (stock has moved out).

## 4. Release flow (`ReleaseSalesOrder`)

High-level: client selects one or more SOs + a bin whitelist + a stock-type whitelist + an optional `allowPartialDelivery` override. Server attempts to allocate stock by creating `PickItem`s on a new `Delivery`, returns a per-line result.

### 4.1 Top-level (svc.cs lines 68-254)

1. **Notify dirty** — `saleFacade.NotifyDirtyTypes(typeof(SalesOrder), typeof(SalesOrderItem))` busts the XPO cache so we read fresh. PG equivalent: nothing — we just read in a transaction.
2. **Open UnitOfWork, impersonate caller's token.**
3. **Load the SO.** 404 if missing.
4. **Resolve `allowPartialDelivery`** — parameter overrides, otherwise inherited from the SO.
5. **Get or create `SalesOrderRelease`:**
   - If `SalesOrderReleaseOid` supplied: append this SO to the existing release (batching across SOs).
   - Else: new `SalesOrderRelease`, issue a Reference via `GetNextSequenceNumber`, create `SalesOrderRelease_BinLocation` + `SalesOrderRelease_PhysicalStockType` rows from the whitelists.
6. **Short-circuit partial-delivery rejection** (lines 151-160):

    ```csharp
    if (!allowPartialDelivery.Value && parameters.SalesOrderItems != null && parameters.SalesOrderItems.Count > 0)
    {
        var quantityRemaining = salesOrder.TotalQuantity - salesOrder.TotalQuantityCancelled - salesOrder.TotalQuantityReleased;
        var quantityToRelease = parameters.SalesOrderItems.Sum(s => s.QuantityToRelease);
        if (quantityRemaining != quantityToRelease)
            releaseErrorMessage = "Attempted to partially release a Sales Order but Allow Partial Delivery is disabled.";
    }
    ```

   The user must request **exactly** the remainder, not a subset.

7. **Create `Delivery`** in status `Generating`, issue its Reference. Done *before* allocation, with errors-so-far already recorded — the Delivery may be deleted later if allocation yields nothing.
8. **Commit** the header-level changes.
9. **Expand "release all remaining"** (lines 190-196): if caller passed no line params, enumerate `salesOrder.SalesOrderItems.Where(i => i.Quantity > i.QuantityCancelled + i.QuantityReleased)` and fill in item parameters.
10. **Per-line release loop** (lines 199-214):
    - If no global error yet: call `ReleaseSalesOrderItem(...)`.
    - If global error already set: still write a failed `SalesOrderReleaseItem` for audit (`CreateFailedReleaseItem`) but don't attempt allocation.
11. **Finalise Delivery** (lines 216-243):
    - If global error OR `DeliveryItems.Count == 0` → **delete the Delivery** (including its Reference? see §10), commit.
    - Else → promote Delivery to `Awaiting Picking and Dispatch`, update SO status to `Released` or `Part Released`, commit.
12. Return `ReleaseSalesOrderResult { SalesOrderReleaseOid, DeliveryOid, TotalQuantityReleased, Changes[] }`.

### 4.2 Per-line (`ReleaseSalesOrderItem`, lines 464-622)

Runs inside the outer transaction.

1. Load the `SalesOrderItem`. Verify it belongs to the right SO. Verify `QuantityToRelease <= Quantity - QuantityCancelled - QuantityReleased`.
2. Enter the retry loop (`attemptsRemaining = MaxReleaseAttempts = 10`, see §7).
3. Inside each attempt:

    ```csharp
    _saleFacade.NotifyDirtyTypes(typeof(PhysicalStock), typeof(Sku), typeof(BinLocation),
                                 typeof(PhysicalStockType), typeof(Delivery),
                                 typeof(DeliveryItem), typeof(PickItem));

    var physicalStocks = uow.Query<PhysicalStock>()
        .Where(p => p.AvailableStock > 0
                 && p.Sku.Oid == salesOrderItem.Sku.Oid
                 && releaseParameters.BinLocationOids.Contains(p.BinLocation.Oid)
                 && releaseParameters.PhysicalStockTypeOids.Contains(p.PhysicalStockType.Oid))
        .ToList();
    // TODO : PhysicalStock should be ordered so the user can control which BinLocations
    //        and PhysicalStockTypes get allocated first
    ```

    Legacy has **no allocation ordering** — iteration order is whatever XPO gives back. This is a known gap (see §9).

4. Greedy fill across PhysicalStocks (lines 502-529): for each row, take `min(quantityRemaining, availableStock)`, create a `PickItem`, increment the row's `OptimisticLockField` manually to force a concurrency check on commit.

    ```csharp
    // Manually increment the OptimisticLockField on the PhysicalStock
    // This will trigger a LockingException on commit if anyone else has modified
    // the PhysicalStockType.Quantity or released some of the items
    physicalStock.SetMemberValue("OptimisticLockField",
        (int)physicalStock.GetMemberValue("OptimisticLockField") + 1);
    ```

5. If partial delivery disabled and qty remains: throw, which falls into the catch at line 600 → rollback + write failed release item.
6. If nothing allocated at all: throw "Failed to find any available stock" → same path.
7. Otherwise:
    - `salesOrderItem.QuantityReleased += quantityReleased`
    - New `SalesOrderReleaseItem` with status `Released` (if `quantityRemaining == 0`) or `PartReleased`
    - New `DeliveryItem` (sku, prices snapshot from SO line, `QuantityReleased=0`)
    - Wire each `PickItem.DeliveryItem = deliveryItem`
    - `uow.CommitChanges()` — this is where the optimistic lock fires if any `PhysicalStock` has been touched since we read it.

8. On `LockingException`: `RollbackTransaction()`, decrement attempts, sleep 10-100ms random, retry.
9. On any other exception: rollback, write a failed `SalesOrderReleaseItem` with the message, and — if a **global** error was set meanwhile — mark **all prior successful release items in this delivery as Failed and reverse their `QuantityReleased`** (lines 609-618).

### 4.3 Observable per-line outcomes

| Case | Release status | DeliveryItem created? | PickItems created? |
|---|---|---|---|
| All qty allocated cleanly | `Released` | yes | yes |
| Partial allocated, partial allowed | `PartReleased` | yes | yes (< requested qty) |
| Partial allocated, partial disallowed | `Failed` | no | no (rolled back) |
| Zero stock | `Failed` | no | no |
| Rejected globally (batch partial guard) | `Failed` | no | no |
| Optimistic lock, 10 retries exhausted | `Failed` | no | no |

## 5. Partial delivery (`ReleaseDelivery`)

Confusingly named — this is **picking + dispatching**, not initial release. It operates on an existing `Delivery` (produced by `ReleaseSalesOrder`).

### 5.1 Inputs
- `DeliveryOid`
- `PickItems[]` — each `{ PickItemOid, QuantityToPick }`
- `ConsignmentNote` — optional. **Its presence flips the mode**:
  - Absent → "progressive pick" (just record `QuantityPicked` on pick items; Delivery stays in picking status).
  - Present → "dispatch" (also transition status, write StockAdjustments, decrement PhysicalStock).

### 5.2 Flow (lines 261-381)

1. `NotifyDirtyTypes(Delivery, DeliveryItem, PickItem)`.
2. Open UoW, load Delivery.
3. For each `PickItemParameter`:
   - Validate `pickItem.CanBePicked` (i.e. `Delivery.Status.CanBePicked == true`).
   - Validate PickItem belongs to the supplied Delivery.
   - Validate `QuantityToPick <= Quantity - QuantityPicked`.
   - Apply: `pickItem.QuantityPicked += QuantityToPick; pickItem.DeliveryItem.QuantityReleased += QuantityToPick`.
4. **If `ConsignmentNote` supplied**:
   - Validate `Status.CanBeDispatched`.
   - Reject if already dispatched (`ConsignmentNote` non-empty).
   - Set `delivery.ConsignmentNote`, transition status to `Dispatched`.
   - For **every** pick item on the delivery (including those picked earlier): write a `StockAdjustment` (reason "Dispatched", notes "Dispatched on Delivery <ref>"), then decrement `PhysicalStock.Quantity` at (SKU, Bin, StockType). Guards against going negative: `physicalStock.Quantity - stockAdjustment.Quantity < 0 → throw`.

   The comment at line 332 is worth keeping:

    ```csharp
    // Create StockAdjustments for all QuantityPicked on the PickItems
    // (We do this directly here instead of calling the InventoryMethodService
    // because we need the StockAdjustments and the new Delivery status to be
    // written at the same time)
    ```

   Atomicity between "status = Dispatched" and "stock reduced" is load-bearing: once `IsStockAdjusted=true`, the `CommittedStock` alias stops counting these picks, so if they weren't also decremented from `Quantity`, `AvailableStock` would jump by that amount.

5. Single commit at the end.

### 5.3 "Fully fulfilled parent" — note the gap

The legacy code **does not** explicitly walk up to `SalesOrderRelease` and mark it complete when all its Deliveries are dispatched. There is no `SalesOrderRelease.Status` field at all. "Completion" of the parent release is implicit — an external query tests whether all linked SOs reached `Released` status. The downstream SO transition to `Completed` likewise happens elsewhere (probably in invoicing/receipt).

## 6. Bin location allocation

### 6.1 Taxonomy
- **BinLocation**: physical bin. Dozens to thousands per warehouse.
- **PhysicalStockType**: classification of stock state. Common seeded values in TotalLink include `Available`, `Reserved`, `Quarantined`, `Damaged` (actual seed lives in the Inventory module — not re-read here).
- **PhysicalStock**: the (SKU × Bin × StockType) triple holding a `Quantity`. The row is the unit of reservation and dispatch.

### 6.2 How "reservation" works
Reservation is soft:
- A `PickItem` with `Quantity = N` against `(Bin, StockType)` counts as **committed** while its `Delivery.Status.IsStockAdjusted = false`.
- `CommittedStock` on `PhysicalStock` subtracts this from `Quantity` to yield `AvailableStock`.
- Dispatching the delivery (setting `IsStockAdjusted=true` via status transition) in the same transaction that decrements `PhysicalStock.Quantity` keeps the arithmetic consistent.

No dedicated reservation table — the pick items are the reservations.

### 6.3 Allocation policy
- Caller provides a whitelist of Bins and StockTypes at release time (stored on `SalesOrderRelease_BinLocation` / `SalesOrderRelease_PhysicalStockType`).
- Per-line loop queries `PhysicalStock` filtered by that whitelist, with `AvailableStock > 0`.
- Iteration order is unspecified (XPO default). No prioritisation by expiry, proximity, LIFO/FIFO, etc.
- Greedy: take from the first row until either the request is satisfied or we run out of rows.

### 6.4 Where a DeliveryItem ends up with multiple PickItems
When a line's qty doesn't fit in one `PhysicalStock` row. E.g. request 50 of SKU-X, bin A has 30 and bin B has 20 — you get two PickItems under one DeliveryItem.

## 7. Locking & retry

### 7.1 Mechanism
XPO's `OptimisticLockField` is an int column on each persistent class. XPO increments it on every update and checks the old value in the WHERE clause; a mismatch raises `LockingException`.

The release code **manually bumps** it on every `PhysicalStock` it reads (line 511):

```csharp
physicalStock.SetMemberValue("OptimisticLockField",
    (int)physicalStock.GetMemberValue("OptimisticLockField") + 1);
```

Purpose: force a lock check even though we're only creating a child `PickItem`, not modifying the stock row's own columns. Without this, two concurrent releases could each see `AvailableStock=10`, each create a PickItem for 10, and nobody would detect the over-commit until dispatch time — when one of them would discover `Quantity < 0` and throw too late.

### 7.2 Retry loop (lines 481-597)

```csharp
var attemptsRemaining = MaxReleaseAttempts;  // 10
while (true) {
    try {
        // ... allocate + commit ...
        return releaseErrorMessage;
    }
    catch (LockingException) {
        uow.RollbackTransaction();
        if (--attemptsRemaining <= 0)
            throw new ServiceMethodException(
                $"Failed to release after {MaxReleaseAttempts} attempts.");
        Thread.Sleep(new Random().Next(10, 100));
    }
}
```

Notes:
- **Backoff**: uniform random 10-100ms per retry. Not exponential. `new Random()` is instantiated per sleep, which seeds from the clock — on fast machines multiple instances can get identical seeds (classic .NET gotcha). Not a correctness bug here but worth avoiding.
- **Only `LockingException` retries.** All other exceptions fall to the outer catch which writes a failed `SalesOrderReleaseItem`.
- **Per-line retries**, not whole-release retries. Each SO line has its own 10-attempt budget.

### 7.3 Why 10
No comment explains the choice. Given ~55ms mean sleep, 10 retries = ~550ms worst-case stall per line. Empirical; would drop with fewer concurrent dispatchers or more stock granularity.

## 8. Edge cases the legacy code handles

- **Release "everything remaining"** — if caller omits `SalesOrderItems`, server enumerates lines where `Quantity > QuantityCancelled + QuantityReleased`.
- **Partial release disallowed with batch request** — if caller passes a line list, the sum must equal remaining on the SO (exact match, not "at most"). Rejected up-front before any allocation.
- **Zero-quantity-remaining lines** skipped in the "release all" expansion (filter at line 192).
- **Cancelled quantity mid-order** — the `QuantityCancelled` field is folded into every remaining-qty calculation, so cancelling a line after a partial release shrinks the outstanding amount correctly.
- **Stock ran out while allocating** — if `AllowPartialDelivery=true`, creates a `PartReleased` line with whatever could be allocated. If `false`, rolls back *that line only* and writes it as `Failed`; other lines in the same release can still succeed.
- **Single line across multiple bins** — handled by looping over `physicalStocks` and creating multiple `PickItem`s under one `DeliveryItem`.
- **All lines fail** — the Delivery is deleted (line 221) and the Release kept (it carries the failed `SalesOrderReleaseItem` audit trail).
- **Mid-release cascade failure** — if a later line throws and sets a *global* error, all *prior* successful items in that Delivery are reverted: status flipped to `Failed`, `SalesOrderItem.QuantityReleased` decremented (lines 609-618). This is the only place the code reverses already-committed work within the same call.
- **Concurrent release on the same stock** — optimistic lock + retry (§7).
- **Picking more than allocated** — rejected (`QuantityToPick > Quantity - QuantityPicked`).
- **Dispatching a not-yet-dispatchable delivery** — rejected via `Status.CanBeDispatched == false`.
- **Double dispatch** — rejected: `ConsignmentNote` already non-empty.
- **Dispatch would drive stock negative** — rejected per-pick-item before the commit (line 355).
- **Missing PhysicalStock row on dispatch** — throws "Failed to find a Physical Stock entry for one of the Pick Items" (line 352). Happens if someone deleted the bin/stock-type row between release and dispatch.
- **Multi-SO batching** — a single `SalesOrderRelease` can cover multiple SOs. Client sends per-SO `ReleaseSalesOrder` calls carrying the first call's returned `SalesOrderReleaseOid` (see `SalesOrderListViewModel.cs` line 201-212).
- **Cancellation token on the client** — client loop honours a `CancellationToken` between SOs but never cancels mid-SO (server call is not cancellable).

### Edge cases **not** handled (gaps)
- No allocation ordering (FIFO/FEFO/proximity).
- No expiry-date awareness.
- No "reserved for another customer" stock type honoured — the PhysicalStockType whitelist is coarse.
- No reversal of a completed release (no "un-release" endpoint).
- No compensation if the dispatch transaction fails after the status transition is committed (transactional, so unlikely but not modelled).

## 9. Notes for a PG rebuild

### 9.1 What to drop
- **XPO-isms**: `UnitOfWork`, `ExplicitLoading`, `PersistentAlias`, `Aggregated`, WCF faults (`FaultException<ServiceFault>`). Use Npgsql transactions with `ISOLATION LEVEL READ COMMITTED` (the default). Replace persistent aliases with views or generated columns.
- **Manual OptimisticLockField bumping.** Postgres gives you better primitives (see 9.3).
- **`NotifyDirtyTypes`** — Npgsql reads inside a transaction are already consistent.
- **Find-by-Name status lookups** (`uow.Query<DeliveryStatus>().FirstOrDefault(s => s.Name == "Generating")`). The comment `// TODO : DeliveryStatus should be found by Order instead of Name` appears twice in the legacy code — fix it the first time by using a text enum or a stable `code` column, not a display name.
- **Sequence service round-trips** — use `BIGSERIAL` or a dedicated sequence per entity.

### 9.2 Transport
Replace the WCF `ISaleFacade` with a small REST surface on Central.Api:

| Legacy | Proposed |
|---|---|
| `ReleaseSalesOrder(params)` | `POST /api/fulfilment/releases` |
| `ReleaseDelivery(params)` | `POST /api/fulfilment/deliveries/{id}/pick` (pick only) and `POST /api/fulfilment/deliveries/{id}/dispatch` (consignment note + stock decrement) — **split the two modes**; the overloaded "consignment note presence = dispatch mode" in the legacy is a trap. |
| Client batches across SOs by threading `SalesOrderReleaseOid` | Keep the same: first call returns the release id, subsequent calls include it. |

Return RFC 7807 problem-json on errors. The `Changes[]` array the legacy returns to drive client cache invalidation is unnecessary — Central uses SignalR `DataChanged` events.

### 9.3 Concurrency in PG
The legacy uses row-level optimistic locking on `PhysicalStock`. In PG, the cleanest equivalent is **`SELECT ... FOR UPDATE`** on the candidate stock rows inside the release transaction:

```sql
SELECT id, quantity, /* ... */
  FROM physical_stock
 WHERE sku_id = $1
   AND bin_location_id = ANY($2)
   AND physical_stock_type_id = ANY($3)
   AND available_stock > 0
 ORDER BY /* explicit policy: FIFO by received_at, or by bin priority */
 FOR UPDATE SKIP LOCKED;
```

`SKIP LOCKED` lets concurrent releases pick non-overlapping rows without blocking. With deterministic ordering you eliminate the retry loop entirely — no more `MaxReleaseAttempts=10`. If you must keep optimistic locking (e.g. app-server-side queuing precludes long-held row locks), add `version INTEGER NOT NULL` to `physical_stock` and bump-on-update.

If the release touches multiple SOs in one call, take a transaction-level **advisory lock** keyed by `(sku_id, bin_location_id)` tuples to prevent deadlocks from inconsistent lock order:

```sql
SELECT pg_advisory_xact_lock(hashtext('physical_stock'::text), id) FROM ...
```

### 9.4 Schema shape (opinionated)

```sql
CREATE TABLE sales_order_releases (
    id             UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    reference      BIGINT NOT NULL UNIQUE,
    created_at     TIMESTAMPTZ NOT NULL DEFAULT now(),
    created_by     UUID NOT NULL REFERENCES app_users(id),
    -- whitelists stored inline as arrays is tempting but don't —
    -- you want to display them, join them, audit them.
    status         TEXT NOT NULL DEFAULT 'open'
        CHECK (status IN ('open','closed','cancelled'))
);

CREATE TABLE sales_order_release_bin_locations (
    release_id       UUID NOT NULL REFERENCES sales_order_releases(id) ON DELETE CASCADE,
    bin_location_id  UUID NOT NULL REFERENCES bin_locations(id),
    PRIMARY KEY (release_id, bin_location_id)
);
-- similar for physical_stock_types and orders

CREATE TABLE sales_order_release_items (
    id                    UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    release_id            UUID NOT NULL REFERENCES sales_order_releases(id),
    sales_order_item_id   UUID NOT NULL REFERENCES sales_order_items(id),
    quantity_requested    INTEGER NOT NULL,
    status                TEXT NOT NULL CHECK (status IN ('released','part_released','failed')),
    error_message         TEXT,
    created_at            TIMESTAMPTZ NOT NULL DEFAULT now()
);
```

Give `SalesOrderRelease` a real `status` (the legacy lacks this — §5.3). Keeps queries honest.

### 9.5 Partial delivery tracking
Legacy keeps `quantity_released` as a running int on the line and `quantity_picked` on the pick item. For PG: keep the integers but generate `quantity_remaining` as a view column — every ad-hoc query recomputes it wrong otherwise. The legacy bug-prone expression `Quantity - QuantityCancelled - QuantityReleased` has to move into one place.

### 9.6 Stock decrement on dispatch
Do it transactionally with the status transition, exactly like the legacy. Write the `stock_adjustments` audit row in the same transaction. Reject negatives with an `EXCLUDE` or a `CHECK (quantity >= 0)` on `physical_stock.quantity` — let the DB enforce the invariant so a bug in application code can't drive it negative.

## 10. What we deliberately didn't copy

- **WCF faulting + `ServiceFault`** — REST + problem-json is the Central standard.
- **XPO `ModifiesParent` pattern** — ORM-specific; PG FKs cover it.
- **`NotifyDirtyTypes` cache busting** — we don't have the WCF-side cache.
- **Status lookups by display name** — fragile; the legacy code has two `// TODO` comments admitting this. Use text enums or stable codes.
- **`new Random()` per-retry for backoff jitter** — replace with a shared `Random.Shared` or `Random.NextInt64()` from a cached instance.
- **Manually setting `OptimisticLockField` via reflection (`SetMemberValue`)** — ugly and brittle. PG row locks are clean.
- **Deleting the Delivery on total failure** (line 221) — consider keeping a failed Delivery as an audit record with `status='void'`. Legacy deletes plus removes the reference number, which we'd want to preserve for traceability. The sequence-number hole left behind isn't strictly a problem, but the audit gap is.
- **Overloading `ReleaseDelivery` with "consignment note = dispatch mode"** — split into two endpoints (pick, dispatch). The mode flip is easy to get wrong from the client side.
- **Client-side `Changes[]` cascade for cache invalidation** — Central uses SignalR `DataChanged` events.
- **`MaxReleaseAttempts = 10` hardcoded constant** — if we do keep an optimistic-lock retry path, make it configurable (appsetting, not constant).
- **No `SalesOrderRelease.status` field** — the legacy lacks one entirely; we should add one.
- **Parallel per-SO release in the client with per-SO server transactions** — workable, but batching by `SalesOrderReleaseOid` is done in the client, not the server. A server-side batch endpoint that takes a list of SOs in one transaction would be cleaner and avoid the client's careful `salesOrderReleaseOid.HasValue` threading.
