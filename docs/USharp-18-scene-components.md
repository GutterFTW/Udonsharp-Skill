# Scene Components Reference

**Sources:**
- https://creators.vrchat.com/worlds/components/
- https://creators.vrchat.com/worlds/components/vrc_scenedescriptor
- https://creators.vrchat.com/worlds/components/vrc_objectsync
- https://creators.vrchat.com/worlds/components/vrc_pickup
- https://creators.vrchat.com/worlds/components/vrc_station
- https://creators.vrchat.com/worlds/components/vrc_mirrorreflection
- https://creators.vrchat.com/worlds/components/vrc_spatialaudiosource
- https://creators.vrchat.com/worlds/components/vrc_avatarpedestal
- https://creators.vrchat.com/worlds/components/vrc_uishape
- https://creators.vrchat.com/worlds/components/vrc_portalmarker

---

## Component Limits

| Category | World Limit |
|---|---|
| Active VRCPhysBones + VRCPhysBoneColliders | 1024 |
| Active VRCContactSenders + VRCContactReceivers | 1024 |

---

## VRC_SceneDescriptor

Required on every scene. Usually applied via the `VRCWorld` prefab.

| Property | Description |
|---|---|
| `Spawns` | Array of transforms used as spawn points |
| `Spawn Radius` | Random radius around each spawn point (0 = exact) |
| `Spawn Order` | First / Sequential / Random / Demo |
| `Spawn Orientation` | Default / Align Player With Spawn Point / Align Room With Spawn Point |
| `Reference Camera` | Camera whose settings (near/far clip, clear flags) are applied to players |
| `Respawn Height -Y` | Y-height at which players and pickups respawn |
| `Object Behaviour At Respawn` | Destroy / Respawn (for pickups) |
| `Forbid Free Modification` | Non-synced objects can't be moved by non-master |
| `Forbid User Portals` | Prevents users from opening portals via world menu |
| `User Custom Voice Falloff Range` | Enables custom min/max voice distance settings |
| `Interact Passthrough` | Layer mask for interactions that pass through to lower objects |

`Dynamic Prefabs` and `Dynamic Materials` are deprecated (SDK3 unused).

---

## VRC_ObjectSync

Synchronises a GameObject's transform (position, rotation, kinematic state, gravity) to all players. Requires a Rigidbody.

### Properties

| Property | Description |
|---|---|
| `Allow Collision Ownership Transfer` | **Buggy — avoid.** Transfers ownership on collision with another player's object |
| `Force Kinematic On Remote` | Forces rigidbody into kinematic mode for non-owners |

### Methods (call from UdonSharpBehaviour)

```csharp
VRCObjectSync sync = GetComponent<VRCObjectSync>();

sync.SetKinematic(bool value);      // Toggle kinematic physics
sync.SetGravity(bool value);        // Toggle gravity
sync.FlagDiscontinuity();           // Call BEFORE teleporting — disables smoothing for one frame
sync.TeleportTo(Transform target);  // Move to target
sync.Respawn();                     // Return to original spawn location
```

---

## VRC_Pickup

Allows an object to be picked up and held by players. Requires a Rigidbody and a Collider.

### Properties

| Property | Description |
|---|---|
| `Pickupable` | Whether the object can be picked up |
| `Proximity` | Max grab distance (meters). VR hover reach = 0.4m × avatar scale |
| `Disallow Theft` | Prevent others from taking it from a player's hand |
| `Orientation` | How the object is held (Any / Gun / Grip / etc.) |
| `Exact Gun / Exact Grip` | Transform for exact hand alignment |
| `Auto Hold` | Whether object stays in hand after grab (Yes / No / Auto / Sometimes) |
| `Use Text` | Prompt text shown when held (requires Auto Hold = Yes) |
| `Allow Manipulation When Equipped` | Allow re-gripping while held (controller users) |
| `Throw Velocity Boost Min Speed` | Min speed to trigger throw boost |
| `Throw Velocity Boost Scale` | Throw speed multiplier |
| `Momentum Transfer Method` | How collision force is applied (requires AllowCollisionTransfer) |

