using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvalonDock.Controls;
using AvalonDock.Core;
using AvalonDock.Layout;
using AvalonDock.Themes;

namespace AvalonDock.Avalonia.PlatformTests
{
	/// <summary>
	/// Docking scenarios on the real windowing backend. Drags are driven through the same entry points the
	/// pointer handlers use, with real screen coordinates, so window placement, the overlay window, the drop
	/// target hit testing and DPI scaling are those of the platform.
	/// </summary>
	/// <remarks>
	/// Usage: <c>AvalonDock.Avalonia.PlatformTests [--screenshots &lt;dir&gt;] [--expect-transparent-overlay]</c>.
	/// </remarks>
	internal static class Program
	{
		private static readonly List<string> Failures = new List<string>();
		private static string _screenshotDir;
		private static bool _expectTransparentOverlay;

		[STAThread]
		public static int Main(string[] args)
		{
			for (var i = 0; i < args.Length; i++)
			{
				if (args[i] == "--screenshots" && i + 1 < args.Length) _screenshotDir = args[++i];
				else if (args[i] == "--expect-transparent-overlay") _expectTransparentOverlay = true;
			}

			if (_screenshotDir != null) Directory.CreateDirectory(_screenshotDir);

			AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace().SetupWithoutStarting();

			var done = new CancellationTokenSource();
			var exitCode = 1;
			Dispatcher.UIThread.Post(async () =>
			{
				try
				{
					await RunAllAsync();
					exitCode = Failures.Count == 0 ? 0 : 1;
				}
				catch (Exception ex)
				{
					Console.WriteLine($"FATAL {ex}");
				}
				finally
				{
					done.Cancel();
				}
			});

			// A scenario that never finishes must not hang the CI job.
			var watchdog = new Timer(_ => { Console.WriteLine("FATAL timeout"); Environment.Exit(2); }, null, TimeSpan.FromMinutes(5), Timeout.InfiniteTimeSpan);
			Dispatcher.UIThread.MainLoop(done.Token);
			watchdog.Dispose();

			Console.WriteLine(Failures.Count == 0 ? "ALL PASSED" : $"{Failures.Count} FAILURE(S):\n  " + string.Join("\n  ", Failures));
			return exitCode;
		}

		private static async Task RunAllAsync()
		{
			Console.WriteLine($"Platform: {Environment.OSVersion} / {System.Runtime.InteropServices.RuntimeInformation.OSDescription}");
			var scenarios = new (string Name, Func<Task> Run)[]
			{
				("Layout is shown and screen coordinates are consistent", LayoutAndCoordinatesAsync),
				("A floating window is placed where the model says", FloatingWindowPlacementAsync),
				("Dragging an anchorable into a document pane docks it", DragIntoDocumentPaneAsync),
				("Dragging an anchorable to the left edge of the manager docks it there", DragToManagerEdgeAsync),
				("Tearing out the last document of a pane collapses the group", TearOutLastDocumentAsync),
				("Maximizing and restoring a floating window", MaximizeRestoreAsync),
				("Detaching an anchorable to a window and back", DetachAndReattachAsync),
				("ToggleDockingManager docks, collapses and moves anchorables", ToggleManagerAsync),
			};

			foreach (var (name, run) in scenarios)
			{
				var before = Failures.Count;
				try
				{
					await run();
				}
				catch (Exception ex)
				{
					Fail($"{name}: exception {ex}");
				}

				Console.WriteLine($"{(Failures.Count == before ? "PASS" : "FAIL")} {name}");
			}
		}

