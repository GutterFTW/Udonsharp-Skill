# VIP Manager — Component & Data Definitions

**Status:** v1.1 behavior; documentation reconciled October 18, 2026. Open policy questions are recorded in CONTRACT.md.
See also: [CONTRACT.md](CONTRACT.md), [NETWORKING.md](NETWORKING.md)

---

## System Overview

| Script | Sync Mode | Role |
|--------|-----------|------|
| `VipWhitelistManager` | **Manual** | Authority for access checks, synced lists, local barrier gating |
| `VipWhitelistUI` | **NoVariableSync** | In-world scroll list; forwards toggle events to manager |
| `VipWhitelistRow` | **NoVariableSync** | Per-player row; prefab event entry points |

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

**Read-Only:** A protected target cannot be edited by Patreon/staff or the instance creator. **Super Admins can override**. Target protection currently uses the first matching role; whether overlapping roles should use any Read-Only membership remains unresolved in CONTRACT.md. The Super Admin VIP row remains non-interactable in the UI.

### `maxSyncedManual` (default **256**, no hard code cap)
Capacity for `syncedManual` and `syncedDj` arrays. VRChat manual-sync payload size (~280 KB per serialization) is the practical upper bound.

### `superAdminWhitelist`
Hardcoded Super Admins — all areas; every Super Admin VIP toggle is immutable in UI (DJ policy is separate).

---

## Synced Fields ([UdonSynced])

| Field | Purpose |
|-------|---------|
| `syncedManual` + `syncedManualCount` | Runtime manual **VIP** grants from UI toggles |
| `syncedDj` + `syncedDjCount` | Runtime manual **DJ** grants from DJ subsystem toggles |
| `syncedDjSystemEnabled` | Global DJ subsystem on/off (off = list persists, toggles hidden, gating off) |
| `initialOwner` | Normalized instance creator name; stale/empty synced records are repaired from retained identity |
| `roleListVersion` | UI rebuild counter when role Pastebin data changes |

Manual list entries **persist until revoked**, even when the player leaves. Pastebin role assignments return on rejoin.

---

## `initialOwner`

Resolved from `Networking.InstanceOwner` or the local `Networking.IsInstanceOwner` flag, then
retained locally and synced. The current object owner repairs stale/empty snapshots on join,
deserialization, and ownership transfer. Creator permission checks also accept live identity
before the synced name arrives. There is **no first-joiner fallback**: if no creator can be
identified or retained (including Public/Group/Build & Test), nobody holds this role. Configured
Super Admin/VIP roles can manage DJs independently of creator identity and instance type.

Grants: **DJ management** + `*` marker in UI. Does **not** grant VIP add/revoke unless a role does.

**Pinned UI row:** Always shown with `*` even when offline (counts toward `MAX_ROWS` 256).

---

## UI Row Visibility

Every currently in-world player gets a row, including players with no permissions. An offline player retains a row when they have **any** of:
- Super Admin
- `initialOwner`
- Any Pastebin role
- Manual VIP (`syncedManual`)
- Manual DJ (`syncedDj`)

**Prune** permission-less passer-bys on leave. Existing role/Super Admin rows can be retained after those players leave; the UI does not enumerate the full remote role roster. Manual VIP/DJ entries and the retained creator are also added when absent. Rebuild reconciliation must remove an already-offline row after its final presence reason is revoked, before allocating replacements at the row cap. New regression coverage requires coordinated execution.

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
4. Role Pastebin: each client loads at Start; membership is not synced. Failed downloads log errors without automatic retry. Different fetch outcomes/times can produce different role access until clients reload successfully.

---

## Verification

Manual in-instance testing in VRChat is the **expected** way to validate behavior. See [README.md](README.md#testing--verification) for the recommended checklist. Editor NUnit tests are optional smoke checks only.

This is the intended contract and a source map, not a runtime sign-off. Historical test results and current source checks do not establish live multiplayer compliance. See README.md for acceptance work and CONTRACT.md for open authority questions.
