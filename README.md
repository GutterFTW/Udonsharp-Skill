# VIP Manager

VRChat world asset for **VIP area access** and **DJ booth access**, built with [UdonSharp](https://udonsharp.docs.vrchat.com/). World builders configure role-based whitelists (loaded from Pastebin URLs), an in-world admin UI, and local barrier objects that open or close per player.

---

## Features

- **VIP access** — Gate restricted areas using local barrier objects that disable when the local player is authorized.
- **DJ access** — Separate DJ list and role permissions, with optional global DJ system toggle.
- **Role-based whitelists** — Each role can load members from a `VRCUrl` (Pastebin or compatible text host), with per-role colors and permissions.
- **Manual lists** — Instance-synced manual VIP and DJ entries, editable from the in-world UI.
- **In-world admin UI** — Scrollable player list with per-player VIP and DJ toggles.
- **Super Admins** — Hardcoded display names; all areas; all Super Admin VIP toggles immutable in UI; can edit others including Read-Only roles.

---

## Requirements

- Unity project with **VRChat SDK 3.x**
- **UdonSharp**
- **TextMeshPro** and **Unity UI**

---

## Quick Setup

### 1. Add the manager

Place **one** `VipWhitelistManager` in your scene. A single instance per world is recommended.

### 2. Configure roles (custom inspector)

Open the manager in the Inspector. The custom editor lets you add roles with:

| Setting | Purpose |
|---------|---------|
| Role name | Display label and lookup key |
| Pastebin URL | `VRCUrl` pointing to a plain-text member list |
| Color | Name color in the UI |
| Add / Revoke | Can edit other players' VIP toggles |
| VIP Access | Members of this role get VIP area access |
| DJ Access | Members of this role get DJ booth access |
| Read Only | Protected targets require Super Admin for VIP/DJ edits; creator status alone does not bypass this |

Set **Super Admin Whitelist** to exact VRChat display names for permanent admins. Super Admins are hardcoded and allowed into all areas by default; their VIP toggles are immutable in the UI.

### Recommended role setup

A typical venue configuration:

| Role | Add | Revoke | VIP Access | DJ Access | Read Only |
|------|-----|--------|------------|-----------|-----------|
| **Super Admin** | — | — | — | — | — (use `superAdminWhitelist` instead) |
| **Patreon Subscriber** (or VIP) | ✓ | ✓ | ✓ | — | — |
| **DJ** | — | — | — | ✓ | — |
| **Staff** | ✓ | ✓ | ✓ | — | — |

- **Patreon Subscribers / VIPs** can add and remove VIP access for other players during an instance.
- **Super Admins** — all Super Admin VIP toggles are immutable in UI; they can edit other players including Read-Only roles.
- The **instance starter** can manage DJ toggles but cannot edit VIP unless they belong to a role with Add/Revoke permissions.

Load Patreon/VIP member names from a Pastebin URL on the role's `rolePastebinUrls` entry.

### 3. Wire barrier objects

| Inspector field | Wire to | Behavior |
|-----------------|---------|----------|
| `objectsToDisableWhenAuthed` | VIP area blockers (colliders, meshes, doors) | **Active** when player lacks VIP access; **inactive** when authorized |
| `djAreaObjects` | DJ booth blockers | **Active** when player lacks DJ access; **inactive** when authorized |

Barriers should start **active** in the scene. The manager toggles them locally per client.

### 4. Add the UI

Drop in `VIP UI.prefab`, or create your own with `VipWhitelistUI`:

- Assign `contentRoot` (scroll content transform)
- Assign `rowTemplate` (prefab with `VipWhitelistRow`)
- Assign `djSystemToggle` (global DJ on/off toggle)

Assign the UI to the manager's `lists[]` array in the Inspector. All UI panels must be pre-assigned before play (no runtime registration).

### 5. Wire row prefab events

On each row `Toggle`:

| Toggle | Event target | Method |
|--------|--------------|--------|
| VIP / Auth | `VipWhitelistRow` | `AuthToggled` |
| DJ | `VipWhitelistRow` | `DJToggled` |

On the DJ system toggle:

| Toggle | Event target | Method |
|--------|--------------|--------|
| DJ System | `VipWhitelistUI` | `DJSystemEnabled` |

Row toggles are auto-discovered by GameObject name hints:

- Auth: `auth`, `allow`
- In-world: `here`, `inworld`, `present`
- DJ: `dj`, `mix`, `stage`

---

## Role Pastebin Format

Plain text, one display name per line **or** comma-separated:

```
Alice
Bob
Charlie
```

- Names are trimmed and matched case-insensitively against VRChat display names.
- Up to **256 members** stored per role (`ROLE_MEMBER_CAP`).
- Up to **1000 tokens** parsed per URL load; excess tokens are skipped.
- If two or more roles share the same Pastebin URL, the list is fetched once and populates **all** matching roles.

Every client fetches the role URLs independently at its own `Start()` - not just the manager owner (see [Networking & Persistence](#networking--persistence) below). Failures log errors; there is no automatic retry. Role rosters may differ after a failed request or a remote edit between clients' downloads. Rejoining triggers a fresh local download. This differs from Global Microphone's shared permanent roster.

---

## Usage for World Builders

### Query access from other scripts

**Always check Super Admin first.** Super Admins are hardcoded and allowed into all areas by default.

```csharp
public VipWhitelistManager vipManager;

bool HasVipAccess(string name)
{
    if (vipManager.IsSuperAdmin(name)) return true;
    return vipManager.IsAuthed(name);
}

bool HasDjAccess(string name)
{
    if (vipManager.IsSuperAdmin(name)) return true;
    return vipManager.IsDj(name);
}
```

See [CONTRACT.md](CONTRACT.md) for the full public API.

### Custom gates

If you manage your own doors or teleporters instead of barrier objects, use `HasVipAccess(displayName)` or `HasDjAccess(displayName)` as above. Strip role prefixes from names if your source includes them (e.g. `(VIP) Alice`).

### DJ system toggle

When the DJ system is **off**:

- Per-row DJ toggles are hidden in the UI.
- All `djAreaObjects` are deactivated for every player (DJ gating disabled; barriers removed).

When **on**, only players on the DJ list, in a DJ-enabled role, or Super Admins get DJ area access.

---

## How Access Is Granted

A player is **VIP authorized** if any of these match (case-insensitive display name):

1. Super Admin (whitelist, or role named "Super Admin" — display-name text like a literal `(Super Admin)` prefix grants nothing)
2. Manual synced VIP list (`OnRowToggled` / UI)
3. Member of a role with **VIP Access** permission

A player has **DJ access** (for `IsDj` and area gating) if:

1. On the manual synced DJ list, **or**
2. Member of a role with **DJ Access** permission

**Super Admins** always receive DJ **area** access via `EvaluateLocalDjAccess`, even if `IsDj()` returns false.

---

## Networking & Persistence

- Manager uses **Manual Sync** (`BehaviourSyncMode.Manual`).
- Synced per instance: manual VIP list, manual DJ list, DJ system enabled flag, initial owner name, role list version.
- Role member rosters are **not** synced — each client loads them from Pastebin URLs locally.
- **No VRChat persistence** — all lists reset when the instance ends.

Ownership is taken on edits; non-owner edits are staged until ownership transfer confirms, then serialize with a 10-frame throttle and clog retry.

---

## Limits

| Limit | Value |
|-------|-------|
| Synced manual VIP/DJ entries | `maxSyncedManual` (default 256; VRChat sync payload is practical limit) |
| UI rows | 256 (`MAX_ROWS`) |
| Role members per role | 256 |
| Pastebin tokens parsed | 1000 |
| Name matching | Trim + case-insensitive on display names |

---

## Testing & Verification

**Manual testing in VRChat is the primary verification method** for this asset. Automated tests are optional helpers only — they do not replace in-instance checks of sync, UI, and barrier behavior.

### Recommended manual test flow

1. **Build & Test** (or Build & Reload) with debug logging enabled on the manager if needed.
2. **Single client first** — confirm role Pastebin loads, barriers toggle, and a UI row appears for every in-world player (not just permitted ones, so staff can grant access).
3. **Two clients** — verify VIP/DJ toggle changes sync (`OnDeserialization`), DJ system toggle syncs, and barriers update on both clients.
4. **Late joiner** — third client receives current manual lists and correct barrier state.
5. **DJ management recovery across instance types** — have another authorized staff member change the DJ list (transferring manager ownership), then confirm the original authorized operator can still grant/revoke DJ access and change the global DJ switch. Have the current object owner leave, check again, then have the original operator leave and rejoin. Confirm controls, synced DJ lists/system state, and guest booth barriers on both clients. Repeat for Invite/Friends and Public/Group instances. In Invite/Friends, also test the creator with no configured role and confirm the `*` pinned row. Public/Group/Build & Test do not expose creator identity, so use a configured operator for those tests.
6. **Super Admin** — confirm Super Admin VIP toggles are locked for all actors while Super Admins can edit other Read-Only role members.

### Acceptance gaps and decision provenance

The original creator losing DJ management is a human-reported defect. The current creator-recovery, staged edits and retry changes need actual VRChat acceptance; the live defect is not yet confirmed resolved. The existing contract provides the other access rules. No first-joiner or replacement-master fallback has been chosen.

Add these cases to the multi-client flow above:

| Journey | Expected result / evidence needed |
| --- | --- |
| Creator edits a Read-Only target | VIP/DJ controls are disabled for that target unless the creator is also Super Admin; mutation entry points reject the same edit |
| Add-only / revoke-only operator | Only the permitted direction changes manual VIP state; UI restores authoritative access on denial |
| Revoke a role-granted player | Removing a manual grant does not remove remotely granted access |
| Remote roster download fails on one client | Error is visible; manual/Super Admin/creator sources still behave correctly; no role parity is claimed |
| Remove one offline manual entry and add another before the next snapshot | Same-count membership replacement updates row presence on the receiver; a source correction and regression cases are provided; coordinated execution and live acceptance remain required |
| Long list and ownership churn | Scroll smoothly, preserve event-driven toggles, and verify original operator rights after ownership changes/leave/rejoin |

The April [scroll audit](.planning/debug/scroll-stutter-vip-list.md) still awaits real interaction verification. The [scene audit](.planning/debug/unity-scene-audit.md) records historical SDK-generated descriptor mismatches awaiting editor regeneration; this is not a current permission-system test result. See [CONTRACT.md](CONTRACT.md#decision-provenance-and-unresolved-policy) for unresolved overlapping-role, Super Admin DJ-target, and licensing choices. Do not change those rules merely to match current code.

### Optional automated assets

| Asset | Purpose |
|-------|---------|
| `Editor/VipManagerEditorTests.cs` | Edit-mode smoke tests (inspector config only) |
| `Editor/VipDjInteractivityTests.cs` | Creator/Read-Only DJ controls, Super Admin override and ordinary creator targets; current added cases require coordinated execution |
| `Editor/VipListReconciliationTests.cs` | Same-count VIP/DJ replacements, offline revocation and retained presence reasons; current added cases require coordinated execution |
| `Editor/VipInitialOwnerTests.cs` | Creator permissions, stale snapshot/ownership recovery, staged DJ grants/system changes with and without creator identity, and failed-send retry using controlled SDK hooks; no live transport |
| `VipManagerTests.cs` | Optional runtime harness in a test scene |
| `Example Scene.unity` | Reference wiring |
| `TEST_REPORT.md` | Historical edit-mode report (Jan 2026) |

---

## File Overview

| File | Role |
|------|------|
| `VipWhitelistManager.cs` | Core logic, sync, area gating, role loading |
| `VipWhitelistUI.cs` | Player list UI, DJ system toggle |
| `VipWhitelistRow.cs` | Per-row toggle forwarding |
| `Editor/VipWhitelistManagerEditor.cs` | Custom role inspector |
| `VIP UI.prefab` | Ready-made UI prefab |

---

## License

The intended commercial terms below conflict with the placeholder MIT grant in LICENSE.txt. Owner confirmation is required before distribution; this documentation audit does not choose or replace licensing terms.

Commercial asset for distribution via Gumroad, Booth, or similar platforms.

**Grant:** The purchasing VRChat user may use VIP Manager in **all worlds they publish** on VRChat.

**Restrictions:**

- No redistribution of source files or compiled assets to third parties.
- No resale or sublicensing.
- One license per purchasing VRChat account unless otherwise stated at point of sale.

Contact the seller for team or multi-creator licensing terms.

---

## See Also

- [CONTRACT.md](CONTRACT.md) — Integration contract for other world scripts
- [DEFINITIONS.md](DEFINITIONS.md) — Component and data definitions
- [NETWORKING.md](NETWORKING.md) — Udon sync and ownership reference
- [UdonSharp docs](https://udonsharp.docs.vrchat.com/)
