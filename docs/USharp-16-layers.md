# Unity Layers in VRChat

**Source:** https://creators.vrchat.com/worlds/layers

---

## Overview

Unity layers organize GameObjects, determine collisions/raycasts, and control rendering culling masks.

VRChat worlds automatically use VRChat's built-in layer configuration. If you rename, remove, or change
the collision matrix of built-in layers, **your changes are overridden on upload**.

Layers 22–31 are free user layers — you can edit them and they won't be overridden on upload.

---

## VRChat Layer Reference

| # | Name | Usage | Stickers |
|---|---|---|---|
| 0 | Default | Unity default. Used for Avatar Pedestals. | ✔ |
| 1 | TransparentFX | Unity Flare Assets. | ❌ |
| 2 | IgnoreRaycast | Ignored by Unity Physics Raycasts (not by VRChat raycasts). | ❌ |
| 3 | Item | VRChat user-placed items. **Objects on this layer are moved to layer 0 on upload.** | ❌ |
| 4 | Water | Unity Standard Assets, VRChat Portals, VRChat Mirrors, Post Processing. | ✔ |
| 5 | UI | Unity's default UI layer. ⚠ Ignored by VRChat UI pointer unless menu is open. Ignored by VRChat camera unless UI is enabled. | ❌ |
| 6 | reserved6 | ⚠ Reserved by VRChat. Objects moved to layer 0 on upload. | ❌ |
| 7 | reserved7 | ⚠ Reserved by VRChat. Objects moved to layer 0 on upload. | ❌ |
| 8 | Interactive | Unused by Unity/VRChat. Recommended for colliders where you don't want stickers. | ❌ |
| 9 | Player | All VRChat players except the local player. | ❌ |
| 10 | PlayerLocal | Local player rendering (head bone excluded in humanoid avatars). | ❌ |
| 11 | Environment | Unused by Unity/VRChat. | ✔ |
| 12 | UiMenu | ⚠ VRChat nameplates. Ignored by UI pointer unless menu is open. | ❌ |
| 13 | Pickup | VRChat Pickups (auto-assigned when adding VRCPickup). Does not collide with players. | ❌ |
| 14 | PickupNoEnvironment | Collides only with layer 13 (Pickup). | ❌ |
| 15 | StereoLeft | Unused. | ❌ |
| 16 | StereoRight | Unused. | ❌ |
| 17 | Walkthrough | Colliders do not collide with players. | ✔ |
| 18 | MirrorReflection | Local player rendered in mirrors only. Renderers only appear in mirrors, not in main camera. Colliders don't block VRChat raycasts. | ❌ |
| 19 | InternalUI | ⚠ VRChat internal UI (debug consoles, etc.). Avoid using. | ❌ |
| 20 | HardwareObjects | ⚠ Virtual representations of physical hardware (controllers, trackers). Avoid using. | ❌ |
| 21 | reserved4 | ⚠ Reserved by VRChat. Objects moved to layer 0 on upload. | ❌ |
| 22–30 | (user) | Unused by Unity/VRChat. Free to use. Not overridden on upload. | (✔)* |
| 31 | (user) | ⚠ Used by Unity Editor Preview. Otherwise free to use. | (✔)* |

*Layers 22–31 allow stickers by default. You can disable them by adding them to the Interaction Passthrough list.

---

## Stickers

[VRChat+](https://hello.vrchat.com/vrchatplus) users can place image stickers on `Collider` components.

- Only some layers allow stickers (see ✔ column above).
- To prevent stickers on a specific collider, move it to layer 8 (Interactive).
- To disable stickers completely: edit your world on the VRChat website → disable stickers.

---

## Physics and Layers

When using Physics raycasts/overlaps, **always limit tested layers** to avoid accidentally picking up objects on reserved layers.

Always use `Utilities.IsValid(obj)` on any object returned by Physics calls — reserved-layer objects may return `null`:

```csharp
int layerMask = (1 << 0) | (1 << 11) | (1 << 17); // Default, Environment, Walkthrough
RaycastHit hit;
if (Physics.Raycast(transform.position, transform.forward, out hit, 100f, layerMask))
{
    if (Utilities.IsValid(hit.collider))
    {
        // safe to use hit.collider.gameObject
    }
}
```

If `Utilities.IsValid` returns false, the object is "protected" — using it will throw an exception and halt the UdonBehaviour.

---

## Interaction Block and Passthrough

Interaction (remote grab, UI laser) is **blocked** by most VRChat layers. The following layers are **transparent** to interaction (pass-through):
- `UiMenu`
- `UI`
- `PlayerLocal`
- `MirrorReflection`

### Interaction Passthrough for User Layers

User layers (22–31) block interaction by default. Use the **"Interact Passthrough"** mask in your scene descriptor to make specific layers transparent to interaction.

> **VR note:** VR players can penetrate colliders with their physical hand. A collider penetrated by the hand does not block interaction from that VR player.

---

## `CullingMask` and `LayerCullDistances` in VRCCameraSettings

When setting `CullingMask` on `VRCCameraSettings.ScreenCamera`:
- `reserved` layers, `MirrorReflection`, and `InternalUI` cannot be changed.
- `InternalUI` may read 0 on some platforms even if visible (due to camera stacking).

When setting `LayerCullDistances` (array of 32 floats):
- `reserved` layers and `InternalUI` always read 0 and cannot be changed.
- A value of 0 for any layer means use `FarClipPlane` for that layer.
