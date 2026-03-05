# VIP Manager - Copilot Instructions

This repository contains a VIP whitelist management system for VRChat worlds built with Unity, UdonSharp, and VRChat SDK3.

## Project Overview

VIP Manager is a role-based VIP and manual whitelist system for VRChat worlds that allows:
- Dynamic role loading from Pastebin URLs
- Manual player whitelist management with network synchronization
- Role-based permissions (VIP access, DJ access, add/revoke players)
- Inspector-only setup with no code modifications required by users

## Technology Stack

- **Unity**: Game engine for VRChat world development
- **UdonSharp**: High-level C#-like language that compiles to Udon (VRChat's scripting system)
- **VRChat SDK3**: VRChat's world SDK
- **TextMeshPro**: For UI text rendering

## Code Conventions

### UdonSharp Constraints

**CRITICAL**: UdonSharp has strict limitations compared to standard C#. Always follow these rules:

1. **No Generics**: UdonSharp does not support generic types or methods
   - Use typed array helper methods instead (e.g., `ResizeStringArray`, `ResizeBoolArray`)
   - DO NOT use `List<T>`, `Dictionary<K,V>`, or other generic collections

2. **No LINQ**: LINQ is not supported
   - Use manual loops and array operations
   - Example: Use `for` loops instead of `.Select()`, `.Where()`, etc.

3. **Limited String Operations**:
   - Avoid advanced string methods
   - Use `string.IsNullOrEmpty()`, `Trim()`, `ToLowerInvariant()`, `IndexOf()`, `Substring()`, `Replace()`, `Split()`
   - StringBuilder is available and preferred for string concatenation in loops

4. **Array Constraints**:
   - Use single-dimensional arrays only
   - Jagged arrays cause Udon VM issues - flatten them (see `roleMembersFlat` pattern)
   - Always initialize arrays with explicit sizes

5. **No Async/Await**: All operations must be synchronous or use Unity coroutines/events
   - Use `SendCustomEventDelayedFrames()` for deferred execution
   - Network operations use callback methods (e.g., `OnStringLoadSuccess`)

6. **Networking** (VRChat Udon Networking):
   - Use `[UdonSynced]` attribute for synchronized variables
   - Call `RequestSerialization()` to sync changes (manual sync mode only)
   - Always check `Networking.IsOwner()` before modifying synced data
   - Use `Networking.SetOwner()` to take ownership when needed
   - Synced variables must be serializable types (primitives, Vector3, Color, string, VRCUrl, etc.)
   - Arrays are supported but must be single-dimensional
   - Object references (GameObjects, Components) cannot be synced
   - Late joiners receive current synced state via `OnDeserialization()`
   - Network events use `SendCustomNetworkEvent()` for RPC-style calls

### Naming Conventions

1. **Classes**: PascalCase (e.g., `VipWhitelistManager`, `VipWhitelistRow`)
2. **Public Fields/Properties**: camelCase (e.g., `roleNames`, `maxSyncedManual`)
3. **Private Fields**: camelCase with underscore prefix (e.g., `_cachedRoleCount`, `_lastSerializationFrame`)
4. **Constants**: UPPER_SNAKE_CASE (e.g., `ROLE_MEMBER_CAP`, `ACCESS_CACHE_SIZE`)
5. **Public Methods**: PascalCase (e.g., `RegisterList`, `GetRoleIndex`)
6. **Private Methods**: PascalCase (e.g., `EnsureRoleBuffers`, `CompactSyncedManual`)
7. **Udon Event Receivers**: Prefix with underscore for UI callbacks (e.g., `_OnAuthToggle`, `_OnRowToggled`)

### Code Style

1. **Attributes**:
   - Use `[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]` on all UdonSharp classes
   - Use `[Header("Section Name")]` to organize inspector fields
   - Use `[Tooltip("Description")]` for user-facing inspector fields
   - Use `[HideInInspector]` for runtime-only public fields

2. **Comments**:
   - Prefer clear code over comments
   - Use comments to explain UdonSharp-specific workarounds or performance optimizations
   - Document cache invalidation logic and synchronization patterns

3. **Null Checks**:
   - Always use `Utilities.IsValid(player)` for VRCPlayerApi validation
   - Use `string.IsNullOrEmpty()` for string validation
   - Check array bounds before accessing elements

