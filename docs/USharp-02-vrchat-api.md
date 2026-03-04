# VRChat API Reference

**Source:** https://udonsharp.docs.vrchat.com/vrchat-api

---

## Methods

### `VRCInstantiate`
```csharp
GameObject VRCInstantiate(GameObject original)
```
Creates a **local, non-synced** copy of a prefab. This is NOT networked.
For networked spawning, use `VRCObjectPool`.

---

## Classes

### `Utilities` — `VRC.SDKBase.Utilities`
```csharp
bool    Utilities.IsValid(object obj)       // safe null check (especially for VRCPlayerApi after player leaves)
void    Utilities.ShuffleArray(int[] array) // randomly shuffles array elements
```

---

### `Networking` — `VRC.SDKBase.Networking` (static)

**Properties:**
```csharp
bool         Networking.IsMaster          // is local player instance master?
VRCPlayerApi Networking.LocalPlayer       // the current local player
bool         Networking.IsNetworkSettled  // is network ready?
```

**Methods:**
```csharp
bool         Networking.IsOwner(VRCPlayerApi player, GameObject obj)
bool         Networking.IsOwner(GameObject obj)                  // local player owns?
VRCPlayerApi Networking.GetOwner(GameObject obj)
void         Networking.SetOwner(VRCPlayerApi player, GameObject obj)
bool         Networking.IsObjectReady(GameObject obj)
void         Networking.Destroy(GameObject obj)
string       Networking.GetUniqueName(GameObject obj)
DateTime     Networking.GetNetworkDateTime()
double       Networking.GetServerTimeInSeconds()
int          Networking.GetServerTimeInMilliseconds()
double       Networking.CalculateServerDeltaTime(double timeInSeconds, double previousTimeInSeconds)
```

---

### `VRCPlayerApi` — `VRC.SDKBase.VRCPlayerApi`

**Properties:**
```csharp
bool   player.isLocal
string player.displayName
bool   player.isMaster
int    player.playerId
```

**Static Methods:**
```csharp
int          VRCPlayerApi.GetPlayerCount()
VRCPlayerApi VRCPlayerApi.GetPlayerById(int playerId)
int          VRCPlayerApi.GetPlayerId(VRCPlayerApi player)
VRCPlayerApi[] VRCPlayerApi.GetPlayers(VRCPlayerApi[] players)  // pre-allocate array!
```

**Instance Methods:**
```csharp
// Position
Vector3    GetPosition()
Quaternion GetRotation()
Vector3    GetVelocity()
void       SetVelocity(Vector3 velocity)
bool       IsPlayerGrounded()
void       TeleportTo(Vector3 pos, Quaternion rot)
void       TeleportTo(Vector3 pos, Quaternion rot, SpawnOrientation orientation)
void       TeleportTo(Vector3 pos, Quaternion rot, SpawnOrientation orientation, bool lerpOnRemote)
TrackingData GetTrackingData(VRCPlayerApi.TrackingDataType type)
Vector3    GetBonePosition(HumanBodyBones bone)
Quaternion GetBoneRotation(HumanBodyBones bone)

// Locomotion (local player only)
float GetWalkSpeed()   void SetWalkSpeed(float speed)      // default 2
float GetRunSpeed()    void SetRunSpeed(float speed)       // default 4
float GetStrafeSpeed() void SetStrafeSpeed(float speed)    // default 2
float GetJumpImpulse() void SetJumpImpulse(float impulse)  // default 0 (disabled)
float GetGravityStrength() void SetGravityStrength(float strength) // default 1
void  Immobilize(bool immobile)
bool  IsUserInVR()
void  UseLegacyLocomotion()

// Voice
void SetVoiceGain(float gain)                  // dB 0-24, default 15
void SetVoiceDistanceNear(float near)          // meters 0-1M; keep at 0
void SetVoiceDistanceFar(float far)            // meters 0-1M, default 25
void SetVoiceVolumetricRadius(float radius)    // meters 0-1000, default 0
void SetVoiceLowpass(bool enabled)

// Avatar audio
void SetAvatarAudioGain(float gain)            // dB 0-10, default 10
void SetAvatarAudioNearRadius(float distance)  // meters default 0
void SetAvatarAudioFarRadius(float distance)   // meters default 40
void SetAvatarAudioVolumetricRadius(float r)   // default 0
void SetAvatarAudioForceSpatial(bool force)
void SetAvatarAudioCustomCurve(bool allow)

// Tags (local only, not networked)
void   SetPlayerTag(string tagName, string tagValue)
string GetPlayerTag(string tagName)
void   ClearPlayerTags()

// Pickups
VRCPickup GetPickupInHand(VRC.SDKBase.VRC_Pickup.PickupHand hand)
void      EnablePickup(bool enable)
void      PlayHapticEventInHand(PickupHand hand, float duration, float amplitude, float frequency)

// Station
void UseAttachedStation()

// Other
bool IsOwner(GameObject obj)
```

---

### `UdonBehaviour` — `VRC.Udon.UdonBehaviour`

> ⚠️ Cannot be retrieved with `GetComponent<UdonBehaviour>()`.  
> Must use: `(UdonBehaviour)GetComponent(typeof(UdonBehaviour))`

**Properties:**
```csharp
bool DisableInteractive  // disable pointer raycast, outline, and tooltip
```

