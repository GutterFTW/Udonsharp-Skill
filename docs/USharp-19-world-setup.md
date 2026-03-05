# World Setup, SDK Prefabs & Publishing

**Sources:**
- https://creators.vrchat.com/worlds/creating-your-first-world
- https://creators.vrchat.com/worlds/sdk-prefabs
- https://creators.vrchat.com/worlds/supported-assets
- https://creators.vrchat.com/worlds/submitting-a-world-to-be-made-public
- https://creators.vrchat.com/worlds/clientsim/

---

## Creating a World — Step-by-Step

### 1. Add VRC_SceneDescriptor

Open the VRChat SDK panel: `VRChat SDK > Show Control Panel > Builder`.
Click **"Set up the scene"** to auto-add a `VRCWorld` prefab with `VRC_SceneDescriptor` attached.

Or manually drag `Packages/com.vrchat.worlds/Samples/UdonExampleScene/Prefabs/World/VRCWorld.prefab` into your scene.

### 2. Create Spawn Points

Add one or more empty GameObjects where players should spawn. Assign them to the `Spawns` array on `VRC_SceneDescriptor`.

- `Spawn Order`: Random is most common.
- `Spawn Radius`: 0 = exact point; >0 = random disc.
- The forward direction of the spawn transform is the player's facing direction.

### 3. Configure the Descriptor

| Setting | Recommended |
|---|---|
| Respawn Height | Set below your floor (e.g. -100) so players don't fall forever |
| Reference Camera | Optional; controls near/far clip planes for all players |
| Object Behaviour At Respawn | Respawn (for pickups) or Destroy |
| Forbid User Portals | Enable if portals would break your world |

### 4. Configure World Info in SDK

In `VRChat SDK > Show Control Panel > Builder`:
- World Name, Description
- Content tags/warnings (content warnings deprecated; just follow Guidelines)
- Capacity (max players) and Recommended capacity
- Thumbnail image

### 5. Run Validations

The SDK panel shows Errors and Warnings. Errors must be fixed before upload.
Some issues have **"Auto Fix"** buttons.

### 6. Build & Upload

- **Build & Test** — builds locally; opens a test instance (see [USharp-14-build-test.md](USharp-14-build-test.md))
- **Build & Upload** — packages and publishes to VRChat servers

Uploaded worlds are visible in: `VRChat SDK > Show Control Panel > Content Manager`

---

## SDK Prefabs

All prefabs found in `Packages/com.vrchat.worlds/Samples/UdonExampleScene/Prefabs/`

| Prefab | Path | Notes |
|---|---|---|
| `VRCWorld` | `World/` | Includes VRC_SceneDescriptor; drop into every new scene |
| `VRCAvatarPedestal` | `AvatarPedestal/` | Display + switch avatar |
| `VRCChair` | `VRCChair/VRCChair3` | Sit-able chair with `VRC_Station` |
| `VRCMirror` | `VRCMirror/` | Toggleable mirror |
| `VRCPortalMarker` | `VRCPortalMarker/` | Walk-in world portal; must be at scene root |
| `VRCVideoSync (AVPro)` | `VideoPlayers/` | Synced video; supports livestreams |
| `VRCVideoSync (Unity)` | `VideoPlayers/` | Synced video; desktop editor preview supported |
| `SimplePenSystem` | `SimplePenSystem/` | 3D drawing pen |
| `UdonVariableSync` | `Udon Variable Sync/` | Demo of synced variable patterns |

---

## Supported Third-Party Assets

| Asset | Notes |
|---|---|
| **TextMeshPro** | Built into Unity 2018+. Strongly recommended over legacy `Text`. Import via `Window > TextMeshPro > Import TMP Essential Resources`. |
| **Post Processing Stack v2** | Import via Package Manager. **Do NOT import the `Tests` folder** — causes compile errors. Access in Udon via `VRCQualitySettings`. |
| **Final IK** | VRChat uses a heavily modified fork — behaviour may differ from stock FinalIK. |
| **Dynamic Bone** | Only versions **up to v1.3.0** work. The current Asset Store version may be incompatible. |

---

## ClientSim (Test Worlds in Unity Editor)

ClientSim lets you play-test your world in the Unity Editor without launching VRChat.

**Open:** `VRChat SDK > ClientSim Settings`

### Features

- Control the local player with keyboard/mouse/gamepad
- Inspect UdonBehaviour variable values at runtime
- Pick up and use `VRC_Pickup` objects
- Trigger `Interact` events
- Click `VRC_UIShape` canvases
- Enter `VRC_Station` seats
- Objects tagged `EditorOnly` are deleted (matching VRChat behaviour)

### Settings Windows

| Window | Purpose |
|---|---|
| ClientSim Settings | Configure player height, spawn, controller type |
| In-game settings (Esc) | Toggle menu, adjust settings mid-play |
| PlayerData window | View/edit player persistence data |
| PlayerObject window | View player object state |

### Known ClientSim Networking Differences

ClientSim only simulates a **single local player** — no remote players.

| Behavior | In-Client | ClientSim |
|---|---|---|
| `OnPostSerialization.byteCount` | Actual bytes serialized | Count of serialized properties |
| `OnDeserialization.sendTime` | Server send timestamp | Always 0 |
| `OnDeserialization.receiveTime` | Local receive timestamp | Always 0 |

---

## Publishing a World

### Community Labs

New worlds must go through **Community Labs** before becoming fully public.

- Submit via `VRChat.com > Edit World > Danger Zone > World Visibility > Publish`
  or during upload in Unity.
- One world per user per 7 days.
- Updates don't reset Community Labs status.
- Target file size: **under 200 MB**.

### Performance Checklist

- Aim for 45+ FPS with a single VR user at spawn.
- Use VR-compatible shaders (single-pass stereo rendering required).
- Use mobile-optimized shaders on Android/Quest.
- Bake lighting — do NOT rely only on real-time lights.
- Limit mirrors: max 1 active, off by default, triggered by proximity or player toggle.
- Limit video players: >2 simultaneous players degrades performance.
- Avoid screen-space post-processing: chromatic aberration, SSAO, SSR cause VR issues.
- Use super-sampled UI shader for crisp in-world text.
- Do NOT use `.blend` files directly — export FBX from Blender.

### World Categorization

| Category | How to qualify |
|---|---|
| Avatar World | Include "avatar", "avatars", "avi", or "avis" in the world **title** |
| Game World | Add the tag `game` during upload |

Don't miscategorize; VRChat may hide or remove worlds that abuse categorization.

### Avatar World Rules

- All pedestals must contain "reasonably optimized" avatars.
- No TOS-violating avatar content allowed.
- Placeholder avatars swapped after going public = 1-month Community Labs ban.
- Supports Marketplace avatars (non-owners see purchase page).
