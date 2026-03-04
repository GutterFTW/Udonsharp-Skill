# UI Events

**Source:** https://creators.vrchat.com/worlds/udon/ui-events

---

## Overview

Unity UI components (Canvas-based) can have their events (OnClick, OnValueChanged, etc.) point directly
to fields and methods on various component types — **no UdonBehaviour wrapper needed** for simple wiring.

---

## Allowed Event Targets

The following components and their public methods can be directly targeted in the Unity Inspector  
from UI events (`OnClick`, `OnValueChanged`, `OnEndEdit`, etc.):

### Core Unity
- **`GameObject`** — `SetActive(bool)`
- **`AudioSource`** — play/stop/pause/volume/clip/etc.
- **`Animator`** — `SetBool`, `SetFloat`, `SetInteger`, `SetTrigger`, `Play`, `enabled`, etc.
- **`ParticleSystem`** — `Play`, `Stop`, `Pause`, `Emit`, etc.
- **`Light`** — `enabled`, `intensity`, `range`, `color`, etc.
- **`MeshRenderer`** — `enabled`, `material`, etc.
- **`SkinnedMeshRenderer`** — `enabled`, etc.
- **`LineRenderer`** — `enabled`, etc.
- **`TrailRenderer`** — `enabled`, `time`, etc.
- **`Camera`** — `enabled`, etc.
- **`Rigidbody`** — `isKinematic`, `useGravity`, etc.

### Unity UI Components
- **`Button`** — `interactable`
- **`Slider`** — `value`, `minValue`, `maxValue`, `interactable`
- **`Toggle`** — `isOn`, `interactable`
- **`Dropdown`** — `value`, `interactable`
- **`InputField`** — `text`, `interactable`
- **`Scrollbar`** — `value`, `size`, `numberOfSteps`
- **`ScrollRect`** — `horizontalNormalizedPosition`, `verticalNormalizedPosition`
- **`Text`** — `text`, `fontSize`, `color`
- **`Image`** — `enabled`, `color`, `fillAmount`, `sprite`
- **`RawImage`** — `enabled`, `color`, `texture`, `uvRect`
- **`CanvasGroup`** — `alpha`, `interactable`, `blocksRaycasts`
- **`Canvas`** — `enabled`

### VRC-Specific
- **`UdonBehaviour`** — `SendCustomEvent(string)`, `RunProgram`, `Interact`
- **`VRC_Pickup`** — `pickupable`, `proximity`
- **`VRCStation`** — `PlayerMobility`, `canUseStationFromStation`, `disableStationExit`
- **`VRCAvatarPedestal`** — `ChangeAvatarsOnUse`

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