		private static async Task LayoutAndCoordinatesAsync()
		{
			var f = await Fixture.CreateAsync();
			try
			{
				var docPane = f.Manager.GetVisualDescendants().OfType<LayoutDocumentPaneControl>().Single();
				Check(docPane.Bounds.Width > 300 && docPane.Bounds.Height > 200, $"document pane has a size ({docPane.Bounds})");
				Check(f.Manager.GetVisualDescendants().OfType<LayoutDocumentTabItem>().Count() == 2, "two document tabs");

				// The screen area of the manager must lie within the window's own screen rectangle, as reported by
				// Window.Position and the client size: both are what the drag code relies on.
				var scaling = f.Window.RenderScaling;
				var managerArea = f.Manager.GetScreenArea();
				var windowOrigin = f.Window.PointToScreen(default);
				var windowArea = new Rect(windowOrigin.X, windowOrigin.Y, f.Window.ClientSize.Width * scaling, f.Window.ClientSize.Height * scaling);
				Check(windowArea.Inflate(2).Contains(managerArea), $"manager screen area {managerArea} lies within the window {windowArea} (scaling {scaling})");
				Check(Math.Abs(managerArea.Width - f.Manager.Bounds.Width * scaling) < 2, $"manager screen width {managerArea.Width} = bounds {f.Manager.Bounds.Width} x scaling {scaling}");

				var local = new Point(37, 41);
				var roundTrip = f.Manager.ScreenToLocal(f.Manager.LocalToScreen(local));
				Check(Math.Abs(roundTrip.X - local.X) < 1 && Math.Abs(roundTrip.Y - local.Y) < 1, $"screen/local round trip {local} -> {roundTrip}");
				await ScreenshotAsync(f.Window, "01-layout");
			}
			finally
			{
				f.Close();
			}
		}

		private static async Task FloatingWindowPlacementAsync()
		{
			var f = await Fixture.CreateAsync();
			try
			{
				f.Tool1.Float();
				await SettleAsync();
				var fw = f.Manager.FloatingWindows.SingleOrDefault();
				Check(fw != null && fw.IsVisible, "the floating window is shown");
				if (fw == null) return;

				var model = (LayoutFloatingWindow)fw.Model;
				var content = f.Tool1;
				var scaling = fw.DesktopScaling;
				Check(Math.Abs(fw.Position.X - content.FloatingLeft * scaling) <= 2 && Math.Abs(fw.Position.Y - content.FloatingTop * scaling) <= 2,
					$"window position {fw.Position} matches the model ({content.FloatingLeft}, {content.FloatingTop}) x {scaling}");

				// Moving the window is written back into the model.
				var target = new PixelPoint(fw.Position.X + (int)(40 * scaling), fw.Position.Y + (int)(30 * scaling));
				fw.Position = target;
				await SettleAsync();
				Check(Math.Abs(content.FloatingLeft * scaling - fw.Position.X) <= 2 && Math.Abs(content.FloatingTop * scaling - fw.Position.Y) <= 2,
					$"moved window {fw.Position} is stored in the model ({content.FloatingLeft}, {content.FloatingTop})");
				Check(model.Root == f.Manager.Layout, "the floating window belongs to the layout");
				Check(ShowsText(fw, "Content of Tool 1"), "the floating window shows the content");
				await ScreenshotAsync(fw, "02-floating");
			}
			finally
			{
				f.Close();
			}
		}

		private static async Task DragIntoDocumentPaneAsync()
		{
			var f = await Fixture.CreateAsync();
			try
			{
				f.Tool2.Float();
				await SettleAsync();
				var fw = f.Manager.FloatingWindows.Single();
				var docPane = f.Manager.GetVisualDescendants().OfType<LayoutDocumentPaneControl>().Single();

				var overlay = await DragOverAsync(fw, CaptionPoint(fw), docPane.GetScreenArea().Center);
				Check(overlay != null, "the drop target overlay is shown");
				if (overlay == null) return;
				ReportOverlayMode(overlay);

				var into = FindPart(overlay, "PART_DocumentPaneDropTargetInto");
				Check(into?.IsEffectivelyVisible == true, "the 'into' target of the document pane is shown");
				if (into == null) return;
				await DragToAsync(fw, into.GetScreenArea().Center);
				Check(fw.CurrentDragService?.CurrentDropTarget != null, "a drop target is under the pointer");
				await ScreenshotAsync(f.Window, "03-drop-targets");

				var docked = fw.EndDrag(true);
				await SettleAsync();
				Check(docked, "the drop was accepted");
				Check(f.Tool2.Parent is LayoutDocumentPane, $"the anchorable is in the document pane (parent {f.Tool2.Parent?.GetType().Name})");
				Check(f.Manager.Layout.FloatingWindows.Count == 0, "no floating window is left in the layout");
				Check(!f.Manager.FloatingWindows.Any(w => w.IsVisible), "no floating window is left on screen");
			}
			finally
			{
				f.Close();
			}
		}

