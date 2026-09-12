# Foxy Overlay

[![CI](https://github.com/alik-r/foxy-overlay/actions/workflows/ci.yml/badge.svg)](https://github.com/alik-r/foxy-overlay/actions/workflows/ci.yml)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Platform](https://img.shields.io/badge/platform-Windows-0078D4)](#install)

A desktop port of the [Foxy Jumpscare Terraria mod](https://steamcommunity.com/workshop/filedetails/?id=3525193051):
every second, it rolls a one-in-ten-thousand chance, and when the dice come up Foxy
he lunges out of your screen — transparently, over whatever you happen to be doing.

Then it goes back to waiting. It waits for hours.

![Foxy lunging over a desktop](docs/demo.gif)

---

## Install

Grab a zip from [Releases](https://github.com/alik-r/foxy-overlay/releases), unzip it
somewhere permanent, and run `FoxyOverlay.exe`.

- **`...-win-x64-selfcontained.zip`** — no prerequisites, larger download.
- **`...-win-x64.zip`** — smaller, needs the [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0).

There is no installer and no visible window. The app sits in the notification area;
right-click its icon for **Settings**, **Trigger now**, **Pause** and **Exit**.
"Start with Windows" in settings writes a single `HKCU` Run-key entry — no admin
rights, no service, nothing else touched.

---

## The interesting part: WPF cannot play a transparent video

The obvious implementation is a borderless `AllowsTransparency="True"` window with a
`MediaElement` in it. That does not work, and it is worth explaining why, because it is
what stalled the first version of this project.

Setting `AllowsTransparency="True"` turns the window into a **layered** window, and WPF
drops layered windows to **software rendering**. `MediaElement` is backed by Direct3D
and the Enhanced Video Renderer, so on a software-rendered surface it composites
nothing at all — you get a black rectangle, or an empty one. No amount of chroma-key
shader work fixes this, because the video never reaches the compositor in the first place.

So this app does not play a video. **It plays an image sequence.**

```
                     BUILD TIME (scripts/bake.sh, once)
  foxy.mp4  ──ffmpeg chromakey + despill──▶  frame_000..024.png (RGBA)  +  audio.wav
  green screen, 946x720, 25 frames                    committed to assets/foxy/

                     RUNTIME (once, at startup)
  25 PNGs  ──WPF PNG decoder──▶  25 raw Pbgra32 byte[]  (64 MB, ~490 ms)

                     RUNTIME (per jumpscare)
  CompositionTarget.Rendering ──▶ WriteableBitmap.WritePixels ──▶ layered click-through
       (indexed by wall clock)      one shared surface            window, per monitor
```

Chroma keying happens **once, offline**, not on every frame of every playback. At
runtime the app only blits pre-multiplied BGRA into a `WriteableBitmap`, which software
rendering composites perfectly. Audio is a separate `SoundPlayer` on the extracted WAV,
so it never touches the video path.

A few details that turned out to matter:

- **One shared `WriteableBitmap`, not 25 `BitmapSource`s.** WPF keeps a render-side copy
  of every distinct `ImageSource` it draws. Holding the frames as bitmaps cost **598 MB**
  for a one-second clip; holding them as `byte[]` and blitting into a single surface
  costs **~285 MB**, and every monitor draws the same surface.
- **The overlay windows are created once and reused.** Constructing a layered WPF window
  costs ~600 ms, which was the entire delay between the dice landing and Foxy appearing.
  Built at startup and merely shown on trigger, that drops to **6–34 ms**.
- **Frames are indexed by wall clock, not by counting renders.** If the machine is busy
  the clip drops a frame and still ends on time, staying in sync with the audio instead
  of drifting behind it.
- **Both WPF and Win32 are told where the window goes.** WPF's `Left/Top/Width/Height`
  stay `NaN` if you only call `SetWindowPos`, so its layout pass re-sizes the window to
  the content's natural size and undoes the positioning.

---

## Measured behaviour

Measured on the development machine (1920×1200 at 150% scaling, .NET 8.0.11), from the
app's own log. Your numbers will differ; the point is the shape.

| | Before | After |
| --- | ---: | ---: |
| Time from trigger to first frame on screen | ~600 ms | **6–34 ms** |
| Steady-state memory | 598 MB | **~285 MB** |
| Memory drift over 34 consecutive jumpscares | — | **none** (flat ±6 MB) |
| Frames drawn per playback | — | **24–25 of 25** |
| Playback vs. nominal clip length (1043 ms) | — | **1043–1080 ms** |
| Teardown | — | **~4 ms** |
| Pack decode, once at startup | — | **~490 ms** (64 MB of pixels) |

The app logs a line like this after every jumpscare, so these are checkable rather than
claimed:

```
INFO: jumpscare finished: 25 frames, first frame on screen in 13ms,
      clip 1054ms (nominal 1043ms), teardown 3ms, 25/25 frames drawn
```

**The 64 MB of decoded frames is a deliberate trade.** Decoding on trigger instead would
save that memory and cost ~490 ms before Foxy appears, which defeats the entire purpose
of a jumpscare.

---

## Configuration

Settings live in `%AppData%\FoxyOverlay\config.json` and are editable from the tray.
Out-of-range values are clamped on load and the correction is logged, so a hand-edited
file can never stop the app from starting.

| Setting | Default | Meaning |
| --- | --- | --- |
| `ChanceX` | `10000` | One-in-`ChanceX` odds, rolled once per second. Min 2, max 100,000,000. |
| `CooldownSeconds` | `3` | Quiet period after a scare before rolling resumes. |
| `PackPath` | `null` | A baked pack folder, or a green-screen video to bake. `null` uses the bundled Foxy. |
| `IsMuted` | `false` | Skip the audio track. |
| `Enabled` | `true` | Master switch. |
| `AllMonitors` | `true` | Show on every monitor, or just the primary. |
| `HeightFraction` | `0.85` | Overlay height as a fraction of screen height. |
| `StartWithWindows` | `false` | Mirrors the `HKCU` Run-key entry. |

At the default odds a jumpscare is 99% likely within about **12 hours 47 minutes** of
uptime, and averages one every ~2 hours 47 minutes. The settings window works this out
for whatever odds you type.

Also in `%AppData%\FoxyOverlay\`:

- `app.log` — rotates at 1 MB, keeps 3 archives.
- `stats.json` — lifetime scare and roll counts, flushed once a minute.
- `packs/` — packs baked from your own videos, cached by source path and timestamp.

---

## Using your own video

The source needs a **solid green background** (the bundled clip keys `#00FE00`).

**From the tray:** Settings → Video → Browse, pick a video, Save. It is baked on first
use into `%AppData%\FoxyOverlay\packs\` — this needs `ffmpeg` on `PATH`. The settings
window tells you if it cannot find one.

**Ahead of time**, which needs no ffmpeg on the target machine:

```bash
scripts/bake.sh my-clip.mp4 ./my-pack            # defaults: chromakey 0x00FE00:0.12:0.06
scripts/bake.sh my-clip.mp4 ./my-pack 0x00FF00 0.18 0.08   # key, similarity, blend
```

Then point `PackPath` at `./my-pack`. A pack is just a folder:

```
my-pack/
  pack.json          width, height, frameCount, frameRate, framePattern, audioFile
  frame_000.png ...  RGBA frames, keyed
  audio.wav          16-bit PCM (SoundPlayer accepts nothing else)
```

`PackLoader` validates the whole thing up front — every declared frame must exist —
so a broken pack fails at startup rather than halfway through a scare.

---

## Building

**You do not need Windows to build this**, only to run it. `EnableWindowsTargeting` in
`Directory.Build.props` lets the `net8.0-windows` WPF project restore and compile on
Linux and macOS; the XAML markup compiler is managed code and runs anywhere. Only the
final `.exe` apphost is platform-specific.

```bash
dotnet build                    # everything, including the WPF app, on any OS
dotnet test                     # Core and Services suites (no display needed)
dotnet publish FoxyOverlay.Media/FoxyOverlay.Media.csproj \
  -c Release -r win-x64 --self-contained false -o out    # produces FoxyOverlay.exe
```

CI builds and tests on both `ubuntu-latest` and `windows-latest` for exactly this reason.
See [docs/BUILDING.md](docs/BUILDING.md) for developing from WSL against a Windows host.

---

## Project layout

```
FoxyOverlay.Core/         net8.0     config, logging, stats, pack loading and baking,
                                     probability and overlay geometry — no UI, no Windows
FoxyOverlay.Services/     net8.0     JumpscareService: the once-a-second roll loop
FoxyOverlay.Media/        net8.0-win WPF tray app, layered overlay window, frame player
assets/source/foxy.mp4               the original green-screen clip
assets/foxy/                         the baked pack, shipped with the app
scripts/bake.sh                      the asset pipeline
```

Everything that can be tested without a screen deliberately lives outside the WPF
project — including the overlay's placement maths, which is a pure function in
`Core/OverlayGeometry.cs`.

---

## What was broken before

This started as an unfinished project. For the record, and because the fixes are the
substance of the repository:

| Symptom | Cause |
| --- | --- |
| Green rectangle, or nothing at all | Chroma key was never implemented, and `MediaElement` cannot render on a layered window regardless |
| Jumpscare fired exactly once per launch | The service disposed its own timer on trigger and waited for the UI to call back; any failure in the handler wedged it at `Playing` forever |
| Hung on the very first trigger | Default `VideoPath` was `""`, so `new Uri("")` threw inside the playback thread and the `TaskCompletionSource` was never completed — the caller awaited it forever |
| A thread leaked per jumpscare | Each playback span a new STA thread running `Dispatcher.Run()` that was never shut down on the failure paths |
| Closing the settings window killed the app | The settings window *was* the app — `StartupUri` with the default shutdown mode |
| `NullReferenceException` on replay | `OnClosing` assigned `null` to the XAML-generated `mediaElement` field |
| Crash on shutdown before start | `StopAsync` dereferenced a timer that `StartAsync` had never created |
| Two tests asserted the opposite of what they set up | They passed `() => false` as the trigger predicate, then asserted that a trigger happened |

---

## Known limitations

- **Windows only.** Layered click-through windows and the tray icon are Win32.
- **Not visible over exclusive-fullscreen games.** A topmost layered window cannot draw
  over an exclusive-fullscreen swap chain. Borderless-windowed mode works fine.
- **~285 MB resident** while idle, most of it decoded frames. See the trade above.
- **Baking your own video needs ffmpeg**; the bundled pack does not.

---

## Licence

MIT. The Foxy character and the source clip belong to their respective owners; this
repository is a port of a joke mod and claims nothing over them.
