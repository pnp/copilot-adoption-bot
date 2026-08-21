# Scaling & Performance Playbook

This document records the **deliberate architectural position** for data storage, the
**capacity model** at the target scale, and a **escalation ladder** to follow if the first
large real-world test shows a bottleneck.

> **Design position:** everything stays on the **Azure Storage account** (Tables, Blobs,
> Queues). Azure Storage is universally accepted by customers, needs no additional
> licensing, and the existing RBAC + private-endpoint configuration already covers it.
> Nothing in this document requires a new backing service unless a documented tripwire
> is actually hit in production.

---

## 1. Target scale

| Dimension | Target |
|---|---|
| Users in tenant | 150,000 |
| Nudges per user per day | ~1 |
| Delivery rows written per day | ~150,000 |
| Delivery rows per year | ~55,000,000 |
| Expected reply rate (AI follow-up) | assume 5–10% |

The headline number is reassuring — the **average** write rate is low:

```
150,000 / 86,400 s = 1.74 delivery rows per second
```

Azure Table Storage targets **~2,000 entities/s per partition** and **~20,000 entities/s
per account**. The workload sits three orders of magnitude below the account ceiling.
Scale problems in this system come from **access patterns**, not from storage throughput.

---

## 2. Prerequisite: instrument before the first large test

**Do not run a 150k test without these.** Several failure modes are silent by
construction — messages can be recorded as delivered when they were not — so an
uninstrumented test will look successful while dropping users.

Minimum instrumentation:

| Metric | Why |
|---|---|
| `nudge.sent` / `nudge.failed` / `nudge.throttled` counters | Detect silent drops |
| `nudge.send_ms` histogram | Detect creeping per-send latency |
| Queue depth, sampled continuously | Detect a stalled or backing-up drain |
| Batch progress on the batch row (`SentCount`, `FailedCount`, `LastProgressUtc`) | Operator visibility without log scraping |
| Storage `Transactions` split by `ResponseType` | Detect `ClientThrottlingError` / `ServerBusyError` |
| Process working set + gen2 GC | Detect memory pressure before OOM |
| AI tokens/day and concurrent completions | Detect cost runaway |

Log **aggregates** at `Information` and per-message detail at `Debug`. 150,000
Information entries per batch is both expensive to ingest and useless to read.

---

## 3. Capacity model — what we expect, and what to compare against

Baseline expectations for a 150,000-recipient batch on the current design:

| Stage | Model | Expected |
|---|---|---|
| Enqueue | 150,000 sends ÷ 16 parallel × ~20 ms | **~3 min** |
| Delivery drain | 8 concurrent ÷ ~400 ms per send = ~20 msg/s | **~2 hours** |
| Teams rate-limit floor | ~1,800 proactive ops / 30 s | **~42 min minimum** |
| Log writes | 1,500 transactions × 100, across 16 shard partitions | seconds |
| History storage | 55M rows × ~400 B ≈ 22 GB | **~$1–2 / month** |

If measured numbers are within ~2× of these, the design is behaving. If they are
10× worse, work down the relevant ladder in §4.

---

## 4. Escalation ladder

Each ladder is ordered **cheapest and least disruptive first**. Do not jump to the
bottom rung without evidence.

### 4.1 Delivery throughput (drain takes too long)

**Signal:** queue depth flat or falling too slowly; batch not complete within the send window.

1. **Raise per-worker concurrency.** `BatchMessageProcessorService` uses
   `maxParallelism = 8`. Make it configurable and raise to 16–32. Watch for Graph/Teams
   429s as you do — throughput is usually limited by the *remote* rate limit, not by us.
2. **Scale out worker instances.** Storage Queues load-balance across consumers
   naturally; each instance dequeues its own messages. This is the main lever and needs
   no code change once the worker runs outside the web app.
3. **Tune dequeue batch vs visibility.** Keep `dequeue count ≈ concurrency` so a whole
   batch finishes well inside the visibility timeout, and renew visibility for in-flight
   messages rather than relying on a long fixed timeout.
4. **Spread the send window.** With per-user timezone scheduling, load naturally spreads
   across ~12 hours instead of bursting — this often removes the problem entirely and
   improves the user experience at the same time.

### 4.2 Enqueue time (batch takes too long to be accepted)