		private static async Task DragToManagerEdgeAsync()
		{
			var f = await Fixture.CreateAsync();
			try
			{
				f.Tool2.Float();
				await SettleAsync();
				var fw = f.Manager.FloatingWindows.Single();
				var overlay = await DragOverAsync(fw, CaptionPoint(fw), f.Manager.GetScreenArea().Center);
				var left = FindPart(overlay, "PART_DockingManagerDropTargetLeft");
				Check(left?.IsEffectivelyVisible == true, "the left edge target of the manager is shown");
				if (left == null) return;
				await DragToAsync(fw, left.GetScreenArea().Center);
				var service = fw.CurrentDragService;
				Console.WriteLine($"  left target {left.GetScreenArea()} manager {f.Manager.GetScreenArea()} overlay {overlay.ScreenArea} host {service?.CurrentHost} target {service?.CurrentDropTarget?.Type} last point {fw.LastDragScreenPoint}");
				var docked = fw.EndDrag(true);
				await SettleAsync();
				Check(docked, "the drop was accepted");
				Check(f.Tool2.Parent is LayoutAnchorablePane pane && f.Manager.Layout.RootPanel.Children.FirstOrDefault() == pane,
					"the anchorable is in a new pane at the left edge");
				var paneControl = f.Manager.GetVisualDescendants().OfType<LayoutAnchorablePaneControl>().FirstOrDefault(p => p.Model == f.Tool2.Parent);
				Check(paneControl != null && paneControl.TranslatePoint(default, f.Manager)?.X < 5, "the new pane is shown at the left edge");
				// The docked pane keeps a usable width rather than collapsing to its title bar.
				Check(paneControl != null && paneControl.Bounds.Width > 80, $"the new pane has a usable width ({paneControl?.Bounds.Width})");
			}
			finally
			{
				f.Close();
			}
		}

		private static async Task TearOutLastDocumentAsync()
		{
			var f = await Fixture.CreateAsync();
			try
			{
				// Doc 2 alone in a second pane; floating it empties that pane while the drag starts.
				f.DocumentPane.Children.Remove(f.Doc2);
				var group = (LayoutDocumentPaneGroup)f.DocumentPane.Parent;
				group.Orientation = global::Avalonia.Layout.Orientation.Horizontal;
				group.Children.Add(new LayoutDocumentPane(f.Doc2));
				await SettleAsync();

				f.Manager.StartDraggingFloatingWindowForContent(f.Doc2, false);
				await SettleAsync();
				var fw = f.Manager.FloatingWindows.Single(w => w.IsVisible);
				var remainingPane = f.Manager.GetVisualDescendants().OfType<LayoutDocumentPaneControl>().Single(p => p.IsEffectivelyVisible);
				var overlay = await DragOverAsync(fw, CaptionPoint(fw), remainingPane.GetScreenArea().Center);
				var into = FindPart(overlay, "PART_DocumentPaneDropTargetInto");
				Check(into?.IsEffectivelyVisible == true, "the drop targets of the remaining pane are shown");
				fw.EndDrag(false);
				await SettleAsync();
				Check(f.Doc2.IsFloating, "the document floats");
				Check(f.Manager.GetVisualDescendants().OfType<LayoutDocumentPaneControl>().Count(p => p.IsEffectivelyVisible) == 1, "the emptied pane is gone");
			}
			finally
			{
				f.Close();
			}
		}

