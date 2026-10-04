using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;
using AvalonDock;
using AvalonDock.Core;
using AvalonDock.DependencyInjection;
using AvalonDock.Serializer.Xml;
using ToggleTestApp.ViewModels;

namespace ToggleTestApp
{
	/// <summary>
	/// Avalonia port of the WPF AvalonDockCodeApp's main window. The layout priority, the layout persistence and
	/// the "unknown toolbox" layout are those of the WPF application.
	/// </summary>
	public partial class MainWindow : Window
	{
		/// <summary>Title of the tool window that the "unknown toolbox" layout carries.</summary>
		public const string UnknownToolboxTitle = "Ghost Toolbox";

		/// <summary>Content id of that tool window, which no toolbox of this application answers to.</summary>
		private const string UnknownToolboxContentId = "ghostToolbox";

		public MainWindow(MainViewModel viewModel, ToggleDockOptions? dockOptions = null)
		{
			DataContext = viewModel;
			InitializeComponent();

			if (dockOptions != null)
			{
				dockManager.ButtonSize = dockOptions.ButtonSize;
				dockManager.DefaultDockWidth = dockOptions.DefaultDockWidth;
				dockManager.DefaultDockHeight = dockOptions.DefaultDockHeight;
				dockManager.ShowHeaderMinimizeButton = dockOptions.ShowHeaderMinimizeButton;
				dockManager.ShowHeaderOptionsButton = dockOptions.ShowHeaderOptionsButton;

				if (Enum.TryParse<DockLayoutPriority>(dockOptions.LayoutPriority, out var priority))
				{
					dockManager.LayoutPriority = priority;
				}
			}
		}

		private static string LayoutFilePath =>
			Path.Combine(AppContext.BaseDirectory, "ToggleDockLayout.config");

		private static string UnknownToolboxLayoutFilePath =>
			Path.Combine(AppContext.BaseDirectory, "ToggleDockLayout.Unknown.config");

		private void OnLayoutPriorityChanged(object? sender, RoutedEventArgs e)
		{
			menuBottomFullWidth.IsChecked = sender == menuBottomFullWidth;
			menuSidesFullHeight.IsChecked = sender == menuSidesFullHeight;
			menuDefaultPriority.IsChecked = sender == menuDefaultPriority;

			if (menuBottomFullWidth.IsChecked)
				dockManager.LayoutPriority = DockLayoutPriority.BottomFullWidth;
			else if (menuSidesFullHeight.IsChecked)
				dockManager.LayoutPriority = DockLayoutPriority.SidesFullHeight;
			else
				dockManager.LayoutPriority = DockLayoutPriority.Default;
		}

		private void OnThemeChanged(object? sender, RoutedEventArgs e)
		{
			menuDark.IsChecked = sender == menuDark;
			menuLight.IsChecked = sender == menuLight;

			var isDark = sender != menuLight;
			var application = Application.Current!;
			application.RequestedThemeVariant = isDark ? ThemeVariant.Dark : ThemeVariant.Light;
			CodeAppHost.SetAppThemeResources(application, isDark);
		}

		private void OnExit(object? sender, RoutedEventArgs e) => Close();

		// ===== Layout persistence =====

		private void OnSaveLayout(object? sender, RoutedEventArgs e)
		{
			new XmlLayoutSerializer(dockManager).Serialize(LayoutFilePath);
		}

		private void OnLoadLayout(object? sender, RoutedEventArgs e)
		{
			LoadLayout(LayoutFilePath);
		}

		/// <summary>
		/// Writes a copy of the saved layout with one extra tool window whose content id no toolbox
		/// answers to, then loads it. This is what a layout stored by an earlier version of an
		/// application looks like after one of its toolboxes has been removed.
		/// </summary>
		/// <param name="sender">The menu item.</param>
		/// <param name="e">The event arguments.</param>
		private void OnLoadLayoutWithUnknownToolbox(object? sender, RoutedEventArgs e)
		{
			if (!File.Exists(LayoutFilePath))
			{
				OnSaveLayout(sender, e);
			}

			var document = XDocument.Load(LayoutFilePath);
			var root = document.Root;
			if (root == null)
			{
				return;
			}

			var leftSide = root.Element("LeftSide");
			if (leftSide == null)
			{
				leftSide = new XElement("LeftSide");
				root.Add(leftSide);
			}

			var group = leftSide.Element("LayoutAnchorGroup");
			if (group == null)
			{
				group = new XElement("LayoutAnchorGroup");
				leftSide.Add(group);
			}

			group.Add(new XElement(
				"LayoutAnchorable",
				new XAttribute("Title", UnknownToolboxTitle),
				new XAttribute("ContentId", UnknownToolboxContentId)));

			document.Save(UnknownToolboxLayoutFilePath);
			LoadLayout(UnknownToolboxLayoutFilePath);
		}

		/// <summary>
		/// Restores a stored layout, reconnecting each tool window to the toolbox that carries its
		/// content id. A stored item no toolbox answers to is left without content, which is the
		/// signal the serializer uses to keep it out of the restored layout.
		/// </summary>
		/// <param name="path">The layout file to load.</param>
		private void LoadLayout(string path)
		{
			if (!File.Exists(path) || DataContext is not MainViewModel viewModel)
			{
				return;
			}

			var serializer = new XmlLayoutSerializer(dockManager);
			serializer.LayoutSerializationCallback += (_, args) =>
			{
				var toolbox = viewModel.LayoutService.Anchorables
					.OfType<IToolbox>()
					.FirstOrDefault(t => t.Id == args.Model.ContentId);

				// Only overwrite when this application owns the content id. Leaving the value alone
				// keeps the content the previous layout supplied, which is what restores documents.
				if (toolbox != null)
				{
					args.Content = toolbox;
				}
			};

			serializer.Deserialize(path);
		}
	}
}
