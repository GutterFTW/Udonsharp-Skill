# VRC Graphics

**Sources:**
- https://creators.vrchat.com/worlds/udon/vrc-graphics/
- https://creators.vrchat.com/worlds/udon/vrc-graphics/asyncgpureadback
- https://creators.vrchat.com/worlds/udon/vrc-graphics/vrc-camera-settings
- https://creators.vrchat.com/worlds/udon/vrc-graphics/vrc-quality-settings
- https://creators.vrchat.com/worlds/udon/vrc-graphics/vrchat-shader-globals

---

## VRCGraphics

Udon exposes a subset of Unity's `Graphics` class via `VRCGraphics`, and shader utilities via `VRCShader`.

### VRCGraphics.Blit()

Copies a source texture into a destination `RenderTexture` with a shader (null destination not allowed).
See [Graphics.Blit](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Graphics.Blit.html).

> **Quest note:** `VRCGraphics.Blit` requires either `ZTest Always` in the shader **or** depth disabled
> on the target `RenderTexture`, otherwise it silently fails.

### VRCGraphics.DrawMeshInstanced()

Draws the same mesh multiple times using GPU instancing.
See [Graphics.DrawMeshInstanced](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Graphics.DrawMeshInstanced.html).

### VRCShader.PropertyToID()

Returns a shader property ID from a name. Call once at startup, cache the result — IDs are stable.

> **Restriction:** The property name must be prefixed with `_Udon`, or be the literal string `_AudioTexture`,
> to be usable with `VRCShader.SetGlobal*`. The ID is still returned regardless.

### VRCShader.SetGlobal*()

Sets a global shader property visible to **all shaders in the world, including avatar shaders**.

Available variants:
```csharp
VRCShader.SetGlobalColor(int id, Color value)
VRCShader.SetGlobalFloat(int id, float value)
VRCShader.SetGlobalFloatArray(int id, float[] value)
VRCShader.SetGlobalInteger(int id, int value)   // Note: actually stored as float (Unity bug)
VRCShader.SetGlobalMatrix(int id, Matrix4x4 value)
VRCShader.SetGlobalMatrixArray(int id, Matrix4x4[] value)
VRCShader.SetGlobalTexture(int id, Texture value)
VRCShader.SetGlobalVector(int id, Vector4 value)
VRCShader.SetGlobalVectorArray(int id, Vector4[] value)
```

Usage pattern:
```csharp
private int _propId;

void Start()
{
    _propId = VRCShader.PropertyToID("_Udon_MyProperty");
}

void Update()
{
    VRCShader.SetGlobalFloat(_propId, Mathf.Sin(Time.time));
}
```

---

## AsyncGPUReadback

Reads pixel/texture data from the GPU to the CPU asynchronously (does not stall the main thread).

### Differences from Unity's API

- Use `VRCAsyncGPUReadback` instead of `AsyncGPUReadback`.
- Instead of providing an `Action<>` callback, pass `(IUdonEventReceiver)this`. The behaviour receives `OnAsyncGpuReadbackComplete`.
- Use `TryGetData()` instead of `GetData()` on the completed request.

### Usage

```csharp
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Rendering;
using VRC.Udon.Common.Interfaces;

public class AGPURB : UdonSharpBehaviour
{
    public Texture texture;

    void Start()
    {
        VRCAsyncGPUReadback.Request(texture, 0, (IUdonEventReceiver)this);
    }

    public void OnAsyncGpuReadbackComplete(VRCAsyncGPUReadbackRequest request)
    {
        if (request.hasError)
        {
            Debug.LogError("GPU readback error!");
            return;
        }
        var px = new Color32[texture.width * texture.height];
        Debug.Log("GPU readback success: " + request.TryGetData(px));
        Debug.Log("GPU readback data: " + px[0]);
    }
}
```

---

## VRCCameraSettings

Provides access to and limited control over the local user's screen camera and photo camera.

### Static Accessors
```csharp
VRCCameraSettings.ScreenCamera   // user's main view camera (stereo in VR)
VRCCameraSettings.PhotoCamera    // handheld photo/video camera (null in ClientSim)
```

> `PhotoCamera` properties only update when the camera is active. Check the `Active` property first.  
> `PhotoCamera` is never null in the real VRChat client.

### Read-Only Properties
```csharp
Vector3    Position          // world-space position (all 0 if Active == false)
Quaternion Rotation          // world-space rotation
Vector3    Forward, Up, Right // convenience world-space vectors
int        PixelWidth, PixelHeight  // render target size in pixels
float      Aspect            // aspect ratio
float      FieldOfView       // vertical FOV (may be inaccurate for VR)
bool       Active            // true if camera is rendering; always true for ScreenCamera
bool       StereoEnabled     // true for ScreenCamera when user is in VR
```

### Read-Write Properties
```csharp
float          NearClipPlane         // clamped 0.001–0.05; user "Forced Camera Near Distance" may override
float          FarClipPlane          // min NearClipPlane + 0.1
bool           AllowHDR
DepthTextureMode DepthTextureMode    // enable depth texture for shader effects
bool           UseOcclusionCulling   // default true; requires baked occlusion data
bool           AllowMSAA             // default true; false disables MSAA regardless of user settings
LayerMask      CullingMask           // ScreenCamera only; reserved/MirrorReflection/InternalUI cannot change
CameraClearFlags ClearFlags
Color          BackgroundColor       // used when ClearFlags == SolidColor
float[]        LayerCullDistances    // must have 32 entries; 0 = use FarClipPlane
// LayerCullSpherical is exposed but disabled (no-op) due to UI culling issues
```

