# VIP Manager — Component & Data Definitions

**Status:** Locked for code alignment (v1.0 contract)  
See also: [CONTRACT.md](CONTRACT.md), [NETWORKING.md](NETWORKING.md)

---

## System Overview

| Script | Sync Mode | Role |
|--------|-----------|------|
| `VipWhitelistManager` | **Manual** | Authority for access checks, synced lists, local barrier gating |
| `VipWhitelistUI` | **None** | In-world scroll list; forwards toggle events to manager |
| `VipWhitelistRow` | **None** | Per-player row; prefab event entry points |

**Design split:**
- **Must match all clients** → `[UdonSynced]` fields on the manager
- **Local per client** → Role Pastebin rosters, barrier GameObject state, UI layout, caches

---

## VipWhitelistManager — Inspector Fields

### `lists` (VipWhitelistUI[])
All UI panels that refresh when synced data changes. **Pre-assigned in the Inspector only** before play. No runtime registration.

### `objectsToDisableWhenAuthed` / `djAreaObjects`
Local barrier objects. **Active = blocked**, **inactive = access granted**. DJ barriers inactive for everyone when DJ system is off.

### Role arrays
Parallel per-role config: name, Pastebin URL, color, Add, Revoke, VIP Access, DJ Access, Read-Only.

**Read-Only:** Protects members from Patreon/staff edits. **Super Admins can override** and edit Read-Only players (own Super Admin row stays non-interactable).

### `maxSyncedManual` (default **256**, no hard code cap)
Capacity for `syncedManual` and `syncedDj` arrays. VRChat manual-sync payload size (~280 KB per serialization) is the practical upper bound.

### `superAdminWhitelist`
Hardcoded Super Admins — all areas, immutable own row in UI.

---

## Synced Fields ([UdonSynced])

| Field | Purpose |
|-------|---------|
| `syncedManual` + `syncedManualCount` | Runtime manual **VIP** grants from UI toggles |
| `syncedDj` + `syncedDjCount` | Runtime manual **DJ** grants from DJ subsystem toggles |
| `syncedDjSystemEnabled` | Global DJ subsystem on/off (off = list persists, toggles hidden, gating off) |
| `initialOwner` | Permanent normalized name of instance starter; never overwritten |
| `roleListVersion` | UI rebuild counter when role Pastebin data changes |

Manual list entries **persist until revoked**, even when the player leaves. Pastebin role assignments return on rejoin.

---

## `initialOwner`

Recorded **once** on first `Start()` by the manager owner:

1. `Networking.InstanceOwner` if valid
2. Else first joiner (local player on first owner `Start()`)

Grants: **DJ management** + `*` marker in UI. Does **not** grant VIP add/revoke unless a role does.

**Pinned UI row:** Always shown with `*` even when offline (counts toward `MAX_ROWS` 256).

---

## UI Row Visibility

Show a row when the player has **any** of:
- Super Admin
- `initialOwner`
- Any Pastebin role
- Manual VIP (`syncedManual`)
- Manual DJ (`syncedDj`)

**Prune** permission-less passer-bys on leave. In-world role members always get rows.

---

## Access Checks

- `IsAuthed` — VIP area (Super Admin → manual VIP → VIP role)
- `IsDj` — DJ membership (manual DJ → DJ role); Super Admin handled separately for areas
- External scripts: `IsSuperAdmin()` first, then `IsAuthed()` / `IsDj()`

---

## Networking Summary

1. Editor takes object ownership (`SetOwner`) before synced writes
2. `RequestSerialization()` on owner (10-frame throttle, clog retry)
3. Late joiners receive synced state via `OnDeserialization()`
4. Role Pastebin: local load at Start only; eventual consistency OK

---

## Verification

Manual in-instance testing in VRChat is the **expected** way to validate behavior. See [README.md](README.md#testing--verification) for a recommended test checklist. Editor NUnit tests are optional smoke checks only.

## Code Alignment Checklist

- [x] `initialOwner` via `InstanceOwner` → first joiner
- [x] Super Admin overrides Read-Only for **other** players
- [x] Remove `RegisterList()` — inspector `lists[]` only
- [x] Separate `djListDirty` flag; `NotifyLists` watches `syncedDjCount`
- [x] `maxSyncedManual` default 256, no hard cap 100
- [x] Pinned `initialOwner` row + UI row pruning
- [x] Manual DJ offline rows in rebuild
