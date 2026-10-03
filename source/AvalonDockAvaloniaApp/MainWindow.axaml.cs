using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using AvalonDock;
using AvalonDock.Core;
using AvalonDock.Layout;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Styling;

namespace AvalonDockAvaloniaApp
{
	public partial class MainWindow : Window
	{
		private DockingManager _manager;
		private int _documentCount;

		public MainWindow()
		{
			InitializeComponent();
			ShowClassic();
		}

		private void OnClassicClick(object sender, RoutedEventArgs e) => ShowClassic();

		private void OnToggleClick(object sender, RoutedEventArgs e) => ShowToggle();

		private void OnLightClick(object sender, RoutedEventArgs e) => Application.Current.RequestedThemeVariant = ThemeVariant.Light;

		private void OnDarkClick(object sender, RoutedEventArgs e) => Application.Current.RequestedThemeVariant = ThemeVariant.Dark;

		private void OnNewDocumentClick(object sender, RoutedEventArgs e)
		{
			var pane = _manager?.Layout?.LastFocusedDocument?.Parent as LayoutDocumentPane
				?? _manager?.Layout?.Descendents().OfType<LayoutDocumentPane>().FirstOrDefault();
			if (pane == null) return;
			var document = CreateDocument();
			pane.Children.Add(document);
			document.IsActive = true;
		}

		/// <summary>The classic manager: tool panes around a document area, with auto-hide sides and floating windows.</summary>
		private void ShowClassic()
		{
			var documents = new LayoutDocumentPane();
			documents.Children.Add(CreateDocument());
			documents.Children.Add(CreateDocument());

			var explorer = new LayoutAnchorablePane { DockWidth = new GridLength(220) };
			explorer.Children.Add(CreateTool("Explorer", "Solution items would be listed here."));
			explorer.Children.Add(CreateTool("Search", "Search results would be listed here."));

			var properties = new LayoutAnchorablePane { DockWidth = new GridLength(240) };
			properties.Children.Add(CreateTool("Properties", "Properties of the selection."));

			var output = new LayoutAnchorablePane { DockHeight = new GridLength(160) };
			output.Children.Add(CreateTool("Output", "Build output."));
			output.Children.Add(CreateTool("Error List", "No errors."));

			var center = new LayoutPanel { Orientation = Avalonia.Layout.Orientation.Vertical };
			center.Children.Add(new LayoutDocumentPaneGroup(documents));
			center.Children.Add(output);

			var root = new LayoutPanel { Orientation = Avalonia.Layout.Orientation.Horizontal };
			root.Children.Add(explorer);
			root.Children.Add(center);
			root.Children.Add(properties);

			var layout = new LayoutRoot { RootPanel = root };
			var toolbox = new LayoutAnchorGroup();
			toolbox.Children.Add(CreateTool("Toolbox", "Auto-hidden: hover or click the tab on the left."));
			layout.LeftSide.Children.Add(toolbox);

			Show(new DockingManager { Layout = layout });
		}

		/// <summary>The toggle manager: every tool window has a sidebar button that docks it into its zone.</summary>
		private void ShowToggle()
		{
			var documents = new LayoutDocumentPane();
			documents.Children.Add(CreateDocument());

			var layout = new LayoutRoot { RootPanel = new LayoutPanel(new LayoutDocumentPaneGroup(documents)) };
			AddToolbox(layout, new SampleToolbox("Explorer", DockZone.LeftTop) { IsOpenByDefault = true, Shortcut = "Ctrl+Alt+L" });
			AddToolbox(layout, new SampleToolbox("Search", DockZone.LeftTop) { Shortcut = "Ctrl+Alt+F" });
			AddToolbox(layout, new SampleToolbox("Outline", DockZone.LeftBottom));
			AddToolbox(layout, new SampleToolbox("Properties", DockZone.RightTop) { IsOpenByDefault = true });
			AddToolbox(layout, new SampleToolbox("Notifications", DockZone.RightBottom));
			AddToolbox(layout, new SampleToolbox("Terminal", DockZone.BottomLeft) { Shortcut = "Ctrl+Alt+T" });
			AddToolbox(layout, new SampleToolbox("Problems", DockZone.BottomRight));

			Show(new ToggleDockingManager
			{
				Layout = layout,
				LayoutItemTemplate = new FuncDataTemplate<SampleToolbox>((toolbox, _) => new TextBlock
				{
					Text = toolbox.Description,
					Margin = new Thickness(8),
					TextWrapping = Avalonia.Media.TextWrapping.Wrap,
				}),
			});
		}

		private static void AddToolbox(LayoutRoot layout, SampleToolbox toolbox)
		{
			var side = toolbox.Zone switch
			{
				DockZone.LeftTop or DockZone.LeftBottom => layout.LeftSide,
				DockZone.RightTop or DockZone.RightBottom => layout.RightSide,
				_ => layout.BottomSide,
			};
			var group = new LayoutAnchorGroup();
			side.Children.Add(group);
			group.Children.Add(new LayoutAnchorable { Title = toolbox.Title, ContentId = toolbox.Id, Content = toolbox });
		}

		private static LayoutAnchorable CreateTool(string title, string text)
			=> new LayoutAnchorable { Title = title, ContentId = title, Content = new TextBlock { Text = text, Margin = new Thickness(8), TextWrapping = Avalonia.Media.TextWrapping.Wrap } };

		private LayoutDocument CreateDocument()
		{
			var number = ++_documentCount;
			return new LayoutDocument
			{
				Title = $"Document {number}",
				ContentId = $"document{number}",
				Content = new TextBox
				{
					AcceptsReturn = true,
					Text = $"Document {number}\n\nDrag this tab out to float it, then drop it onto the arrows that appear over a pane.",
					BorderThickness = default,
				},
			};
		}

		private void Show(DockingManager manager)
		{
			_manager = manager;
			this.FindControl<ContentControl>("Host").Content = manager;
		}

		/// <summary>A minimal tool window view model for the toggle manager.</summary>
		private sealed class SampleToolbox : IToolbox, INotifyPropertyChanged
		{
			private bool _isOpen;

			public SampleToolbox(string title, DockZone zone)
			{
				Id = title;
				Title = title;
				Zone = zone;
				Description = $"{title} - docked in the {zone} zone. Click its sidebar button to collapse it, or drag the button to another zone.";
			}

			public event PropertyChangedEventHandler PropertyChanged;

			public string Description { get; }

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

			public object Icon { get; set; }

			public bool IsOpen
			{
				get => _isOpen;
				set
				{
					if (_isOpen == value) return;
					_isOpen = value;
					OnPropertyChanged();
				}
			}

			public bool OnClose() => true;

			public void OnSelected()
			{
			}

			private void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
		}
	}
}