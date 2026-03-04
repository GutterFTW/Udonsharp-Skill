# Networking Specs, Stats & Advanced Topics

**Sources:**
- https://creators.vrchat.com/worlds/udon/networking/network-details
- https://creators.vrchat.com/worlds/udon/networking/network-stats
- https://creators.vrchat.com/worlds/udon/networking/network-components
- https://creators.vrchat.com/worlds/udon/networking/ownership
- https://creators.vrchat.com/worlds/udon/networking/performance
- https://creators.vrchat.com/worlds/udon/networking/compatibility
- https://creators.vrchat.com/worlds/udon/networking/network-id-utility

---

## Bandwidth Limits

| Metric | Limit |
|---|---|
| Total Udon outgoing throughput | ~11 KB/s per client |
| Manual sync: max bytes per serialization | ~280,496 bytes |
| Continuous sync: max bytes per serialization | ~200 bytes |
| Network event max parameter data | 16 KB per call |
| Network event practical outgoing rate | ~8–10 KB/s (with overhead) |

When the limit is exceeded, `Networking.IsClogged` returns `true`:
- **Continuous** behaviours: will fail to send and log errors.
- **Manual** behaviours: will cache and retry automatically.

### Syncing Arrays

Always initialize synced array variables to at least an empty array. Uninitialized synced arrays will
**prevent the entire behaviour from syncing**:

```csharp
// GOOD — always initialized
[UdonSynced] private int[] _scores = new int[0];

// BAD — uninitialized will break sync
[UdonSynced] private int[] _scores;
```

### Prioritization of Visible Objects

Udon prioritizes syncing game objects visible to the local player. Objects not in the camera frustum
receive a lower sync priority automatically.

### Multiple UdonBehaviours on One Object

If a GameObject has both a Manual and a Continuous UdonBehaviour, **both will act as Manual** (most
restrictive mode wins).

---

## Networking Properties Reference

These are available on the static `Networking` class:

| Property | Type | Description |
|---|---|---|
| `Networking.LocalPlayer` | `VRCPlayerApi` | The local player |
| `Networking.IsMaster` | `bool` | Is local player the instance master? Do not use for security/gating. |
| `Networking.Master` | `VRCPlayerApi` | Always-valid VRCPlayerApi of current master |
| `Networking.IsInstanceOwner` | `bool` | `true` for instance creator in Invite/Friends instances. Always `false` in Group/Public instances |
| `Networking.InstanceOwner` | `VRCPlayerApi` | Instance creator's VRCPlayerApi; `null` if they've left. Instance ownership never changes |
| `Networking.IsNetworkSettled` | `bool` | `true` when all synced data has been received and applied |
| `Networking.IsClogged` | `bool` | `true` when outbound data exceeds send capacity |
| `Networking.SimulationTime(GameObject)` | `float` | Current simulation timestamp of a networked object |
| `Networking.SimulationTime(VRCPlayerApi)` | `float` | Current simulation timestamp of a player |

### Simulation Time

`Networking.SimulationTime` is a timestamp used internally by `VRCObjectSync` for smooth replication.

```csharp
// Measure a player's effective latency:
float latency = Time.realtimeSinceStartup - Networking.SimulationTime(player);
```

---

## Networking Events Reference

### `OnPreSerialization`
Fires just before synced data is sent. Set any synced variables you want updated here.

### `OnDeserialization`
Fires when synced data arrives. All synced variables are updated by the time this fires.
Use to react to new values.

### `OnDeserialization(DeserializationResult)` *(overload)*
Same as above, with timing information:

```csharp
public override void OnDeserialization(DeserializationResult result)
{
    float sendTime    = result.sendTime;    // Time.realtimeSinceStartup on sender at send time
    float receiveTime = result.receiveTime; // Time.realtimeSinceStartup on this client at receive time
    bool fromStorage  = result.isFromStorage; // true if restored from persistence storage

    float latency = receiveTime - sendTime;
    Debug.Log($"Deserialization latency: {latency:F3}s, from storage: {fromStorage}");
}
```

> Note: `sendTime` can be negative if sent before this client launched VRChat.
> Do not share raw `sendTime` across clients — use offsets relative to `Time.realtimeSinceStartup`.

### `OnPostSerialization(SerializationResult)`
Fires after an attempt to send serialized data:

```csharp
public override void OnPostSerialization(SerializationResult result)
{
    bool success   = result.success;
    int bytesSent  = result.byteCount;
    Debug.Log($"Sent {bytesSent} bytes, success={success}");
}
```

### `OnOwnershipRequest`
Fires before ownership is transferred. Return `true` to approve, `false` to deny.
Runs on both the requester and the current owner — be sure both sides agree, or a desync will occur.

```csharp
public override bool OnOwnershipRequest(VRCPlayerApi requester, VRCPlayerApi newOwner)
{
    // Example: only allow if game is not in progress
    return !_gameInProgress;
}
```

### `OnOwnershipTransferred`
Fires for all clients when ownership changes.

```csharp
public override void OnOwnershipTransferred(VRCPlayerApi newOwner)
{
    Debug.Log($"New owner: {newOwner.displayName}");
    if (Networking.IsOwner(gameObject))
    {
        // Now safe to set synced variables and call RequestSerialization
    }
}
```

### `OnMasterTransferred`
Fires for all clients when the instance master changes (previous master left).

```csharp
public override void OnMasterTransferred(VRCPlayerApi newMaster)
{
    Debug.Log($"New master: {newMaster.displayName}");
}
```

---

## Object Ownership — Deep Reference

- Every networked GameObject's default owner is the **instance master**.
- Ownership transfers when a player calls `Networking.SetOwner(player, gameObject)`.
- If the owner leaves, VRChat **automatically assigns a new owner** before `OnPlayerLeft` fires.
- Cannot Modify synced variables unless you are the owner.