**Version 1.1+:** Auto Hold is simplified to a checkbox — checked = hold until second grab or drop, unchecked = drop on release.

### Udon Callbacks

```csharp
public override void OnPickup() {}
public override void OnDrop() {}
public override void OnPickupUseDown() {}   // Trigger pressed while held
public override void OnPickupUseUp() {}     // Trigger released
```

### Udon API

```csharp
VRCPickup pickup = GetComponent<VRCPickup>();
pickup.Drop();                              // Drop from current holder
pickup.Drop(VRCPlayerApi player);           // Force specific player to drop
bool held = pickup.IsHeld;
VRCPlayerApi holder = pickup.currentPlayer;
VRCPickup.PickupHand hand = pickup.currentHand;  // LeftHand / RightHand / None
pickup.pickupable = false;                  // Disable pickup dynamically
pickup.DisallowTheft = true;
```

---

## VRC_Station

Allows players to sit or stand in a defined location. Used for chairs, vehicles, and avatar seats.

### Properties

| Property | Description |
|---|---|
| `Player Mobility` | Mobile / Immobilize / Immobilize For Vehicle |
| `Can Use Station From Station` | Allow switching stations while seated |
| `Animation Controller` | Optional override for sitting animation layer |
| `Disable Station Exit` | Prevent exiting by moving/jumping — use `ExitStation` node |
| `Seated` | Whether avatar uses seated IK |
| `Station Enter Player Location` | Transform: where player is placed on enter |
| `Station Exit Player Location` | Transform: where player is placed on exit |

### Udon Callbacks

```csharp
public override void OnStationEntered(VRCPlayerApi player) {}
public override void OnStationExited(VRCPlayerApi player) {}
```

### Udon API

```csharp
VRCStation station = GetComponent<VRCStation>();
station.UseStation(Networking.LocalPlayer);
station.ExitStation(Networking.LocalPlayer);
```

### Avatar Animator Parameters

| Parameter | Meaning |
|---|---|
| `InStation` | True when avatar is in any station |
| `Seated` | True when Seated-IK is active (only if station has `Seated` enabled) |
| `AvatarVersion` | 3 = SDK3, <3 = legacy SDK2 |

### Setup Requirements

1. `VRC_Station` component with entry/exit transforms pointing to station or child transform
2. Collider (usually `Is Trigger = true`)
3. UdonBehaviour with station control script
4. (Optional) Mesh/mesh renderer

Avatar station limits: max 6 per avatar; entry and exit must be ≤ 2 meters apart.

---

## VRC_MirrorReflection

Creates a real-time mirror. Requires a MeshRenderer — writes to `_MainTex` of the first material.

Use the `VRCMirror` SDK prefab as a starting point (`Packages/.../Prefabs/VRCMirror`).

| Property | Description |
|---|---|
| `Disable Pixel Lights` | Pixel lights fall back to vertex lighting (big perf gain) |
| `Turn Off Mirror Occlusion` | Disable occlusion culling in mirror (fixes flickering) |
| `Reflect Layers` | Only these layers render in mirror; Water layer never renders |
| `Mirror Resolution` | Auto / fixed resolution (max 2048×2048 for default quality) |
| `Maximum Antialiasing` | MSAA level (can be overridden by client settings) |
| `Custom Shader` | Override mirror material shader |
| `Camera Clear Flags` | From Reference Camera / Skybox / Solid Color / Depth |
| `Custom Skybox` | Used if clear flags = Custom Skybox |
| `Custom Clear Color` | Used if clear flags = Solid Color (alpha respected) |

**Performance notes:**
- Keep mirrors off by default; enable with trigger or button.
- Limit to 1 active mirror per room.
- Offer "low quality" (fewer layers) and "high quality" modes.
- Resolutions >2048px require users to set "Unlimited" in VRChat settings.

---

## VRC_SpatialAudioSource

Adds 3D spatialization to a Unity `AudioSource`. Auto-adds `AudioSource` on placement. Works on both worlds and avatars.