**Methods:**
```csharp
void   SendCustomEvent(string eventName)
void   SendCustomNetworkEvent(NetworkEventTarget target, string eventName)
void   SendCustomEventDelayedSeconds(string eventName, float delaySeconds, EventTiming eventTiming)
void   SendCustomEventDelayedFrames(string eventName, int delayFrames, EventTiming eventTiming)
object GetProgramVariable(string symbolName)
void   SetProgramVariable(string symbolName, object value)
Type   GetProgramVariableType(string symbolName)
void   RequestSerialization()
```

---

### `VRCObjectSync` — `VRC.SDK3.Components.VRCObjectSync`

```csharp
bool AllowCollisionOwnershipTransfer  // ⚠️ BUGGY — avoid
void SetKinematic(bool value)
void SetGravity(bool value)
void FlagDiscontinuity()              // call before teleporting to disable smoothing
void TeleportTo(Transform targetLocation)
void Respawn()
```

---

### `VRCPickup` — `VRC.SDK3.Components.VRCPickup`

**Properties:**
```csharp
bool         IsHeld
VRCPlayerApi currentPlayer
PickupHand   currentHand
bool         pickupable
float        proximity
bool         DisallowTheft
PickupOrientation orientation
AutoHoldMode AutoHold
string       InteractionText
string       UseText
```

**Methods:**
```csharp
void Drop()
void Drop(VRCPlayerApi instigator)
void GenerateHapticEvent(float duration, float amplitude, float frequency)
void PlayHaptics()
```

---

### `VRCObjectPool` — `VRC.SDK3.Components.VRCObjectPool`

```csharp
GameObject[] Pool           // all pool objects
GameObject   TryToSpawn()   // returns null if pool is empty
void         Return(GameObject obj)
```

> Pool manages active state automatically and syncs to late joiners.
> `OnEnable()` fires on an object when it is spawned from the pool.

---

### `VRCStation` — `VRC.SDK3.Components.VRCStation`

**Properties:**
```csharp
VRCStation.Mobility PlayerMobility   // Mobile | Immobilize | ImmobilizeForVehicle
bool   canUseStationFromStation
bool   disableStationExit
bool   seated
Transform stationEnterPlayerLocation
Transform stationExitPlayerLocation
RuntimeAnimatorController animatorController
```

**Methods:**
```csharp
void UseStation(VRCPlayerApi player)
void ExitStation(VRCPlayerApi player)
```

---

### `VRCAvatarPedestal` — `VRC.SDK3.Components.VRCAvatarPedestal`

```csharp
string    blueprintId
Transform Placement
bool      ChangeAvatarsOnUse
float     scale
void      SwitchAvatar(string id)
void      SetAvatarUse(VRCPlayerApi instigator)  // instigator must be local player
```

---

### `VRCPortalMarker` — `VRC.SDK3.Components.VRCPortalMarker`

```csharp
string roomId
void   RefreshPortal()
```

---

### `VRCUrl` — `VRC.SDKBase.VRCUrl`

```csharp
// Can only be constructed at editor time (not runtime)
VRCUrl(string url)
string Get()           // retrieve URL value
VRCUrl VRCUrl.Empty    // static empty URL
```

---

### `VRCUrlInputField` — `VRC.SDK3.Components.VRCUrlInputField`

```csharp
VRCUrl GetUrl()
void   SetUrl(VRCUrl url)
```

---

### `InputManager` — `VRC.SDKBase.InputManager` (static)

```csharp
bool          IsUsingHandController()
VRCInputMethod GetLastUsedInputMethod()
void          EnableObjectHighlight(GameObject obj, bool enable)
void          EnableObjectHighlight(Renderer r, bool enable)
```

---

## Enums

### `TrackingDataType`
```csharp
VRCPlayerApi.TrackingDataType.Head
VRCPlayerApi.TrackingDataType.LeftHand
VRCPlayerApi.TrackingDataType.RightHand
VRCPlayerApi.TrackingDataType.Origin   // center of VR playspace
```

### `NetworkEventTarget`
```csharp
NetworkEventTarget.All    // all clients
NetworkEventTarget.Owner  // only object owner
```

### `SpawnOrientation`
```csharp
VRC_SceneDescriptor.SpawnOrientation.Default
VRC_SceneDescriptor.SpawnOrientation.AlignPlayerWithSpawnPoint
VRC_SceneDescriptor.SpawnOrientation.AlignRoomWithSpawnPoint
```

### `EventTiming`
```csharp
EventTiming.Update
EventTiming.LateUpdate
```

### `BehaviourSyncMode`
```csharp
BehaviourSyncMode.Any           // default; user sets mode in inspector
BehaviourSyncMode.None          // no sync, SendCustomNetworkEvent disabled
BehaviourSyncMode.Continuous    // automatic frequent sync
BehaviourSyncMode.Manual        // call RequestSerialization() manually
BehaviourSyncMode.NoVariableSync // no variables, events still work
```

### `UdonSyncMode`
```csharp
UdonSyncMode.None    // no interpolation (default)
UdonSyncMode.Linear  // lerp
UdonSyncMode.Smooth  // smoothed
```

### `PickupHand`
```csharp
VRC_Pickup.PickupHand.None / .Left / .Right
```

### `PickupOrientation`
```csharp
VRC_Pickup.PickupOrientation.Any / .Grip / .Gun
```

### `AutoHoldMode`
```csharp
VRC_Pickup.AutoHoldMode.AutoDetect / .Yes / .No
```

### `VRCStation.Mobility`
```csharp
VRCStation.Mobility.Mobile
VRCStation.Mobility.Immobilize
VRCStation.Mobility.ImmobilizeForVehicle
```

---

## Syncable Variable Types

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
| `string` | 2 bytes/char (~50 char limit in practice) |
| `VRCUrl` | 2 bytes/char |
