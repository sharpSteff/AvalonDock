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
					IsVisible = anchorable?.IsVisible ?? true,
					Container = content.Parent?.GetType().Name,
				});
			}

			return snapshot;
		}

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

		private static bool IsLoaded(DockingManager manager)
		{
#if AVALONIA
			return manager.IsAttachedToVisualTree();
#else
			return manager.IsLoaded;
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

		public List<ContentSnapshot> Contents { get; set; } = new List<ContentSnapshot>();
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

		public bool IsVisible { get; set; }

		public string Container { get; set; }
	}
}