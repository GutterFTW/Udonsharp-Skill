# VIP Manager — Integration Contract

This document defines the **public integration surface** of `VipWhitelistManager` and related components. World scripts that call into this asset should depend only on APIs listed here.

**Version:** 1.0  
**Stability:** Display-name-based trust model; not a security boundary.  
**Networking detail:** See [NETWORKING.md](NETWORKING.md).

---

## Components

| Type | Sync mode | Role |
|------|-----------|------|
| `VipWhitelistManager` | Manual | Core authority for access, sync, and local gating |
| `VipWhitelistUI` | None | In-world player list; calls into manager |
| `VipWhitelistRow` | None | Per-row toggle events; wired from prefab |

---

## Inspector Contract

### VipWhitelistManager

| Field | Type | Contract |
|-------|------|----------|
| `lists` | `VipWhitelistUI[]` | UI panels pre-assigned in Inspector only (one manager per world). |
| `objectsToDisableWhenAuthed` | `GameObject[]` | VIP barriers. Set **inactive** when local player is authorized. |
| `djAreaObjects` | `GameObject[]` | DJ barriers. Set **inactive** when local player has DJ access or DJ system is off. |
| `roleNames` | `string[]` | Role display names. Index is the role ID. |
| `rolePastebinUrls` | `VRCUrl[]` | Per-role member list URLs. |
| `roleColors` | `Color[]` | Per-role UI name color. |
| `roleCanAddPlayers` | `bool[]` | Role members may add VIP access for others. |
| `roleCanRevokePlayers` | `bool[]` | Role members may revoke VIP access for others. |
| `roleCanVipAccess` | `bool[]` | Role members receive VIP area access. |
| `roleCanDjAccess` | `bool[]` | Role members receive DJ access. |
| `roleCanReadOnly` | `bool[]` | Staff cannot edit members; Super Admins may override. |
| `superAdminWhitelist` | `string[]` | Exact display names treated as Super Admin. |
| `superAdminNameColor` | `Color` | Super Admin name color in UI. |
| `maxSyncedManual` | `int` | Shared capacity for manual VIP and DJ lists (default **256**, no hard code cap; VRChat payload is practical limit). |
| `accessCacheSize` | `int` | Auth/DJ result cache size (default 128). |
| `playerNameCacheSize` | `int` | Player name cache size (default 128). |
| `roleIndexCacheSize` | `int` | Role lookup cache size (default 128). |
| `isSuperAdminCacheSize` | `int` | Super Admin cache size (default 128). |
| `enableDebugLogs` | `bool` | Verbose debug logging. |
| `logColor` | `Color` | Debug log color. |

### VipWhitelistUI

| Field | Type | Required |
|-------|------|----------|
| `contentRoot` | `Transform` | Yes |
| `rowTemplate` | `GameObject` | Yes (must contain `VipWhitelistRow`) |
| `djSystemToggle` | `Toggle` | Recommended |

`manager` is injected by `VipWhitelistManager.Start()` for each UI in `lists[]`. Do not assign it on the UI directly.

---

## Public Constants

```csharp
VipWhitelistManager.MAX_ROWS          // 256
VipWhitelistManager.ROW_POOL_SIZE     // 256
VipWhitelistManager.PLAYER_BUF_SIZE   // 128
VipWhitelistManager.LOG_THROTTLE_SIZE // 64
```

---

## Query API (stable)

All name parameters accept display names with optional role prefixes (e.g. `(VIP) Alice`). Names are normalized: trimmed, lowercased for comparison, role prefix stripped.

### Access checks

| Method | Returns | Semantics |
|--------|---------|-----------|
| `IsAuthed(string name)` | `bool` | VIP access: Super Admin, manual list, or VIP-enabled role member |
| `IsDj(string name)` | `bool` | DJ list or DJ-enabled role member. **Does not** include Super Admin |
| `IsSuperAdmin(string name)` | `bool` | Whitelist, `(Super Admin)` prefix, or "Super Admin" role |
| `IsInstanceInitialOwner(VRCPlayerApi player)` | `bool` | Player started the instance (synced) |
| `IsInstanceInitialOwner(string name)` | `bool` | Matches permanent synced `initialOwner` name |
| `GetInitialOwnerName()` | `string` | Synced normalized name of instance starter |
| `PlayerHasUiPresence(string name)` | `bool` | Whether player should appear in VIP UI |

