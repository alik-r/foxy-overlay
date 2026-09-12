# Building and developing

## Requirements

- **.NET 8 SDK** — any OS. `dotnet --version` should report 8.x.
- **ffmpeg + ffprobe** — only for re-baking packs, not for building or running.
- **Windows with the .NET 8 Desktop Runtime** — only to *run* the app.

## The short version

```bash
dotnet build          # builds everything, WPF app included, on Linux/macOS/Windows
dotnet test           # Core and Services suites; no display required
```

## Why the WPF project builds on Linux

`Directory.Build.props` sets `EnableWindowsTargeting=true`. That lets NuGet restore the
Windows targeting packs on any OS, and WPF's XAML markup compiler (`PresentationBuildTasks`)
is managed code that runs cross-platform. What you get is a working
`FoxyOverlay.dll` — everything but the native `.exe` launcher.

To produce a runnable `FoxyOverlay.exe` you need the win-x64 apphost pack, which
`dotnet publish -r win-x64` downloads:

```bash
dotnet publish FoxyOverlay.Media/FoxyOverlay.Media.csproj \
  -c Release -r win-x64 --self-contained false -o out
```

If you cannot fetch that pack (a locked-down network, for instance), you can still run
the portable output through the Windows `dotnet` host:

```bash
dotnet publish FoxyOverlay.Media/FoxyOverlay.Media.csproj \
  -c Release --self-contained false -p:UseAppHost=false -o out
# then, on Windows:
"C:\Program Files\dotnet\dotnet.exe" out\FoxyOverlay.dll
```

Note that the portable route does **not** apply `app.manifest`, because the manifest is
embedded in the apphost. The app therefore inherits `dotnet.exe`'s DPI awareness rather
than the `PerMonitorV2` the manifest asks for. It still works, but a real `.exe` build is
the supported configuration.

## Developing from WSL against a Windows host

This is how the project is developed: edit and build in WSL, run on Windows.

```bash
# build in WSL
dotnet publish FoxyOverlay.Media/FoxyOverlay.Media.csproj \
  -c Release -r win-x64 --self-contained false -o /tmp/foxy

# copy to the Windows side (building straight onto /mnt/c is slow)
rm -rf /mnt/c/Users/<you>/FoxyOverlay && mkdir -p /mnt/c/Users/<you>/FoxyOverlay
cp -r /tmp/foxy/. /mnt/c/Users/<you>/FoxyOverlay/

# launch it
cmd.exe /c start "" "C:\Users\<you>\FoxyOverlay\FoxyOverlay.exe"
```

The log is the fastest way to see what happened:

```bash
tail -f /mnt/c/Users/<you>/AppData/Roaming/FoxyOverlay/app.log
```

Useful settings while working on playback — small, silent, one monitor, and constant:

```json
{ "ChanceX": 2, "CooldownSeconds": 0, "IsMuted": true,
  "AllMonitors": false, "HeightFraction": 0.15 }
```

### Behind a TLS-inspecting proxy

Corporate networks that intercept TLS will break `dotnet restore` with
`NU1301: Unable to load the service index`. Point .NET at a CA bundle that includes the
proxy's root, without touching the system trust store:

```bash
echo | openssl s_client -showcerts -connect api.nuget.org:443 -servername api.nuget.org 2>/dev/null \
  | awk '/BEGIN CERTIFICATE/,/END CERTIFICATE/' > ~/.certs/proxy-chain.pem
cat /etc/ssl/certs/ca-certificates.crt ~/.certs/proxy-chain.pem > ~/.certs/ca-bundle.pem
export SSL_CERT_FILE=~/.certs/ca-bundle.pem
```

Some proxies additionally block `.nupkg` downloads outright (a `503` whose body is an
HTML "file transfer blocked" page). No client-side setting fixes that; build from a
network that permits it, or use an internal package mirror.

## Re-baking the bundled pack

```bash
./scripts/bake.sh assets/source/foxy.mp4 assets/foxy
```

CI re-bakes on every run and fails if the result's shape drifts from the committed
`pack.json`. It deliberately does not compare bytes — PNG output varies between ffmpeg
builds, and that is not a real failure.

## Diagnosing the overlay

Every run logs where the overlay actually landed, which is the fastest way to
investigate "it's on the wrong monitor" or "it's the wrong size":

```
INFO: overlay bounds: requested 867x660 at 526,270; actual 867x660 at 526,270;
      wpf 578x440 at 350.7,180; image 578x440
```

`requested` and `actual` are **physical** pixels; `wpf` is DIPs. On a 150%-scaled
display the two differ by exactly 1.5x, which is correct, not a bug. If you capture the
screen to check the overlay's position, make the capture DPI-aware
(`SetProcessDpiAwarenessContext(-4)`) — a DPI-unaware capture returns a cropped view of
a scaled desktop and will make a correctly placed overlay look misplaced.