		private static async Task MaximizeRestoreAsync()
		{
			var f = await Fixture.CreateAsync();
			try
			{
				f.Tool1.Float();
				await SettleAsync();
				var fw = f.Manager.FloatingWindows.Single();
				var before = (fw.Position, fw.Bounds.Size);
				fw.ToggleMaximizeCommand.Execute(null);
				await SettleAsync(400);
				Check(fw.WindowState == WindowState.Maximized && fw.IsMaximized, $"maximized (state {fw.WindowState})");
				fw.ToggleMaximizeCommand.Execute(null);
				await SettleAsync(400);
				Check(fw.WindowState == WindowState.Normal && !fw.IsMaximized, $"restored (state {fw.WindowState})");
				Check(Math.Abs(fw.Bounds.Width - before.Size.Width) < 2 && Math.Abs(fw.Bounds.Height - before.Size.Height) < 2,
					$"restored size {fw.Bounds.Size} equals {before.Size}");
			}
			finally
			{
				f.Close();
			}
		}

		private static async Task DetachAndReattachAsync()
		{
			var f = await Fixture.CreateAsync();
			try
			{
				f.Manager.AllowDetachedWindows = true;
				f.Manager.DetachAnchorableToWindow(f.Tool1);
				await SettleAsync();
				Check(f.Manager.IsDetached(f.Tool1), "detached");
				var detached = f.Manager.GetDetachedWindow(f.Tool1);
				Check(detached != null && detached.IsVisible, "a standalone window shows the anchorable");
				Check(detached != null && ShowsText(detached, "Content of Tool 1"), "the standalone window shows the content");
				await ScreenshotAsync(detached, "04-detached");
				f.Manager.ReattachAnchorable(f.Tool1);
				await SettleAsync();
				Check(!f.Manager.IsDetached(f.Tool1), "reattached");
				Check(ShowsText(f.Manager, "Content of Tool 1"), "the content is back in the manager");
			}
			finally
			{
				f.Close();
			}
		}

		private static async Task ToggleManagerAsync()
		{
			var documentPane = new LayoutDocumentPane(new LayoutDocument { Title = "Doc", Content = new TextBlock { Text = "Document content" } });
			var root = new LayoutRoot { RootPanel = new LayoutPanel(new LayoutDocumentPaneGroup(documentPane)) };
			var explorer = AddToolbox(root, "Explorer", DockZone.LeftTop);
			var output = AddToolbox(root, "Output", DockZone.BottomLeft);
			var manager = new ToggleDockingManager
			{
				Layout = root,
				LayoutItemTemplate = new global::Avalonia.Controls.Templates.FuncDataTemplate<Toolbox>((t, _) => new TextBlock { Text = "Content of " + t.Title }),
			};
			var window = new Window { Width = 1000, Height = 700, Content = manager, Position = new PixelPoint(60, 60) };
			window.Show();
			await SettleAsync();
			try
			{
				Check(explorer.IsAutoHidden && output.IsAutoHidden, "the anchorables start collapsed");
				manager.ToggleAnchorable(explorer, DockZone.LeftTop);
				await SettleAsync();
				Check(!explorer.IsAutoHidden && ShowsText(manager, "Content of Explorer"), "Explorer is docked");
				await ScreenshotAsync(window, "05-toggle");

				var overlay = ToggleDockDragOverlay.StartProgrammaticDrag(output, manager, new Point(10, 10));
				Check(overlay != null, "the zone overlay is shown");
				if (overlay == null) return;
				await SettleAsync();
				var zone = overlay.DropZones.First(z => z.Zone == DockZone.RightTop && z.Label != null);
				overlay.MoveTo(zone.Rect.Center);
				await SettleAsync();
				await ScreenshotAsync(window, "06-toggle-zones");
				overlay.DropAt(zone.Rect.Center);
				await SettleAsync();
				Check(manager._rightTopBar?.ContainsAnchorable(output) == true, "Output moved to the right top bar");
				Check(!output.IsAutoHidden, "Output is docked in its new zone");
				var pane = manager.GetVisualDescendants().OfType<LayoutAnchorablePaneControl>().FirstOrDefault(p => p.Model == output.Parent);
				Check(pane != null && pane.TranslatePoint(default, manager)?.X > manager.Bounds.Width / 2, "Output is shown on the right");
			}
			finally
			{
				window.Close();
				await SettleAsync();
			}
		}

