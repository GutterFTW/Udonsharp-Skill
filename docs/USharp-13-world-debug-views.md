# World Debug Views

**Source:** https://creators.vrchat.com/worlds/udon/world-debug-views

---

## Overview

VRChat's in-client debug views help diagnose world issues. Access them via the **"Toggle Debug UI"** button on the Quick Menu Settings page.

Some views are restricted — by default only the world creator sees them. Enable **World Debugging** in your world's settings on the VRChat website to allow others (users must rejoin after enabling).

The status of World Debugging is shown in the bottom-left corner of the debug menu.

---

## Shortcuts

Open debug views directly with: `Right Shift` + `` ` `` (key left of `1`) + `<Number>`

> If `` ` `` isn't directly left of `1` on your keyboard, use whatever key is in that position.

---

## Debug Menu Pages (Tabs)

### 1 — AssetBundle and Memory View

Shows memory usage for loaded avatars, worlds, items, and other assets.
Primarily for troubleshooting VRChat, rarely needed during world development.

> **Warning:** The "Force GC" buttons trigger garbage collection. They do **not** improve performance and may cause instability if used repeatedly.

### 2 — Version & Info

Displays VRChat build info and hotkey reference (in red). Buttons toggle debug world overlays.

> First shortcut press shows a minimal HUD; press again for the full view.

### 3 — Log Viewer

Displays the output log in-client. Shows Udon crashes, `Debug.Log` output, and Unity/VRChat errors.

- Filter buttons at the top toggle log types.
- **Background** mode collects logs when the debug menu is closed (persists across restarts).
- **Warning:** Background log collection has a noticeable performance impact.

### 4 — Players

Displays per-player networking stats:
- **M** — is instance master
- **L** — is local player
- **VR** — is in VR
- **Group** — internal networking group (objects combined by distance for batched sends)
- **Intrvl** — time between synced self-data sends
- **Fnl D** — targeted delay between owner and viewer

The top section shows general realtime networking state, including **"Suffering"** (indicates Udon is sending too much data). Most values are readable via the [Network Stats API](https://creators.vrchat.com/worlds/udon/networking/network-stats/).

### 6 — Net Objects *(restricted)*

Displays all networked objects in the world:
- **Owner** — playerID of the object owner
- **Group** — internal networking group
- **Sleeping** — object has stopped transmitting (VRCObjectSync objects only)
- **Delay** — current owner-to-viewer delay
- **Size** — bytes per serialization
- **Bps** — approximate bytes/second
- **Since Last** — time since last data send
- **Interval** — approximate syncs per second

> Note: viewing this page with many objects can cause performance slowdowns.

### Audio Sources *(restricted)*

Shows all active `AudioSource` components in the world:
- **Name** — GameObject name
- **Clip** — assigned AudioClip name (`<null>` if unset)
- **Type** — World / Avatar / Internal (regular users only see World)
- **Vol** — Unity volume (excludes `VRC_SpatialAudioSource` gain)
- **3D** — `spatialBlend` value
- **Act/Prog** — playing status and clip progress
- **VRC/SAS** — whether it has `VRC_SpatialAudioSource` / Steam Audio conversion
- **Dist** — physical distance and virtual near-field distance in brackets

Sources can be sorted by Scene order or distance from listener.

---

## Debug World Overlays

World overlays show data directly in the scene, not in a menu tab. Enable them via shortcuts or from the Version & Info (2) page.

> All overlays require World Debugging enabled — or that you uploaded the current world.

### 7 — PhysBone & Contact Overlay

Highlights all nearby PhysBones and Contacts. Useful for debugging unexpected PhysBone/Contact behavior.

### 8 — Network Object Info Overlay

Shows a floating panel on every synced object:
- **P** — owner ping
- **Q** — data quality (100% = no dropped packets)
- **O** — owner playerID
- **G** — networking group
- **Held** — whether the object is currently held (pickups only)
- **Status** — current state: `Should Sleep`, `Player`, `Held`, `Discontinuity`, etc.

### 9 — Player Info Overlay

Similar to overlay 8, but shows debug stats floating at each player's feet.

### 0 — VRC_UiShape Debug Overlay

Shows outlines of every `VRC_UiShape` in the world.
Also displays a text overlay (desktop: on screen, VR: on hands) indicating what the UI pointer is targeting.
Useful for diagnosing UI interaction issues.