4. **Performance** (UdonSharp Performance Tips):
   - Cache frequently accessed values (e.g., `GetLocalPlayer()` cached per-frame)
   - Use normalized parallel arrays to avoid repeated `ToLowerInvariant()` calls
   - Implement runtime caches for expensive lookups (see `_accessCacheKeys` pattern)
   - Batch array operations to reduce allocation overhead
   - Avoid `Update()` when possible; use events or `SendCustomEventDelayedFrames()`
   - Pool objects instead of instantiating/destroying frequently
   - Cache component references; avoid repeated `GetComponent()` calls
   - Minimize string concatenation in hot paths; use `StringBuilder` for loops

### Editor Scripts

1. **Conditional Compilation**: Wrap Unity Editor code with:
   ```csharp
   #if !COMPILER_UDONSHARP && UNITY_EDITOR
   // Editor-only code here
   #endif
   ```

2. **Custom Editors**: Use `[CustomEditor(typeof(ClassName))]` for inspector customization
3. **Reorderable Lists**: Use `UnityEditorInternal.ReorderableList` for array management in inspector

## UdonSharp & VRChat API

### VRCPlayerApi

- Use `Networking.LocalPlayer` to get the local player
- Use `VRCPlayerApi.GetPlayers(VRCPlayerApi[] players)` to populate an array with all players (returns int count)
  - Example: `VRCPlayerApi[] players = new VRCPlayerApi[100]; int count = VRCPlayerApi.GetPlayers(players);`
- Track players using `OnPlayerJoined()` and `OnPlayerLeft()` events for dynamic player lists
- Always validate with `Utilities.IsValid(player)` before accessing player properties
- Common properties: `displayName`, `playerId`, `isLocal`, `isMaster`
- Player positions: `GetPosition()`, `GetRotation()`, `GetBonePosition()`, `GetBoneRotation()`
- Player state: `IsUserInVR()`, `GetRunSpeed()`, `GetWalkSpeed()`, `GetJumpImpulse()`

### Event Execution Order

Common Udon event execution order:
1. `Start()` - Called once when the script is initialized
2. `OnPlayerJoined()` - Called when any player joins
3. `OnPlayerLeft()` - Called when any player leaves
4. `OnOwnershipTransferred()` - Called when ownership changes
5. `OnDeserialization()` - Called when synced data is received
6. `Update()` - Called every frame (use sparingly)

### String and Data Loading

- Use `VRCStringDownloader.LoadUrl()` for loading remote text/JSON
- Implement `OnStringLoadSuccess()` and `OnStringLoadError()` callbacks
- Use `VRCImageDownloader.LoadImage()` for loading remote images
- URLs must meet VRChat's remote URL requirements (see [External URLs docs](https://creators.vrchat.com/worlds/udon/external-urls))
- Approved domains include: GitHub (raw.githubusercontent.com), Pastebin (pastebin.com/raw), and others
- World creators can request additional URL whitelisting through VRChat's support system
- Data containers: Use `DataDictionary` and `DataList` for complex data (UdonSharp 1.x+)

### UI Events

- Wire UI events via UnityEvent in inspector (Button.onClick, Toggle.onValueChanged, etc.)
- Use `SendCustomEvent("MethodName")` pattern for UI callbacks
- For Toggles: Use `SetIsOnWithoutNotify()` to update state without triggering events
- TextMeshPro is recommended over legacy Unity UI Text

## Architecture Patterns

### Data Synchronization

- **Manual Serialization**: Control when data syncs with `RequestSerialization()`
- **Rate Limiting**: Enforce minimum frame intervals between serializations (see `MIN_SERIALIZATION_FRAME_INTERVAL`)
- **Network Clog Detection**: Check `Networking.IsClogged` before requesting serialization
- **Dirty Flags**: Use flags (`manualDirty`, `djSystemDirty`) to track pending changes
- **Sync Modes**: Use `BehaviourSyncMode.Manual` for controlled serialization or `BehaviourSyncMode.Continuous` for automatic sync
- **Ownership Transfer**: Only the owner can modify synced variables; use `Networking.SetOwner()` to transfer ownership
- **Late Joiners**: Implement `OnDeserialization()` to handle incoming synced data for late joiners
- **Bandwidth**: Keep synced data minimal; large arrays or frequent updates can cause network congestion

### Caching Strategy

- **Player Name Cache**: Cache `playerId -> normalizedName` mappings to avoid repeated `displayName` access
- **Access Cache**: Cache `IsAuthed` and `IsDj` results per normalized name
- **Role Index Cache**: Cache role membership lookups
- **Cache Invalidation**: Clear caches when role lists or synced arrays change

