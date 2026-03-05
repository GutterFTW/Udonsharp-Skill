# UI Events

**Source:** https://creators.vrchat.com/worlds/udon/ui-events

---

## Overview

Unity UI components (Canvas-based) can have their events (OnClick, OnValueChanged, etc.) point directly
to fields and methods on various component types — **no UdonBehaviour wrapper needed** for simple wiring.

---

## Allowed Event Targets

The following components and their properties/methods can be directly targeted in the Unity Inspector
from UI events (`OnClick`, `OnValueChanged`, `OnEndEdit`, etc.). This is the complete official list.

### Scene Components
- **`GameObject`** — `SetActive`
- **`Collider`** — `enabled`, `isTrigger`
- **`Light`** — `Reset`, `bounceIntensity`, `colorTemperature`, `cookie`, `enabled`, `intensity`, `range`, `shadowBias`, `shadowNearPlane`, `shadowNormalBias`, `shadowStrength`, `spotAngle`
- **`MeshRenderer`** — `shadowCastingMode`, `enabled`, `probeAnchor`, `receiveShadows`, `lightProbeUsage`
- **`SkinnedMeshRenderer`** — `allowOcclusionWhenDynamic`, `shadowCastingMode`, `enabled`, and more
- **`LineRenderer`** — `enabled`, `endWidth`, `startWidth`, `loop`, `useWorldSpace`, `widthMultiplier`, and more
- **`TrailRenderer`** — `Clear`, `enabled`, `emitting`, `endWidth`, `startWidth`, `widthMultiplier`, and more
- **`Projector`** — `aspectRatio`, `enabled`, `nearClipPlane`, `farClipPlane`, `fieldOfView`, `orthographic`, `orthographicSize`

### Audio Components
- **`AudioSource`** — `Pause`, `Play`, `PlayDelayed`, `PlayOneShot`, `Stop`, `UnPause`, `bypassEffects`, `dopplerLevel`, `enabled`, `loop`, `maxDistance`, `minDistance`, `mute`, `pitch`, `playOnAwake`, `priority`, `spatialize`, `spread`, `time`, `volume`, and more
- **`AudioDistortionFilter`** — `decayRatio`, `delay`, `dryMix`, `enabled`, `wetMix`
- **`AudioEchoFilter`** — `decayRatio`, `delay`, `dryMix`, `enabled`, `wetMix`
- **`AudioHighPassFilter`** — `cutoffFrequency`, `enabled`, `highpassResonanceQ`
- **`AudioLowPassFilter`** — `cutoffFrequency`, `enabled`, `lowpassResonanceQ`
- **`AudioReverbFilter`** — `decayHFRatio`, `decayTime`, `density`, `diffusion`, `dryLevel`, `enabled`, `hfReference`, `reflectionsDelay`, `reflectionsLevel`, `reverbDelay`, `reverbLevel`, `room`, `roomHF`, `roomLF`
- **`AudioReverbZone`** — `decayHFRatio`, `decayTime`, `density`, `diffusion`, `enabled`, `HFReference`, `LFReference`, `maxDistance`, `minDistance`, `reflections`, `reflectionsDelay`, `room`, `roomHF`, `roomLF`

### Animation & Particles
- **`Animator`** — `Play`, `PlayInFixedTime`, `Rebind`, `SetBool`, `SetFloat`, `SetInteger`, `SetTrigger`, `ResetTrigger`, `speed`
- **`ParticleSystem`** — `Clear`, `Emit`, `Pause`, `Play`, `Simulate`, `Stop`, `TriggerSubEmitter`, `time`, `useAutoRandomSeed`
- **`ParticleSystemForceField`** — `endRange`, `gravityFocus`, `length`, `multiplyDragByParticleSize`, `multiplyDragByParticleVelocity`, `startRange`

