# Input Events

**Source:** https://creators.vrchat.com/worlds/udon/input-events

---

## Overview

Input events fire when the player presses/releases a button or moves an axis.
They report a normalized, cross-platform input — the same event fires whether the player is on 
Desktop or VR.

> ⚠️ Input is NOT detected while VRChat menus are open.
> All held inputs are **released** when a menu opens, and **re-pressed** when it closes.

---

## Button Events (bool value)

Override these in an `UdonSharpBehaviour`:

```csharp
public override void InputJump(bool value, UdonInputEventArgs args)
{
    if (value) Debug.Log("Jump pressed");
    else       Debug.Log("Jump released");
}

public override void InputUse(bool value, UdonInputEventArgs args) { }
public override void InputGrab(bool value, UdonInputEventArgs args) { }
public override void InputDrop(bool value, UdonInputEventArgs args) { }
```

| Event | Desktop | VR |
|---|---|---|
| `InputJump` | Spacebar | Typically face button |
| `InputUse` | Left click / E | Trigger |
| `InputGrab` | Left click | Grip |
| `InputDrop` | Right click | Grip release / button |

---

## Axis Events (float value)

```csharp
public override void InputMoveHorizontal(float value, UdonInputEventArgs args) { }
public override void InputMoveVertical(float value, UdonInputEventArgs args) { }
public override void InputLookHorizontal(float value, UdonInputEventArgs args) { }
public override void InputLookVertical(float value, UdonInputEventArgs args) { }
```

| Event | Desktop | VR |
|---|---|---|
| `InputMoveHorizontal` | A/D | Left stick X |
| `InputMoveVertical` | W/S | Left stick Y |
| `InputLookHorizontal` | Mouse X | Right stick X |
| `InputLookVertical` | Mouse Y | Right stick Y |

Axis values are normalized to the range `[-1, 1]`.

---

## UdonInputEventArgs

Both button and axis events receive an `UdonInputEventArgs` argument with additional fields:

```csharp
public override void InputJump(bool value, UdonInputEventArgs args)
{
    bool isHeld       = args.floatValue > 0.5f;     // pressure/amount
    bool isVR         = args.handType != HandType.None;
    HandType hand     = args.handType;              // HandType.Left / .Right / .None
    UdonInputEventType ev = args.eventType;         // the event type enum
}
```

### `HandType` Enum
```csharp
HandType.None  // Desktop
HandType.Left  // VR left hand
HandType.Right // VR right hand
```

---

## Input Method Changed

Fires when the player switches between Desktop and VR modes or between controller types:

```csharp
public override void OnInputMethodChanged(VRCInputMethod inputMethod)
{
    switch(inputMethod)
    {
        case VRCInputMethod.Keyboard:    Debug.Log("Desktop keyboard"); break;
        case VRCInputMethod.Mouse:       Debug.Log("Desktop mouse"); break;
        case VRCInputMethod.Controller:  Debug.Log("VR or gamepad controller"); break;
        case VRCInputMethod.Gaze:        Debug.Log("Gaze input"); break;
    }
}
```

---

## Unity Input Methods

Standard Unity input methods still work in Udon:

```csharp
// Keyboard  
bool spaceDown = Input.GetKeyDown(KeyCode.Space);

// Axes
float h = Input.GetAxis("Horizontal");
float v = Input.GetAxis("Vertical");

// Mouse
Vector3 mousePos = Input.mousePosition;
```

> These catch all input, **including while VRChat menus are open**. Prefer the Udon input events above
> when you want menu-safe behavior.
