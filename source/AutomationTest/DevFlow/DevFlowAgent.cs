#nullable disable
#if AVALONIA
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.VisualTree;
using LeXtudio.DevFlow.Agent.Avalonia;
#else
using System.Windows;
using LeXtudio.DevFlow.Agent.Wpf;
#endif
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using AvalonDock;
using AvalonDock.Controls;
using AvalonDock.Layout;
using LeXtudio.DevFlow.Agent.Core;
using Microsoft.Maui.DevFlow.Agent.Core;

namespace AvalonDock.UITests.Agent
{
	/// <summary>
	/// Starts the DevFlow agent that the UI tests drive. The test harness sets DEVFLOW_AGENT_PORT when
	/// it launches a demo application; without it the application runs as usual.
	/// </summary>
	internal static class DevFlowAgent
	{
		public const string PortVariable = "DEVFLOW_AGENT_PORT";

		public static void StartIfRequested(Application application)
		{
			if (!int.TryParse(Environment.GetEnvironmentVariable(PortVariable), out var port) || port <= 0)
				return;

			var options = new AgentOptions { Port = port };
#if AVALONIA
			application.AddAvaloniaDevFlowAgent(options);
#else
			application.AddWpfDevFlowAgent(options);
#endif
		}
	}

	/// <summary>
	/// DevFlow actions that report the layout model of the main window's DockingManager (named
	/// "dockManager" in both demo applications). The UI tests assert against this model, which is the
	/// same on WPF and Avalonia, instead of against platform-specific visual trees.
	/// </summary>
	[DevFlowUIThread]
	public static class AvalonDockDevFlowActions
	{
		/// <summary>The layout as JSON; the agent returns other result types as their ToString().</summary>
		[DevFlowAction("avalondock-layout", Description = "Describes the contents, panes and floating windows of the main DockingManager, as JSON.")]
		public static string GetLayout() => JsonSerializer.Serialize(CreateSnapshot());

		private static LayoutSnapshot CreateSnapshot()
		{
			var manager = FindDockingManager();
			var snapshot = new LayoutSnapshot();
			if (manager?.Layout == null)
				return snapshot;

			var window = GetMainWindow();
			snapshot.WindowTitle = window?.Title;
#if AVALONIA
			snapshot.WindowWidth = window?.Bounds.Width ?? 0;
			snapshot.WindowHeight = window?.Bounds.Height ?? 0;
#else
			snapshot.WindowWidth = window?.ActualWidth ?? 0;
			snapshot.WindowHeight = window?.ActualHeight ?? 0;
#endif

			var root = manager.Layout;
			snapshot.ManagerFound = true;
			snapshot.ManagerLoaded = IsLoaded(manager);
			snapshot.FloatingWindowCount = root.FloatingWindows.Count;
			snapshot.ActiveContent = root.ActiveContent?.Title;
			snapshot.LastFocusedDocument = root.LastFocusedDocument?.Title;

			snapshot.AutoHideWindowContent = (manager.AutoHideWindow?.Model as LayoutContent)?.Title;
			foreach (var floatingWindow in manager.FloatingWindows)
			{
				snapshot.FloatingWindows.Add(new FloatingWindowSnapshot
				{
					Contents = floatingWindow.Model.Descendents().OfType<LayoutContent>().Select(c => c.Title).ToList(),
#if AVALONIA
					IsVisible = floatingWindow.IsVisible,
					Width = floatingWindow.Bounds.Width,
					Height = floatingWindow.Bounds.Height,
#else
					IsVisible = floatingWindow.IsVisible,
					Width = floatingWindow.ActualWidth,
					Height = floatingWindow.ActualHeight,
#endif
				});
			}

			var contents = root.Descendents().OfType<LayoutContent>().Concat(root.Hidden).Distinct();
			foreach (var content in contents)
			{
				var anchorable = content as LayoutAnchorable;
				snapshot.Contents.Add(new ContentSnapshot
				{
					Kind = content is LayoutDocument ? "Document" : "Anchorable",
					Title = content.Title,
					ContentId = content.ContentId,
					IsActive = content.IsActive,
					IsSelected = content.IsSelected,
					IsFloating = content.IsFloating,
					IsHidden = anchorable?.IsHidden ?? false,
					IsAutoHidden = anchorable?.IsAutoHidden ?? false,
					IsDetached = anchorable?.IsDetached ?? false,
					IsVisible = anchorable?.IsVisible ?? true,
					Container = content.Parent?.GetType().Name,
				});
			}

			return snapshot;
		}

		/// <summary>
		/// Closes the floating or detached window that holds the content with this title the way the system
		/// menu, Alt+F4 or the taskbar do: a WM_SYSCOMMAND SC_CLOSE on WPF, the platform's close request on
		/// Avalonia. The close is posted, because the content's Hiding handler may ask a modal question.
		/// </summary>
		[DevFlowAction("avalondock-close-window", Description = "Closes the floating or detached window holding the content with the given title, like the system menu does.")]
		public static bool CloseWindowOf(string title)
		{
			var window = GetApplicationWindows().FirstOrDefault(w => w switch
			{
				LayoutFloatingWindowControl floating => floating.Model.Descendents().OfType<LayoutContent>().Any(c => c.Title == title),
				DetachedAnchorableWindow detached => detached.Model?.Title == title,
				_ => false,
			});
			if (window == null)
				return false;

#if AVALONIA
			Avalonia.Threading.Dispatcher.UIThread.Post(window.Close);
#else
			var handle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
			PostMessage(handle, 0x0112 /* WM_SYSCOMMAND */, new IntPtr(0xF060) /* SC_CLOSE */, IntPtr.Zero);
#endif
			return true;
		}