### Camera Mode
```
ScreenCamera modes:
  Screen       — default rendering
  FocusView    — active in Focus View (mobile)

PhotoCamera modes:
  PhotoOrVideo — Photo or Stream mode (includes Emoji, Stickers)
  Print        — "Prints" skin active
  DroneHandheld — Drone mode
  DroneFPV     — Drone FPV mode
  Unknown      — Active == false
```

### Static Functions
```csharp
// VR eye positions (equivalent to ScreenCamera.Position for non-VR)
Vector3    VRCCameraSettings.GetEyePosition(Camera.StereoscopicEye eye)
Quaternion VRCCameraSettings.GetEyeRotation(Camera.StereoscopicEye eye)

// Replacement for Camera.current (only populated during rendering events)
void VRCCameraSettings.GetCurrentCamera(
    out VRCCameraSettings internalComponent,
    out Camera externalComponent)
// internalComponent = ScreenCamera or PhotoCamera when a known internal cam is rendering
// externalComponent = the Camera component when a world camera is rendering
// Both null when Camera.current is null OR when an avatar camera is rendering
// WARNING: may return null,null during VRChat's internal render steps — always handle this!
```

### Event
```csharp
public override void OnVRCCameraSettingsChanged() { }
// Fires when the user changes camera-related graphics settings (e.g. Near Clip Override)
// May fire every frame or multiple times per frame — keep processing minimal
```

---

## VRCQualitySettings

A thin read-only wrapper over `UnityEngine.QualitySettings`.

### Exposed Properties (read-only)
```csharp
int              AntiAliasing
int              PixelLightCount
float            LODBias
int              MaximumLODLevel
ShadowResolution ShadowResolution
float            ShadowDistance
int              ShadowCascades
int              vSyncCount
```

### Shadow Distance Override (read-write)
```csharp
// Override shadow distance per quality tier
VRCQualitySettings.SetShadowDistance(float low, float medium, float high, float mobile)
// Or single value for all tiers:
VRCQualitySettings.SetShadowDistance(float value)
// Clamped 0.1–10000. Shows a warning in user graphics settings. Reset on world load.

// Remove override (restore user setting)
VRCQualitySettings.ResetShadowDistance()

// Additional shadow properties (read-write)
float   shadowCascade2Split
Vector3 shadowCascade4Split
```

### Event
```csharp
public override void OnVRCQualitySettingsChanged() { }
// Fires when user changes graphics settings that affect exposed properties
```

---

## VRChat Shader Globals

VRChat exposes special global shader variables usable in any world or avatar shader.

> **Warning:** Do not use the `_VRChat` prefix for your own shader variables — it is a protected namespace.

### Camera & Mirror Globals

| Variable | Type | Description |
|---|---|---|
| `_VRChatCameraMode` | float | 0=normal, 1=VR handheld camera, 2=desktop handheld camera, 3=screenshot |
| `_VRChatCameraMask` | uint | `cullingMask` of active camera (when CameraMode != 0) |
| `_VRChatMirrorMode` | float | 0=not in mirror, 1=mirror in VR, 2=mirror in desktop |
| `_VRChatFaceMirrorMode` | float | 1 when rendering the face mirror, 0 otherwise |
| `_VRChatMirrorCameraPos` | float3 | World-space mirror camera position (0,0,0 when not in mirror) |
| `_VRChatScreenCameraPos` | float3 | World-space main screen camera position (0,0,0 if inactive) |
| `_VRChatPhotoCameraPos` | float3 | World-space photo camera position (0,0,0 if inactive) |
| `_VRChatScreenCameraRot` | float4 | World-space rotation quaternion of screen camera |
| `_VRChatPhotoCameraRot` | float4 | World-space rotation quaternion of photo camera |

### Time Globals

All time globals use `uint` bit-patterns and should be declared as `uint` in shaders.

| Variable | Description |
|---|---|
| `_VRChatTimeUTCUnixSeconds` | Lower 32 bits of current UTC Unix timestamp (seconds). Treat as unsigned. |
| `_VRChatTimeNetworkMs` | Synchronized network time in ms (same as `Networking.GetServerTimeInMilliseconds`). May wrap. |
| `_VRChatTimeEncoded1` | Packed: bits 0-4=UTC hour, 5-10=UTC minute, 11-16=shared second, 17-21=local hour, 22-27=local minute |
| `_VRChatTimeEncoded2` | Packed: bits 0-9=shared millisecond, 10=tz-offset sign, 11-26=tz offset in seconds, 27-31=reserved |

Time values reflect the user's "preferred timezone" setting. "Current time of day" always shows the
**observer's** local time (relevant for avatar shaders).

### Time Helper Functions (HLSL)

```hlsl
#include "Packages/com.vrchat.base/ShaderLibrary/VRCTime.cginc"

uint VRC_GetUTCUnixTimeInSeconds();
uint VRC_GetNetworkTimeInMilliseconds();
void VRC_GetUTCTime(out uint hours, out uint minutes, out uint seconds, out uint milliseconds);
void VRC_GetLocalTime(out uint hours, out uint minutes, out uint seconds, out uint milliseconds);
int  VRC_GetTimezoneOffsetSeconds();
```
