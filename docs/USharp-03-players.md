# Players API

**Sources:**
- https://creators.vrchat.com/worlds/udon/players/
- https://creators.vrchat.com/worlds/udon/players/getting-players
- https://creators.vrchat.com/worlds/udon/players/player-positions
- https://creators.vrchat.com/worlds/udon/players/player-forces
- https://creators.vrchat.com/worlds/udon/players/player-audio
- https://creators.vrchat.com/worlds/udon/players/player-avatar-scaling
- https://creators.vrchat.com/worlds/udon/players/player-collisions

---

## Getting Players

```csharp
// Local player (the player running this script)
VRCPlayerApi local = Networking.LocalPlayer;

// Get all players — pre-allocate array (world hard cap is 80)
VRCPlayerApi[] _players = new VRCPlayerApi[80];

int count = VRCPlayerApi.GetPlayerCount();
VRCPlayerApi.GetPlayers(_players);  // fills array, reuse to avoid GC
for (int i = 0; i < count; i++)
{
    if (!Utilities.IsValid(_players[i])) continue;
    // work with _players[i]
}

// By ID
VRCPlayerApi player = VRCPlayerApi.GetPlayerById(playerId);

// Player properties
player.displayName   // string
player.playerId      // int
player.isLocal       // bool
player.isMaster      // bool
```

> ⚠️ Always check `Utilities.IsValid(player)` before accessing VRCPlayerApi — 
> the object may be invalid after a player leaves.

---

## Player Tag System

A lightweight, local-only key/value tag system per player (not synced).

```csharp
player.SetPlayerTag("role", "chef");
string role = player.GetPlayerTag("role");   // returns "" if not set
player.ClearPlayerTags();
```

> `GetPlayersWithTag` is not currently working in Udon (returns a List, unavailable in Udon).

---

## Player Positions

```csharp
vector3    player.GetPosition()
Quaternion player.GetRotation()
Vector3    player.GetVelocity()
void       player.SetVelocity(Vector3 velocity)  // LOCAL PLAYER ONLY — sets IsGrounded to false
bool       player.IsPlayerGrounded()

// Bones
Vector3    player.GetBonePosition(HumanBodyBones.Head)
Quaternion player.GetBoneRotation(HumanBodyBones.Spine)

// Tracking data (VR: from headset/trackers | Desktop: from bones)
VRCPlayerApi.TrackingData td = player.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
Vector3    td.position
Quaternion td.rotation

// TrackingDataType values:
// Head         — head (VR: HMD, Desktop: head bone)
// LeftHand     — left hand (VR: controller, Desktop: bone)
// RightHand    — right hand
// Origin       — VR playspace center, or player position for Desktop/remote
// AvatarRoot   — avatar root transform (doesn't rotate with head in FBT)
```

---

## Teleporting

```csharp
// LOCAL PLAYER ONLY
player.TeleportTo(
    targetTransform.position,
    targetTransform.rotation,
    VRC_SceneDescriptor.SpawnOrientation.Default,
    false   // lerpOnRemote: false = instant for remote viewers (more bandwidth)
);
```

**Rules:**
- Udon can **only teleport the local player**. Use network events to instruct remote players to teleport.
- **Do NOT teleport inside `OnDeserialization`** — avatar may clip through geometry.
  Use: `SendCustomEventDelayedFrames("TeleportPlayer", 1, EventTiming.Update);`
- `lerpOnRemote: true` = normal movement (smooth, less bandwidth).
- `lerpOnRemote: false` = instant teleport for remote viewers (more bandwidth, use for frequent short teleports).
- Stations can prevent teleportation from Udon.

---

## Player Forces (LOCAL PLAYER ONLY)

```csharp
player.SetWalkSpeed(float speed)      // default 2,  range ~0-5
player.GetWalkSpeed()

player.SetRunSpeed(float speed)       // default 4,  range ~0-10
player.GetRunSpeed()

player.SetStrafeSpeed(float speed)    // default 2,  range ~0-5
player.GetStrafeSpeed()

player.SetJumpImpulse(float impulse)  // default 0 (no jump), range ~0-10
player.GetJumpImpulse()

player.SetGravityStrength(float mult) // default 1 (Earth), range ~0-10
player.GetGravityStrength()

player.Immobilize(bool immobile)      // true = locks position (VR: avatar stays, view can move)
```

---

## Player Audio

### Voice Settings

```csharp
// Gain: default 15 dB, range 0-24
player.SetVoiceGain(15f);

// Near radius: strongly recommended to keep at 0 for spatialization
player.SetVoiceDistanceNear(0f);

// Far radius: default 25 meters, range 0-1,000,000
// Set to 0 to effectively mute the player's voice
player.SetVoiceDistanceFar(25f);

// Volumetric radius: default 0, range 0-1000
// Keep at 0 unless you want voice to seem to come from a large area
player.SetVoiceVolumetricRadius(0f);

// Lowpass filter on distant voices (helps clarity in noisy worlds)
player.SetVoiceLowpass(true);
```

### Avatar Audio Settings

```csharp
// Max gain for avatar audio sources: dB 0-10, default 10
player.SetAvatarAudioGain(10f);

// Near start radius for avatar audio: default 0 meters
player.SetAvatarAudioNearRadius(0f);

// Far end radius: default 40 meters
player.SetAvatarAudioFarRadius(40f);

// Volumetric radius: default 0
player.SetAvatarAudioVolumetricRadius(0f);

// Force spatialization on avatar audio sources
player.SetAvatarAudioForceSpatial(false);

// Allow pre-configured custom curve on avatar audio sources
player.SetAvatarAudioCustomCurve(false);
```
