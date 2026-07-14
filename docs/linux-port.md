# AvalonDock on Linux (LibreWPF portable backend)

Status of the Linux leg of the cross-platform effort tracked in
[Dirkster99/AvalonDock#609](https://github.com/Dirkster99/AvalonDock/issues/609).
This branch merges lextm's macOS groundwork from the
[lextudio fork](https://github.com/lextudio/AvalonDock) (platform abstraction layer,
adorner-based splitter ghost, managed caption drag, DevFlow test harness — see
`docs/librewpf.md` for his analysis of the LibreWPF drag issues) and adapts it for Linux.

## What works on Linux

Verified headless (Xvfb + lavapipe software Vulkan) against the **public
`LibreWPF.Sdk 0.1.0-preview.14`** from nuget.org:

- all AvalonDock component projects and the sample apps build with zero warnings
- MVVMTestApp runs, renders, and embeds the LeXtudio DevFlow HTTP agent
- docking operations driven through the agent work: seeding documents, floating an
  anchorable into a real floating `Window`, layout round-trips
  (`eng/linux-smoke-test.sh` asserts this in CI)

## Building

Two SDK arrangements are supported:

| | SDK | portable-leg TFM |
|---|---|---|
| default (CI, contributors) | public `LibreWPF.Sdk` preview from nuget.org (pinned in `global.json`) | `net10.0-windows` |
| LibreWPF development | locally built `~/wpf` LibreWPF, `11.0.0-dev` | `net11.0-windows` |

The default needs nothing but a .NET 10 SDK:

```bash
dotnet build source/Components/AvalonDock -c Release
```

To hack on LibreWPF itself (e.g. to test WPF-internal fixes before they ship in a
public preview), switch to a locally built SDK:

```bash
./eng/use-local-librewpf.sh   # swaps global.json + source/NuGet.config (don't commit)
dotnet build source/Components/AvalonDock -c Release -p:LibreWpfTargetFramework=net11.0-windows
```

The `LibreWpfTargetFramework` property (defined in `source/Directory.Build.props`)
selects the TFM of the portable leg on non-Windows hosts. On Windows hosts nothing
changes: the multi-target legs (`net9.0-windows;net10.0-windows;net48`) all build as
classic WPF (portable mode is forced off in `Directory.Build.props`).

Note: the public preview does **not** yet contain lextm's WPF-internal captured-drag
fixes (`MouseDevice.Synchronize` guard, portable window-move capture guard — see
`docs/librewpf.md`), so interactive splitter/caption drags may still misbehave until
those land upstream in LibreWPF.

## Running the sample apps headless

```bash
sudo apt-get install -y xvfb mesa-vulkan-drivers fontconfig fonts-liberation2
pip install fonttools && ./eng/linux-test-fonts.sh   # see "Fonts" below
./eng/linux-smoke-test.sh
```

## Platform layer

`source/Components/AvalonDock/Platform/` (from the lextudio merge) abstracts cursor,
DPI, and native-window operations. The Linux implementations use Xlib
(`X11Interop.cs`) and work on X11 and on Wayland via XWayland:

- `LinuxCursorService` — `XQueryPointer` (position + button state)
- `LinuxDpiService` — scale from `Xft.dpi`, work area from `_NET_WORKAREA`
- `LinuxNativeWindowService` — geometry via `XGetGeometry`/`XTranslateCoordinates`,
  EWMH client messages for activate/close/always-on-top, `_NET_WM_WINDOW_OPACITY`

Everything degrades to defaults/no-ops when no X display is available, and X errors
are trapped so stale window ids can't abort the process. The floating-window drag
engine itself doesn't need any of this: on non-Windows it uses the managed caption
drag (`LayoutFloatingWindowControl`, `UsePortableCaptionDrag`), which is pure WPF.

## Known issues / limitations

- **Default font fail-fast (LibreWPF preview.14):** the portable backend defaults all
  text to family "Arial" and its composite-font fallback fail-fasts on Linux when no
  such family exists. Until fixed upstream, `eng/linux-test-fonts.sh` clones the
  metric-compatible Liberation Sans as "Arial" into `~/.fonts`. Desktop systems with
  a real Arial (or mscorefonts) are unaffected.
- **DevFlow instance actions:** the published `LeXtudio.DevFlow.Agent.*` 0.1.14
  packages only discover `[DevFlowAction]` on **public static** methods. MVVMTestApp's
  `DockDiagnostics` verbs work; TestApp's `avd.*` verbs are instance methods and need
  a newer agent, so `DevFlowIntegrationTests` currently runs in graceful-skip mode in
  CI (the suite passes; it exercises real docking once an agent with instance-action
  support is available).
- **Cross-window DPI mismatch (LibreWPF):** dropping a floating window back onto a
  docked target can fail because LibreWPF reports different coordinate scales for
  different windows — see the "Re-dock" section in `docs/librewpf.md`. Fix belongs in
  LibreWPF; no AvalonDock change required.

## CI

`.github/workflows/librewpf-linux.yml` builds all components/apps with the public
SDK, runs the DevFlow test suite, and runs the runtime smoke test under Xvfb.
