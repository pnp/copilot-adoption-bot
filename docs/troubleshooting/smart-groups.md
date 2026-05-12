# Smart Groups

[← Back to Troubleshooting Guide](../TROUBLESHOOTING.md)

## Why does resolving a smart group take so long?

**Symptoms:**
- Clicking *Resolve* (or *Preview*) on a smart group spins for several seconds — sometimes a minute or more on large tenants.
- Sending a nudge to a smart group has a noticeable delay before the first message is queued.
- Subsequent loads of the same group are near-instant, then go slow again later.

This is expected. Smart group resolution is **not** a simple database query — it is an AI call against your full user directory. The result is cached so that you only pay the cost occasionally, not on every page load.

### How the cache works

Smart group membership is stored in the `smartgroupmembers` Azure Table, partitioned by smart group ID. The owning group entity in the `smartgroups` table tracks two cache fields:

- `LastResolvedDate` — when the group was last fully resolved by AI
- `LastResolvedMemberCount` — how many members were returned

The service uses a **1 hour TTL** on this cache (`SmartGroupService.ResolveSmartGroupMembers`):

| Situation | Behavior | Speed |
|-----------|----------|-------|
| `LastResolvedDate` is less than 1 hour old, members exist in `smartgroupmembers` | Cached rows are returned directly. `FromCache = true`. | Fast (storage read) |
| `LastResolvedDate` is null, older than 1 hour, or the cache rows are missing | Full AI re-resolution runs and the cache is rewritten. `FromCache = false`. | Slow (Graph + AI) |
| The caller passes `forceRefresh = true` | Cache is bypassed and AI runs even if it's fresh. | Slow (Graph + AI) |
| Preview (`PreviewSmartGroupMembers`) | Never reads or writes the cache — always runs against AI. | Always slow |

When you send a nudge to a smart group, `GetSmartGroupUpns` checks the same 1 hour TTL and silently re-runs resolution if the cache is stale. That is why the *first* nudge of the day to a given smart group is slower than later ones.

### Why the cold path is slow

On a cache miss the service has to do three things end‑to‑end before it can return:

1. **Load every user with metadata** via `CachedUserService.GetAllUsersWithMetadataAsync()`. If the user cache is warm, this is a table scan of `usercache`; if not, it is a Microsoft Graph `/users` enumeration (delta or full) plus a join with the cached Copilot usage data. On large tenants this alone can be tens of seconds. See [Copilot Stats Issues](./copilot-stats.md) for the related user/stats cache.
2. **Send the full candidate list and the natural‑language group description to AI Foundry** (`AIFoundryService.ResolveSmartGroupMembersAsync`). This is a single LLM call whose latency scales with the number of users serialised into the prompt and the model deployed in your AI Foundry project.
3. **Write the matched members back** to `smartgroupmembers` using batched table transactions, then update `LastResolvedDate` / `LastResolvedMemberCount` on the group entity.

There is no streaming UI here — the API only returns once all three steps have completed.

### What to do about it

Most of the time the right answer is "nothing, that's how it's designed to work." Specific issues:

1. **First resolution after deployment is the slowest.** The user cache may be empty and has to be populated from Graph before AI can be called. Run a user cache sync (or wait for the scheduled one) before exercising smart groups for the first time.
2. **Every page load triggers a fresh AI call.** Check that `LastResolvedDate` is actually being written on the group. A common cause is a 1+ hour gap between requests, in which case the cache is doing exactly what it should. If the gap is shorter, look for callers passing `forceRefresh = true`, or for the cache rows in `smartgroupmembers` being deleted out of band.
3. **Resolution times out.** The AI Foundry call is the long pole. Check the Foundry project deployment — a model that has been scaled to zero or is throttled will block the request. Application Insights dependencies will show the call duration.
4. **Preview is always slow.** Expected — *Preview* is the "I'm still editing the description" path and intentionally never caches. Save the group and use *Resolve* instead once you're happy with the description.
5. **Members look stale.** They can be up to 1 hour old by design. Use *Resolve* with the force refresh option to bypass the cache and re-run AI immediately.
6. **AI is not configured.** If `AIFoundryService` is not registered (`IsAIEnabled = false`, i.e. Copilot Connected mode disabled), any cache‑miss resolution will throw `"AI Foundry is not configured. Copilot Connected mode is disabled."` — the cache will never refill until AI Foundry is configured. See [Setup](../SETUP.md) for AI Foundry configuration.
