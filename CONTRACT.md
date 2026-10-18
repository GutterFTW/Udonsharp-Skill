# VIP Manager — Integration Contract

This document defines the **public integration surface** of `VipWhitelistManager` and related components. World scripts that call into this asset should depend only on APIs listed here.

**Version:** 1.1 (documentation reconciled October 18, 2026)
**Stability:** Display-name-based trust model; not a security boundary.
**Networking detail:** See [NETWORKING.md](NETWORKING.md).

---

## Components

| Type | Sync mode | Role |
|------|-----------|------|
| `VipWhitelistManager` | Manual | Core authority for access, sync, and local gating |
| `VipWhitelistUI` | NoVariableSync | In-world player list; calls into manager |
| `VipWhitelistRow` | NoVariableSync | Per-row toggle events; wired from prefab |

---

## Decision provenance and unresolved policy

The human-reported defect is that the original instance creator eventually loses DJ-management access. The creator-recovery work must preserve that identity through ownership changes and stale snapshots. No human decision authorizes a first-joiner, replacement-master, or object-owner fallback. Public/Group/Build & Test instances require configured operators when VRChat exposes no creator identity.

The existing July/August product contract establishes separate VIP and DJ grants, local barriers, Super Admin override, Read-Only target protection, one manager, and instance-scoped state. This reconciliation preserves those rules; it does not claim each was independently reconfirmed by the human in the current task. The Read-Only creator UI correction follows the existing authority restriction rather than granting a new permission.

The following are **unresolved human choices**, not newly approved rules:

- **Overlapping roles:** source currently selects the first matching role for target Read-Only protection and display metadata, while access/add/revoke permissions scan applicable roles. Decide whether *any* Read-Only membership must protect the target before changing that authority rule.
- **Super Admin DJ targets:** the UI prevents non-Super-Admins from editing these targets; the mutation gate currently relies on Read-Only protection and does not impose a separate Super Admin-target lock. Decide the intended direct-API rule before widening or narrowing permission. The Super Admin's area access already remains granted independently of manual DJ membership.
- **Privileged role label:** source selects the first role name containing "Super Admin", although existing builder wording says a role named "Super Admin". Exact-name versus substring selection has not been confirmed by the human; record and resolve this discrepancy before changing privilege assignment.
- **License:** README/this contract describe commercial restrictions, while LICENSE.txt contains a placeholder MIT grant. These conflict; neither is silently superseded by this audit. Obtain the owner's licensing decision before distribution.