### Role metadata

| Method | Returns |
|--------|---------|
| `GetRoleIndex(string name)` | Role index for a player name, or `-1`. First matching role wins. |
| `GetRoleIndexByRoleName(string roleNameQuery)` | Role index by role name (case-insensitive contains). |
| `GetRoleNameByIndex(int idx)` | Role display name |
| `GetRoleColorByIndex(int idx)` | Role color |
| `GetRoleCanAdd(int idx)` | Add permission |
| `GetRoleCanRevoke(int idx)` | Revoke permission |
| `GetRoleCanDj(int idx)` | DJ access permission |
| `GetRoleIsReadOnly(int idx)` | Read-only flag |
| `GetRoleMemberCountPublic(int idx)` | Parsed member count |
| `GetRoleMemberAt(int roleIdx, int memberIdx)` | Member display name |

### Manual lists (synced)

| Method | Returns |
|--------|---------|
| `GetManualCount()` | Synced manual VIP count |
| `GetManualAt(int idx)` | Synced manual VIP name at index |
| `GetSyncedDjCount()` | Synced manual DJ count |
| `GetSyncedDjAt(int idx)` | Synced manual DJ name at index |
| `GetSyncedDjSystemEnabled()` | Global DJ system on/off |

### Local permission checks

| Method | Returns | Who passes |
|--------|---------|------------|
| `CanLocalEditTarget(string targetName)` | `bool` | Local player has Add or Revoke role; Read-Only blocks staff only |
| `CanLocalEditAny()` | `bool` | `CanLocalEditTarget(localPlayerName)` |
| `CanLocalManageDj()` | `bool` | Super Admin, instance initial owner, or member of a VIP-enabled role |

### Super Admin access (external scripts)

Super Admins are hardcoded via `superAdminWhitelist` and are allowed into **all areas** by default. External scripts should check Super Admin status before other access methods:

```csharp
bool HasVipAccess(string name)
{
    if (manager.IsSuperAdmin(name)) return true;
    return manager.IsAuthed(name);
}

bool HasDjAccess(string name)
{
    if (manager.IsSuperAdmin(name)) return true;
    return manager.IsDj(name);
}
```

`IsAuthed()` already returns `true` for Super Admins, but the explicit `IsSuperAdmin()` check makes intent clear and matches area-gating behavior for DJ zones.

### Edit permissions summary

| Actor | Can add/revoke VIP | Can manage DJ toggles | Immutable in UI |
|-------|-------------------|----------------------|-------------------|
| Super Admin | Yes (including Read-Only targets) | Yes | Yes (own row) |
| Patreon Subscriber / VIP role (Add + Revoke) | Yes | No (unless also VIP-enabled role) | No |
| Instance initial owner | No (unless role grants it) | Yes | No |
| DJ role (DJ Access only) | No | No | No |

---

## Mutation API

Callable from UI or other scripts. All mutations take ownership and trigger manual serialization.

| Method | Effect |
|--------|--------|
| `OnRowToggled(string playerName, bool isOn)` | Add/remove player from synced manual VIP list |
| `DjAdd(string exact)` | Add to synced manual DJ list |
| `DjRemove(string exact)` | Remove from synced manual DJ list |
| `SetDjSystemEnabledState(bool enabled)` | Toggle global DJ system; updates UI and `djAreaObjects` |
| `EvaluateLocalAccess(bool forceUpdate = false)` | Re-evaluate VIP barriers for local player |
| `EvaluateLocalDjAccess()` / `EvaluateLocalDjAccess(bool forceUpdate)` | Re-evaluate DJ barriers for local player |
| `NotifyLists()` | Full UI refresh |
| `NotifyListsForName(string playerName, bool isAuthed)` | Update one player's row |
| `EnsureRoleBuffersInitialized()` | Ensure role arrays are sized (call before role queries at startup) |
| `ProcessPendingManualSerialization()` | Flush pending manual-list sync (UI timing support) |
| `ProcessDeferredListRebuild()` | Deferred full UI rebuild (called via `SendCustomEventDelayedFrames`) |

