# Network Events (Deep Dive)

**Source:** https://creators.vrchat.com/worlds/udon/networking/events

---

## Overview

Network events allow one-way communication between scripts. When executed, the event fires **once**
for the targeted players currently in the instance.

> ⚠️ Events are **not replayed for late joiners**. Use synced variables for state that must persist.
> Use events only for temporary actions (sound effects, visual effects, cosmetic triggers).

---

## Defining a Network-Callable Method

Since SDK 3.8.1, decorate any method with `[NetworkCallable]` to allow it to be called over the network.
Methods must be `public`. The name does NOT have to avoid underscores if `[NetworkCallable]` is present.

```csharp
using VRC.SDK3.UdonNetworkCalling;

[NetworkCallable]
public void DoSomething()
{
    // executes on receiving clients
}

// With custom rate limit (default is 5/sec, max 100/sec):
[NetworkCallable(maxEventsPerSecond: 10)]
public void FrequentAction()
{
    Debug.Log("Called over network");
}
```

### Legacy Events (Backwards Compatibility)

Any `public` method **not starting with `_`** can still be called over the network without
`[NetworkCallable]`, but this is not recommended. Methods with `[NetworkCallable]` use component-index
targeting; legacy methods use GameObject-broadcast semantics (calls all UdonBehaviours on the object).

> ⚠️ **To prevent a method from ever being called over the network, prefix it with `_`.**
> This is a security measure — use it for all internal methods.

---

## Calling a Network Event

```csharp
// On the current (this) behaviour:
SendCustomNetworkEvent(NetworkEventTarget.All,   nameof(MyEvent));
SendCustomNetworkEvent(NetworkEventTarget.Others, nameof(MyEvent));
SendCustomNetworkEvent(NetworkEventTarget.Owner,  nameof(MyEvent));
SendCustomNetworkEvent(NetworkEventTarget.Self,   nameof(MyEvent)); // local loopback only

// On a different behaviour:
otherBehaviour.SendCustomNetworkEvent(NetworkEventTarget.All, nameof(MyEvent));

// Or explicitly via NetworkCalling (identical):
// NetworkCalling.SendCustomNetworkEvent(...);
```

### Event Targeting

| Target | Who receives it |
|---|---|
| `NetworkEventTarget.All` | All players including local (local executes immediately) |
| `NetworkEventTarget.Others` | All players **except** the local player |
| `NetworkEventTarget.Owner` | Only the object owner |
| `NetworkEventTarget.Self` | Only the local player (no network send, bypasses rate limiting) |

> When `All` or `Others` sends to remote players AND includes local, the local player executes
> immediately rather than waiting for network delivery.

### Sending to a Specific Player

There is no direct "send to player X" target. Workarounds:
1. Use `NetworkEventTarget.All` and check `playerId` inside the method.
2. Use the player's owned object as a target with `NetworkEventTarget.Owner`.

---

## Events with Parameters (SDK 3.8.1+)

A network event can carry up to **8 parameters**. Parameter types must be syncable variable types.
Mark the receiving method `[NetworkCallable]`:

```csharp
using VRC.SDK3.UdonNetworkCalling;

// Receiver (called on all targeted clients):
[NetworkCallable]
public void OnReceiveScore(int playerId, int score)
{
    Debug.Log($"Player {playerId} scored {score}");
}

// Sender:
public void BroadcastScore(int playerId, int score)
{
    SendCustomNetworkEvent(NetworkEventTarget.All, nameof(OnReceiveScore), playerId, score);
}
```

### Parameter Size Limits

| Data | Size |
|---|---|
| `int` | 4 bytes |
| `float` | 4 bytes |
| `Vector3` | 12 bytes |
| `string` of 400 chars | ~400 bytes (UTF-8) |
| `byte[1024]` | 1024 bytes — splits into 2 internal events |
| `byte[16384]` | 16 KB — maximum allowed total size |

- Total parameter data per call: **16 KB maximum**
- Total outgoing bandwidth: hard-capped at ~**18 KB/s** (practical ~8–10 KB/s with overhead)
- Events >1024 bytes are split into multiple internal events, consuming more rate-limit budget

---

## Rate Limiting

```csharp
// Default: 5 events/sec. Range: 1–100 events/sec.
[NetworkCallable(maxEventsPerSecond: 20)]
public void MyRatedEvent() { }
```

- Rate limiting is applied **both** client-side (queuing) and server-side (protection against exploits).
- Queued events are delivered in order. Events are never silently dropped by local clients.
- Server may drop events if a client exceeds another client's configured rate limit (mismatched world versions).
- `NetworkEventTarget.Self` always bypasses rate limiting (no network send).

### Congestion Monitoring

```csharp
using VRC.SDK3.UdonNetworkCalling;

void Update()
{
    // Check if network is congested globally
    if (Networking.IsClogged)
        Debug.LogWarning("Network is clogged!");

    // How many events queued for a specific event name on this behaviour:
    int q = NetworkCalling.GetQueuedEvents((VRC.Udon.Common.Interfaces.IUdonEventReceiver)this, "MyEvent");

    // Total queued events across all behaviours in the world:
    int total = NetworkCalling.GetAllQueuedEvents();
}
```

---

## Identifying the Sender

Inside a network event handler, use `NetworkCalling` to know who triggered it:

```csharp
using VRC.SDK3.UdonNetworkCalling;

[NetworkCallable]
public void OnInteracted()
{
    VRCPlayerApi caller = NetworkCalling.CallingPlayer; // null if not in a network call
    bool isNetworkCall  = NetworkCalling.InNetworkCall;

    if (Utilities.IsValid(caller))
        Debug.Log($"Called by: {caller.displayName}");
}
```

| Property | Type | Description |
|---|---|---|
| `NetworkCalling.CallingPlayer` | `VRCPlayerApi` | The player who sent this event. `null` if called locally. |
| `NetworkCalling.InNetworkCall` | `bool` | `true` while executing a received network event. |
| `NetworkCalling.GetQueuedEvents(receiver, eventName)` | `int` | Events queued for a specific method. |
| `NetworkCalling.GetAllQueuedEvents()` | `int` | Total queued events across the whole world. |

---

## Event Ordering

- Events from a single sender arrive in order (A sent before B → A received before B).
- Order is guaranteed across all behaviours in the scene **for a single sender**.
- No ordering guarantee when multiple players send events simultaneously.
- Rate-limited events can be "skipped" by non-rate-limited events in the queue.

---

## Security

- **Always prefix internal methods with `_` to prevent remote calls.**
- Set `maxEventsPerSecond` as low as feasible to prevent event spam exploits.
- Rate limits are applied server-side to protect against malicious clients.

---

## Sync Mode Requirement

`SendCustomNetworkEvent` is **disabled** for behaviours with `BehaviourSyncMode.None`.
Use `BehaviourSyncMode.NoVariableSync` if you need events but no variable sync.