### Unity UI Components
- **`Button`** — `enabled`, `interactable`, `targetGraphic`
- **`Dropdown`** — `captionText`, `enabled`, `interactable`, `itemText`, `targetGraphic`, `template`, `value`
- **`Image`** — `alphaHitTestMinimumThreshold`, `enabled`, `fillAmount`, `fillCenter`, `fillClockwise`, `fillOrigin`, `maskable`, `preserveAspect`, `raycastTarget`, `useSpriteMesh`
- **`InputField`** — `ForceLabelUpdate`, `caretBlinkRate`, `caretPosition`, `caretWidth`, `characterLimit`, `customCaretColor`, `enabled`, `interactable`, `readOnly`, `selectionAnchorPosition`, `text`, `textComponent`, `selectionFocusPosition` *(max 16,000 characters)*
- **`Mask`** — `enabled`, `showMaskGraphic`
- **`RawImage`** — `enabled`, `maskable`, `raycastTarget`
- **`RectMask2D`** — `enabled`
- **`Scrollbar`** — `enabled`, `handleRect`, `interactable`, `numberOfSteps`, `size`, `targetGraphic`, `value`
- **`ScrollRect`** — `content`, `decelerationRate`, `elasticity`, `enabled`, `horizontal`, `horizontalNormalizedPosition`, `horizontalScrollbar`, `horizontalScrollbarSpacing`, `inertia`, `scrollSensitivity`, `vertical`, `verticalNormalizedPosition`, `verticalScrollbar`, `verticalScrollbarSpacing`, `viewport`
- **`Selectable`** — `enabled`, `interactable`, `targetGraphic`
- **`Slider`** — `enabled`, `fillRect`, `handleRect`, `interactable`, `maxValue`, `minValue`, `normalizedValue`, `targetGraphic`, `value`, `wholeNumbers`
- **`Text`** — `alignByGeometry`, `enabled`, `fontSize`, `lineSpacing`, `maskable`, `raycastTarget`, `resizeTextForBestFit`, `resizeTextMaxSize`, `resizeTextMinSize`, `supportRichText`, `text`
- **`Toggle`** — `enabled`, `group`, `interactable`, `isOn`, `targetGraphic`
- **`ToggleGroup`** — `allowSwitchOff`, `enabled`

### VRC / Udon
- **`UdonBehaviour`** — `RunProgram`, `SendCustomEvent`, `Interact`

---

## Wiring UI to UdonSharp

### Clicking a Button to call Udon method:
1. Add `UdonBehaviour` component to any GameObject (containing your U# script).
2. On the Button's `OnClick` event:
   - Drag the GameObject with UdonBehaviour as target.
   - Select `UdonBehaviour → SendCustomEvent (string)`.
   - Type the method name as the string value.

> ⚠️ Method name must be `public` and **not** start with `_`.

### Wiring a Slider value to Udon:
```csharp
public class VolumeController : UdonSharpBehaviour
{
    [SerializeField] private AudioSource audioSource;

    public void OnVolumeChanged()
    {
        // Called from Slider.OnValueChanged → UdonBehaviour.SendCustomEvent("OnVolumeChanged")
        // Then read the slider value separately via SerializeField
    }
}
```

Or use `SetProgramVariable` from a Slider:
1. Slider `OnValueChanged` → `UdonBehaviour.SetProgramVariable`
2. Set symbol name = `"volume"`, value = `[Slider value]`

### TextMeshPro Notes

`TextMeshPro` and `TextMeshProUGUI` are not in the allowed whitelist but can still be targeted
after adding them to AllowedUIComponents in your VRC project settings (if supported).
For general use, drive Text via UdonBehaviour.

---

## Example: Toggle and Text

```csharp
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class UIManager : UdonSharpBehaviour
{
    [SerializeField] private UnityEngine.UI.Toggle muteToggle;
    [SerializeField] private UnityEngine.UI.Text statusText;
    [SerializeField] private AudioSource music;

    // Called by Toggle.OnValueChanged → UdonBehaviour.SendCustomEvent("OnMuteToggle")
    public void OnMuteToggle()
    {
        bool muted = muteToggle.isOn;
        music.mute = muted;
        statusText.text = muted ? "Muted" : "Playing";
    }
}
```