### Debug logging

| Method | Notes |
|--------|-------|
| `DebugLog(string msg)` | Log when `enableDebugLogs` is true |
| `DebugLogWarning(string msg)` | Warning log (respects `enableDebugLogs`) |
| `DebugLogError(string msg)` | Error log (always emitted) |

---

## VRChat Callbacks (VipWhitelistManager)

| Method | Trigger |
|--------|---------|
| `OnPlayerJoined(VRCPlayerApi player)` | Incremental UI row add/update; local access eval only when `player.isLocal` |
| `OnPlayerLeft(VRCPlayerApi player)` | Incremental UI row offline/prune; no access eval |
| `OnDeserialization()` | Synced state received |
| `OnOwnershipTransferred(VRCPlayerApi newOwner)` | Ownership changes |
| `OnStringLoadSuccess(IVRCStringDownload result)` | Role Pastebin URL loaded |
| `OnStringLoadError(IVRCStringDownload result)` | Role URL load failed |

---

## UI Event Contract

Wire these from prefab Toggle `OnValueChanged` events:

| Component | Method | Trigger |
|-----------|--------|---------|
| `VipWhitelistRow` | `AuthToggled()` | VIP toggle changed |
| `VipWhitelistRow` | `DJToggled()` | DJ toggle changed |
| `VipWhitelistUI` | `DJSystemEnabled()` | DJ system toggle changed |
| `VipWhitelistUI` | `_RefreshNow()` | Manual refresh button |

Row methods forward to `VipWhitelistUI._OnRowToggled` / `DJToggled`, which call manager mutations.

---

## VipWhitelistUI Public API

| Method | Purpose |
|--------|---------|
| `RebuildPlayerList()` | Rebuild scroll list from current players |
| `UpdateRowForName(string playerName, bool isAuthed)` | Update one row's VIP toggle state |
| `UpdateRowTogglesFromAuth()` | Refresh all row toggle states from manager |
| `SetDjSystemEnabled(bool enabled)` | Apply DJ system on/off to UI (called by manager) |
| `_OnRowToggled(string playerName, bool isOn)` | VIP toggle handler; forwards to manager |
| `DJToggled(string playerName)` | DJ toggle handler; forwards to manager |
| `DJSystemEnabled()` | DJ system toggle handler; forwards to manager |
| `_RefreshNow()` | Manual refresh button handler |
| `IsAuthed(string name)` | Delegates to manager `IsAuthed` |

### VipWhitelistUI callbacks

| Method | Trigger |
|--------|---------|
| `OnPlayerJoined(VRCPlayerApi player)` | Incremental row add/update (no full rebuild) |
| `OnPlayerLeft(VRCPlayerApi player)` | Incremental row offline/prune (no full rebuild) |

---

## VipWhitelistRow Public API

| Method | Purpose |
|--------|---------|
| `AuthToggled()` | VIP toggle event entry point (prefab wiring) |
| `DJToggled()` | DJ toggle event entry point (prefab wiring) |
| `_OnAuthToggle(bool isOn)` | Alternative VIP toggle handler with bool param |
| `SetAuthStateWithoutNotify(bool state)` | Set VIP toggle without firing callbacks |
| `SetDjStateWithoutNotify(bool state)` | Set DJ toggle without firing callbacks |

---

## Local Gating Guarantees

### VIP (`objectsToDisableWhenAuthed`)

```
barrier.active = !IsAuthed(localPlayer)
```

- Unauthorized → barriers **active** (blocking)
- Authorized → barriers **inactive** (access granted)

Evaluated on: `Start()`, local player `OnPlayerJoined`, `OnDeserialization`, manual list change, role URL load.

### DJ (`djAreaObjects`)

```
barrier.active = syncedDjSystemEnabled && !hasDjAccess
hasDjAccess = IsDj(localPlayer) || IsSuperAdmin(localPlayer)
```

