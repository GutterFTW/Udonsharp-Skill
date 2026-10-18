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
| `roleMembersFlat` (Pastebin rosters) | Product uses independent per-client Start downloads; same URL does not guarantee identical content or successful fetches |
| Barrier GameObject active state | Derived locally from access checks |
| UI row pool / layout | Derived from player list + manager state |
| Auth/DJ caches | Performance optimization; rebuilt from source data |

---

## Remote role loading and failure behavior

Each client loads every distinct configured nonempty URL at Start. One successful download populates all roles with that exact URL. Roster parsing accepts newlines/commas, trims names, deduplicates case-insensitively, and stores at most 256 members per role after inspecting at most 1000 tokens. Truncation and download failures emit errors even when debug logging is off.

There is no automatic role-download retry or public mid-instance reload API. A failed initial load leaves that client's corresponding role empty; manual grants, configured Super Admins and identifiable creator DJ-management remain separate sources. Rejoining creates a new local Start download, not an instance-wide cached snapshot. Remote edits or failures can therefore produce different role membership across clients. `roleListVersion` signals refresh work and does not synchronize the rosters themselves. Do not promise the microphone module's v1.2 shared-roster parity for VIP roles.

This is an established VIP design plus observed failure behavior; a shared roster or retry strategy would be a separate proposed change requiring product/ownership review.

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
User toggles VIP/DJ or the global DJ system
  → SetOwner(localPlayer) if not owner
  → Owner:  mutate synced fields locally → dirty flag = true
  → Not owner yet: edit is STAGED (no [UdonSynced] writes)
      → OnOwnershipTransferred / ProcessPendingEditApplication (after ownership confirms)
      → ApplyPendingEdits(): permissions re-checked, edits applied, dirty flags set
  → TrySerializeManualChanges()
  → RequestSerialization() (if not clogged, ≥10 frames since last)
  → Remote clients: OnDeserialization()
```

**Ownership-race mitigation (multiplayer acceptance pending):** edits from non-owners are no longer applied optimistically right after
requesting ownership. They are staged in a pending buffer (`StagePendingEdit`) and applied by
`OnOwnershipTransferred` / `ProcessPendingEditApplication` only once ownership transfer actually
confirms this client as owner, with permissions re-validated at apply time. A concurrent
`OnDeserialization()` from the outgoing owner during the wait can no longer silently overwrite a
local edit before application. Source checks support the staged-write mitigation; real concurrent ownership/transport behavior still requires the acceptance checklist. The global DJ
system switch also stages its latest requested state until ownership confirms, re-checks permissions
at apply time, and restores the authoritative toggle visual while waiting or when denied.

---

## Serialization Throttle

VIP Manager enforces:

- **Minimum 10 frames** between `RequestSerialization()` calls
- **Clog retry** — if `Networking.IsClogged`, retry after 10 frames via `ProcessPendingManualSerialization`
- **Failed-send retry** — `OnPostSerialization` checks `success`; a failed send schedules another full snapshot even after the request cleared dirty flags

Contract guarantee: **best-effort propagation**, not instant or sub-second SLA.

VRChat manual sync rate-limits based on payload size. VIP Manager's payload is small (string arrays up to 256 entries + counts + bools + `roleListVersion`).

---

## Late Joiners

From [Network Variables](https://creators.vrchat.com/worlds/udon/networking/variables):

> Late joiners receive the latest state of the variable, regardless of sync type.

VIP Manager `OnDeserialization()` then:

1. Compacts and deduplicates synced manual VIP/DJ lists
2. Clears access caches
3. Retains/repairs the creator record and calls `NotifyLists()` (rebuild selection tracks `roleListVersion`, resolved creator name and copied VIP/DJ membership snapshots, including changes that preserve list counts)
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

Creator DJ management checks VRChat's identity directly (`Networking.IsInstanceOwner` for the local player, or the player returned by `Networking.InstanceOwner`). It also accepts the retained creator name, so permission does not depend on receiving a particular synced snapshot. Both the UI and mutation gate use this same creator check.

Each client retains a normalized creator name locally. The current object owner repairs a stale/empty `initialOwner` record from that identity on join, deserialization, and ownership transfer. A deferred refresh after these callbacks updates permissions and the pinned creator row after per-frame UI caches expire. When the creator leaves, clients retain their name; when they return, live identity takes precedence again.

When VRChat cannot identify a creator (**Public**, **Group**, and **Build & Test** instances), and no creator record exists, `initialOwner` stays **empty**. There is no first-joiner/object-owner/master fallback. These instances need configured Super Admin or role permissions for DJ management.

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

## Player Join / Leave

From [Event Nodes — OnPlayerJoined / OnPlayerLeft](https://creators.vrchat.com/worlds/udon/graph/event-nodes/#onplayerjoined):

- Both events fire on **every client** for **every** join/leave.
- On late join, `OnPlayerJoined` runs for **all existing players** (including self).
- Keep join/leave handlers lightweight; avoid full list rebuilds or barrier re-evaluation per remote player.

VIP Manager pattern:

| Event | Manager | UI |
|-------|---------|-----|
| `OnPlayerJoined` (remote) | Forwards to each UI's `OnPlayerJoined`; observes/repairs creator identity and coalesces a deferred permission refresh | Add/update one row; rebuild only if creator identity changed |
| `OnPlayerJoined` (local) | `EvaluateLocalAccess(true)` + `EvaluateLocalDjAccess(true)` | Same incremental row path |
| `OnPlayerLeft` (any) | Prune player name cache; no access/barrier eval | Mark offline or remove row |
| `OnDeserialization` | Full cache clear + `NotifyLists()` + access eval | Deferred rebuild via manager |

Local barrier objects (`objectsToDisableWhenAuthed`, `djAreaObjects`) are **not** re-evaluated when remote players join or leave.

---

## Verification

Networking behavior (ownership transfer, deserialization, late joiners, manual sync throttle) must be validated **manually** with multiple VRChat clients. See [README.md](README.md#testing--verification).

---

## External Script Mutations

Other UdonSharp scripts may call `OnRowToggled`, `DjAdd`, `DjRemove`, or `SetDjSystemEnabledState`. The same ownership + serialization rules apply. UI is the primary editor, but mutations from any caller will sync if ownership is obtained.

---

## Security Model

Trust-based on VRChat display names and role membership. No `OnOwnershipRequest` gate. No `[NetworkCallable]` permission checks. The manager itself re-checks edit permissions in every mutation entry point (`OnRowToggled`, `DjAdd`, `DjRemove`, `SetDjSystemEnabledState`, and staged-edit application), non-owner edits wait for confirmed ownership, and `initialOwner` is reserved to the instance creator — UI `interactable` state is a convenience, not the enforcement layer.

Not suitable as an anti-cheat or cryptographic access control system.
