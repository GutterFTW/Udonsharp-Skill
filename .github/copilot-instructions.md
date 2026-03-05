# GitHub Copilot — UdonSharp World & Script Development

## What Is UdonSharp?

UdonSharp (U#) is a compiler that converts C# code into VRChat's Udon assembly bytecode.
It is **not** standard C# — it compiles a subset of C# features to run inside the VRChat Udon VM.
All scripts must inherit from `UdonSharpBehaviour` (not `MonoBehaviour`).

**Required using statements for almost every script:**
```csharp
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
```

---

## Core Differences from Regular C#

- Inherit from `UdonSharpBehaviour`, **not** `MonoBehaviour`.
- Arrays (`[]`) are the **only** supported collection type. `List<T>`, `Dictionary<T>`, etc. are unavailable.
- `GetComponent<UdonBehaviour>()` must be written as:
  `(UdonBehaviour)GetComponent(typeof(UdonBehaviour))`
  (`GetComponent<T>()` works for all other Unity component types.)
- Field initializers are evaluated at **compile time**; use `Start()` for scene-dependent initialization.
- Numeric casts are **overflow-checked** due to UdonVM limitations.
- Struct mutating methods (e.g. `Vector3.Normalize()`) do **not** modify in-place — use the return value:
  `myVec = Vector3.Normalize(myVec);`
- Recursive methods need the `[RecursiveMethod]` attribute.
- `GetType()` may return `object[]` for jagged arrays instead of the typed array.

---

## Supported C# Features

- `if`, `else`, `while`, `for`, `do`, `foreach`, `switch`, `return`, `break`, `continue`
- Ternary operator `condition ? a : b` and null coalescing `??`
- Implicit and explicit type conversions
- Arrays and array indexers
- All built-in arithmetic operators
- Conditional short-circuit: `true || Foo()` will NOT call `Foo()`
- `typeof()`
- `out` / `ref` parameters on extern methods (e.g., `Physics.Raycast`)
- User-defined methods with parameters, return values, `out`/`ref`, extension methods, `params`
- User-defined properties (get/set)
- Static user methods
- `UdonSharpBehaviour` inheritance, virtual methods, and overrides
- Unity/Udon event callbacks with arguments
- String interpolation `$"Hello {name}"`
- Field initializers
- Jagged arrays
- Cross-behaviour field access and method calls

---

## UdonSharp Attributes

| Attribute | Purpose |
|---|---|
| `[UdonSynced]` | Syncs a variable over the network |
| `[UdonSynced(UdonSyncMode.Linear)]` | Linearly interpolated sync |
| `[UdonSynced(UdonSyncMode.Smooth)]` | Smoothed sync |
| `[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]` | Manual sync mode (use `RequestSerialization()`) |
| `[UdonBehaviourSyncMode(BehaviourSyncMode.Continuous)]` | Frequent automatic sync |
| `[UdonBehaviourSyncMode(BehaviourSyncMode.None)]` | No sync; `SendCustomNetworkEvent` also disabled |
| `[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]` | No synced variables, but events still work |
| `[DefaultExecutionOrder(int)]` | Sets Update/LateUpdate/FixedUpdate execution order |
| `[RecursiveMethod]` | Allows a method to call itself recursively |
| `[FieldChangeCallback(nameof(Property))]` | Calls a property setter when network sync or `SetProgramVariable` changes the field |
| `[NetworkCallable]` | Marks a method as callable via `SendCustomNetworkEvent` (SDK 3.8.1+). Required for events with parameters |
| `[NetworkCallable(maxEventsPerSecond: N)]` | Same, with custom rate limit (1–100 events/sec; default 5) |
| Standard Unity: `[Header]`, `[SerializeField]`, `[HideInInspector]`, `[NonSerialized]`, `[Space]`, `[Tooltip]`, `[ColorUsage]`, `[GradientUsage]`, `[TextArea]` | Same as normal Unity |

### FieldChangeCallback Example
```csharp
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class ToggleExample : UdonSharpBehaviour
{
    public GameObject toggleObject;

    [UdonSynced, FieldChangeCallback(nameof(SyncedToggle))]
    private bool _syncedToggle;

    public bool SyncedToggle
    {
        set { _syncedToggle = value; toggleObject.SetActive(value); }
        get => _syncedToggle;
    }

    public override void Interact()
    {
        Networking.SetOwner(Networking.LocalPlayer, gameObject);
        SyncedToggle = !SyncedToggle; // Use the property, NOT _syncedToggle directly
        RequestSerialization();
    }
}
```

---

## Syncable Variable Types

Only these types can be decorated with `[UdonSynced]`:

| Type | Size |
|---|---|
| `bool` | 1 byte |
| `sbyte`, `byte` | 1 byte |
| `short`, `ushort` | 2 bytes |
| `int`, `uint` | 4 bytes |
| `long`, `ulong` | 8 bytes |
| `float` | 4 bytes |
| `double` | 8 bytes |
| `Vector2` | 8 bytes |
| `Vector3` | 12 bytes |
| `Vector4` | 16 bytes |
| `Quaternion` | 16 bytes |
| `Color` | 16 bytes |
| `Color32` | 4 bytes |
| `char` | 2 bytes |
| `string` | 2 bytes/char (~50 char practical limit) |
| `VRCUrl` | 2 bytes/char |

---

## VRChat API — Common Classes

### `Networking` (static, `VRC.SDKBase.Networking`)
```csharp
Networking.LocalPlayer              // VRCPlayerApi — the current player
Networking.IsMaster                 // bool — is local player the instance master? (don't use for security)
Networking.Master                   // VRCPlayerApi — always-valid current master
Networking.IsInstanceOwner          // bool — true only for instance creator (Invite/Friends instances)
Networking.InstanceOwner            // VRCPlayerApi — instance creator (null if they left)
Networking.IsNetworkSettled         // bool — all synced data received and applied
Networking.IsClogged                // bool — true when outbound data exceeds send capacity
Networking.IsOwner(gameObject)      // bool — is local player owner of object?
Networking.IsOwner(player, gameObject)
Networking.GetOwner(gameObject)     // VRCPlayerApi
Networking.SetOwner(player, gameObject)
Networking.SimulationTime(gameObject)  // float — simulation timestamp for smooth replication
Networking.SimulationTime(player)      // float — player simulation timestamp
Networking.GetServerTimeInSeconds() // double
Networking.GetNetworkDateTime()     // DateTime
```

### `NetworkCalling` (static, `VRC.SDK3.UdonNetworkCalling`)

Available inside or after `[NetworkCallable]` event handlers:
```csharp
using VRC.SDK3.UdonNetworkCalling;

NetworkCalling.CallingPlayer        // VRCPlayerApi — who sent this event (null if local)
NetworkCalling.InNetworkCall        // bool — true while executing a received network event
NetworkCalling.GetQueuedEvents(receiver, eventName) // int — events queued for one method
NetworkCalling.GetAllQueuedEvents() // int — total queued events across the whole world
```

### `VRCPlayerApi` (`VRC.SDKBase.VRCPlayerApi`)
```csharp
// Properties
player.isLocal          // bool
player.displayName      // string
player.isMaster         // bool
player.playerId         // int

// Position & movement (local player only for setters)
player.GetPosition()    // Vector3
player.GetRotation()    // Quaternion
player.GetVelocity()    // Vector3
player.SetVelocity(Vector3)
player.IsPlayerGrounded()  // bool
player.TeleportTo(Vector3 pos, Quaternion rot)
player.TeleportTo(Vector3 pos, Quaternion rot, SpawnOrientation orientation, bool lerpOnRemote)

// Tracking data
player.GetTrackingData(VRCPlayerApi.TrackingDataType.Head)      // TrackingData
player.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand)
player.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand)
player.GetTrackingData(VRCPlayerApi.TrackingDataType.Origin)
player.GetBonePosition(HumanBodyBones bone)   // Vector3
player.GetBoneRotation(HumanBodyBones bone)   // Quaternion

// Locomotion (local player only)
player.SetWalkSpeed(float)      // default 2
player.SetRunSpeed(float)       // default 4
player.SetStrafeSpeed(float)    // default 2
player.SetJumpImpulse(float)    // default 0 (disabled)
player.SetGravityStrength(float) // default 1 (Earth)
player.Immobilize(bool)
player.IsUserInVR()             // bool

// Audio (voice)
player.SetVoiceGain(float)              // dB, 0–24, default 15
player.SetVoiceDistanceNear(float)      // meters, default 0 (leave at 0)
player.SetVoiceDistanceFar(float)       // meters, default 25
player.SetVoiceVolumetricRadius(float)  // meters, default 0
player.SetVoiceLowpass(bool)

// Tags
player.SetPlayerTag(string tagName, string tagValue)
player.GetPlayerTag(string tagName)     // string
player.ClearPlayerTags()

// Pickups
player.GetPickupInHand(VRC.SDKBase.VRC_Pickup.PickupHand hand)  // VRCPickup
player.EnablePickup(bool)
player.PlayHapticEventInHand(PickupHand hand, float duration, float amplitude, float frequency)

// Static get-players
VRCPlayerApi.GetPlayerCount()           // int
VRCPlayerApi.GetPlayers(VRCPlayerApi[] players)  // fills pre-allocated array
VRCPlayerApi.GetPlayerById(int id)      // VRCPlayerApi
```

### `UdonBehaviour` (`VRC.Udon.UdonBehaviour`)
```csharp
behaviour.SendCustomEvent(string eventName)
behaviour.SendCustomNetworkEvent(NetworkEventTarget target, string eventName)
behaviour.SendCustomEventDelayedSeconds(string eventName, float delaySeconds, EventTiming eventTiming)
behaviour.SendCustomEventDelayedFrames(string eventName, int delayFrames, EventTiming eventTiming)
behaviour.GetProgramVariable(string symbolName)     // object
behaviour.SetProgramVariable(string symbolName, object value)
behaviour.GetProgramVariableType(string symbolName) // Type
behaviour.RequestSerialization()
behaviour.DisableInteractive                        // bool property
```

### `VRCObjectSync` (`VRC.SDK3.Components.VRCObjectSync`)
```csharp
sync.SetKinematic(bool)
sync.SetGravity(bool)
sync.FlagDiscontinuity()    // use before teleporting object
sync.TeleportTo(Transform)
sync.Respawn()
sync.AllowCollisionOwnershipTransfer  // bool property — WARNING: this is buggy, avoid
```

### `VRCPickup` (`VRC.SDK3.Components.VRCPickup`)
```csharp
pickup.Drop()
pickup.Drop(VRCPlayerApi)
pickup.IsHeld              // bool
pickup.currentPlayer       // VRCPlayerApi
pickup.currentHand         // PickupHand
pickup.pickupable          // bool
pickup.proximity           // float
pickup.DisallowTheft       // bool
pickup.GenerateHapticEvent(float duration, float amplitude, float frequency)
```

### `VRCObjectPool` (`VRC.SDK3.Components.VRCObjectPool`)
```csharp
pool.TryToSpawn()          // GameObject (null if none available)
pool.Return(GameObject)
pool.Pool                  // GameObject[]
```

### `VRCStation` (`VRC.SDK3.Components.VRCStation`)
```csharp
station.UseStation(VRCPlayerApi)
station.ExitStation(VRCPlayerApi)
station.PlayerMobility     // VRCStation.Mobility
station.seated             // bool
station.disableStationExit // bool
```

### `Utilities` (`VRC.SDKBase.Utilities`)
```csharp
Utilities.IsValid(object obj)           // bool — safe null check for VRCPlayerApi etc.
Utilities.ShuffleArray(int[] array)
```

### `VRCInstantiate`
```csharp
// Creates a LOCAL, non-synced copy of a prefab. NOT networked.
GameObject obj = VRCInstantiate(prefab);
// For networked objects, use VRCObjectPool instead.
```

---

## Important UdonBehaviour Events

Override these in your `UdonSharpBehaviour`:
```csharp
// Unity lifecycle
void Start()
void Update()
void LateUpdate()
void FixedUpdate()
void OnEnable()
void OnDisable()
void OnDestroy()

// VRChat player events
public override void OnPlayerJoined(VRCPlayerApi player) {}
public override void OnPlayerLeft(VRCPlayerApi player) {}

// Interaction
public override void Interact() {}       // Players click/use this object

// Networking
public override void OnPreSerialization() {}
public override void OnPostSerialization(SerializationResult result) {}
public override void OnDeserialization() {}
public override void OnDeserialization(DeserializationResult result) {} // includes sendTime, receiveTime, isFromStorage
public override void OnOwnershipTransferred(VRCPlayerApi player) {}
public override bool OnOwnershipRequest(VRCPlayerApi requester, VRCPlayerApi newOwner) { return true; } // approve/deny
public override void OnMasterTransferred(VRCPlayerApi newMaster) {}   // previous master left

// Pickup
public override void OnPickup() {}
public override void OnDrop() {}
public override void OnPickupUseDown() {}
public override void OnPickupUseUp() {}

// Station
public override void OnStationEntered(VRCPlayerApi player) {}
public override void OnStationExited(VRCPlayerApi player) {}

// Input events
public override void InputJump(bool value, UdonInputEventArgs args) {}
public override void InputUse(bool value, UdonInputEventArgs args) {}
public override void InputGrab(bool value, UdonInputEventArgs args) {}
public override void InputDrop(bool value, UdonInputEventArgs args) {}
public override void InputMoveHorizontal(float value, UdonInputEventArgs args) {}
public override void InputMoveVertical(float value, UdonInputEventArgs args) {}
public override void InputLookHorizontal(float value, UdonInputEventArgs args) {}
public override void InputLookVertical(float value, UdonInputEventArgs args) {}
```

---

## Networking Rules & Patterns

### Ownership
- Every GameObject has one **owner** (default: instance master = longest-staying player).
- Only the owner sends synced variable data to others.
- Transfer ownership: `Networking.SetOwner(Networking.LocalPlayer, gameObject)`
- **After SetOwner, do NOT immediately set synced variables** — the old owner must first acknowledge the transfer (can take seconds on high latency). Use `OnOwnershipTransferred` callback.
- Do NOT call `Networking.SetOwner` on objects with `VRCObjectSync` collision ownership transfer enabled — it is buggy and causes lag.

### Sync Modes
- **Manual** (`BehaviourSyncMode.Manual`): Call `RequestSerialization()` when you want to push data. Reliable.
- **Continuous** (`BehaviourSyncMode.Continuous`): Frequent automatic updates. Less reliable.
- **None/NoVariableSync**: No network overhead; `SendCustomNetworkEvent` disabled for None.

### Network Events
- `SendCustomNetworkEvent(NetworkEventTarget.All, "MethodName")` — sends to all clients
- `SendCustomNetworkEvent(NetworkEventTarget.Others, "MethodName")` — sends to all except local player
- `SendCustomNetworkEvent(NetworkEventTarget.Owner, "MethodName")` — sends to object owner
- `SendCustomNetworkEvent(NetworkEventTarget.Self, "MethodName")` — local loopback only (no network, no rate limit)
- Since SDK 3.8.1: mark methods with `[NetworkCallable]` (from `VRC.SDK3.UdonNetworkCalling`). Events can carry up to 8 parameters (syncable types). Total parameter size: 16 KB max.
- Legacy: any `public` method not starting with `_` is still callable without `[NetworkCallable]` (parameter-less only).
- **Methods starting with `_` will NEVER receive network events** — use for internal/private methods as a security measure.
- Events arrive **before** synced variable updates — don't assume the variable is updated when the event fires.
- Default rate limit: 5 events/sec per method. Set via `[NetworkCallable(maxEventsPerSecond: N)]` (max 100).
- Check `Networking.IsClogged` to detect network saturation.
- Use `NetworkCalling.CallingPlayer` inside handlers to identify the sender.

### VRCInstantiate vs Object Pooling
- `VRCInstantiate` creates LOCAL non-synced objects only.
- For networked/synced spawning, use `VRCObjectPool` (`TryToSpawn()` / `Return()`).

---

## Networking Bandwidth Limits

| Metric | Limit |
|---|---|
| Udon total outgoing throughput | ~11 KB/s |
| Manual sync max bytes/serialization | ~280,496 bytes |
| Continuous sync max bytes/serialization | ~200 bytes |
| Network event max parameter data | 16 KB per call |
| Practical outgoing event rate | ~8–10 KB/s (with overhead) |

- Always initialize synced arrays: `[UdonSynced] private int[] _arr = new int[0];` — uninitialized arrays break sync.
- Multiple UdonBehaviours on one object: most restrictive sync mode (Manual) wins.
- Use `Networking.IsClogged` to detect saturation. Continuous behaviours drop data; Manual behaviours retry.
- Network stats available via `VRC.SDK3.Network.Stats` static class (`Stats.ThroughputPercentage`, `Stats.BytesOutAverage`, etc.).

---

## Performance Guidelines

- **Udon runs 200–1000× slower than C#.** Avoid complex loops in `Update()`.
- Prefer Unity/VRC components over Udon for anything possible (Animator, ParticleSystem, etc.).
- Use time-slicing for heavy operations — spread work across multiple frames.
- Cache component references in `Start()` — `GetComponent<T>()` is expensive in Udon.
- Minimize `public` methods — Udon scans them all when calling `SendCustomEvent`.
- Keep related behavior in one `UdonSharpBehaviour` rather than splitting across many.
- `GetComponent<UdonSharpBehaviourType>()` triggers a full search loop — cache in Start.

---

## Editor Scripting

- UdonSharp creates a **proxy** C# object for every behaviour (disabled, hidden in inspector).
- In editor scripts: use `targetGO.AddUdonSharpComponent<T>()` and `targetGO.GetUdonSharpComponent<T>()`.
- After modifying a proxy: call `proxy.ApplyProxyModifications()`.
- Before reading from a stored proxy reference: call `proxy.UpdateProxy()`.
- Destroy with `UdonSharpEditorUtility.DestroyImmediate()`.
- Wrap editor-only code in `#if !COMPILER_UDONSHARP && UNITY_EDITOR`.
- Always start `OnInspectorGUI` with: `if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;`
- Gizmos: wrap `OnDrawGizmos` in the same `#if` block; call `this.UpdateProxy()` inside it.

---

## Teleportation Rules

- Udon can **only teleport the local player** — use network events to instruct remote players to teleport themselves.
- **Never teleport inside `OnDeserialization`** — avatar may clip through geometry. Use `SendCustomEventDelayedFrames("TeleportMethod", 1)`.
- `lerpOnRemote: false` = instant for remote viewers (costs more bandwidth). Use when teleporting frequently over short distances.

---

## Input Events Quick Reference

| Event | Desktop | VR |
|---|---|---|
| `InputJump` | Spacebar | Face button |
| `InputUse` | Left click | Trigger |
| `InputGrab` | Left click | Grip |
| `InputDrop` | Right click | Grip release / button |
| `InputMoveHorizontal` | A/D | Left stick X |
| `InputMoveVertical` | W/S | Left stick Y |
| `InputLookHorizontal` | Mouse X | Right stick X |
| `InputLookVertical` | Mouse Y | Right stick Y |

Input is NOT detected while VRChat menus are open. All held inputs are released on menu open and re-pressed on menu close.

---

## Debugging

- `Debug.Log("message")` / `Debug.LogWarning` / `Debug.LogError` — shows in Unity console and VRChat log.
- UdonSharp runtime exception watcher maps Udon errors back to C# line numbers in Unity console.
- Launch VRChat with: `--enable-debug-gui --enable-sdk-log-levels --enable-udon-debug-logging`
- Logs saved to: `C:\Users\YourName\AppData\LocalLow\VRChat\VRChat\`
- If a behaviour throws an exception, it **halts entirely**. Search "halted" in the log.

---

## Data Containers (VRCJson / DataList / DataDictionary)

Available in VRC SDK3. These provide JSON-like data structures in Udon.
- `DataList` — ordered list of `DataToken` values
- `DataDictionary` — key/value store using `DataToken`
- `DataToken` — wrapper for any supported type (bool, int, float, string, DataList, DataDictionary, null, etc.)
- `VRCJson.TryDeserializeFromJson(string, out DataToken)` — parse JSON
- `VRCJson.TrySerializeToJson(DataToken, JsonExportType, out string)` — generate JSON

---

## String Loading & Image Loading

```csharp
// String loading
[SerializeField] private VRCUrl url;
void Start() { VRCStringDownloader.LoadUrl(url, (IUdonEventReceiver)this); }
public override void OnStringLoadSuccess(IVRCStringDownload result) { /* result.Result */ }
public override void OnStringLoadError(IVRCStringDownload result) { /* result.Error */ }

// Image loading
[SerializeField] private VRCUrl imageUrl;
void Start() { VRCImageDownloader downloader = new VRCImageDownloader(); downloader.DownloadImage(imageUrl, material, (IUdonEventReceiver)this); }
public override void OnImageLoadSuccess(IVRCImageDownload result) { /* assign result.Material */ }
public override void OnImageLoadError(IVRCImageDownload result) { }
```

---

## Persistence (Player Data & Player Objects)

- **Player Data** – per-player, saved across world visits. Use `VRCPlayerApi.TryGetPlayerData` / `SetPlayerData`.
- **Player Objects** – GameObjects owned and managed per-player via `VRCEnablePersistence` component.
- Events: `OnPlayerDataUpdated(VRCPlayerApi player)` fires when player data changes.

---

## UI Events (Allowed Targets from Unity UI)

Unity UI `OnClick` / `OnValueChanged` events can target these directly (no UdonBehaviour needed):
- `GameObject.SetActive`
- `Animator.*`, `AudioSource.*`, `ParticleSystem.*`
- `Light`, `MeshRenderer`, `SkinnedMeshRenderer`, `LineRenderer`, `TrailRenderer`
- `Button`, `Slider`, `Toggle`, `Dropdown`, `InputField`, `Scrollbar`, `ScrollRect`, `Text`, `Image`, `RawImage`
- `UdonBehaviour.SendCustomEvent`, `UdonBehaviour.RunProgram`, `UdonBehaviour.Interact`

---

## Source Reference URLs

- https://udonsharp.docs.vrchat.com/
- https://udonsharp.docs.vrchat.com/udonsharp (Attributes)
- https://udonsharp.docs.vrchat.com/vrchat-api (VRChat API)
- https://udonsharp.docs.vrchat.com/editor-scripting
- https://udonsharp.docs.vrchat.com/examples
- https://udonsharp.docs.vrchat.com/random-tips-&-performance-pointers
- https://udonsharp.docs.vrchat.com/networking-tips-&-tricks
- https://creators.vrchat.com/worlds/udon/players/
- https://creators.vrchat.com/worlds/udon/event-execution-order
- https://creators.vrchat.com/worlds/udon/input-events
- https://creators.vrchat.com/worlds/udon/data-containers/
- https://creators.vrchat.com/worlds/udon/persistence/
- https://creators.vrchat.com/worlds/udon/string-loading
- https://creators.vrchat.com/worlds/udon/image-loading
- https://creators.vrchat.com/worlds/udon/ui-events
- https://creators.vrchat.com/worlds/udon/debugging-udon-projects
- https://creators.vrchat.com/worlds/udon/networking/events
- https://creators.vrchat.com/worlds/udon/networking/variables
- https://creators.vrchat.com/worlds/udon/networking/ownership
- https://creators.vrchat.com/worlds/udon/networking/network-components
- https://creators.vrchat.com/worlds/udon/networking/network-details
- https://creators.vrchat.com/worlds/udon/networking/network-stats
- https://creators.vrchat.com/worlds/udon/networking/late-joiners
- https://creators.vrchat.com/worlds/udon/networking/performance
- https://creators.vrchat.com/worlds/udon/networking/compatibility
- https://creators.vrchat.com/worlds/udon/networking/debugging
- https://creators.vrchat.com/worlds/udon/networking/network-id-utility
- https://creators.vrchat.com/worlds/udon/vrc-graphics/
- https://creators.vrchat.com/worlds/udon/vrc-graphics/asyncgpureadback
- https://creators.vrchat.com/worlds/udon/vrc-graphics/vrc-camera-settings
- https://creators.vrchat.com/worlds/udon/vrc-graphics/vrc-quality-settings
- https://creators.vrchat.com/worlds/udon/vrc-graphics/vrchat-shader-globals
- https://creators.vrchat.com/worlds/udon/world-debug-views
- https://creators.vrchat.com/worlds/udon/using-build-test
- https://creators.vrchat.com/worlds/udon/vm-and-assembly/
- https://creators.vrchat.com/worlds/layers
