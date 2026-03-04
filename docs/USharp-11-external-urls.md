# String Loading & Image Loading

**Sources:**
- https://creators.vrchat.com/worlds/udon/string-loading
- https://creators.vrchat.com/worlds/udon/image-loading

---

## String Loading

### Overview

`VRCStringDownloader` downloads the contents of a URL as a plain string.
This is useful for loading remote config, scoreboards, news text, or any JSON data.

**Rate limit:** ~1 request per 5 seconds per instance.
**Size limit:** 100 MB per download, but keep it reasonable for user experience.
**CORS:** The URL must allow requests from VRChat's origin.

### Usage

```csharp
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.StringLoading;
using VRC.Udon.Common.Interfaces;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class StringLoader : UdonSharpBehaviour
{
    [SerializeField] private VRCUrl url;
    [SerializeField] private UnityEngine.UI.Text displayText;

    void Start()
    {
        VRCStringDownloader.LoadUrl(url, (IUdonEventReceiver)this);
    }

    public override void OnStringLoadSuccess(IVRCStringDownload result)
    {
        displayText.text = result.Result;
    }

    public override void OnStringLoadError(IVRCStringDownload result)
    {
        Debug.LogError($"String load failed: {result.Error} (HTTP {result.ErrorCode})");
    }
}
```

### `IVRCStringDownload` Properties
```csharp
string Result       // the downloaded string content (empty on error)
string Error        // error message (empty on success)
int    ErrorCode    // HTTP status code on failure
VRCUrl Url          // the URL that was requested
```

### Notes
- `VRCUrl` objects can only be created in the Unity editor, not at runtime.
  If you need dynamic URLs, use a `VRCUrlInputField` component for user input.
- Use `VRCJson.TryDeserializeFromJson` to parse the result as JSON (see docs/05-data-containers.md).

---

## Image Loading

### Overview

`VRCImageDownloader` downloads an image from a URL and applies it to a `Material` or `Texture`.
Images must be `.png` or `.jpg`.

**Rate limit:** ~1 request per 5 seconds per instance.
**Size limit:** 2048×2048 px maximum recommended; larger may fail.

### Basic Usage

```csharp
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Image;
using VRC.Udon.Common.Interfaces;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class ImageLoader : UdonSharpBehaviour
{
    [SerializeField] private VRCUrl imageUrl;
    [SerializeField] private Renderer targetRenderer;

    private VRCImageDownloader _downloader;

    void Start()
    {
        _downloader = new VRCImageDownloader();
        _downloader.DownloadImage(imageUrl, targetRenderer.material, (IUdonEventReceiver)this);
    }

    public override void OnImageLoadSuccess(IVRCImageDownload result)
    {
        Debug.Log($"Image loaded: {result.SizeInMemoryBytes} bytes");
        targetRenderer.material = result.Material;
    }

    public override void OnImageLoadError(IVRCImageDownload result)
    {
        Debug.LogError($"Image load failed: {result.Error} (HTTP {result.ErrorCode})");
    }
}
```

### Apply to a RawImage (UI)

```csharp
[SerializeField] private UnityEngine.UI.RawImage rawImage;
private VRCImageDownloader _downloader;

void Start()
{
    _downloader = new VRCImageDownloader();
    // Pass null for material to get just the Texture2D
    _downloader.DownloadImage(imageUrl, null, (IUdonEventReceiver)this);
}

public override void OnImageLoadSuccess(IVRCImageDownload result)
{
    rawImage.texture = result.Result; // Texture2D
}
```

### `IVRCImageDownload` Properties
```csharp
Texture2D Result          // loaded texture (null on error)
Material  Material        // material updated with the texture (or null)
string    Error           // error message
int       ErrorCode       // HTTP status code on failure
VRCUrl    Url             // requested URL
int       SizeInMemoryBytes
```

### Notes
- Keep a reference to the `VRCImageDownloader` instance; it will be GC'd if you don't.
- The same downloader can download multiple images sequentially.
- Dispose the downloader with `_downloader.Dispose()` when it's no longer needed to free memory.

---

## URL Allowlisting

Both string and image loading require the URL domain to be added to the **URL Allow List** in your
VRChat world's scene descriptor, or the domain must be in VRChat's global allow list.

To add a domain:
1. Select your `VRCSceneDescriptor` in the scene.
2. In the Inspector, add the domain to "URL Allow List" (e.g., `example.com`).
