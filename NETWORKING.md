# VIP Manager — Udon Networking Reference

Reference for how VIP Manager uses VRChat's networking model. Sourced from [VRChat Creator Docs](https://creators.vrchat.com/worlds/udon/networking/).

---

## Sync Mode

`VipWhitelistManager` uses **Manual Sync**:

```csharp
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class VipWhitelistManager : UdonSharpBehaviour
```

`VipWhitelistUI` and `VipWhitelistRow` use **No Variable Sync** — they hold no `[UdonSynced]` fields.

---

## What Gets Synced

| Field | Type | Why it must sync |
|-------|------|------------------|
| `syncedManual` | `string[]` | All clients must agree who has manual VIP access |
| `syncedManualCount` | `int` | Active count for manual VIP array |
| `syncedDj` | `string[]` | All clients must agree who has manual DJ access |
| `syncedDjCount` | `int` | Active count for manual DJ array |
| `syncedDjSystemEnabled` | `bool` | All clients must agree if DJ gating is active |
| `initialOwner` | `string` | All clients must agree who has DJ management rights |
| `roleListVersion` | `int` | Signals UI rebuild when role data changed |

### What does NOT sync

| Data | Why local-only is OK |
|------|---------------------|
| `roleMembersFlat` (Pastebin rosters) | Same URL on every client; loaded at Start |
| Barrier GameObject active state | Derived locally from access checks |
| UI row pool / layout | Derived from player list + manager state |
| Auth/DJ caches | Performance optimization; rebuilt from source data |

---

## Ownership Rules

From [Object Ownership](https://creators.vrchat.com/worlds/udon/networking/ownership):

1. **Only the object owner** can modify synced variables on that object
2. A player must call `Networking.SetOwner(localPlayer, gameObject)` before their edits persist
3. Ownership transfer is **asynchronous** — do not call `RequestSerialization()` immediately after `SetOwner`; wait for `OnOwnershipTransferred`
4. When the owner leaves, VRChat **automatically assigns a new owner**
5. Do not use `IsMaster` for access gating — use ownership or `IsInstanceOwner` as appropriate

### VIP Manager ownership flow

```
User toggles VIP/DJ
  → SetOwner(localPlayer) if not owner
  → Mutate synced fields locally
  → manualDirty / djListDirty / djSystemDirty / initialOwnerDirty = true
  → OnOwnershipTransferred (new owner)
  → TrySerializeManualChanges()
  → RequestSerialization() (if not clogged, ≥10 frames since last)
  → Remote clients: OnDeserialization()
```

---

## Serialization Throttle

VIP Manager enforces:

- **Minimum 10 frames** between `RequestSerialization()` calls
- **Clog retry** — if `Networking.IsClogged`, retry after 10 frames via `ProcessPendingManualSerialization`

Contract guarantee: **best-effort propagation**, not instant or sub-second SLA.

VRChat manual sync rate-limits based on payload size. VIP Manager's payload is small (string arrays up to 256 entries + counts + bools + `roleListVersion`).

---

## Late Joiners

From [Network Variables](https://creators.vrchat.com/worlds/udon/networking/variables):

> Late joiners receive the latest state of the variable, regardless of sync type.

VIP Manager `OnDeserialization()` then:

1. Compacts and deduplicates synced manual VIP/DJ lists
2. Clears access caches
3. Calls `NotifyLists()` (full rebuild if `roleListVersion`, `syncedManualCount`, or `syncedDjCount` changed)
4. Re-evaluates `objectsToDisableWhenAuthed` and `djAreaObjects` for local player
5. Broadcasts DJ system toggle state to UI

Late joiners still load role Pastebin data locally at their own `Start()`. `roleListVersion` is synced with manual/DJ lists so clients know when a full UI rebuild is required after role URL updates.

---

## Instance Owner vs Object Owner

From [Network Components](https://creators.vrchat.com/worlds/udon/networking/network-components):

| API | Meaning |
|-----|---------|
| `Networking.IsInstanceOwner` | `true` if local player created the instance (Invite/Invite+/Friends/Friends+ only) |
| `Networking.InstanceOwner` | `VRCPlayerApi` of instance creator; `null` if not present |
| `Networking.IsOwner(player, obj)` | Whether player owns a specific networked GameObject |
| `Networking.IsMaster` | Sync master — **do not use for security** |

**Important:** `initialOwner` is recorded from `Networking.InstanceOwner` (first joiner when that is unavailable), **not** from object ownership. `IsInstanceOwner` is always `false` in **Public**, **Group**, and **Build & Test** instances — VIP Manager does not use it for access gating.

---

## Instance Master

The first joiner owns all networked objects by default. Master changes when master leaves. This is separate from instance owner and from per-object ownership after `SetOwner`.

VIP Manager does not use `IsMaster` for any access decisions.

---

## Array Sync Requirement

From [Networking Specs](https://creators.vrchat.com/worlds/udon/networking/network-details):

> Always initialize synced arrays to some value. If synced arrays are left uninitialized, the behaviour will not sync!

VIP Manager initializes:

```csharp
[UdonSynced] private string[] syncedManual = new string[256];
[UdonSynced] private string[] syncedDj = new string[256];
```

---

## Verification

Networking behavior (ownership transfer, deserialization, late joiners, manual sync throttle) must be validated **manually** with multiple VRChat clients. See [README.md](README.md#testing--verification).

---

## External Script Mutations

Other UdonSharp scripts may call `OnRowToggled`, `DjAdd`, `DjRemove`, or `SetDjSystemEnabledState`. The same ownership + serialization rules apply. UI is the primary editor, but mutations from any caller will sync if ownership is obtained.

---

## Security Model

Trust-based on VRChat display names and role membership. No `OnOwnershipRequest` gate. No `[NetworkCallable]` permission checks. Any player who can reach the UI and has edit permissions can take object ownership and mutate synced lists.

Not suitable as an anti-cheat or cryptographic access control system.