### Array Management

- **Flattened Arrays**: Use `offset = roleIndex * CAPACITY + memberIndex` pattern
- **Parallel Arrays**: Maintain normalized arrays alongside original data (e.g., `syncedManual` + `syncedManualNorm`)
- **Compaction**: Remove null/duplicate entries to maintain data integrity

## Testing and Debugging

1. **Debug Logging**:
   - Use the centralized `DebugLog()` method
   - Respect the `enableDebugLogs` flag
   - Use colored output with `ColorToHex()` helper
   - Throttle repeated logs to avoid console spam
   - Use `Debug.Log()`, `Debug.LogWarning()`, `Debug.LogError()` for different severity levels

2. **Testing in VRChat**:
   - Test with multiple players to verify network synchronization
   - Test ownership transfer scenarios
   - Verify UI updates on player join/leave events
   - Test role permission inheritance
   - Test as both master and non-master clients
   - Test late-join scenarios to ensure synced state is received correctly

3. **Networking Debugging**:
   - Use VRChat's built-in Network Stats (Ctrl+N in-game) to monitor bandwidth
   - Check `Networking.IsClogged` to detect network congestion
   - Log `Networking.GetOwner()` to verify ownership state
   - Use `OnPreSerialization()` and `OnDeserialization()` for sync debugging
   - Monitor serialization frequency to avoid exceeding rate limits

4. **Common Issues**:
   - Object array EXTERN errors → flatten to single-dimensional arrays
   - Null reference on networked callbacks → check `Utilities.IsValid()`
   - UI not updating → ensure `NotifyLists()` is called after data changes
   - Serialization failures → verify ownership and check `IsClogged`
   - Desynced state → ensure `OnDeserialization()` updates all dependent caches/UI
   - Ownership conflicts → implement proper ownership transfer logic

## Documentation Standards

1. **README.md**: Inspector-focused user documentation (no code required by end users)
2. **Code Comments**: Explain UdonSharp constraints, optimization rationale, and non-obvious logic
3. **Inspector Tooltips**: User-friendly descriptions for all public configuration fields

## Common Tasks

### Adding a New Role Permission

1. Add a new `bool[]` field (e.g., `roleCanNewPermission`)
2. Add corresponding inspector property in `VipWhitelistManagerEditor.cs`
3. Update `SyncArraySizes()`, `onAddCallback`, and `onRemoveCallback` in the editor
4. Add permission check method (e.g., `GetRoleCanNewPermission(int idx)`)
5. Update role parsing and permission evaluation logic

### Adding a New Synced Variable

1. Add `[UdonSynced]` attribute to the field
2. Mark as dirty when modified (create new dirty flag if needed)
3. Call `TrySerializeManualChanges()` after modification
4. Update `OnDeserialization()` to handle incoming changes
5. Update cache invalidation logic if needed

### Optimizing Performance

1. **Reduce String Allocations**: Use cached normalized strings
2. **Batch Operations**: Process multiple items in single loop
3. **Cache Lookups**: Store frequently accessed results
4. **Defer Updates**: Use `SendCustomEventDelayedFrames()` for non-critical updates
5. **Minimize Serialization**: Batch changes before requesting serialization

## Security Considerations

1. **Read-Only Roles**: Respect `roleCanReadOnly` flag to prevent unauthorized edits
2. **Super Admin**: Always validate super admin status before granting elevated permissions
3. **Ownership Validation**: Check `Networking.IsOwner()` before writing synced data
4. **Initial Owner**: Record first owner to grant persistent management permissions

## File Structure

```
/
├── VipWhitelistManager.cs      # Main manager script with role/auth logic
├── VipWhitelistUI.cs           # UI list controller
├── VipWhitelistRow.cs          # Individual player row controller
├── Editor/
│   └── VipWhitelistManagerEditor.cs  # Custom inspector for manager
├── Template.prefab             # UI row template
├── VIP Manager.prefab          # Manager prefab
├── VIP UI.prefab               # UI list prefab
└── README.md                   # User documentation
```

## Build and Deploy

This package is distributed as a Unity package (.unitypackage):
1. Import into Unity project with VRChat SDK3 and UdonSharp
2. Drag prefabs into scene
3. Configure roles and permissions in inspector
4. Upload world to VRChat

No build system or automated tests are present - testing happens in VRChat client.

## Official Documentation References

