# Items in Udon Worlds

**Source:** https://creators.vrchat.com/worlds/items

---

## Overview

VRChat users can spawn **Items** in your world — tools, toys, and gadgets provided by VRChat. Items are enabled by default for all worlds.

> Items are a newer feature. If they cause issues in your world, you can disable them via your world settings on VRChat.com.

---

## Disabling Items

To turn off items for your world:

1. Go to [My Worlds](https://vrchat.com/home/content/worlds) on VRChat.com.
2. Open your world's **Edit** page (click the thumbnail, not the title).
3. In **Default Content Settings**, find **"Items Enabled"**.
4. Press the button to disable and provide a reason.
5. Scroll to the top and press **Save**.

---

## Showing Items in Mirrors

Items spawn on the **"Item"** layer (previously named `reserved3` in older SDK versions).

If your world uses an older VRCMirror prefab or you've manually configured the reflect layers, items will not appear in mirrors.

**Fix:** Add the `Item` layer to the mirror's **Reflect Layers** dropdown.

> The latest VRCMirror prefab reflects the Item layer correctly by default.

---

## Avoiding Items in Physics Methods

Udon scripts **cannot reference Items** — they appear as `null` objects.

If an `UdonBehaviour` processes an item via a Physics callback, it **throws an exception and halts itself**.

### Best Practices

**1. Use LayerMask to exclude the Item layer:**

```csharp
// Exclude the "Item" layer (layer 10) from raycasts/overlaps
int layerMask = ~(1 << LayerMask.NameToLayer("Item"));
if (Physics.Raycast(origin, direction, out RaycastHit hit, maxDist, layerMask))
{
    // hit.collider will never be an Item
}
```

**2. Validate objects with `Utilities.IsValid` before using them:**

```csharp
void OnTriggerEnter(Collider other)
{
    if (!Utilities.IsValid(other.gameObject)) return;  // Item or protected object
    // safe to use 'other' here
}
```

This pattern applies to any Physics method that returns a GameObject or Collider:
- `OnTriggerEnter/Stay/Exit`
- `OnCollisionEnter/Stay/Exit`
- `Physics.Raycast`, `Physics.OverlapSphere`, etc.

See [Physics and Layers](https://creators.vrchat.com/worlds/layers#physics-and-layers) for a complete list of methods that require this check.

---

## Item Layer Reference

| Layer Name | Notes |
|---|---|
| `Item` | Layer used by all spawned items (was `reserved3` in older SDKs) |

When building layer masks in code, always use `LayerMask.NameToLayer("Item")` rather than hardcoding the number, in case VRChat reassigns layers in a future SDK.
