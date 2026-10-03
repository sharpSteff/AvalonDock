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
the same way on every platform, including X11 window managers without compositing.

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

`source/AvalonDockAvaloniaApp` is a sample application with both managers and a light/dark switch:

```bash
dotnet run --project source/AvalonDockAvaloniaApp
```