**Signal:** time from "batch accepted" to "all messages enqueued" is many minutes.

1. **Raise enqueue parallelism** from 16 to 32–64. Storage Queues target ~2,000
   messages/s per queue, so there is room.
2. **Chunk and checkpoint** so enqueue progress survives a worker restart and does not
   restart from zero.
3. **Tripwire → Azure Service Bus.** Storage Queues have **no batch-send API**, so 150,000
   messages means 150,000 round trips. Service Bus `SendMessagesAsync` would collapse
   that to ~1,500 calls, and adds native dead-lettering.
   *Only worth adopting if enqueue time is genuinely the bottleneck* — in a background
   job, a few minutes of enqueue is usually irrelevant.

### 4.3 Table partition throttling

**Signal:** storage `Transactions` showing `ClientThrottlingError` / `ServerBusyError`
above ~1% sustained; rising storage latency.

1. **Increase the shard count** on the delivery partition key
   (`PartitionKey = {batchId}~{shard}`) from 16 to 64. Make it a config value.
   Re-sharding applies to **new batches only**, so there is no data migration.
2. **Confirm reads are targeted** — any remaining throttling usually means something is
   still scanning. Non-key filters (`RecipientUpn`, `Status`, `MessageBatchId`) are the
   usual culprit.
3. **Shard the user tables** (`usercache`, `ConversationCache`) by a hash bucket of the
   object id if user-directory operations are the source.

### 4.4 Statistics / dashboard latency

**Signal:** stats endpoints slower than ~1–2 s.

1. **Verify counters are being used.** Aggregates must come from the batch row
   (~700 rows/year), never from delivery rows. If a stats call is slow, something is
   still scanning history.
2. **Add daily rollup rows** if the number of batches ever grows large.
3. **Tripwire → external analytics.** Only if genuine ad-hoc analysis is required
   ("failure reasons by department last quarter"). Archive raw rows to Blob as
   JSONL/Parquet before dropping old tables, then point **Synapse Serverless** or
   **Azure Data Explorer** at the container. This is an *additive, read-side* choice —
   the application keeps writing to Storage and needs no change.

### 4.5 Graph / Teams throttling

**Signal:** sustained 429s, growing `Retry-After` values.

1. **Reduce dispatch rate** — this is a remote limit; going faster makes it worse.
   Implement a shared token bucket sized to ~1,500 ops/30 s to stay under the limit
   deliberately rather than discovering it.
2. **Remove avoidable calls first.** Resolving UPN→object id from the user cache and
   folding `assignedLicenses` into the delta query eliminate ~300,000 calls/day between
   them. Do this before touching rate configuration.
3. **Spread across the day** via timezone-aware scheduling.

### 4.6 Memory pressure

**Signal:** working set above ~70% of available; gen2 growth; any `OutOfMemoryException`.

1. **Switch the App Service worker to 64-bit.** There is no reason to run .NET 10 as a
   32-bit process; this is a one-setting change.
2. **Confirm nothing loads a full table.** Point reads and paging should mean memory is
   independent of tenant size. If memory scales with user count, a full-table load has
   been reintroduced.
3. **Move up from B1.** B1 is 1 core / 1.75 GB and also serves the SPA, the admin API and
   the bot endpoint. Note **`AlwaysOn` is fixed off by policy**, which is a further reason
   background work should not live in the web app.

### 4.7 AI cost / latency

**Signal:** token spend above budget; Foundry 429s; follow-up replies slow.

1. **Confirm the concurrency gate and token caps are in place** (bounded parallelism,
   request timeout, trimmed history, card *summary* rather than raw card JSON).
2. **Cache aggressively** — system prompt, and per-batch card context.
3. **Use a smaller/cheaper model** for follow-up chat; reserve the larger model for
   genuinely ambiguous work.
4. **Consider Provisioned Throughput (PTU)** only if sustained concurrency justifies it.

### 4.8 Storage growth

**Signal:** storage cost or table size becoming notable.

1. **Shorten the retention window.** History lives in date-suffixed tables
   (`messagelogs<YYYYMM>`), so retention is `DeleteTableAsync` — one instant call rather
   than millions of row deletes.
2. **Archive to Blob before dropping** if the data may be needed later. Cool/Archive tier
   makes long-term retention nearly free.

