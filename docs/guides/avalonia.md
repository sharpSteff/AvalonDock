---
title: Avalonia
layout: default
parent: Guides
nav_order: 6
description: "Using DockingManager and ToggleDockingManager in Avalonia applications with AvalonDock.Avalonia."
---

# AvalonDock for Avalonia
{: .no_toc }

`AvalonDock.Avalonia` is a port of the WPF controls to [Avalonia](https://avaloniaui.net/) 12. It
contains the `DockingManager`, the `ToggleDockingManager`, the layout model and the default theme, and
runs on Windows, Linux and macOS.

{: .note }
The port is new. The layout model, the commands and the behaviour follow the WPF library closely, but
the WPF theme packages (`AvalonDock.Themes.*`) and the WPF serializers are not available for it yet.

1. TOC
{:toc}

---

## Installation

Reference the project (a NuGet package `Dirkster.AvalonDock.Avalonia` is prepared by the project file):

```xml
<ProjectReference Include="..\Components\AvalonDock.Avalonia\AvalonDock.Avalonia.csproj" />
```

The library targets `net8.0` and `net10.0` and depends on `Avalonia` 12.1.

## Setting up the theme

Add the AvalonDock styles after the application theme. The default look comes in a light and a dark
variant and follows `RequestedThemeVariant`:

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:ad="https://github.com/Dirkster99/AvalonDock">
    <Application.Styles>
        <FluentTheme />
        <ad:AvalonDockTheme />
    </Application.Styles>
</Application>
```

If the application does not add `AvalonDockTheme`, the first `DockingManager` that is shown adds it
on its own, the way WPF finds `generic.xaml`.

## A DockingManager

The XML namespace `https://github.com/Dirkster99/AvalonDock` maps `AvalonDock`, `AvalonDock.Controls`,
`AvalonDock.Layout` and `AvalonDock.Converters`, so the layout is written as in WPF:

```xml
<ad:DockingManager>
    <ad:LayoutRoot>
        <ad:LayoutPanel Orientation="Horizontal">
            <ad:LayoutAnchorablePane DockWidth="220">
                <ad:LayoutAnchorable Title="Explorer" ContentId="explorer">
                    <TextBlock Text="Explorer" />
                </ad:LayoutAnchorable>
            </ad:LayoutAnchorablePane>
            <ad:LayoutDocumentPaneGroup>
                <ad:LayoutDocumentPane>
                    <ad:LayoutDocument Title="Document 1" ContentId="doc1">
                        <TextBox AcceptsReturn="True" />
                    </ad:LayoutDocument>
                </ad:LayoutDocumentPane>
            </ad:LayoutDocumentPaneGroup>
        </ad:LayoutPanel>
    </ad:LayoutRoot>
</ad:DockingManager>
```

`DocumentsSource`/`AnchorablesSource`, `LayoutItemTemplate`/`LayoutItemTemplateSelector`,
`LayoutItemContainerStyle` (a `ControlTheme` here) and the other MVVM hooks work as in WPF.

## A ToggleDockingManager

The `ToggleDockingManager` shows a sidebar button for each anchorable; a click docks the anchorable
into its zone or collapses it again. View models implement `IToolbox` from `AvalonDock.Core`, which
decides the zone, the shortcut and whether the tool window is open:

```csharp
var manager = new ToggleDockingManager
{
    Layout = layout,               // anchorables placed in LeftSide / RightSide / BottomSide
    LayoutItemTemplate = new FuncDataTemplate<MyToolbox>((toolbox, _) => new MyToolboxView()),
};
```

`IToolbox.IsOpen` is kept in sync both ways, `IsOpenByDefault` opens a tool window when the manager is
loaded, and `IToolbox.Shortcut` (for example `Ctrl+Alt+T`) becomes a key binding of the window. A
button or a title can be dragged onto another zone. See
[ToggleDockingManager]({{ site.baseurl }}{% link guides/toggle-docking-manager.md %}) for the concepts.

## Differences from WPF

| WPF | Avalonia |
|:----|:---------|
| `Style` properties (`DocumentPaneControlStyle`, `LayoutItemContainerStyle`, ...) | `ControlTheme` |
| `DataTemplateSelector`, `StyleSelector` | `AvalonDock.Controls.DataTemplateSelector`, `StyleSelector` |
| `ControlTemplate` properties (`AnchorTemplate`, ...) | `IControlTemplate` |
| `ImageSource IconSource` | `IImage IconSource` |
| `Theme` | `DockTheme` (Avalonia's `StyledElement.Theme` is the control theme) |
| Auto-hide flyout in a child window | A control in the manager's template |
| Drop target overlay in a transparent window | `OverlayWindowMode`: a transparent window where the platform composites transparency, the window's overlay layer otherwise (`Auto`) |

Dragging is driven by Avalonia's own pointer events rather than by the native window move loop: the
pointer is captured by the window the drag started in, and the floating window follows it. This behaves
the same way on every platform.

## Platforms

| Platform | Drop target overlay |
|:---------|:--------------------|
| Windows | Transparent window over the host |
| macOS | Transparent window over the host |
| Linux (X11) with a compositing manager - GNOME, KDE, Xfce and most desktops, or XWayland | Transparent window over the host |
| Linux (X11) without a compositing manager | Drawn into the host window; while the pointer is over a host the dragged window shrinks to a small caption beside the pointer, so it does not hide the drop targets |

Whether transparent windows are available is found out once per process, with `OverlayWindowMode.Auto`
(the default). `OverlayWindowMode.Window` and `OverlayWindowMode.InWindow` force one of the two.

Screen coordinates in the drag and drop code are device pixels, as returned by
`Visual.PointToScreen`.

## Testing

The headless tests in `source/AutomationTest/AvalonDock.Avalonia.Tests` use `Avalonia.Headless.NUnit`
and drive the controls with simulated mouse input - tab tear-out, tab reordering, splitter drags,
docking through the drop targets, the auto-hide flyout and the sidebar buttons of the
`ToggleDockingManager`. They need no display:

```bash
dotnet test source/AvalonDock.Avalonia.sln
```

`source/AutomationTest/AvalonDock.Avalonia.PlatformTests` runs docking scenarios against the real
windowing backend of the OS - real windows, real screen coordinates and the real overlay - and saves
screenshots. CI runs it on Windows, macOS and Linux (with and without a compositing manager):

```bash
dotnet run --project source/AutomationTest/AvalonDock.Avalonia.PlatformTests -- --screenshots shots
```

`source/AvalonDockAvaloniaApp` is a sample application with both managers and a light/dark switch:

```bash
dotnet run --project source/AvalonDockAvaloniaApp
```

## Demo applications

`source/AvaloniaDemoProject` ports the two WPF demo applications to Avalonia:

```bash
# Port of source/TestApp (DockingManager)
dotnet run --project source/AvaloniaDemoProject

# Port of source/AvalonDockCodeApp (ToggleDockingManager, MVVM + dependency injection)
dotnet run --project source/AvaloniaDemoProject -- --app code
```

Both keep the window titles, menu headers, tool window titles and content ids of the WPF applications.
The code application compiles the view models of `source/AvalonDockCodeApp/ViewModels` unchanged apart from
a few `#if AVALONIA` using aliases (the project defines `AVALONIA`); two small shims stand in for
`Dispatcher` and `Microsoft.Win32.OpenFolderDialog`. Only the views, the WPF-only icon helpers and the
main windows are written for Avalonia. What is not ported: WinForms hosting (a placeholder takes its
place), the WPF themes other than the default one (light and dark are available), and the LibreWPF and
DevFlow diagnostics.