- DJ system off → all barriers **inactive** (gating disabled)
- DJ system on, no access → barriers **active**
- DJ system on, has access → barriers **inactive**

Evaluated on: `Start()`, local player `OnPlayerJoined`, `OnDeserialization`, DJ list/system change, manual toggle.

---

## Synced State

| Variable | Type | Scope |
|----------|------|-------|
| `syncedManual` / `syncedManualCount` | `string[]`, `int` | Manual VIP list |
| `syncedDj` / `syncedDjCount` | `string[]`, `int` | Manual DJ list |
| `syncedDjSystemEnabled` | `bool` | Global DJ toggle (default `true`) |
| `initialOwner` | `string` | Permanent normalized name of instance starter (never overwritten) |
| `roleListVersion` | `int` | Bumped when role URL data changes |

Role member arrays are **local only** (loaded from URLs, not networked).

---

## Guarantees

1. **Name normalization** — All comparisons use trim + case-insensitive matching on display names.
2. **Deduplication** — Manual VIP/DJ lists deduplicate case-insensitively on sync.
3. **Read-Only enforcement** — Staff cannot edit Read-Only role members; Super Admins may override (own Super Admin row stays non-interactable).
4. **Super Admin UI** — Super Admin VIP toggles are non-interactable on their own row.
5. **initialOwner** — Recorded once from `Networking.InstanceOwner` or first joiner; pinned UI row with `*` even when offline.
6. **UI rows** — Shown only for players with permissions (role, manual lists, Super Admin, instance starter); passer-bys pruned on leave.
7. **Manual lists** — Persist until revoked; not cleared when player leaves.
8. **Local-only gating** — Barrier object state is evaluated per client from synced data + locally loaded roles.
9. **Single owner serialization** — Editors take object ownership before writing synced fields; best-effort manual sync.
10. **One manager per world** — Exactly one `VipWhitelistManager` per scene.

---

## Non-Goals & Limitations

| Topic | Detail |
|-------|--------|
| No `VRCPlayerApi` overload for `IsAuthed` | Pass `player.displayName` |
| No auth-change callbacks | External scripts must poll or hook UI events |
| No `[NetworkCallable]` API | Use direct method calls on the manager reference |
| No cross-instance persistence | Lists are instance-scoped only |
| `IsDj` ≠ Super Admin | Super Admin DJ area access is handled in `EvaluateLocalDjAccess` only |
| Display-name trust | Not cryptographically secure; suitable for social/trust-based gating |
| Multiple managers | Not supported; use one manager per world |
| Role priority | First matching role in inspector order wins for `GetRoleIndex` |

---

## Expected Wiring Patterns

### Pattern A: Inspector barriers (recommended)

Assign blocker GameObjects directly to `objectsToDisableWhenAuthed` and `djAreaObjects`. No extra scripts required.

### Pattern B: Custom script gate

```csharp
public VipWhitelistManager vipManager;

public void OnTriggerEnter(Collider other)
{
    VRCPlayerApi player = Networking.LocalPlayer;
    string name = player.displayName;
    if (vipManager.IsSuperAdmin(name) || vipManager.IsAuthed(name))
    {
        // allow entry
    }
}
```

### Pattern C: DJ booth check

```csharp
string name = player.displayName;
bool canDj = vipManager.IsSuperAdmin(name) || vipManager.IsDj(name);
```

This matches `EvaluateLocalDjAccess` behavior for Super Admins.

---

## Verification

Contract compliance is validated through **manual in-instance testing** in VRChat (multi-client sync, UI, barriers, late joiners). Automated editor tests are optional smoke checks for inspector configuration only — not a substitute for manual verification. See [README.md](README.md#testing--verification).

---

## Changelog

### 1.0 (July 2026)

- Documented public query, mutation, inspector, and UI event surfaces.
- Defined barrier gating semantics, manual sync scope, and networking reference.
- Added Super Admin access pattern, edit-permission matrix, and commercial license terms.
- Aligned implementation: `initialOwner`, Read-Only override, `lists[]` inspector-only, DJ dirty flag, UI pruning.