Current source observations above are discrepancy evidence, not normative approval. The acceptance checklist in README.md must be satisfied before claiming runtime compliance.

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
| `maxSyncedManual` | `int` | Capacity of **each** manual VIP/DJ list (default **256** per list; VRChat payload is the practical limit). |
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
```

---

## Query API (stable)

All name parameters accept display names with optional role prefixes (e.g. `(VIP) Alice`). Names are normalized: trimmed, lowercased for comparison, role prefix stripped.

### Access checks

| Method | Returns | Semantics |
|--------|---------|-----------|
| `IsAuthed(string name)` | `bool` | VIP access: Super Admin, manual list, or VIP-enabled role member |
| `IsDj(string name)` | `bool` | DJ list or DJ-enabled role member. **Does not** include Super Admin |
| `IsSuperAdmin(string name)` | `bool` | Explicit whitelist or membership in the configured "Super Admin" role. A literal display-name prefix grants no privilege |
| `IsInstanceInitialOwner(VRCPlayerApi player)` | `bool` | VRChat identifies the player as creator, or their name matches the retained creator record |
| `IsInstanceInitialOwner(string name)` | `bool` | Matches the live or retained creator name |
| `GetInitialOwnerName()` | `string` | Normalized creator name: live VRChat identity first, then locally retained/synced record |
| `PlayerHasUiPresence(string name)` | `bool` | Whether an offline/absent player should keep a pinned UI row (role, manual lists, Super Admin, instance starter). Not used to gate in-world join rows — every present player gets a row. |

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
| `CanLocalEditTarget(string targetName)` | `bool` | Local player has Add or Revoke permission, or is Super Admin; Read-Only targets require Super Admin. Actual mutations separately enforce Add versus Revoke |
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
| Super Admin | Yes (including Read-Only targets) | Yes | All Super Admin VIP toggles are immutable in UI |
| Patreon Subscriber / VIP role (Add + Revoke) | Yes | No (unless also VIP-enabled role) | No |
| Instance initial owner | No (unless role grants it) | Yes | No |
| DJ role (DJ Access only) | No | No | No |

---

## Mutation API

Callable from UI or other scripts. All mutations take ownership and trigger manual serialization; edits made before ownership confirms are staged and applied (permissions re-checked) when ownership transfer lands.

| Method | Effect |
|--------|--------|
| `OnRowToggled(string playerName, bool isOn)` | Add/remove player from synced manual VIP list |
| `DjAdd(string exact)` | Add to synced manual DJ list |
| `DjRemove(string exact)` | Remove from synced manual DJ list |
| `SetDjSystemEnabledState(bool enabled)` | Toggle global DJ system; updates UI and `djAreaObjects` |
| `EvaluateLocalAccess(bool forceUpdate = false)` | Re-evaluate VIP barriers for local player |
| `EvaluateLocalDjAccess()` / `EvaluateLocalDjAccess(bool forceUpdate)` | Re-evaluate DJ barriers for local player |
| `NotifyLists()` | Schedule a coalesced rebuild when VIP/DJ membership, role version or creator identity changes; otherwise refresh existing row values |
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
| `initialOwner` | `string` | Normalized instance creator name; current object owner repairs stale/empty snapshots from the retained creator identity |
| `roleListVersion` | `int` | Bumped when role URL data changes |

Role member arrays are **local only** (loaded from URLs, not networked).

---

## Guarantees

1. **Name normalization** — All comparisons use trim + case-insensitive matching on display names.
2. **Deduplication** — Manual VIP/DJ lists deduplicate case-insensitively on sync.
3. **Read-Only enforcement** - For a target protected by Read-Only, ordinary staff and the instance creator cannot edit VIP or DJ grants; Super Admins may override. Creator DJ-management authority does not bypass this target restriction. Multi-role protection semantics remain an open policy question below.
4. **Super Admin UI** — Every Super Admin target's VIP toggle is non-interactable for every actor; this does not describe the separate DJ toggle policy.
5. **initialOwner** — Identified from `Networking.InstanceOwner` or the local `Networking.IsInstanceOwner` flag. Each client retains the creator name across stale/empty snapshots; the current object owner repairs the synced record on join, deserialization, and ownership transfer. Creator DJ management also checks VRChat identity directly, without waiting for sync. The pinned UI row has `*`, even when offline. Object ownership/master status alone never grants creator privileges. If no creator can be identified or retained, this role stays unavailable.
6. **UI rows** — Shown for every currently in-world player (so staff can grant VIP/DJ from a fresh row), plus offline entries with permissions (role, manual lists, Super Admin, instance starter); permission-less rows pruned on leave.
7. **Manual lists** — Persist until revoked; not cleared when player leaves.
8. **Local-only gating** — Barrier object state is evaluated per client from synced data + locally loaded roles.
9. **Single owner serialization** — Editors request object ownership before writing synced fields; non-owner edits are staged in a pending buffer and applied when ownership transfer confirms (see NETWORKING.md's ownership flow), then sync is best-effort with the 10-frame throttle.
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

### Documentation reconciliation (October 18, 2026)

- Removed the stale claim that a literal Super Admin prefix grants privileges.
- Clarified per-list capacity, direction-specific VIP mutations, coalesced refresh behavior, and creator Read-Only restrictions.
- Recorded unresolved authority/licensing choices without changing their policy.
- Required UI parity for creator Read-Only targets, same-count list replacements and offline revocation; added regression execution remains pending.

### 1.1 (August 2026)

- Non-owner edits (VIP/DJ toggles) are staged and applied on confirmed ownership instead of optimistic synced-array writes; closes the previously documented two-editor race.
- `initialOwner` records only the instance creator (`Networking.InstanceOwner`); no first-joiner fallback, so the role can't be claimed by racing object ownership.
- Display-name text (e.g. a literal `(Super Admin)` prefix) no longer grants Super Admin; identity is whitelist/role-membership only.
- Roles sharing one Pastebin URL all populate from a single download; the URL-to-role match parses every matching role.
- DJ list at capacity logs a warning; a `maxSyncedManual <= 0` fallback is surfaced in the console.

### 1.0 (July 2026)

- Documented public query, mutation, inspector, and UI event surfaces.
- Defined barrier gating semantics, manual sync scope, and networking reference.
- Added Super Admin access pattern, edit-permission matrix, and commercial license terms.
- Aligned implementation: `initialOwner`, Read-Only override, `lists[]` inspector-only, DJ dirty flag, UI pruning.
