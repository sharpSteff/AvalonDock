using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using AvaloniaDemoProject.Shared;
using AvalonDock;
using AvalonDock.Layout;
using AvalonDock.Serializer.Xml;

namespace TestApp
{
	/// <summary>
	/// Avalonia port of the WPF TestApp's main window. The handlers below are those of the WPF application; the
	/// LibreWPF and DevFlow diagnostics of the WPF window are not part of it.
	/// </summary>
	public partial class MainWindow : Window, INotifyPropertyChanged
	{
		private readonly DispatcherTimer _timer;
		private int _testTimer;
		private IBrush _testBackground = Brushes.Transparent;
		private string _focusedElement = string.Empty;

		public MainWindow()
		{
			InitializeComponent();

			var rnd = new Random();
			_timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.0) };
			_timer.Tick += (s, e) =>
			{
				TestTimer++;
				TestBackground = new SolidColorBrush(Color.FromRgb((byte)rnd.Next(0, 255), (byte)rnd.Next(0, 255), (byte)rnd.Next(0, 255)));
				FocusedElement = FocusManager?.GetFocusedElement()?.ToString() ?? string.Empty;
			};
			_timer.Start();

			DataContext = this;

			dockManager.Layout.PropertyChanged += OnLayoutRootPropertyChanged;
			FindAnchorable("toolWindow1").Hiding += OnToolWindow1Hiding;
		}

		public new event PropertyChangedEventHandler? PropertyChanged;

		/// <summary>Gets or sets a counter that ticks every second; documents and tool windows bind to it.</summary>
		public int TestTimer
		{
			get => _testTimer;
			set
			{
				_testTimer = value;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TestTimer)));
			}
		}

		/// <summary>Gets or sets a background that changes every second.</summary>
		public IBrush TestBackground
		{
			get => _testBackground;
			set
			{
				_testBackground = value;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TestBackground)));
			}
		}

		/// <summary>Gets or sets a description of the element with keyboard focus.</summary>
		public string FocusedElement
		{
			get => _focusedElement;
			set
			{
				_focusedElement = value;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FocusedElement)));
			}
		}

		protected override void OnClosed(EventArgs e)
		{
			_timer.Stop();
			base.OnClosed(e);
		}

		private LayoutAnchorable FindAnchorable(string contentId)
			=> dockManager.Layout.Descendents().OfType<LayoutAnchorable>().Single(a => a.ContentId == contentId);

		private static string LayoutFilePath(object? sender)
			=> Path.Combine(AppContext.BaseDirectory, $"AvalonDock_{(sender as MenuItem)?.Header}.config");

		private void OnLayoutRootPropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == "ActiveContent")
				Debug.WriteLine(string.Format("ActiveContent-> {0}", ((LayoutRoot)sender!).ActiveContent));
		}

		private void OnLoadLayout(object? sender, RoutedEventArgs e)
		{
			var fileName = LayoutFilePath(sender);
			if (!File.Exists(fileName)) return;
			var serializer = new XmlLayoutSerializer(dockManager);
			using var stream = new StreamReader(fileName);
			serializer.Deserialize(stream);
		}

		private void OnSaveLayout(object? sender, RoutedEventArgs e)
		{
			var serializer = new XmlLayoutSerializer(dockManager);
			using var stream = new StreamWriter(LayoutFilePath(sender));
			serializer.Serialize(stream);
		}

		private void OnShowWinformsWindow(object? sender, RoutedEventArgs e) => ShowToolWindow("WinFormsWindow");

		private void OnShowToolWindow1(object? sender, RoutedEventArgs e) => ShowToolWindow("toolWindow1");

		private void ShowToolWindow(string contentId)
		{
			var anchorable = FindAnchorable(contentId);
			if (anchorable.IsHidden)
				anchorable.Show();
			else if (anchorable.IsVisible)
				anchorable.IsActive = true;
			else
				anchorable.AddToLayout(dockManager, AnchorableShowStrategy.Bottom | AnchorableShowStrategy.Most);
		}

		private void AddTwoDocuments_click(object? sender, RoutedEventArgs e)
		{
			var firstDocumentPane = dockManager.Layout.Descendents().OfType<LayoutDocumentPane>().FirstOrDefault();
			if (firstDocumentPane != null)
			{
				firstDocumentPane.Children.Add(new LayoutDocument { Title = "Test1" });
				firstDocumentPane.Children.Add(new LayoutDocument { Title = "Test2" });
			}

			var leftAnchorGroup = dockManager.Layout.LeftSide.Children.FirstOrDefault();
			if (leftAnchorGroup == null)
			{
				leftAnchorGroup = new LayoutAnchorGroup();
				dockManager.Layout.LeftSide.Children.Add(leftAnchorGroup);
			}

			leftAnchorGroup.Children.Add(new LayoutAnchorable { Title = "New Anchorable" });
		}

		private void OnFloatToolWindow1(object? sender, RoutedEventArgs e)
		{
			var toolWindow1 = FindAnchorable("toolWindow1");
			if (toolWindow1.IsHidden)
				toolWindow1.Show();
			if (toolWindow1.CanFloat && !toolWindow1.IsFloating)
				toolWindow1.Float();
		}

		private void DockManager_DocumentClosing(object? sender, DocumentClosingEventArgs e)
		{
			if (!ConfirmDialog.Ask(this, "Are you sure you want to close the document?", "AvalonDock Sample"))
				e.Cancel = true;
		}

		private void OnDumpToConsole(object? sender, RoutedEventArgs e)
		{
			// As in the WPF application: dumping the layout needs TRACE on the AvalonDock project.
		}

		private void OnUnloadManager(object? sender, RoutedEventArgs e)
		{
			if (layoutRoot.Children.Contains(dockManager))
				layoutRoot.Children.Remove(dockManager);
		}

		private void OnLoadManager(object? sender, RoutedEventArgs e)
		{
			if (!layoutRoot.Children.Contains(dockManager))
				layoutRoot.Children.Add(dockManager);
		}

		private void OnToolWindow1Hiding(object? sender, CancelEventArgs e)
		{
			if (!ConfirmDialog.Ask(this, "Are you sure you want to hide this tool?", "AvalonDock"))
				e.Cancel = true;
		}

		private void OnShowHeader(object? sender, RoutedEventArgs e)
		{
			// As in the WPF application, where the toggle is commented out.
		}

		/// <summary>
		/// Creates a new anchorable to test whether a floating window sizes itself to the control it contains;
		/// see <see cref="DockingManager.AutoWindowSizeWhenOpened"/> and <see cref="TestUserControl"/>.
		/// </summary>
		private void OnNewFloatingWindow(object? sender, RoutedEventArgs e)
		{
			var anchorable = new LayoutAnchorable
			{
				Title = "Floating window with initial usercontrol size",
				Content = new TestUserControl(),
			};
			anchorable.AddToLayout(dockManager, AnchorableShowStrategy.Most);
			anchorable.Float();
		}

		private void OnSwitchTheme(object? sender, RoutedEventArgs e)
		{
			if (sender is not MenuItem { Tag: string themeTag }) return;
			if (Application.Current != null)
				Application.Current.RequestedThemeVariant = themeTag == "Light" ? ThemeVariant.Light : ThemeVariant.Dark;
		}

		// The Edit menu acts on the focused text box, like WPF's application commands do.
		private TextBox? FocusedTextBox => FocusManager?.GetFocusedElement() as TextBox;

		private void OnUndo(object? sender, RoutedEventArgs e) => FocusedTextBox?.Undo();

		private void OnRedo(object? sender, RoutedEventArgs e) => FocusedTextBox?.Redo();

		private void OnCut(object? sender, RoutedEventArgs e) => FocusedTextBox?.Cut();

		private void OnCopy(object? sender, RoutedEventArgs e) => FocusedTextBox?.Copy();

		private void OnPaste(object? sender, RoutedEventArgs e) => FocusedTextBox?.Paste();
	}
}
