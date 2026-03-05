# UdonSharp Skill — AI Context Package

A portable **AI skill** for VS Code that gives GitHub Copilot, Claude, and compatible AI
assistants deep, accurate knowledge about **UdonSharp** VRChat world and script development.

Import this skill into any VRChat / UdonSharp project to get AI assistance that understands
the UdonVM constraints, VRChat APIs, networking rules, and best practices right out of the box.

---

## What This Package Contains

```
.github/
  copilot-instructions.md   ← GitHub Copilot custom workspace instructions
CLAUDE.md                   ← Claude project memory (auto-read by Claude)
docs/
  01-udonsharp-overview.md  ← Language features, differences from C#
  02-vrchat-api.md          ← Full VRChat API reference
  03-players.md             ← Player API (positions, forces, audio, getting players)
  04-networking.md          ← Ownership, sync modes, event rules, patterns
  05-data-containers.md     ← DataList, DataDictionary, VRCJson
  06-persistence.md         ← Player Data and Player Objects
  07-input-events.md        ← Button and axis input events
  08-ui-events.md           ← Allowed Unity UI event targets
  09-editor-scripting.md    ← Custom inspectors, proxy system
  10-debugging.md           ← Debug workflow and log reading
  11-external-urls.md       ← String loading and image loading
  12-vrc-graphics.md        ← VRCGraphics, AsyncGPUReadback, VRCCameraSettings, VRCQualitySettings, Shader Globals
  13-world-debug-views.md   ← In-client debug views and overlays
  14-build-test.md          ← Build & Test, Build & Reload, multiple clients
  15-vm-assembly.md         ← Udon VM overview and Udon Assembly reference
  16-layers.md              ← Unity layers in VRChat (layer table, stickers, physics)
  17-allowlisted-components.md ← Complete list of allowed world components by category
  18-scene-components.md     ← VRC_SceneDescriptor, VRC_ObjectSync, VRC_Pickup, VRC_Station, VRC_MirrorReflection, VRC_SpatialAudioSource, VRC_AvatarPedestal, VRC_UIShape, VRC_PortalMarker
  19-world-setup.md          ← Creating worlds, SDK prefabs, supported assets, ClientSim, publishing
  20-video-players.md        ← VRCAVProVideoPlayer and VRCUnityVideoPlayer reference
  21-midi.md                 ← Realtime MIDI and MIDI playback (VRCMidiListener, VRCMidiPlayer)
  22-event-execution-order.md ← Unity + VRChat event execution order
  23-items.md                ← Items in Udon worlds (Item layer, disabling, physics safety)
snippets/
  udonsharp.code-snippets   ← VS Code C# code snippets
README.md                   ← This file
```

---

## How to Use

### Option A — Copy into your project (Recommended)

1. Copy this entire folder into your Unity project under `Assets/` (e.g. `Assets/UdonSharp-Skill/`).
2. VS Code will automatically pick up `.github/copilot-instructions.md` for GitHub Copilot.
3. Claude reads `CLAUDE.md` automatically when you open the project folder.
4. The `snippets/udonsharp.code-snippets` file is picked up as workspace snippets by VS Code.

### Option B — Reference as a VS Code workspace

1. Open VS Code with this folder as the workspace root.
2. Add your actual Unity project as a second workspace folder (multi-root workspace).
3. GitHub Copilot will merge the instructions from both workspaces.

### Option C — Manual snippet import

1. Press `Ctrl+Shift+P` → **Preferences: Configure User Snippets** → `csharp.json`.
2. Copy the contents of `snippets/udonsharp.code-snippets` into that file.

---

## Available Snippets

Type any prefix in a `.cs` C# file and press `Tab` to expand:

| Prefix | What it inserts |
|---|---|
| `udon-behaviour` | Basic `UdonSharpBehaviour` template |
| `udon-behaviour-manual` | Template with `BehaviourSyncMode.Manual` |
| `udon-behaviour-continuous` | Template with `BehaviourSyncMode.Continuous` |
| `udon-field-callback` | `[UdonSynced]` + `[FieldChangeCallback]` property pair |
| `udon-interact` | `Interact()` override |
| `udon-player-joined` | `OnPlayerJoined()` override |
| `udon-player-left` | `OnPlayerLeft()` with `IsValid` guard |
| `udon-ownership-transfer` | `OnOwnershipTransferred()` with ownership check |
| `udon-deserialization` | `OnDeserialization()` override |
| `udon-pickup-events` | All 4 pickup event overrides |
| `udon-station-events` | Station enter/exit overrides |
| `udon-network-event` | `SendCustomNetworkEvent(...)` call |
| `udon-delayed-event` | `SendCustomEventDelayedSeconds(...)` |
| `udon-delayed-frames` | `SendCustomEventDelayedFrames(...)` |
| `udon-set-owner` | Take ownership of current GameObject |
| `udon-is-owner` | Ownership check with if-block |
| `udon-request-serialization` | `RequestSerialization()` |
| `udon-get-players` | Non-allocating iterate-all-players pattern |
| `udon-local-player` | `Networking.LocalPlayer` assignment |
| `udon-teleport` | Teleport local player to a transform |
| `udon-locomotion` | Set walk/run/jump/gravity |
| `udon-voice` | Set all voice properties |
| `udon-tracking-data` | Get head/hand/origin tracking data |
| `udon-input-jump` | `InputJump` override |
| `udon-input-use` | `InputUse` override |
| `udon-input-axis` | `InputMoveHorizontal` + `InputMoveVertical` overrides |
| `udon-object-pool` | `VRCObjectPool` spawn + return pattern |
| `udon-object-sync` | `VRCObjectSync` teleport with `FlagDiscontinuity` |
| `udon-string-load` | `VRCStringDownloader` full pattern |
| `udon-json-parse` | `VRCJson.TryDeserializeFromJson` pattern |
| `udon-editor-script` | Custom inspector for `UdonSharpBehaviour` |
| `udon-recursive` | `[RecursiveMethod]` attributed method |
| `udon-is-valid` | `Utilities.IsValid()` guard |
| `udon-get-component-udon` | `GetComponent` for `UdonBehaviour` (non-generic) |

