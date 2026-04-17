# Reference: App-level Sequence Generation (legacy TotalLink)

> Captured 2026-04-17 from `source/TIG.TotalLink.Server` before that tree was removed.
> PostgreSQL's native `NEXTVAL()` is the default for new code in Central. This
> doc is for the cases where you need more than that — formatted codes,
> year-reset counters, sequences that external systems expect.
> See [LEGACY_MIGRATION.md](LEGACY_MIGRATION.md) for context.

## 1. What problem this solves

`NEXTVAL()` on a PostgreSQL sequence gives you fast, monotonic, gap-tolerant
integers. That's perfect for surrogate primary keys but falls short whenever
a number has to leave the database as part of an identifier that a human or
another system will see. Concretely:

- **Formatted identifiers** — `SO-2026-00012`, `INV/11/00042`, `REF-7734000011`.
  You want zero-padding, separators, a prefix, maybe an embedded system code
  or year. PG sequences return bare bigints; you'd wrap every call site with
  the same formatting string and hope nobody drifts.
- **Year-reset counters** — invoice number starts over at `00001` every
  January. `NEXTVAL()` can't do that without a DDL operation (`ALTER
  SEQUENCE ... RESTART`) which isn't transactional and races badly.
- **Name-keyed lookup** — one logical "sequence generator" per entity type
  (SalesOrderRelease, Delivery, Invoice, …) chosen by string key at runtime.
  PG sequences are schema objects, so adding a new one is a migration, not a
  row insert.
- **External system contract** — Navision, SAP, the ME ticketing system, a
  customer portal — all expect numbers in a format they defined. Anything
  you push out needs to match their shape exactly, and often needs to come
  back unchanged on round-trip.
- **Per-row configuration** — you want a UI where an admin can change the
  format string or the reset policy without a DDL migration.

When any of those apply, you're reaching for an app-level sequence table.

## 2. Legacy Sequence entity

Lives at
`source/TIG.TotalLink.Server/Shared/DataModel/TIG.TotalLink.Shared.DataModel.Admin/DataModelCode/Sequence.Designer.cs`.
XPO-generated, three fields on top of the standard `DataObjectBase`
(which supplies `Oid`, row version for optimistic concurrency, audit cols):

```csharp
public partial class Sequence : DataObjectBase
{
    [Size(1000)] public string Name { get; set; }   // lookup key — e.g. "SalesOrderRelease"
    public int    Code       { get; set; }          // short numeric id baked into the formatted output
    public long   NextNumber { get; set; }          // the counter. Returned, then incremented.
}
```

Notable **absences** — captured here so nobody misreads the legacy intent:

- No `Prefix`, no `Format`, no `YearReset`, no `LastResetYear`. The legacy
  design pushed all formatting into a **system-wide** `ReferenceValueFormat`
  setting (see §7), not per-sequence.
- No last-used-timestamp. Whatever year/period semantics you want must be
  layered on top — the entity itself is just a name-keyed `long`.

## 3. The `GetSequenceNumber()` pattern

The method is called `GetSequenceNumber` on the server (the facade renames
it to `GetNextSequenceNumber` for the client). Full body from
`AdminMethodService.svc.cs` lines 41–99:

```csharp
private const int MaxSequenceGenerationAttempts = 10;