		private static LayoutAnchorable AddToolbox(LayoutRoot root, string name, DockZone zone)
		{
			var anchorable = new LayoutAnchorable { Title = name, ContentId = name, Content = new Toolbox { Id = name, Title = name, Zone = zone } };
			var side = zone == DockZone.BottomLeft || zone == DockZone.BottomRight ? root.BottomSide
				: zone == DockZone.RightTop || zone == DockZone.RightBottom ? root.RightSide
				: root.LeftSide;
			var group = new LayoutAnchorGroup();
			side.Children.Add(group);
			group.Children.Add(anchorable);
			return anchorable;
		}

		/// <summary>Starts a drag of <paramref name="fw"/> at <paramref name="from"/> and moves it to <paramref name="to"/> in steps.</summary>
		private static async Task<OverlayWindow> DragOverAsync(LayoutFloatingWindowControl fw, Point from, Point to)
		{
			fw.BeginProgrammaticDrag(from);
			await SettleAsync();
			const int steps = 6;
			for (var i = 1; i <= steps; i++)
			{
				fw.DragTo(new Point(from.X + (to.X - from.X) * i / steps, from.Y + (to.Y - from.Y) * i / steps));
				await SettleAsync(60);
			}

			await SettleAsync();
			return fw.CurrentDragService?.CurrentOverlayWindow as OverlayWindow;
		}

		private static async Task DragToAsync(LayoutFloatingWindowControl fw, Point to)
		{
			fw.DragTo(to);
			await SettleAsync();
			fw.DragTo(to + new Point(1, 0));
			await SettleAsync();
		}

		private static Point CaptionPoint(LayoutFloatingWindowControl fw)
		{
			var caption = fw.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.Name == "PART_Caption") ?? (Control)fw;
			return caption.LocalToScreen(new Point(30, Math.Min(10, caption.Bounds.Height / 2)));
		}

