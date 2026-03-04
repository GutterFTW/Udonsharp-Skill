# Networking Tips & Patterns

**Source:** https://udonsharp.docs.vrchat.com/networking-tips-&-tricks

---

## Overview

Networking in VRChat Udon is heavily rate-limited. Sending too much data causes "Death Runs" — data is
lost because it cannot be sent fast enough. Always prefer simplicity.

---

## Ownership

- Every `GameObject` with an `UdonBehaviour` has a single **owner**.
- The **instance master** (longest-running player) is the default owner.
- Only the owner sends synced variable data to others.
- Ownership is transferred automatically when picking up a `VRCPickup` (if networked).

### Transferring Ownership
```csharp
Networking.SetOwner(Networking.LocalPlayer, gameObject);
```

### ⚠️ Known Issue: SetOwner Race Condition
Do **NOT** set synced variables immediately after `SetOwner`.
The old owner must acknowledge the transfer first (can take seconds on high latency).

**Correct pattern — wait for `OnOwnershipTransferred`:**
```csharp
public override void OnOwnershipTransferred(VRCPlayerApi player)
{
    if (Networking.IsOwner(gameObject))
    {
        // Now safe to set synced variables
        _myValue = 42;
        RequestSerialization();
    }
}
```

---

## Sync Modes

### Manual Sync (Reliable)
```csharp
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
```
- Call `RequestSerialization()` to push data to all clients.
- Updates are reliable when requested.
- Best for state changes that don't happen every frame.

### Continuous Sync (Frequent but lossy)
```csharp
[UdonBehaviourSyncMode(BehaviourSyncMode.Continuous)]
```
- Data is sent automatically at a high rate.
- Some updates may be dropped to save bandwidth.
- Good for position sync — use `UdonSyncMode.Smooth` or `UdonSyncMode.Linear`.

### None / NoVariableSync
```csharp
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]          // events disabled too
[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)] // events still work
```
- No bandwidth overhead for variable syncing.
- Use `NoVariableSync` when you only need `SendCustomNetworkEvent`.

---

## Network Events

```csharp
SendCustomNetworkEvent(NetworkEventTarget.All, "MethodName");
SendCustomNetworkEvent(NetworkEventTarget.Owner, "MethodName");
```

**Rules:**
- Target method must be `public`.
- Methods starting with `_` will NOT receive network events.
- Events arrive **before** synced variable updates — do not assume variable is set when event fires.

### Workaround: Event arrives before variable
```csharp
// Option A: delay event after setting variable
_syncedValue = newValue;
RequestSerialization();
SendCustomEventDelayedSeconds("ApplyChange", 0.5f, EventTiming.Update);

// Option B: detect the value change locally via FieldChangeCallback
[UdonSynced, FieldChangeCallback(nameof(SyncedValue))]
private int _syncedValue;

public int SyncedValue
{
    set { _syncedValue = value; ApplyChange(); }
    get => _syncedValue;
}
```

---

## FieldChangeCallback Pattern

Fires a property setter whenever the field is updated via network sync or `SetProgramVariable`:

```csharp
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class MySync : UdonSharpBehaviour
{
    [UdonSynced, FieldChangeCallback(nameof(IsActive))]
    private bool _isActive;

    public bool IsActive
    {
        set
        {
            _isActive = value;
            gameObject.SetActive(value); // react to change
        }
        get => _isActive;
    }

    public override void Interact()
    {
        Networking.SetOwner(Networking.LocalPlayer, gameObject);
        IsActive = !IsActive; // use property, not _isActive directly
        RequestSerialization();
    }
}
```

> ⚠️ Setting `_isActive` directly from **outside** the UdonBehaviour will not trigger the callback.
> Always use the property. UdonSharp will refuse to compile external direct assignments.

---

## Instantiation

`VRCInstantiate(prefab)` creates a **local, non-synced** copy. It is NOT networked and will only
exist on the local client.

**For networked spawning, use `VRCObjectPool`:**
```csharp
[SerializeField] private VRCObjectPool pool;

public void SpawnObject()
{
    GameObject obj = pool.TryToSpawn();
    if (obj == null) return; // pool exhausted
    // obj is now active and synced for all players
}

public void ReturnObject(GameObject obj)
{
    pool.Return(obj); // deactivates and syncs for all players
}
```

---

## Sending Data to a Specific Player

There is no direct way to send to a specific player. Two workarounds:

1. **Object-per-player** — give each player ownership of a unique object, then find it via `Networking.GetOwner`.
2. **Synced variable broadcast** — sync a variable (e.g., `int targetPlayerId`) then each client reads it
   and checks if `Networking.LocalPlayer.playerId == targetPlayerId`.

---

## Synced String Limit

Strings are approximately **50 characters** in practice due to bandwidth constraints (2 bytes/char).
For larger text, use `VRCStringDownloader` to load from an external URL.
