# MIDI in Udon

**Sources:**
- https://creators.vrchat.com/worlds/udon/midi/
- https://creators.vrchat.com/worlds/udon/midi/realtime-midi
- https://creators.vrchat.com/worlds/udon/midi/midi-playback

---

## Overview

Two MIDI workflows are available in VRChat Udon:

1. **Realtime MIDI** — live input from a hardware MIDI device connected to the user's computer.
2. **MIDI Playback** — pre-recorded `.mid` files played back in sync with an audio clip.

---

## MIDI Events (Both Workflows)

These events fire on any UdonSharpBehaviour registered as a MIDI target:

### `MidiNoteOn`
Fired when a Note On message is received (key pressed / playback hit).

```csharp
public override void MidiNoteOn(int channel, int number, int velocity)
{
    // channel: 0-15
    // number:  0-127 (note pitch)
    // velocity: 0-127 (how hard the note was struck)
}
```

### `MidiNoteOff`
Fired when a Note Off message is received (key released / playback hit).

```csharp
public override void MidiNoteOff(int channel, int number, int velocity)
{
    // velocity is typically 0 for Note Off events
}
```

### `MidiControlChange`
Fired when a control change message is received (knob/slider moved).

```csharp
public override void MidiControlChange(int channel, int number, int value)
{
    // channel: 0-15
    // number:  0-127 (CC number)
    // value:   0-127 (encoder/knob position or increment)
}
```

---

## Realtime MIDI

### Setup

1. Add a `VRCMidiListener` component to any GameObject in your scene.
2. In the Inspector, enable the **Active Events** you want to receive (all off by default).
3. Set the **Behaviour** field to the UdonSharpBehaviour that handles the events.

> `VRCMidiHandler` is **auto-added** at runtime by VRChat — do not add it manually.

### Device Selection

**In Editor (Play Mode):**
Select your device via `VRChat SDK > Midi Utility Window`. The selection is stored per-editor.

**At Runtime (VRChat Client):**
VRChat automatically connects to the first available MIDI device.

To specify a device by name, pass a partial name as a launch argument:
```
--midi=midikeysmasher
```
Matching is case-insensitive and partial (e.g. `--midi=midikey` matches "SchneebleCo MidiKeySmasher 89").

---

## MIDI Playback

### Setup

1. Import a `.mid` file and an audio clip into your Assets folder.
2. Select the `.mid` asset — set its **AudioClip** to the matching audio file.
3. Verify or override the **BPM** on the `.mid` asset (use "Override Bpm" if the events don't line up with audio).
4. Add a `VRCMidiPlayer` component to a GameObject.
5. Assign:
   - `Midi File` — your `.mid` asset
   - `Audio Source` — AudioSource component with your audio clip
   - `Target Behaviours` — array of UdonBehaviours that receive note events

### VRCMidiPlayer Component

#### Inspector Fields

| Field | Description |
|---|---|
| `Midi File` | The `.mid` asset to play |
| `Audio Source` | AudioSource whose clip matches the MIDI data |
| `Target Behaviours` | UdonBehaviours that receive MidiNoteOn/Off events |
| `Display Debug Blocks` | Shows note blocks in Scene View when component is selected |

#### Methods

```csharp
VRCMidiPlayer midiPlayer = GetComponent<VRCMidiPlayer>();
midiPlayer.Play();   // Start playback (MIDI + AudioSource)
midiPlayer.Stop();   // Stop playback
```

#### Properties

```csharp
float time = midiPlayer.time;         // Get/set current playback position (seconds)
MidiData data = midiPlayer.midiData;  // Read all track data before playback
```

### Data Classes

#### `MidiData`
```csharp
MidiTrack[] tracks = midiData.Tracks;
byte bpm           = midiData.Bpm;
```

#### `MidiTrack`
```csharp
MidiBlock[] blocks   = track.Blocks;
byte minNote         = track.minNote;
byte maxNote         = track.maxNote;
byte minVelocity     = track.minVelocity;
byte maxVelocity     = track.maxVelocity;
```

#### `MidiBlock`
```csharp
byte  note         = block.Note;         // 0-127
byte  velocity     = block.Velocity;     // 0-127
byte  channel      = block.Channel;      // 1-16
float startTimeMs  = block.StartTimeMs;
float endTimeMs    = block.EndTimeMs;
float startTimeSec = block.StartTimeSec;
float endTimeSec   = block.EndTimeSec;
float lengthSec    = block.LengthSec;
```

### Example

Load the SDK example: `VRChat SDK > Samples > MidiPlayback`.

---

## Notes

- MIDI Playback fires `MidiNoteOn` and `MidiNoteOff` events only (not `MidiControlChange`).
- MIDI is a **local-only** feature — each player's own device/playback triggers events only on their client.
- Realtime MIDI requires the user to have a MIDI device connected to their computer.