> Note: at ~22 GB/year (~$1–2/month) storage growth is **not** a performance concern once
> history is never scanned. Retention here is compliance and data-minimisation hygiene,
> not survival.

---

## 5. Tripwires — when to genuinely reconsider the backend

Azure Storage stays unless one of these is *measured*, not anticipated:

| Tripwire | Why Storage would stop fitting | Candidate |
|---|---|---|
| Ad-hoc multi-dimensional analytics become a product requirement | Tables cannot aggregate, group or join; no secondary indexes | Blob archive + Synapse Serverless / ADX (read side only) |
| Transactional consistency needed **across** partitions or entities | Table transactions are single-partition, max 100 ops | Azure SQL |
| Hand-rolled secondary indexes multiply beyond ~2 | Each one is a coordinated dual write and a consistency risk | Azure SQL |
| Sustained account-level throttling after sharding to 64+ partitions | Approaching the ~20,000/s account target | Partition across storage accounts, or Cosmos DB |

The first row is by far the most likely, and it is satisfiable **without changing how the
application writes data** — archive to Blob and query externally.

---

## 6. Related

- [Deployment Guide](DEPLOYMENT.md) — resources, tables, queues and containers in use
- [Configuration Reference](CONFIGURATION.md) — tunable settings
- Open scalability issues are tracked in GitHub under the `scalability` and
  `performance` labels.

---

## 7. Hosting: moving background work off the web app

**This is a deployment decision, not a code one.** The application code is now written so the
move is straightforward, but it has not been made.

### What the code already does

`AlwaysOn=false` is fixed by policy, so the worker is unloaded whenever it goes idle —
interruption is the norm, not the exception. Background work is therefore built to survive it:

| Property | Where |
|---|---|
| **Idempotent deliveries** | Delivery rows are keyed by `(batchId~shard, recipientUpn)`, so re-processing upserts rather than duplicating |
| **Checkpointed expansion** | `BatchExpansionService` records progress on the batch row every 5,000 recipients and resumes from there |
| **Transient failures redeliver** | The dispatcher leaves the queue message in place; only terminal outcomes delete it |
| **Rate shaping survives restart** | The token bucket refills from wall-clock time, so a restart doesn't produce a burst |

An interrupted run therefore resumes correctly. What it *cannot* do is make progress while the
worker is unloaded — a 150,000-message drain takes ~2 hours of active processing, and queue
polling generates no HTTP traffic to keep the process alive.

### Options, in order of preference

**A. Azure Functions (queue trigger)** — recommended.
- Native `QueueTrigger`; the platform activates on message arrival, so `AlwaysOn` is irrelevant.
- Scales out on queue depth: 150,000 messages drain across many instances instead of 8-at-a-time
  on one.
- Built-in poison queue, retry policy, per-execution billing.
- Use Flex Consumption or Premium for VNet integration, which this deployment needs for its
  private endpoints.

**B. Azure Container Apps with KEDA.**
- KEDA `azure-queue` scaler scales replicas 0→N on queue length.
- Keeps a single deployable container if splitting the codebase is unattractive.

**C. Stay on App Service, accept the constraint.**
- Viable only if a nudge run is small enough to finish inside an active window, or if something
  external keeps the worker warm (a scheduled ping).
- The hosted services will still be torn down mid-run; correctness is preserved, completion time
  is not.

### Migration shape

The background services are already independent of the web request pipeline, so option A means:

1. New Functions project referencing `Engine`.
2. `BatchMessageProcessorService` → a `[QueueTrigger("batch-messages")]` function.
3. `BatchExpansionService` → a `[QueueTrigger("batch-control")]` function.
4. `CacheWarmupHostedService` → a `[TimerTrigger]` function.
5. The web app keeps only request/response work: accept the batch, return `202`, serve the API
   and SPA.

Nothing in `Engine` needs to change — the services take their dependencies through DI and do not
reference `IApplicationBuilder` or the HTTP pipeline.

### Also outstanding on the current host

- **64-bit worker** (`use32BitWorkerProcess: false`) — no reason to run .NET 10 as 32-bit.
- **`healthCheckPath`** — the app exposes `/health`, but App Service is not configured to use it,
  so an unhealthy instance is never recycled.
- **Data Protection key persistence** — keys regenerate on every restart.