public GetSequenceNumberResult GetSequenceNumber(string sequenceName)
{
    var result = new GetSequenceNumberResult();
    var adminFacade = GetAdminDataFacade();

    try
    {
        using (var uow = adminFacade.CreateUnitOfWork())
        {
            var attemptsRemaining = MaxSequenceGenerationAttempts;

            while (true)
            {
                try
                {
                    // To make sure we get up-to-date data, flag the Sequence table as dirty
                    adminFacade.NotifyDirtyTypes(typeof(Sequence));

                    // Attempt to get the requested sequence
                    var sequence = uow.Query<Sequence>().FirstOrDefault(s => s.Name == sequenceName);
                    if (sequence == null)
                        throw new FaultException<ServiceFault>(new ServiceFault(
                            string.Format("Failed to find a sequence named '{0}'!", sequenceName)));

                    // Store and increment the sequence number
                    result.SequenceCode   = sequence.Code;
                    result.SequenceNumber = sequence.NextNumber;
                    sequence.NextNumber++;

                    // Commit changes
                    uow.CommitChanges();

                    // Record the changed entities
                    result.EntityChanges = new[] { new EntityChange(sequence, EntityChange.ChangeTypes.Modify) };
                    return result;
                }
                catch (LockingException)
                {
                    // Rollback the failed changes
                    uow.RollbackTransaction();

                    // If the sequence was updated elsewhere, retry until attemptsRemaining = 0
                    if (--attemptsRemaining <= 0)
                        throw new FaultException<ServiceFault>(new ServiceFault(
                            string.Format("Failed to get a sequence number for '{0}' after {1} attempts!",
                                sequenceName, MaxSequenceGenerationAttempts)));

                    // Wait a small random delay before the next attempt
                    Thread.Sleep(new Random().Next(10, 100));
                }
            }
        }
    }
    catch (Exception ex) { /* wrap + rethrow as FaultException */ }
}
```

What's going on:

- **Locking primitive** — `LockingException` is XPO's **optimistic**
  concurrency exception. Every `DataObjectBase` carries a `OptimisticLockField`
  row-version; XPO's `CommitChanges()` issues `UPDATE … WHERE oid = ? AND
  optimisticlock = ?`. If another worker bumped the same sequence row
  between the SELECT and the UPDATE, zero rows update and `LockingException`
  is raised.
- **Retry loop** — `MaxSequenceGenerationAttempts = 10`. On each miss, the
  UoW is rolled back and the row is re-read (the `NotifyDirtyTypes` call
  busts the XPO in-memory cache so the next `Query<Sequence>()` hits the
  DB). After 10 collisions in a row, a `FaultException` bubbles to the
  caller.
- **Backoff** — `Thread.Sleep(new Random().Next(10, 100))` — uniform 10–100
  ms jitter. Not exponential. `new Random()` with no seed is time-seeded; in
  this per-request path that's fine.
- **Thread safety** — `new Random()` inside a handler is weak but
  inconsequential here (request-per-thread on IIS, jitter only needs to be
  "not identical across racing requests").

The pattern is **read-mutate-write with optimistic retry**. On a busy
sequence (say an order-number generator during a Black Friday spike) every
concurrent caller reads the same `NextNumber`, one wins the commit, the
others `LockingException` → rollback → sleep → retry. Throughput is
bounded by `1 / (avg_commit_latency + avg_jitter)`, roughly 10–100/sec on
the hottest key. Acceptable for order/invoice issuance, inadequate if you're
numbering every row in a bulk insert.

## 4. How it's called (callers)

Three call sites in the tree, all via the facade method
`IAdminFacade.GetNextSequenceNumber(string)`:

| Caller | Sequence name | Destination of the formatted number |
|---|---|---|
| `ReferenceDataObjectExtension.GenerateReferenceNumber()` (client, sync + async) | `referenceDataObject.GetType().Name` — e.g. `"Contact"`, `"Product"`, `"Location"` | `IReferenceDataObject.Reference` (a `long` on the entity) — shown in UI, indexed in DB, printed on reports |
| `SaleMethodService.svc.cs:123` | `salesOrderRelease.GetType().Name` → `"SalesOrderRelease"` | `SalesOrderRelease.Reference` — the customer-facing order release number |
| `SaleMethodService.svc.cs:176` | `delivery.GetType().Name` → `"Delivery"` | `Delivery.Reference` — the customer-facing despatch note number |

Every caller does the same thing to the result:

```csharp
var result = adminFacade.GetNextSequenceNumber(entity.GetType().Name);
entity.Reference = ReferenceNumberHelper.FormatValue(
    AppContextViewModel.Instance.SystemCode,
    result.SequenceCode,
    result.SequenceNumber,
    AppContextViewModel.Instance.ReferenceValueFormat);
