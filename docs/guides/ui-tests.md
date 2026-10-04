---
title: UI tests
layout: default
parent: Guides
nav_order: 7
description: "The DevFlow UI tests that drive the demo applications on WPF and on Avalonia."
---

# UI tests
{: .no_toc }

`source/AutomationTest/AvalonDock.UITests` drives the two demo applications - `TestApp` (DockingManager)
and `AvalonDockCodeApp` (ToggleDockingManager) - with real clicks and key presses, through a
[DevFlow](https://github.com/lextudio/ui-labs) agent inside the application. The same tests run against
the WPF build of a demo (Windows) and against its Avalonia build (Windows, macOS, Linux), so one test
suite covers both libraries on every platform.

1. TOC
{:toc}

## Running the tests

The tests start the demo they need, so the demos must be built first, in the configuration of the tests.
`AVALONDOCK_UI` selects the build: `wpf` (the default on Windows) or `avalonia` (the default elsewhere).

```bash
git submodule update --init --recursive     # external/ui-labs: the DevFlow agent and driver

# Avalonia (any OS)
dotnet build source/AvalonDock.Avalonia.slnf -c Release
AVALONDOCK_UI=avalonia dotnet test source/AutomationTest/AvalonDock.UITests -c Release --no-build
```

```powershell
# WPF (Windows)
dotnet build source/AvalonDock.sln -c Release
$env:AVALONDOCK_UI = "wpf"
dotnet test source/AutomationTest/AvalonDock.UITests -c Release --no-build
```

On Linux the tests need an X display; CI uses Xvfb with the openbox window manager. A failed test saves a
screenshot of the main window and the layout model next to the test results, or into
`AVALONDOCK_UI_ARTIFACTS` when that is set (CI uploads that folder).

The tests are in the `UI` category; the unit test runs leave them out with `--filter "Category!=UI"`.

## How it works

- **The agent.** `source/AutomationTest/DevFlow/DevFlowAgent.props`, imported by the four demo projects,
  compiles `DevFlowAgent.cs` and references the DevFlow agent for WPF or Avalonia from the
  `external/ui-labs` submodule. The agent only starts when `DEVFLOW_AGENT_PORT` is set, which the test
  harness does when it launches a demo; a normal start of a demo is unaffected.
- **Finding elements.** The tests look elements up by what both builds share: the AvalonDock control
  types (`LayoutDocumentTabItem`, `LayoutAnchorableTabItem`, `ToggleDockButton`, ...), their template part
  names, and the titles and menu headers of the demos.
- **Asserting.** Instead of inferring state from the visual tree, the tests ask the demo for its layout
  model through DevFlow actions defined in `DevFlowAgent.cs`: `avalondock-layout` (contents, selection,
  activation, auto-hide, floating and detached windows), `avalondock-windows`, and a few actions that do
  what the operating system would (closing a window from its system menu, minimizing it).
- **Input.** Taps, right-taps, drags and keys are native input where the platform allows it (SendInput,
  XTest, cliclick); on macOS keys are raised as raw Avalonia input.

## Writing a test

Derive from `UITestBase` (or `ToggleDockTestBase` for AvalonDockCodeApp). Each fixture gets its own
instance of the demo, and its tests run in order within it.

```csharp
public class MyTests : UITestBase
{
    public MyTests() : base(DemoApp.TestApp) { }

    [Test]
    public async Task ClosingADocument_SelectsAnother()
    {
        await CloseDocumentAsync("Document 1", confirm: true);

        await WaitUntilAsync(async () => await FindContentAsync("Document 1") == null, "Document 1 to close");
        Assert.That((await FindContentAsync("Document 2"))!.IsSelected, Is.True);
    }
}
```

Keep tests to what both builds can do; a test that needs a platform-specific feature (WinForms hosting,
say) does not belong here.