---

## Key Rules the AI Will Know

- No `List<T>` or `Dictionary<T>` — use `[]` arrays only
- Never `SetOwner` then immediately set synced variables — wait for `OnOwnershipTransferred`
- Never teleport in `OnDeserialization` — delay by 1 frame
- `VRCInstantiate` is local only — use `VRCObjectPool` for networked spawning
- Network event methods must be `public` and not start with `_`
- Struct methods like `.Normalize()` don't modify in-place — use return value
- Cache `GetComponent<T>()` in `Start()`, never in `Update()`
- Udon runs 200–1000× slower than C# — keep logic out of `Update()`

---

## Source Reference URLs

All content is derived from the official documentation:

- https://udonsharp.docs.vrchat.com/
- https://udonsharp.docs.vrchat.com/vrchat-api
- https://udonsharp.docs.vrchat.com/udonsharp
- https://udonsharp.docs.vrchat.com/editor-scripting
- https://udonsharp.docs.vrchat.com/examples
- https://udonsharp.docs.vrchat.com/random-tips-&-performance-pointers
- https://udonsharp.docs.vrchat.com/networking-tips-&-tricks
- https://udonsharp.docs.vrchat.com/exporting-to-assembly-files
- https://creators.vrchat.com/worlds/udon/udonsharp/
- https://creators.vrchat.com/worlds/udon/udonsharp/attributes
- https://creators.vrchat.com/worlds/udon/udonsharp/performance-tips
- https://creators.vrchat.com/worlds/udon/players/
- https://creators.vrchat.com/worlds/udon/players/getting-players
- https://creators.vrchat.com/worlds/udon/players/player-audio
- https://creators.vrchat.com/worlds/udon/players/player-forces
- https://creators.vrchat.com/worlds/udon/players/player-positions
- https://creators.vrchat.com/worlds/udon/players/player-avatar-scaling
- https://creators.vrchat.com/worlds/udon/players/player-collisions
- https://creators.vrchat.com/worlds/udon/event-execution-order
- https://creators.vrchat.com/worlds/udon/debugging-udon-projects
- https://creators.vrchat.com/worlds/udon/data-containers/
- https://creators.vrchat.com/worlds/udon/data-containers/data-dictionaries/
- https://creators.vrchat.com/worlds/udon/data-containers/data-lists/
- https://creators.vrchat.com/worlds/udon/data-containers/data-tokens/
- https://creators.vrchat.com/worlds/udon/data-containers/vrcjson
- https://creators.vrchat.com/worlds/udon/data-containers/byte-and-bit-operations/
- https://creators.vrchat.com/worlds/udon/external-urls
- https://creators.vrchat.com/worlds/udon/image-loading
- https://creators.vrchat.com/worlds/udon/input-events
- https://creators.vrchat.com/worlds/udon/persistence/
- https://creators.vrchat.com/worlds/udon/persistence/player-data
- https://creators.vrchat.com/worlds/udon/persistence/player-object
- https://creators.vrchat.com/worlds/udon/string-loading
- https://creators.vrchat.com/worlds/udon/ui-events
- https://creators.vrchat.com/worlds/udon/video-players/
- https://creators.vrchat.com/worlds/udon/midi/
- https://creators.vrchat.com/worlds/udon/midi/realtime-midi
- https://creators.vrchat.com/worlds/udon/midi/midi-playback
- https://creators.vrchat.com/worlds/components/
- https://creators.vrchat.com/worlds/components/vrc_scenedescriptor
- https://creators.vrchat.com/worlds/components/vrc_objectsync
- https://creators.vrchat.com/worlds/components/vrc_pickup
- https://creators.vrchat.com/worlds/components/vrc_station
- https://creators.vrchat.com/worlds/components/vrc_mirrorreflection
- https://creators.vrchat.com/worlds/components/vrc_spatialaudiosource
- https://creators.vrchat.com/worlds/components/vrc_avatarpedestal
- https://creators.vrchat.com/worlds/components/vrc_uishape
- https://creators.vrchat.com/worlds/components/vrc_portalmarker
- https://creators.vrchat.com/worlds/creating-your-first-world
- https://creators.vrchat.com/worlds/sdk-prefabs
- https://creators.vrchat.com/worlds/supported-assets
- https://creators.vrchat.com/worlds/submitting-a-world-to-be-made-public
- https://creators.vrchat.com/worlds/clientsim/
- https://creators.vrchat.com/worlds/items
