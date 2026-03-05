# Video Players

**Source:** https://creators.vrchat.com/worlds/udon/video-players/

---

## Overview

VRChat provides two video player components:
- `VRCAVProVideoPlayer` — AVPro-based
- `VRCUnityVideoPlayer` — Unity-based

Both can be added via SDK prefabs found at:
`Packages/com.vrchat.worlds/Samples/UdonExampleScene/Prefabs/VideoPlayers/`

### Community Prefabs
- [VideoTXL](https://github.com/vrctxl/VideoTXL)
- [ProTV](https://gitlab.com/techanon/protv)
- [USharpVideo](https://github.com/MerlinVR/USharpVideo)

---

## AVPro vs Unity Video Player

| Feature | AVPro | Unity |
|---|---|---|
| Live streams (YouTube Live, Twitch, etc.) | ✅ Yes | ❌ No |
| Editor preview (Play Mode) | ❌ No (Build & Test only) | ✅ Yes (direct `.mp4`/`.webm` only) |
| YouTube/Vimeo in editor | ❌ No | ❌ No (client only) |
| Max audio channels | 6 (5.1); no 7.1 | Standard Unity limits |
| Android/Quest support | ✅ Yes | ✅ Yes |

---

## Using the Prefabs

The SDK ships two synced video player prefabs. Both play a URL synchronized for all players in the world.

- Looping is **off by default** in the sync example to enable sync. Remove the `UdonBehaviour` and enable `Loop` on the video player if you want unsync'd loop.
- You do **not** have to use the `UdonSyncPlayer` — you can use a bare `VRC Video Player` component for non-synced usage.

---

## Rate Limiting

**Global limit: one new video URL per user per 5 seconds** (across ALL video players in the world).

This affects:
- Initial load when joining
- `LoadURL` and `PlayURL` calls

**Multiple video players playing simultaneously:** late-joiners will need to send multiple requests. Without management they'll attempt this simultaneously and fail. You must stagger late-join requests when running more than one video player.

---

## Supported Hosts

Full allowlist: https://creators.vrchat.com/worlds/udon/video-players/www-whitelist

| Host | Cost | Link format | Notes |
|---|---|---|---|
| YouTube | Free | `https://www.youtube.com/watch?v=...` | None |
| Vimeo Basic | Free | `https://vimeo.com/...` | None |
| Vimeo Pro/Business | Paid | Direct video links | None |
| Your own host | Varies | Direct `.mp4`/`.webm` link | Outside allowlist = users must enable "Allow Untrusted URLs" |

### CDN Recommendations
- Amazon CloudFront and BunnyCDN have been tested.
- CDNs are NOT on the allowlist — users need "Allow Untrusted URLs".

### Optimizing Self-Hosted Videos

Enable **"fast start"** (web-optimized) to allow streaming without downloading the full file first:
- FFMPEG: `-movflags +faststart`
- HandBrake: tick "Web Optimized"

---

## Android / Quest Compatibility

VRChat's URL resolver is available on Android — no workarounds needed. Both AVPro and Unity players work on Quest.

---

## Udon Integration

Call `PlayURL` or `LoadURL` on a `VRCUrl` to trigger playback:

```csharp
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Components.Video;  // for VRCUrl

public class VideoController : UdonSharpBehaviour
{
    public VRCUnityVideoPlayer videoPlayer;
    public VRCUrl videoUrl;

    public void PlayVideo()
    {
        videoPlayer.LoadURL(videoUrl);
    }

    public override void OnVideoReady() { videoPlayer.Play(); }
    public override void OnVideoStart() { }
    public override void OnVideoEnd() { }
    public override void OnVideoError(VideoError videoError) { }
    public override void OnVideoPlay() { }
    public override void OnVideoPause() { }
    public override void OnVideoLoop() { }
}
```

### Video Player Events

| Event | Fires when |
|---|---|
| `OnVideoReady()` | Video loaded and ready to play |
| `OnVideoStart()` | Playback started |
| `OnVideoEnd()` | Playback ended (not loop) |
| `OnVideoError(VideoError)` | Load failed |
| `OnVideoPlay()` | `Play()` called |
| `OnVideoPause()` | `Pause()` called |
| `OnVideoLoop()` | Video looped |

### VideoError Values

| Value | Meaning |
|---|---|
| `Unknown` | Unspecified error |
| `InvalidURL` | Malformed or unsupported URL |
| `AccessDenied` | Blocked by allowlist (user must enable Untrusted URLs) |
| `PlayerError` | Internal player error |
| `RateLimited` | URL sent too soon after a previous request |