### UdonSharp Documentation
- [UdonSharp Overview](https://udonsharp.docs.vrchat.com/)
- [VRChat API](https://udonsharp.docs.vrchat.com/vrchat-api)
- [UdonSharp Features](https://udonsharp.docs.vrchat.com/udonsharp)
- [Editor Scripting](https://udonsharp.docs.vrchat.com/editor-scripting)
- [Examples](https://udonsharp.docs.vrchat.com/examples)
- [Performance Tips](https://udonsharp.docs.vrchat.com/random-tips-%26-performance-pointers)
- [Networking Tips & Tricks](https://udonsharp.docs.vrchat.com/networking-tips-%26-tricks)
- [Class Exposure Tree](https://udonsharp.docs.vrchat.com/class-exposure-tree)

### VRChat Udon Documentation
- [UdonSharp Guide](https://creators.vrchat.com/worlds/udon/udonsharp/)
- [UdonSharp Attributes](https://creators.vrchat.com/worlds/udon/udonsharp/attributes)
- [Class Exposure Tree](https://creators.vrchat.com/worlds/udon/udonsharp/class-exposure-tree)
- [Configuration](https://creators.vrchat.com/worlds/udon/udonsharp/configuration)
- [Editor Scripting](https://creators.vrchat.com/worlds/udon/udonsharp/editorscripting)
- [Performance Tips](https://creators.vrchat.com/worlds/udon/udonsharp/performance-tips)

### Networking
- [Networking Overview](https://creators.vrchat.com/worlds/udon/networking/)
- [Network Variables](https://creators.vrchat.com/worlds/udon/networking/variables)
- [Network Events](https://creators.vrchat.com/worlds/udon/networking/events)
- [Ownership](https://creators.vrchat.com/worlds/udon/networking/ownership)
- [Late Joiners](https://creators.vrchat.com/worlds/udon/networking/late-joiners)
- [Network Components](https://creators.vrchat.com/worlds/udon/networking/network-components)
- [Network Details](https://creators.vrchat.com/worlds/udon/networking/network-details)
- [Network ID Utility](https://creators.vrchat.com/worlds/udon/networking/network-id-utility)
- [Network Stats](https://creators.vrchat.com/worlds/udon/networking/network-stats)
- [Performance](https://creators.vrchat.com/worlds/udon/networking/performance)
- [Debugging](https://creators.vrchat.com/worlds/udon/networking/debugging)
- [Compatibility](https://creators.vrchat.com/worlds/udon/networking/compatibility)

### Players
- [Players Overview](https://creators.vrchat.com/worlds/udon/players/)
- [Getting Players](https://creators.vrchat.com/worlds/udon/players/getting-players)
- [Player Audio](https://creators.vrchat.com/worlds/udon/players/player-audio)
- [Player Avatar Scaling](https://creators.vrchat.com/worlds/udon/players/player-avatar-scaling)
- [Player Collisions](https://creators.vrchat.com/worlds/udon/players/player-collisions)
- [Player Forces](https://creators.vrchat.com/worlds/udon/players/player-forces)
- [Player Positions](https://creators.vrchat.com/worlds/udon/players/player-positions)

### Data & Resources
- [Event Execution Order](https://creators.vrchat.com/worlds/udon/event-execution-order)
- [Debugging Udon Projects](https://creators.vrchat.com/worlds/udon/debugging-udon-projects)
- [Data Containers](https://creators.vrchat.com/worlds/udon/data-containers/)
- [Data Dictionaries](https://creators.vrchat.com/worlds/udon/data-containers/data-dictionaries/)
- [Data Lists](https://creators.vrchat.com/worlds/udon/data-containers/data-lists/)
- [Data Tokens](https://creators.vrchat.com/worlds/udon/data-containers/data-tokens/)
- [VRCJSON](https://creators.vrchat.com/worlds/udon/data-containers/vrcjson)
- [External URLs](https://creators.vrchat.com/worlds/udon/external-urls)
- [String Loading](https://creators.vrchat.com/worlds/udon/string-loading)
- [Image Loading](https://creators.vrchat.com/worlds/udon/image-loading)
- [Input Events](https://creators.vrchat.com/worlds/udon/input-events)
- [UI Events](https://creators.vrchat.com/worlds/udon/ui-events)

### Persistence
- [Persistence Overview](https://creators.vrchat.com/worlds/udon/persistence/)
- [Player Data](https://creators.vrchat.com/worlds/udon/persistence/player-data)
- [Player Object](https://creators.vrchat.com/worlds/udon/persistence/player-object)



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
