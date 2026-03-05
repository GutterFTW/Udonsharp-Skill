# UdonSharp World & Script Development — Project Memory

This workspace is a reusable **AI skill** for UdonSharp VRChat world and script development.
When working in any project that imports this skill, the AI assistant should apply all knowledge below.

---

## About UdonSharp

UdonSharp (U#) compiles a subset of C# into VRChat's Udon assembly bytecode. It is NOT standard C#.
- All scripts inherit from `UdonSharpBehaviour`, never `MonoBehaviour`.
- Only `arrays []` are available as collections — no `List<T>`, no `Dictionary<T>`.
- Performance is 200–1000× slower than regular C#; avoid complex work in `Update()`.

**Standard required usings:**
```csharp
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
```

---

## Critical Rules — Always Follow

1. **No `List<T>` or `Dictionary<T>`** — use arrays `[]` only.
2. **`GetComponent<UdonBehaviour>()`** must be written as `(UdonBehaviour)GetComponent(typeof(UdonBehaviour))`.
3. **Never set synced variables immediately after `Networking.SetOwner()`** — wait for `OnOwnershipTransferred`.
4. **Never teleport inside `OnDeserialization`** — use `SendCustomEventDelayedFrames("Teleport", 1)`.
5. **`VRCInstantiate` is NOT networked** — use `VRCObjectPool` for networked spawning.
6. **Network event target methods must be `public`** and must NOT start with `_` (unless decorated with `[NetworkCallable]`).
7. **Use `[NetworkCallable]` (SDK 3.8.1+)** to explicitly mark network-callable methods — required for parameter-bearing events. Set `maxEventsPerSecond` as low as feasible.
8. **Struct mutators do not modify in-place** — e.g., `myVec = myVec.normalized;` not `myVec.Normalize()`.
9. **Field initializers run at compile time** — use `Start()` for scene-dependent initialization.
10. **`AllowCollisionOwnershipTransfer` on `VRCObjectSync` is buggy** — avoid it.
11. **Cache `GetComponent<T>()` in `Start()`** — never call repeatedly in `Update()`.
12. **Always initialize synced arrays** — `[UdonSynced] private int[] _arr = new int[0];`. Uninitialized synced arrays break all syncing on the behaviour.
13. **Do not use `IsMaster` for security** — use `IsInstanceOwner` or proper ownership logic instead.

---

## Networking Patterns

### NetworkCallable Events (SDK 3.8.1+)
```csharp
using VRC.SDK3.UdonNetworkCalling;

// Declare:
[NetworkCallable(maxEventsPerSecond: 5)]
public void OnSomethingHappened(int value)
{
    VRCPlayerApi caller = NetworkCalling.CallingPlayer;
    Debug.Log($"{caller?.displayName} triggered with {value}");
}

// Send:
SendCustomNetworkEvent(NetworkEventTarget.All, nameof(OnSomethingHappened), value);
// NetworkEventTarget.Others — all except local
// NetworkEventTarget.Self   — local loopback only (no network send)
```

### Manual Sync (Reliable)
```csharp
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class MySync : UdonSharpBehaviour
{
    [UdonSynced] private int _value;

    public void SetValue(int v)
    {
        Networking.SetOwner(Networking.LocalPlayer, gameObject);
        _value = v;
        RequestSerialization();
    }

    public override void OnDeserialization() { /* use _value here */ }
}
```

### FieldChangeCallback (Auto-callback on sync)
```csharp
[UdonSynced, FieldChangeCallback(nameof(MyProp))]
private bool _myField;

public bool MyProp
{
    set { _myField = value; ApplyChange(); }
    get => _myField;
}
// Always use MyProp = x; not _myField = x; (from outside the behaviour)
```

---

## Player API Quick Reference

```csharp
VRCPlayerApi local = Networking.LocalPlayer;

// Move / teleport
local.TeleportTo(pos, rot, VRC_SceneDescriptor.SpawnOrientation.Default, false);
local.SetWalkSpeed(2f); local.SetRunSpeed(4f); local.SetJumpImpulse(3f);
local.SetGravityStrength(1f); local.Immobilize(false);

// Read position/tracking
local.GetPosition();   local.GetRotation();  local.GetVelocity();
local.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);  // .position / .rotation
local.GetBonePosition(HumanBodyBones.Head);

// Voice
local.SetVoiceGain(15f);          // dB 0-24
local.SetVoiceDistanceFar(25f);   // meters 0-1M
local.SetVoiceVolumetricRadius(0f); // keep at 0 unless needed
local.SetVoiceLowpass(true);

// Enumerate players
int count = VRCPlayerApi.GetPlayerCount();
VRCPlayerApi[] players = new VRCPlayerApi[80]; // world hard cap
VRCPlayerApi.GetPlayers(players);
```

---

## Event Callbacks to Override

```csharp
void Start() / Update() / LateUpdate() / FixedUpdate() / OnEnable() / OnDisable()
public override void OnPlayerJoined(VRCPlayerApi player) {}
public override void OnPlayerLeft(VRCPlayerApi player) {}
public override void Interact() {}
public override void OnPreSerialization() {}
public override void OnPostSerialization(SerializationResult result) {}
public override void OnDeserialization() {}
public override void OnDeserialization(DeserializationResult result) {} // sendTime, receiveTime, isFromStorage
public override void OnOwnershipTransferred(VRCPlayerApi player) {}
public override bool OnOwnershipRequest(VRCPlayerApi requester, VRCPlayerApi newOwner) { return true; }
public override void OnMasterTransferred(VRCPlayerApi newMaster) {}
public override void OnPickup() {} / OnDrop() {} / OnPickupUseDown() {} / OnPickupUseUp() {}
public override void OnStationEntered(VRCPlayerApi player) {}
public override void OnStationExited(VRCPlayerApi player) {}
public override void InputJump(bool value, UdonInputEventArgs args) {}
public override void InputUse(bool value, UdonInputEventArgs args) {}
```

---

## Editor Scripting Rules

Wrap ALL editor code in:
```csharp
#if !COMPILER_UDONSHARP && UNITY_EDITOR
using UnityEditor;
using UdonSharpEditor;
// ... editor code ...
#endif
```

- Use `go.AddUdonSharpComponent<T>()` / `go.GetUdonSharpComponent<T>()` instead of standard `AddComponent` / `GetComponent`.
- After modifying a proxy: `proxy.ApplyProxyModifications()`.
- To refresh a stored proxy reference: `proxy.UpdateProxy()`.
- Destroy: `UdonSharpEditorUtility.DestroyImmediate(behaviour)`.
- Custom inspector header: `if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;`

---

## Debugging Checklist

1. Add `Debug.Log()` before/after suspicious code.
2. Launch VRChat with `--enable-debug-gui --enable-sdk-log-levels --enable-udon-debug-logging`.
3. Check logs at `C:\Users\<Name>\AppData\LocalLow\VRChat\VRChat\`.
4. Search for "halted" in log to find the halted UdonBehaviour.
5. UdonSharp's runtime exception watcher maps errors back to C# line numbers in Unity console.

---

## Key Source URLs
- https://udonsharp.docs.vrchat.com/
- https://udonsharp.docs.vrchat.com/vrchat-api
- https://udonsharp.docs.vrchat.com/udonsharp
- https://udonsharp.docs.vrchat.com/editor-scripting
- https://udonsharp.docs.vrchat.com/networking-tips-&-tricks
- https://udonsharp.docs.vrchat.com/random-tips-&-performance-pointers
- https://creators.vrchat.com/worlds/udon/players/
- https://creators.vrchat.com/worlds/udon/data-containers/
- https://creators.vrchat.com/worlds/udon/persistence/
- https://creators.vrchat.com/worlds/udon/input-events
- https://creators.vrchat.com/worlds/udon/ui-events
- https://creators.vrchat.com/worlds/udon/debugging-udon-projects
- https://creators.vrchat.com/worlds/udon/string-loading
- https://creators.vrchat.com/worlds/udon/image-loading
- https://creators.vrchat.com/worlds/udon/event-execution-order
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
- https://creators.vrchat.com/worlds/whitelisted-world-components
- https://creators.vrchat.com/worlds/udon/video-players/
- https://creators.vrchat.com/worlds/udon/midi/
- https://creators.vrchat.com/worlds/udon/midi/realtime-midi
- https://creators.vrchat.com/worlds/udon/midi/midi-playback
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
- https://creators.vrchat.com/worlds/creating-your-first-world
- https://creators.vrchat.com/worlds/sdk-prefabs
- https://creators.vrchat.com/worlds/supported-assets
- https://creators.vrchat.com/worlds/submitting-a-world-to-be-made-public
- https://creators.vrchat.com/worlds/clientsim/
- https://creators.vrchat.com/worlds/items