		/// <summary>The application's windows - kind (Main, Floating, Detached, Other), title and state - as JSON.</summary>
		[DevFlowAction("avalondock-windows", Description = "Lists the application's windows with their kind, title and window state, as JSON.")]
		public static string GetWindows()
		{
			var main = GetMainWindow();
			return JsonSerializer.Serialize(GetApplicationWindows().Select(w => new WindowSnapshot
			{
				Kind = w == main ? "Main" : w is LayoutFloatingWindowControl ? "Floating" : w is DetachedAnchorableWindow ? "Detached" : "Other",
				Title = w.Title,
				State = w.WindowState.ToString(),
				IsVisible = w.IsVisible,
			}).ToList());
		}

		/// <summary>Sets the state (Normal, Minimized, Maximized) of the window with this title.</summary>
		[DevFlowAction("avalondock-set-window-state", Description = "Sets the window state (Normal, Minimized, Maximized) of the window with the given title.")]
		public static bool SetWindowState(string title, string state)
		{
			var window = GetApplicationWindows().FirstOrDefault(w => w.Title == title);
			if (window == null)
				return false;

			window.WindowState = Enum.Parse<WindowState>(state, ignoreCase: true);
			return true;
		}

		/// <summary>Resizes the main window, in device independent pixels.</summary>
		[DevFlowAction("avalondock-resize-main-window", Description = "Resizes the main window to the given width and height.")]
		public static bool ResizeMainWindow(double width, double height)
		{
			var window = GetMainWindow();
			if (window == null)
				return false;

			window.Width = width;
			window.Height = height;
			return true;
		}

		private static IEnumerable<Window> GetApplicationWindows()
		{
#if AVALONIA
			return (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Windows ?? (IEnumerable<Window>)Array.Empty<Window>();
#else
			return Application.Current?.Windows.OfType<Window>() ?? Enumerable.Empty<Window>();
#endif
		}

		/// <summary>
		/// Moves the floating windows over the document area of the main window, so that they cover neither
		/// its menu nor its tabs and side panels: a new floating window opens at the screen's top left corner,
		/// where it would hide whatever the tests click next.
		/// </summary>
		[DevFlowAction("avalondock-arrange-floating-windows", Description = "Moves the floating windows over the document area of the main window.")]
		public static int ArrangeFloatingWindows()
		{
			var main = GetMainWindow();
			var manager = FindDockingManager();
			if (main == null || manager == null)
				return 0;

			var count = 0;
			foreach (var window in manager.FloatingWindows)
			{
				var offset = 24 * count++;
#if AVALONIA
				var scaling = main.RenderScaling;
				window.Position = new PixelPoint(
					main.Position.X + (int)Math.Round((200 + offset) * scaling),
					main.Position.Y + (int)Math.Round((150 + offset) * scaling));
#else
				window.Left = main.Left + 200 + offset;
				window.Top = main.Top + 150 + offset;
#endif
			}

			return count;
		}

#if !AVALONIA
		[System.Runtime.InteropServices.DllImport("user32.dll")]
		[return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
		private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

#endif
		private static Window GetMainWindow()
		{
#if AVALONIA
			return (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
#else
			return Application.Current?.MainWindow;
#endif
		}

		private static DockingManager FindDockingManager()
		{
#if AVALONIA
			return GetMainWindow()?.FindControl<DockingManager>("dockManager");
#else
			return GetMainWindow()?.FindName("dockManager") as DockingManager;
#endif
		}

		// Whether the manager is in the window's visual tree (Layout > Unload Manager takes it out).
		private static bool IsLoaded(DockingManager manager)
		{
#if AVALONIA
			return manager.IsAttachedToVisualTree();
#else
			return System.Windows.Media.VisualTreeHelper.GetParent(manager) != null;
#endif
		}
	}

	/// <summary>The layout model of a DockingManager, as returned by the avalondock-layout action.</summary>
	public sealed class LayoutSnapshot
	{
		public string WindowTitle { get; set; }

		public double WindowWidth { get; set; }

		public double WindowHeight { get; set; }

		public bool ManagerFound { get; set; }

		public bool ManagerLoaded { get; set; }

		public int FloatingWindowCount { get; set; }

		public string ActiveContent { get; set; }

		public string LastFocusedDocument { get; set; }

		public string AutoHideWindowContent { get; set; }

		public List<FloatingWindowSnapshot> FloatingWindows { get; set; } = new List<FloatingWindowSnapshot>();

		public List<ContentSnapshot> Contents { get; set; } = new List<ContentSnapshot>();
	}

	/// <summary>A window, as listed by the avalondock-windows action.</summary>
	public sealed class WindowSnapshot
	{
		public string Kind { get; set; }

		public string Title { get; set; }

		public string State { get; set; }

		public bool IsVisible { get; set; }
	}

	/// <summary>A floating window of a <see cref="LayoutSnapshot"/>.</summary>
	public sealed class FloatingWindowSnapshot
	{
		public List<string> Contents { get; set; } = new List<string>();

		public bool IsVisible { get; set; }

		public double Width { get; set; }

		public double Height { get; set; }
	}

	/// <summary>One document or anchorable of a <see cref="LayoutSnapshot"/>.</summary>
	public sealed class ContentSnapshot
	{
		public string Kind { get; set; }

		public string Title { get; set; }

		public string ContentId { get; set; }

		public bool IsActive { get; set; }

		public bool IsSelected { get; set; }

		public bool IsFloating { get; set; }

		public bool IsHidden { get; set; }

		public bool IsAutoHidden { get; set; }

		public bool IsDetached { get; set; }

		public bool IsVisible { get; set; }

		public string Container { get; set; }
	}
}