		private static Control FindPart(OverlayWindow overlay, string name)
			=> overlay?.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.Name == name);

		private static void ReportOverlayMode(OverlayWindow overlay)
		{
			var inWindow = overlay.IsShownInWindow;
			Console.WriteLine($"  overlay mode: {(inWindow ? "in the host window (no transparent windows)" : "transparent window")}");
			if (_expectTransparentOverlay) Check(!inWindow, "the overlay is shown in a transparent window");
		}

		private static bool ShowsText(Visual root, string text)
			=> root != null && root.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == text && t.IsEffectivelyVisible);

		private static async Task SettleAsync(int milliseconds = 250)
		{
			await Task.Delay(milliseconds);
			await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
		}

		private static async Task ScreenshotAsync(TopLevel topLevel, string name)
		{
			if (_screenshotDir == null || topLevel == null) return;
			await SettleAsync(100);
			var scaling = topLevel.RenderScaling;
			var size = new PixelSize(Math.Max(1, (int)(topLevel.Bounds.Width * scaling)), Math.Max(1, (int)(topLevel.Bounds.Height * scaling)));
			using var bitmap = new RenderTargetBitmap(size, new Vector(96 * scaling, 96 * scaling));
			bitmap.Render(topLevel);
			using var stream = File.Create(Path.Combine(_screenshotDir, name + ".png"));
			bitmap.Save(stream, new PngBitmapEncoderOptions());
		}

		private static void Check(bool condition, string message)
		{
			if (!condition) Fail(message);
		}

		private static void Fail(string message)
		{
			Failures.Add(message);
			Console.WriteLine($"  FAILED: {message}");
		}

		private sealed class App : Application
		{
			public override void Initialize()
			{
				Styles.Add(new FluentTheme());
				Styles.Add(new AvalonDockTheme());
			}
		}

		private sealed class Toolbox : IToolbox
		{
			public string Id { get; set; }

			public string Title { get; set; }

			public object Context { get; set; }

			public IDockable Owner { get; set; }

			public IFactory Factory { get; set; }

			public bool CanClose { get; set; }

			public bool CanPin { get; set; } = true;

			public bool CanFloat { get; set; } = true;

			public bool CanDrag { get; set; } = true;

			public bool CanDrop { get; set; } = true;

			public bool IsModified { get; set; }

			public bool IsActive { get; set; }

			public DockState DockState { get; set; }

			public string ToolTipText { get; set; }

			public DockZone Zone { get; set; }

			public string Shortcut { get; set; }

			public bool IsOpenByDefault { get; set; }

			public bool IsOpen { get; set; }

			public object Icon { get; set; }

			public bool OnClose() => true;

			public void OnSelected()
			{
			}
		}

		private sealed class Fixture
		{
			public Window Window { get; private set; }

			public DockingManager Manager { get; private set; }

			public LayoutDocumentPane DocumentPane { get; private set; }

			public LayoutDocument Doc1 { get; private set; }

			public LayoutDocument Doc2 { get; private set; }

			public LayoutAnchorable Tool1 { get; private set; }

			public LayoutAnchorable Tool2 { get; private set; }

			public static async Task<Fixture> CreateAsync()
			{
				var f = new Fixture
				{
					Doc1 = new LayoutDocument { Title = "Doc 1", ContentId = "doc1", Content = new TextBlock { Text = "Content of Doc 1" } },
					Doc2 = new LayoutDocument { Title = "Doc 2", ContentId = "doc2", Content = new TextBlock { Text = "Content of Doc 2" } },
					Tool1 = new LayoutAnchorable { Title = "Tool 1", ContentId = "tool1", Content = new TextBlock { Text = "Content of Tool 1" } },
					Tool2 = new LayoutAnchorable { Title = "Tool 2", ContentId = "tool2", Content = new TextBlock { Text = "Content of Tool 2" } },
				};
				f.DocumentPane = new LayoutDocumentPane();
				f.DocumentPane.Children.Add(f.Doc1);
				f.DocumentPane.Children.Add(f.Doc2);
				var tools = new LayoutAnchorablePane { DockWidth = new GridLength(220) };
				tools.Children.Add(f.Tool1);
				tools.Children.Add(f.Tool2);
				var panel = new LayoutPanel { Orientation = global::Avalonia.Layout.Orientation.Horizontal };
				panel.Children.Add(tools);
				panel.Children.Add(new LayoutDocumentPaneGroup(f.DocumentPane));
				f.Manager = new DockingManager { Layout = new LayoutRoot { RootPanel = panel } };
				f.Window = new Window
				{
					Title = "AvalonDock platform test",
					Width = 1000,
					Height = 700,
					Content = f.Manager,
					WindowStartupLocation = WindowStartupLocation.Manual,
					Position = new PixelPoint(60, 60),
				};
				f.Window.Show();
				await SettleAsync(500);
				return f;
			}

			public void Close()
			{
				foreach (var fw in Manager.FloatingWindows.ToList())
					fw.Close();
				foreach (var anchorable in Manager.Layout.Descendents().OfType<LayoutAnchorable>().Where(Manager.IsDetached).ToList())
					Manager.ReattachAnchorable(anchorable);
				Window.Close();
			}
		}
	}
}
