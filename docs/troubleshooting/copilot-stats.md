# Copilot Stats Issues

[← Back to Troubleshooting Guide](../TROUBLESHOOTING.md)

## Error: "Reports.Read.All permission not granted"

**Symptoms:**
- Cannot update Copilot stats
- Permission errors in logs

**Solutions:**

1. **Add Permission:**
   - Go to Azure Portal → Entra ID → App registrations → Your app
   - API permissions → Add permission
   - Microsoft Graph → Application permissions
   - Select `Reports.Read.All`

2. **Grant Admin Consent:**
   - Click "Grant admin consent"
   - Wait 5-10 minutes

3. **Verify Permission:**
   - Check that green checkmark appears next to permission

## No Copilot Data Returned

**Symptoms:**
- Update completes but no data
- Empty statistics

**Solutions:**

1. **Verify Copilot Licenses:**
   - Ensure your tenant has Microsoft 365 Copilot licenses
   - Check that licenses are assigned to users

2. **Check Reporting Period:**
   - Microsoft Graph reports have 24-48 hour delay
   - Ensure users have activity in selected period (D7, D30, etc.)

3. **Verify Active Usage:**
   - Reports only show users with recent activity
   - Test with known active Copilot users

4. **Regional Availability:**
   - Verify Copilot is available in your tenant's region

## API Rate Limiting

**Symptoms:**
- Stats update fails with rate limit errors
- Throttling messages in logs

**Solutions:**

1. **Increase Refresh Interval:**
   ```json
   {
     "CopilotStatsRefreshInterval": "48:00:00"  // 48 hours
   }
   ```

2. **Retry Logic:**
   - Application includes automatic retry with exponential backoff
   - Check logs for retry attempts

3. **Schedule Updates:**
   - Run stats updates during off-peak hours
   - Stagger updates if multiple tenants

## How the Copilot stats cache works (and why it can lag)

**Symptoms:**
- A user's Copilot activity dates in Smart Groups, nudges, or the dashboard look outdated — sometimes by a day or more.
- Running *Update Copilot Stats* in the UI logs `"Copilot stats are still fresh, skipping update"` and returns immediately.
- A user who was sent a nudge does *not* appear in the next Copilot stats import.
- The first Copilot stats update after a deployment is slow; subsequent updates are no‑ops.

This is by design. Copilot stats and user directory data live in the **same `usercache` table**, but they refresh on **different schedules**, and stats are joined onto users by UPN.

### One table, two schedules

The `usercache` Azure Table stores one row per user. Each row holds:

- Directory metadata from Microsoft Graph (`displayName`, `department`, `manager`, `hireDate`, etc.) and license info.
- Copilot activity columns (`CopilotLastActivityDate`, `CopilotChatLastActivityDate`, `TeamsCopilotLastActivityDate`, `WordCopilotLastActivityDate`, etc., plus `LastCopilotStatsUpdate`).

There is **no separate stats table**. Stats are columns on the user row. But the refresh of those two sets of columns is tracked independently in `usersyncmetadata`:

| Column | Refreshed by | Tracked in `usersyncmetadata` | Default TTL | Configured via |
|--------|--------------|-------------------------------|-------------|----------------|
| Directory + license | `UserCacheManager.SyncUsersAsync` (Graph `/users` delta) | `LastDeltaSyncDate`, `LastFullSyncDate`, `DeltaToken` | 1 hour delta, 7 day full sync | `UserCacheConfig:CacheExpiration`, `UserCacheConfig:FullSyncInterval` |
| Copilot activity dates | `UserCacheManager.UpdateCopilotStatsAsync` (Graph `getMicrosoft365CopilotUsageUserDetail`) | `LastCopilotStatsUpdate` | 24 hours | `UserCacheConfig:CopilotStatsRefreshInterval` |

Both are configurable in `appsettings.json` under `UserCacheConfig`. The reporting window for the Copilot API (D7, D30, D90, D180) is controlled by `UserCacheConfig:CopilotStatsPeriod` (default `D30`).

### Order of operations on a stats refresh

When `UpdateCopilotStatsAsync` runs (manually via `POST /api/UserCache/UpdateCopilotStats`, or implicitly when downstream code requests it):

1. Check `LastCopilotStatsUpdate` in `usersyncmetadata`. If less than `CopilotStatsRefreshInterval` ago, **return immediately** without calling Graph. This is the "still fresh, skipping update" log line.
2. Otherwise call the Graph beta report endpoint (`reports/getMicrosoft365CopilotUsageUserDetail(period='D30')?$format=text/csv`), follow the 302 to the download URL, parse the CSV.
3. For each row in the CSV, **look up the user in `usercache` by UPN** and patch in the activity dates plus `LastCopilotStatsUpdate = UtcNow`. Rows whose UPN is **not** in `usercache` are silently dropped (logged at Debug level).
4. Fetch license info and update the same rows.
5. Write `LastCopilotStatsUpdate = UtcNow` to `usersyncmetadata`.

The user directory sync (`SyncUsersAsync`) is independent and uses Graph delta queries, so it almost never re-pulls every user.

### Why this matters in practice

- **Stats can lag up to 24 hours by design.** The Graph Copilot report itself also lags 24–48 hours, so end-to-end staleness can be 1–3 days even when everything is working. To verify the cache is the cause, check `LastCopilotStatsUpdate` in the `usersyncmetadata` table.
- **Stats are dropped for users not in `usercache`.** If you just added new licensed users, they have to be picked up by a user-cache sync **before** the next stats run, or that stats run will silently skip them. Run `POST /api/UserCache/Sync` first if you need fresh users to show usage immediately.
- **Smart Groups reuse this cache.** `SmartGroupService.ResolveSmartGroupMembers` calls `CachedUserService.GetAllUsersWithMetadataAsync`, which trips the 1 hour directory TTL but **not** the 24 hour stats TTL. A "fresh" smart group resolution can still surface stale Copilot activity dates — that is expected. See [Smart Groups](./smart-groups.md).
- **`Reports.Read.All` is required.** Without it, stats refresh fails but the directory cache still refreshes fine — you will see Copilot columns frozen while user metadata keeps updating.

### Forcing a refresh

Stats:

```http
POST /api/UserCache/UpdateCopilotStats
```

Users (and license info):

```http
POST /api/UserCache/Sync
```

To bypass the "still fresh" guard, clear the timestamp first by clearing the cache, or simply wait for the next interval. There is no built-in `forceRefresh` flag on `UpdateCopilotStatsAsync`. If you need the data right now, run `Sync` then `UpdateCopilotStats`, in that order, after temporarily setting `UserCacheConfig:CopilotStatsRefreshInterval` to a low value (e.g. `00:00:01`) and restarting the app.

> For the full caching architecture (cache-first read paths, fallback to Graph, delta tokens), see [Caching Guide](../../src/Full/Bot/docs/CACHING_GUIDE.md).