| Property | Description |
|---|---|
| `Gain` | Volume boost in dB (0–24 dB; avatars limited to 10 dB) |
| `Far` | Silence radius in meters (default 40m; avatars limited to 40m) |
| `Near` | Falloff starts here (default 0m — keep at 0 for realism) |
| `Volumetric Radius` | Simulates area source (default 0, must be < Far) |
| `Use AudioSource Volume Curve` | Use custom AudioSource rolloff curve instead of inverse-square |
| `Enable Spatialization` | Disable to use AudioSource's own spatialization |

Falloff is inverse-square by default. Animating spatialization properties at runtime is **not** supported; animate other AudioSource properties (pitch, etc.) instead.

### 2D Audio

To use non-spatialized audio:
1. Uncheck `Use Spatialized Audio` on `VRC_SpatialAudioSource`
2. Set `Spatial Blend` on `AudioSource` to 100% 2D

### avatar Audio Compressor Tips

- Use dry audio files (no reverb/delay).
- Normalize audio to −6 to −12 dB headroom.
- Avoid sudden high-amplitude spikes.

---

## VRC_AvatarPedestal

Displays an avatar model and lets players switch into it on interact.

| Property | Description |
|---|---|
| `Blueprint Id` | Avatar blueprint ID (e.g. `avtr_xxxxxx`) |
| `Placement` | Optional transform for avatar display position |
| `Change Avatar On Use` | If true, `SetAvatarUse` changes the local player's avatar |
| `Scale` | Size of displayed avatar preview (does not affect actual avatar) |

### Avatar Visibility Rules

| Avatar Type | Visibility |
|---|---|
| Public | All users can see and use the pedestal |
| Private | Only uploader can see/use; others see an error |
| Marketplace | All users see it; non-owners see the purchase page |

SDK prefab: `Packages/.../UdonExampleScene/Prefabs/AvatarPedestal`

---

## VRC_UIShape

Enables players to interact with a Unity Canvas (point, click, scroll). Required on any interactable world-space UI.

| Property | Description |
|---|---|
| `Allow Focus View` | Show expanded focus view for mobile/tablet users |

### Canvas Setup

1. Add a `Canvas` component (Unity auto-adds EventSystem, GraphicRaycaster).
2. Add `VRC_UIShape` to **the same GameObject** as the Canvas.
3. Change the GameObject layer from `UI` to `Default` (or other non-UI layer).
4. Set Canvas `Render Mode` → **World Space**.
5. Scale down: set x/y/z to ~`0.001`–`0.01` (default 1 = 1 pixel per meter).
6. Set `Navigation` to `None` on all interactive elements to prevent accidental movement.
7. Use TextMeshPro for all text.

### Common Problems

| Symptom | Fix |
|---|---|
| Pointer doesn't appear | `VRC_UIShape` on Canvas, not a child; layer ≠ UI; BoxCollider correct size |
| UI unresponsive | Scene has EventSystem; elements not covered by invisible elements; `Raycast Target` enabled; `Graphic Raycaster` present; viewing from front (Z-forward faces away from player) |
| UI scrolls/moves with input | `Navigation = None` on all elements; `Scroll Sensitivity = 0` on scrollbars |
| Methods not firing | Use only [allowed UI event targets](https://creators.vrchat.com/worlds/udon/ui-events); `SendCustomEvent` method must be `public` |
| VRChat keyboard unwanted | Add `VRCInputFieldKeyboardOverride` component to InputField |

### Focus View (Mobile/Tablet)

Users can expand/zoom in on the canvas. Requirements:
- Canvas configured correctly with `VRC_UIShape`
- `Allow Focus View` enabled on the component
- User is on phone/tablet with touchscreen-only input
- User is 0.6–6 meters from canvas (size-dependent)

---

## VRC_PortalMarker

Creates a walk-in portal to another VRChat world. Must be at the **scene root** for the destination world to sync correctly.

| Property | Description |
|---|---|
| `World ID` | Target world (`wrld_xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx`) |
| `Custom Portal Name` | Override displayed world name |
| `World` | None (use World ID) / Home (user's home world) / Hub (VRChat Hub) |

SDK prefab: `Packages/.../Prefabs/VRCPortalMarker`

Players can walk through to travel, or open it from the VRChat menu for details.