### Instance Master vs. Instance Owner

| | Instance Master | Instance Owner |
|---|---|---|
| Who | The player currently acting as "host" | The player who originally created the instance |
| Changes? | Yes — changes when current master leaves | Never changes |
| API | `Networking.IsMaster` / `Networking.Master` | `Networking.IsInstanceOwner` / `Networking.InstanceOwner` |
| Security use | ❌ Do not use for security/gating | ✅ OK for moderation/access control |
| Group/Public instances | Exists | `IsInstanceOwner` always `false` |

> ⚠️ **Do not use `IsMaster` for security or feature gating** — the master may be unresponsive,
> and any player can become master. Use `IsInstanceOwner` or a proper ownership/moderation system.

### OnOwnershipRequest Control Flow

1. Player A calls `Networking.SetOwner(A, obj)`.
2. `OnOwnershipRequest` fires on Player A (requester) and current owner simultaneously.
3. Both sides must return `true` for the transfer to proceed.
4. If approved, `OnOwnershipTransferred(A)` fires for all clients.

---

## Network Stats API

Access realtime network stats via `VRC.SDK3.Network.Stats` (static class):

### Global Stats

```csharp
using VRC.SDK3.Network;

float throughput   = Stats.ThroughputPercentage; // % of allowed outgoing bandwidth in use
float variance     = Stats.RoundTripVariance;     // variance in server round-trip time
float bytesInMax   = Stats.BytesInMax;            // max bytes received in a second
float bytesOutMax  = Stats.BytesOutMax;           // max bytes sent in a second
float bytesOutAvg  = Stats.BytesOutAverage;       // rolling average bytes sent/sec
float bytesInAvg   = Stats.BytesInAverage;        // rolling average bytes received/sec
float hitches      = Stats.HitchesPerNetworkTick; // average missing samples per tick
float suffering    = Stats.Suffering;             // measure of queued outbound messages
float timeInRoom   = Stats.TimeInRoom;            // seconds in current instance
```

### Per-Object Stats

```csharp
float updateInterval = Stats.GetUpdateInterval(gameObject);       // avg time between sends
float receiveInterval = Stats.GetReceiveInterval(gameObject);     // avg time between receives
float finalDelay      = Stats.GetFinalDelay(gameObject);          // sync time adjustment
int   group           = Stats.GetGroup(gameObject);               // network group ID
float groupDelay      = Stats.GetGroupDelay(gameObject);          // group sync adjustment
bool  sleeping        = Stats.GetSleeping(gameObject);            // true = not sending
int   size            = Stats.GetSize(gameObject);                // bytes in last message
float bytesPerSec     = Stats.GetBytesPerSecondAverage(gameObject); // rolling avg bytes/sec
int   totalBytes      = Stats.GetTotalBytes(gameObject);          // total bytes consumed
int   reliableQueue   = Stats.GetReliableEventsInOutboundQueue(gameObject); // queued reliable events
float lastSendTime    = Stats.GetLastSendTime(gameObject);        // last send timestamp
float lastReceiveTime = Stats.GetLastReceiveTime(gameObject);     // last receive timestamp
```

---

## Network Compatibility

When you upload a new version of your world, it **may be incompatible** with existing live instances
running the previous version.

### What Causes Incompatibility

A world update is incompatible with an older instance if any **synchronized** GameObject has:
- A changed number of synced components (added/removed components).
- Changed component order on synchronised objects.
- Changed number or types of synced variables.

### Impact

- Users in an active old-version instance trying to join will receive a warning and be removed.
- Once all users leave the old instance, new users join the new version without issues.
- Adding/removing non-synced components or changing non-synced variables is safe.

> **Tip:** Finalize the set of synced components and variables before publishing. Plan changes carefully
> to avoid breaking active instances.

---

## Network ID Utility

Network IDs are integer identifiers assigned to each synchronized GameObject, used to match objects
across different platform builds (PC vs. Android) of the same world.

### When You Need It

Only required if you maintain **separate scenes/projects** for different platforms (e.g., PC scene
and Android scene). In that case, IDs must match for the same logical object across both.

### Using the Utility

In the Unity Editor: `VRChat SDK → Utilities → Network ID Import and Export Utility`

1. **Regenerate Scene IDs** — assigns IDs to all synced objects in the scene.
2. **Export** — saves IDs to a JSON file (`{"10":"/CubePickup", "11":"/Prefabs/SyncedPen"}`).
3. **Import** — loads IDs into another scene; objects are matched by scene hierarchy path.
4. **Scan for Conflicts** — detects mismatched IDs and lets you resolve them manually.

> ⚠️ **Do not use `/` in synced GameObject names** — paths use `/` as a delimiter.

---

## Performance Guidelines

### Variables vs. Events

| Use case | Choose |
|---|---|
| Persistent state (door open/closed, scores, game state) | Synced variables |
| Late joiner must see correct state | Synced variables |
| One-time cosmetic action (sound, particle, animation trigger) | Network event |
| No persistence needed | Network event |

### Reducing Bandwidth

- **Only sync when values change** — avoid sending identical data repeatedly.
- **Don't sync rigidbodies** unless necessary — physics simulation can run locally.
- **Use interpolation/extrapolation** to smooth movement instead of high-frequency position updates.
- **Group booleans into a single `int` bitmask** instead of syncing multiple `bool` fields.
- **Prefer `Color32` over `Color`** (4 bytes vs. 16 bytes).
- **Minimize ownership transfers** — each transfer introduces latency.

### Check IsClogged Before Sending

```csharp
void Update()
{
    if (Networking.IsClogged) return; // skip this frame
    // send update
}
```