```

So in practice, "a sequence" in TotalLink is always of the form
`<SystemCode, EntityTypeName-keyed SequenceCode, per-type counter>`
composed into a single `long` by `string.Format` (see §7). The sales-order
path then wraps the whole thing in a distributed unit of work so that if
the wider transaction fails, the sequence row still got incremented — the
number is "lost" but that's considered acceptable (invoice/order gaps are
allowed).

## 5. Why you'd pick this over PG `NEXTVAL`

| Scenario | Winner | Why |
|---|---|---|
| Surrogate PK on a growing table | `NEXTVAL` / `IDENTITY` | Unbeatable throughput. No collisions. No app code. |
| Customer-facing order/invoice number | App-level sequence | Need formatted output, per-sequence config, possibly year reset |
| Number must match an external system's regex | App-level sequence | `NEXTVAL` gives you raw `bigint`; you'd still be formatting in app code — put it in one place |
| Number must reset on Jan 1 | App-level sequence | `ALTER SEQUENCE … RESTART` isn't transactional and breaks under race. A row update with a `WHERE year_changed` guard is |
| Hundreds of distinct named sequences, added via UI | App-level sequence | Each new PG sequence is a DDL migration |
| Bulk insert numbering a million rows | `NEXTVAL` | App-level pattern tops out around 10² ops/sec under contention |
| Number must round-trip through Navision unchanged | App-level sequence | Format is defined once in config, not scattered |
| Strict no-gaps required (compliance audit numbers in some jurisdictions) | Neither trivially — needs a serializable txn wrapper around the whole consuming operation | Both leak gaps on rollback. See §8. |

Rule of thumb: if the number is a **primary key**, use `NEXTVAL`. If the
number is a **business identifier** that leaves the database, use the
app-level pattern.

## 6. Implementation in PG

The opinionated Central rebuild. Single table, single function, no retry
loop needed because `FOR UPDATE` queues instead of throwing.

```sql
-- Schema --------------------------------------------------------------
CREATE TABLE app_sequences (
    name             text        PRIMARY KEY,
    current_value    bigint      NOT NULL DEFAULT 0,
    sequence_code    integer     NOT NULL DEFAULT 0,   -- embedded in formatted output
    prefix           text        NOT NULL DEFAULT '',  -- e.g. 'SO-', 'INV/'
    format           text        NOT NULL DEFAULT '{0}', -- .NET composite format; see §7
    year_reset       boolean     NOT NULL DEFAULT false,
    last_reset_year  integer,
    reset_timezone   text        NOT NULL DEFAULT 'UTC',
    updated_at       timestamptz NOT NULL DEFAULT now()
);

-- Seed --------------------------------------------------------------
INSERT INTO app_sequences (name, sequence_code, format) VALUES
    ('SalesOrderRelease', 21, '{2:#}{0:00}{1:00}'),
    ('Delivery',          22, '{2:#}{0:00}{1:00}'),
    ('Invoice',           30, 'INV-{year}-{2:D6}');  -- year-reset style

