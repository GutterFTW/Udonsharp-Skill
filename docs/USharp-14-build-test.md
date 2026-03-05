# Using Build & Test

**Source:** https://creators.vrchat.com/worlds/udon/using-build-test

---

## Overview

Simple Unity Play Mode works for basic things (timers, mouse events), but for avatar interactions,
networking, and teleportation you need a real VRChat client build.

---

## Initial Setup

1. Open the VRChat Control Panel via **VRChat SDK → Show Control Panel**.
2. Sign in on the **Authentication** tab.
3. On the **Settings** tab, set the **VRChat Client** path:
   - Steam: `C:\Program Files (x86)\Steam\steamapps\common\VRChat\VRChat.exe`
   - Oculus: `C:\Program Files\Oculus\Software\Software\vrchat-vrchat\VRChat.exe`
   - Viveport: `C:\Viveport\ViveApps\469fbcbb-...\VRChat.exe`
4. On the **Builder** tab, click **"Setup Layers for VRChat"** → **"Do it!"**
5. Click **"Set Collision Matrix"** → **"Do it!"**

---

## Running a Test

1. Switch to the **Builder** tab.
2. (Optional) Enable **Force Non-VR** for desktop testing.
3. Press **Build & Test** — Unity builds the world and launches VRChat in a local instance.

---

## Launching Multiple Clients

To test synced variables and network events you need multiple players. Set **Number of Clients** to 2 (or more) and press **Build & Test**.

- Both clients launch with your avatar.
- The first client to load is **Master** and therefore **Owner** of all GameObjects by default.
- The second client can observe and interact, but needs ownership transfer to modify synced state.
- The `SyncButtonAnyone` example transfers ownership to whoever clicks it.

---

## Build & Reload (0 clients)

Set **Number of Clients** to `0` to switch "Build & Test" into **"Build & Reload"**.

- Builds a new world version and moves all already-open clients into the new local instance.
- Skips the VRChat startup/login sequence entirely — much faster iteration.

### --watch-worlds flag

Enable auto-reload on your manually launched client:
```
VRChat.exe --watch-worlds --profile=0 --no-vr --enable-debug-gui --enable-sdk-log-levels --enable-udon-debug-logging -screen-width 1920 -screen-height 1080
```

> **Note:** Clients launched via "Build & Test" (non-zero count) may not join reloaded worlds.
> Set count back to 0, then use "Build & Reload" or "Reload Last Build" to re-sync all clients.

---

## UdonExampleScene

`Packages/com.vrchat.worlds/Samples/UdonExampleScene/` — a reference scene from VRChat with ready-to-use Udon Graph examples covering synced variables, pickups, stations, and more.
