# Event Execution Order

**Source:** https://creators.vrchat.com/worlds/udon/event-execution-order

---

## Overview

Udon and Unity have built-in events that fire automatically. Understanding which fires first helps you avoid initialization-order bugs.

> Unity provides an [official execution order diagram](https://docs.unity3d.com/2022.3/Documentation/Manual/ExecutionOrder.html). VRChat follows Unity's order with VRChat-specific additions.

> **Caution:** VRChat and Unity updates may change this order. Not all events are listed, and certain events (ownership, late join) may fire in a different order depending on circumstances.

---

## General Execution Phases (Unity + Udon)

### Phase 1 — Initialization (once per object lifetime)

| Event | Notes |
|---|---|
| `Awake()` | Called when object is instantiated, even if disabled |
| `OnEnable()` | Called when object/component is enabled |
| `Start()` | Called once before the first frame Update, after Awake |

> In VRChat, **`Start()` runs in the first frame** the behaviour is active. Field initializers run at **compile time** — use `Start()` for anything needing scene references.

### Phase 2 — Physics (FixedUpdate rate, default 50 Hz)

| Event | Notes |
|---|---|
| `FixedUpdate()` | Physics step; runs before `Update` |

### Phase 3 — Per-Frame Updates

| Event | Notes |
|---|---|
| `Update()` | Runs once per rendered frame |
| `LateUpdate()` | Runs after all `Update()` calls in the same frame |

### Phase 4 — Rendering

| Event | Notes |
|---|---|
| `OnRenderObject()` | Runs during rendering; available in Udon |

### Phase 5 — Teardown

| Event | Notes |
|---|---|
| `OnDisable()` | Called when component/object is disabled |
| `OnDestroy()` | Called just before object is destroyed |

---

## VRChat-Specific Event Timing

### Late Joiners

When a player joins a world that's already in progress:
1. All existing `UdonBehaviour`s that were synced have `OnDeserialization()` fire **before** `Start()` on the joining client.
2. `OnPlayerJoined` fires for the **local player themselves** and then for each already-present player (order not guaranteed).

### Network Sync Events

| Event | When |
|---|---|
| `OnPreSerialization()` | Just before variables are serialized and sent (owner only) |
| `OnPostSerialization(SerializationResult)` | Just after serialization completes (owner only) |
| `OnDeserialization()` | After remote data is received and applied (non-owners) |
| `OnOwnershipTransferred(VRCPlayerApi)` | After ownership of the object changes |
| `OnMasterTransferred(VRCPlayerApi)` | After previous master leaves and a new master is promoted |

### Player Events

| Event | When |
|---|---|
| `OnPlayerJoined(VRCPlayerApi)` | A player (including local) enters the instance |
| `OnPlayerLeft(VRCPlayerApi)` | A player leaves the instance |

### Pickup / Interact / Station

| Event | When |
|---|---|
| `Interact()` | Player uses/clicks the object |
| `OnPickup()` | Object is picked up |
| `OnDrop()` | Object is dropped |
| `OnPickupUseDown()` | Use button pressed while holding |
| `OnPickupUseUp()` | Use button released while holding |
| `OnStationEntered(VRCPlayerApi)` | Player sits in station |
| `OnStationExited(VRCPlayerApi)` | Player leaves station |

---

## Execution Order Attribute

Use `[DefaultExecutionOrder(int)]` to control the order of `Update`, `LateUpdate`, and `FixedUpdate` relative to other UdonSharpBehaviours:

```csharp
[DefaultExecutionOrder(-100)]   // Run earlier than default
public class EarlyUpdater : UdonSharpBehaviour
{
    void Update() { /* runs before behaviours with higher order numbers */ }
}

[DefaultExecutionOrder(100)]    // Run later than default
public class LateUpdater : UdonSharpBehaviour
{
    void Update() { /* runs after behaviours with lower order numbers */ }
}
```

Lower numbers execute **earlier**. Default is 0.

---

## Key Gotchas

- `OnDeserialization` can fire **before** `Start()` for late joiners — don't assume initialization is complete.
- Network events (`SendCustomNetworkEvent`) arrive **before** synced variable updates — don't rely on synced variables being updated when a network event fires.
- Never teleport a player inside `OnDeserialization` — avatar may clip through geometry. Use `SendCustomEventDelayedFrames("TeleportMethod", 1)` instead.
- `Start()` runs only **once** — if you disable and re-enable a behaviour, `Start()` does not run again. Use `OnEnable()`/`OnDisable()` for toggle logic.