-- Acquire function --------------------------------------------------
CREATE OR REPLACE FUNCTION next_sequence_value(
    p_name        text,
    p_system_code integer DEFAULT 11
)
RETURNS TABLE (
    sequence_code  integer,
    sequence_number bigint,
    formatted      text
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_row        app_sequences%ROWTYPE;
    v_this_year  integer := EXTRACT(YEAR FROM (now() AT TIME ZONE COALESCE(
        (SELECT reset_timezone FROM app_sequences WHERE name = p_name), 'UTC')))::int;
BEGIN
    -- FOR UPDATE serializes concurrent callers on this row only.
    -- Other sequence rows remain fully parallel.
    SELECT * INTO v_row FROM app_sequences WHERE name = p_name FOR UPDATE;
    IF NOT FOUND THEN
        RAISE EXCEPTION 'Sequence % not found', p_name USING ERRCODE = 'P0002';
    END IF;

    -- Year-reset: reset the counter if we've crossed a year boundary.
    IF v_row.year_reset AND COALESCE(v_row.last_reset_year, 0) <> v_this_year THEN
        v_row.current_value   := 0;
        v_row.last_reset_year := v_this_year;
    END IF;

    v_row.current_value := v_row.current_value + 1;

    UPDATE app_sequences
       SET current_value   = v_row.current_value,
           last_reset_year = v_row.last_reset_year,
           updated_at      = now()
     WHERE name = p_name;

    sequence_code   := v_row.sequence_code;
    sequence_number := v_row.current_value;
    -- Formatting is done in app code (.NET composite format is not 1:1 with
    -- PG format()); this column is advisory. See §7.
    formatted       := v_row.prefix || v_row.current_value::text;
    RETURN NEXT;
END $$;
```

**Why no retry loop is needed.** `SELECT … FOR UPDATE` makes concurrent
callers **wait** (row-level lock) rather than fail-then-retry. The caller
acquires the lock, mutates, releases on commit/rollback. Semantically
identical to the XPO loop, implementation-wise no `Thread.Sleep`, no jitter,
no `MaxAttempts`.

**Lighter alternative — advisory locks.** If you don't care about persisting
`current_value` in the same row and can regenerate from elsewhere, or you
want to guard a counter that lives on another table entirely:

```sql
-- Inside the consuming transaction
SELECT pg_advisory_xact_lock(hashtext('SalesOrderRelease'));
-- ... do your read-mutate-write on whatever table ...
-- Lock auto-releases at txn end.
```

Prefer `FOR UPDATE` on `app_sequences` — it's self-documenting, pairs the
lock with the data, and survives schema changes.

**Transactional scope.** If you want "allocate a number AND use it
atomically, roll back both on failure," call `next_sequence_value` inside
the consuming transaction. The lock is held until that outer txn commits.
That kills concurrency on the hot sequence — every order-creation queues
behind the previous one until it fully commits. Usually acceptable; if
you're hitting it, either (a) accept gaps by calling the function in a
separate short-lived txn (the legacy behaviour) or (b) pre-allocate a block
(§8).

## 7. Format string conventions from the legacy code

The legacy system carried one global format setting in
`Setting_Release.xml`:

```xml
<Setting Name="SystemCode"            Value="11" />
<Setting Name="ReferenceValueFormat"  Value="{2:#}{0:00}{1:00}" />
<Setting Name="ReferenceDisplayFormat" Value="{0:####-####-####-####-####}" />
```

And formatted via .NET composite format in
`ReferenceNumberHelper.FormatValue`:

```csharp
string.Format(format, systemCode, sequenceCode, sequenceNumber);
// with format = "{2:#}{0:00}{1:00}", systemCode=11, sequenceCode=21, seq=42
//   -> "422111"   (then long.Parse → 422111)
```

Positional indices in the default `ReferenceValueFormat`:

| Index | Meaning | Specifier | Result for `(11, 21, 42)` |
|---|---|---|---|
| `{0}` | SystemCode | `:00` → 2-digit zero pad | `11` |
| `{1}` | SequenceCode | `:00` → 2-digit zero pad | `21` |
| `{2}` | SequenceNumber | `:#` → no padding, full number | `42` |

The composition is `{seq}{systemCode:00}{sequenceCode:00}` → `422111`, then
parsed back to a `long` and stored. Display later re-formats with
`FormatDisplay` using `{0:####-####-####-####-####}`.

**What was NOT in the legacy code:**

- No `{year}` token. Year reset is not implemented anywhere in the source.
- No letter prefixes (`SO-`, `INV-`). The entire identifier is numeric.
- No per-sequence format override. One global setting.

So for Central, the "legacy format convention" is narrower than you might
assume: **a single .NET composite format string applied to
`(SystemCode, SequenceCode, SequenceNumber)` producing an all-numeric
identifier**. If you want year prefixes, letter prefixes, or per-sequence
formats — that's new work, not a carry-forward. Preserve the positional
convention `(system, code, number)` so existing external systems keep
working; add new tokens additively.

Rebuilding format application in C# on Central is one line:

```csharp
var formatted = string.Format(seq.Format, systemCode, seq.SequenceCode, seq.Value);
```

## 8. Gotchas

- **Gaps are permanent.** The legacy pattern increments in its own UoW
  commit; if the outer business transaction then rolls back, the number
  is gone. Orders 1000, 1002, 1003 with no 1001. Usually fine. In some
  jurisdictions (Italian e-invoicing, legal archival) this is illegal —
  you need a strict gapless sequence, which means holding the lock across
  the entire consuming transaction (accepting the throughput hit) and
  having a compensating "void" record for rollbacks.
- **Year-reset race at midnight.** Two concurrent calls at
  `2026-12-31T23:59:59.999` / `2027-01-01T00:00:00.001`: one sees old year,
  one sees new. With `FOR UPDATE` this is deterministic — the second caller
  waits, reads the row the first one wrote, sees `last_reset_year = 2026`,
  resets. Fine. Without the row lock (e.g. if you're using advisory locks
  and storing the counter somewhere else), you can get a duplicate `00001`.
- **Reset timezone.** Is "new year" UTC, site-local, customer-local? The
  legacy code didn't reset, so the question didn't arise. Nail it down in
  the table (`reset_timezone text`) rather than hard-coding.
- **Strict monotonicity vs uniqueness.** `NEXTVAL` guarantees monotonicity
  within a single backend but **not** globally (different backends can
  interleave cached ranges). `FOR UPDATE` on `app_sequences` gives you
  strict global monotonicity. Don't assume either unless you asked.
- **Pre-reserved blocks.** If you need `10 000 order numbers/sec`, issue a
  block of (say) 1 000 per caller: the function returns `(start, end)`, the
  caller doles them out in-process, refills when exhausted. Throughput
  scales linearly with block size. Downside: on crash you leak the
  remaining unused numbers — bigger gaps. Useful for synthetic load, rarely
  needed for business identifiers.
- **Lost update without `FOR UPDATE`.** Don't be tempted by `UPDATE
  app_sequences SET current_value = current_value + 1 … RETURNING` without
  the explicit `FOR UPDATE` — that works for a simple counter but breaks
  the moment you add year-reset logic that needs to read-then-decide-then-write.
- **Don't cache.** The legacy XPO code deliberately calls
  `NotifyDirtyTypes(typeof(Sequence))` before every query to bust the
  ORM cache. If you put the result of `next_sequence_value` behind any
  cache in Central, you will hand out duplicates.
- **Prefer one txn per allocation if the outer operation is slow.** If
  "create invoice" includes a 2-second API call to Stripe, holding the
  sequence lock across it is a DoS on order-taking. Take the number first
  in its own short txn (accepting the gap-on-failure risk), then do the
  slow work.

## 9. What we deliberately didn't copy

- **The `LockingException` retry loop.** PG `FOR UPDATE` queues; we don't
  need optimistic retry. The loop was necessary in XPO because XPO only
  does optimistic locking.
- **`NotifyDirtyTypes` cache-bust.** Central's `IDataService` doesn't have
  XPO's entity cache. Not applicable.
- **Composing the final identifier into a `long`.** Legacy stuffed
  `(systemCode, sequenceCode, sequenceNumber)` into one parseable `long`
  because XPO's reference field was `long`. Central should store the
  formatted string directly (`text`) — trivial to index, no parse-round-trip
  ambiguity, allows letter prefixes later.
- **Global system-wide format.** Per-sequence format in the row is
  strictly better. The legacy global was a simplification that constrained
  every entity type to the same numeric shape; there's no reason to carry
  that forward.
- **Facade/WCF plumbing.** All that `IAdminFacade` / `GetAdminDataFacade()`
  / `ServiceFault` wrapping exists because of the WCF client-server
  topology. Central has direct DB access or REST; just call the SQL
  function.
