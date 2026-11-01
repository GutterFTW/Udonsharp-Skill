# VIP Manager — Component & Data Definitions

**Status:** Locked (v1.0 contract, July 2026)  
See also: [CONTRACT.md](CONTRACT.md), [NETWORKING.md](NETWORKING.md)

The v1.0 rules remain intended behavior. Current source conformance and runtime
acceptance are tracked separately in [MAINTENANCE.md](MAINTENANCE.md); the lock
does not establish a tested clean import or verified multiplayer behavior.

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
2. Else the local manager owner at initialization

The intended contract identity is the instance starter. The source fallback
does not establish that the current manager owner is the first joiner, and the
ownership callback does not retry initialization. See UD-07 in
[MAINTENANCE.md](MAINTENANCE.md#known-implementation-and-delivery-gaps) before
claiming creator recovery or using the fallback as proven starter identity.

Grants: **DJ management** + `*` marker in UI. Does **not** grant VIP add/revoke unless a role does.

**Pinned UI row:** Always shown with `*` even when offline (counts toward `MAX_ROWS` 256).

---

## UI Row Visibility

This summary follows the **pre-existing** v1.0
[CONTRACT guarantee 6](CONTRACT.md#guarantees) and its `PlayerHasUiPresence`
definition, both already present at source baseline `a764cfd`. They distinguish
present players from offline retention. This reconciliation does not approve a
new visibility policy or change the locked contract; the earlier summary here
omitted that distinction. `VipWhitelistUI.OnPlayerJoined` and
`RebuildPlayerList` are the corresponding implementation entry points.

Show a row for every currently present player so authorized staff can grant
access. For offline/absent players, retain a row when they have **any** of:
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

Manual in-instance testing in VRChat is the **expected** way to validate behavior. See [README.md](README.md#testing--verification) for the recommended checklist. Editor NUnit tests are optional smoke checks only.

The historical July 2026 alignment claim is not a current verification result.
See the source gap table and NOT RUN acceptance cases in MAINTENANCE.
