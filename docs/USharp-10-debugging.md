# Debugging UdonSharp Projects

**Source:** https://creators.vrchat.com/worlds/udon/debugging-udon-projects

---

## Unity Console

Use standard Unity debug calls — they appear in the Unity console and in VRChat's log:

```csharp
Debug.Log("message");
Debug.LogWarning("warning");
Debug.LogError("error");
Debug.Log($"Player {player.displayName} joined, id={player.playerId}");
```

---

## UdonSharp Runtime Exception Watcher

When an Udon exception occurs, the UdonSharp runtime exception watcher maps Udon bytecode addresses
back to **C# line numbers** in the Unity console. This means stack traces point to your `.cs` file,
not to Udon assembly.

> If the runtime exception watcher is not active, error lines may appear as raw Udon addresses.
> Ensure the UdonSharp package is installed and up-to-date.

---

## Behaviour Halting

If an UdonBehaviour throws a runtime exception, **it halts entirely** — `Update()` and all events stop.
The halted object will be silent until the scene is reset.

Search for `"halted"` in the log to find which behaviour is affected:
```
[UdonBehaviour] An exception occurred in an UdonBehaviour, the program will halt. Exception: ...
```

---

## VRChat Debug Launch Flags

To enable extra logging, launch VRChat with these flags (via Steam launch options or `.bat` file):

```
--enable-debug-gui
--enable-sdk-log-levels
--enable-udon-debug-logging
```

### Example `.bat` file:
```bat
"C:\Program Files (x86)\Steam\steamapps\common\VRChat\VRChat.exe" ^
    --enable-debug-gui ^
    --enable-sdk-log-levels ^
    --enable-udon-debug-logging
```

---

## Log File Location

VRChat logs are saved here:
```
C:\Users\<YourName>\AppData\LocalLow\VRChat\VRChat\
```

Look for files named `output_log_*.txt`.

Useful search terms in the log:
- `"[UdonBehaviour]"` — Udon behaviour events
- `"halted"` — see above
- `"Exception"` — any exceptions thrown
- `"[UdonSharp]"` — UdonSharp compiler/runtime messages

---

## In-Game Debug GUI

With `--enable-debug-gui` active, press **RightShift + ~** in VRChat to toggle the debug
GUI panel which shows FPS, network stats, avatar rendering info, and Udon debugger info.

---

## Common Compiler Errors and Fixes

| Error | Likely Cause | Fix |
|---|---|---|
| `List<T> is not supported` | Using `List<T>` | Convert to array `T[]` |
| `GetComponent<UdonBehaviour>` error | Generic get for UdonBehaviour | Use `(UdonBehaviour)GetComponent(typeof(UdonBehaviour))` |
| Field initializer error | Using runtime data in field initializer | Move to `Start()` |
| `Recursive call... needs [RecursiveMethod]` | Recursion without attribute | Add `[RecursiveMethod]` to the method |
| Variable not syncing | `[UdonSynced]` on unsupported type | Use only supported sync types (see docs/02-vrchat-api.md) |
| Property not triggered | Setting `_field` directly instead of property | Use the property, not the backing field |

---

## Debugging Across Clients

When testing networked behaviour solo in the editor, use **ClientSim** (included in VRC SDK)
to simulate multiple players in the same scene without launching VRChat.

Tips:
- ClientSim simulates `OnPlayerJoined`, `OnPlayerLeft`, and local player properties.
- Use `Networking.LocalPlayer.displayName` to differentiate simulated players.
- Network event delivery is instant in ClientSim — this is NOT how it works in VRChat.
