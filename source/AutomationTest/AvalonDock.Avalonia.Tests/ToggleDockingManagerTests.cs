using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using AvalonDock.Controls;
using AvalonDock.Core;
using AvalonDock.Layout;
using NUnit.Framework;

namespace AvalonDock.Avalonia.Tests
{
	[TestFixture]
	public class ToggleDockingManagerTests
	{
		internal sealed class TestToolbox : IToolbox, INotifyPropertyChanged
		{
			private bool _isOpen;

			public event PropertyChangedEventHandler PropertyChanged;

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

		internal sealed class Fixture
		{
			public Window Window;
			public ToggleDockingManager Manager;
			public Dictionary<string, LayoutAnchorable> Anchorables = new Dictionary<string, LayoutAnchorable>();
			public Dictionary<string, TestToolbox> Toolboxes = new Dictionary<string, TestToolbox>();
		}

		private static Fixture Create(params (string Name, DockZone Zone, bool OpenByDefault)[] tools)
		{
			var f = new Fixture();
			var documentPane = new LayoutDocumentPane(new LayoutDocument { Title = "Doc", Content = new TextBlock { Text = "Document content" } });
			var root = new LayoutRoot { RootPanel = new LayoutPanel(new LayoutDocumentPaneGroup(documentPane)) };
			foreach (var (name, zone, openByDefault) in tools)
			{
				var toolbox = new TestToolbox { Id = name, Title = name, Zone = zone, IsOpenByDefault = openByDefault };
				var anchorable = new LayoutAnchorable { Title = name, ContentId = name, Content = toolbox };
				var side = zone == DockZone.LeftTop || zone == DockZone.LeftBottom ? root.LeftSide
					: zone == DockZone.RightTop || zone == DockZone.RightBottom ? root.RightSide
					: root.BottomSide;
				var group = new LayoutAnchorGroup();
				side.Children.Add(group);
				group.Children.Add(anchorable);
				f.Anchorables[name] = anchorable;
				f.Toolboxes[name] = toolbox;
			}

			f.Manager = new ToggleDockingManager
			{
				Layout = root,
				LayoutItemTemplate = new global::Avalonia.Controls.Templates.FuncDataTemplate<TestToolbox>((t, _) => new TextBlock { Text = "Content of " + t.Title }),
			};
			f.Window = new Window { Width = 1000, Height = 700, Content = f.Manager };
			f.Window.Show();
			DockingManagerTests.Pump(f.Window);
			return f;
		}

		private static ToggleDockButton Button(Fixture f, string name)
			=> f.Manager.GetVisualDescendants().OfType<ToggleDockButton>().Single(b => b.Anchorable == f.Anchorables[name]);

		private static Point CenterIn(Visual visual, Visual root)
			=> visual.TranslatePoint(new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2), root).Value;

		private static void Click(Fixture f, Visual target)
		{
			var p = CenterIn(target, f.Window);
			f.Window.MouseDown(p, MouseButton.Left);
			f.Window.MouseUp(p, MouseButton.Left);
			DockingManagerTests.Pump(f.Window);
		}

		private static IEnumerable<LayoutAnchorablePaneControl> VisiblePanes(Fixture f)
			=> f.Manager.GetVisualDescendants().OfType<LayoutAnchorablePaneControl>().Where(p => p.IsEffectivelyVisible && p.Bounds.Width > 0 && p.Bounds.Height > 0);

		[AvaloniaTest]
		public void Every_Anchorable_Gets_A_Sidebar_Button_And_Starts_Collapsed()
		{
			var f = Create(("Explorer", DockZone.LeftTop, false), ("Outline", DockZone.LeftBottom, false), ("Output", DockZone.BottomLeft, false), ("Properties", DockZone.RightTop, false));
			Assert.That(f.Manager._leftTopBar.ContainsAnchorable(f.Anchorables["Explorer"]), Is.True);
			Assert.That(f.Manager._leftBottomBar.ContainsAnchorable(f.Anchorables["Outline"]), Is.True);
			Assert.That(f.Manager._bottomLeftBar.ContainsAnchorable(f.Anchorables["Output"]), Is.True);
			Assert.That(f.Manager._rightTopBar.ContainsAnchorable(f.Anchorables["Properties"]), Is.True);
			Assert.That(f.Manager.GetVisualDescendants().OfType<ToggleDockButton>().Count(b => b.IsEffectivelyVisible), Is.EqualTo(4));
			Assert.That(f.Anchorables.Values.All(a => a.IsAutoHidden), Is.True);
			Assert.That(VisiblePanes(f), Is.Empty);
			Assert.That(f.Manager.GetVisualDescendants().OfType<LayoutAnchorControl>().Any(a => a.IsEffectivelyVisible), Is.False, "the classic auto-hide anchors are replaced by the bars");
		}

		[AvaloniaTest]
		public void IsOpenByDefault_Docks_The_Toolbox_When_Loaded()
		{
			var f = Create(("Explorer", DockZone.LeftTop, true), ("Output", DockZone.BottomLeft, false));
			Assert.That(f.Anchorables["Explorer"].IsAutoHidden, Is.False);
			Assert.That(f.Toolboxes["Explorer"].IsOpen, Is.True);
			Assert.That(f.Toolboxes["Output"].IsOpen, Is.False);
			Assert.That(VisiblePanes(f).Count(), Is.EqualTo(1));
			Assert.That(DockingManagerTests.ShowsText(f.Manager, "Content of Explorer"), Is.True);
			Assert.That(DockingManagerTests.ShowsText(f.Manager, "Document content"), Is.True, "LayoutItemTemplate does not apply to content that is a control");
		}

		[AvaloniaTest]
		public void Clicking_A_Button_Docks_The_Anchorable_And_Clicking_Again_Collapses_It()
		{
			var f = Create(("Explorer", DockZone.LeftTop, false), ("Output", DockZone.BottomLeft, false));
			Click(f, Button(f, "Explorer"));
			var explorer = f.Anchorables["Explorer"];
			Assert.That(explorer.IsAutoHidden, Is.False);
			Assert.That(f.Toolboxes["Explorer"].IsOpen, Is.True);
			Assert.That(Button(f, "Explorer").IsChecked, Is.True);
			Assert.That(Button(f, "Explorer").IsAnchorableFocused, Is.True);
			var pane = VisiblePanes(f).Single();
			Assert.That(pane.Bounds.Width, Is.EqualTo(250).Within(2), "DefaultDockWidth");
			Assert.That(pane.TranslatePoint(default, f.Manager).Value.X, Is.LessThan(60), "docked on the left");
			Assert.That(pane.GetVisualDescendants().OfType<ToggleAnchorablePaneTitle>().Any(t => t.IsEffectivelyVisible), Is.True);
			Assert.That(DockingManagerTests.ShowsText(f.Manager, "Content of Explorer"), Is.True);

			Click(f, Button(f, "Explorer"));
			Assert.That(explorer.IsAutoHidden, Is.True);
			Assert.That(f.Toolboxes["Explorer"].IsOpen, Is.False);
			Assert.That(Button(f, "Explorer").IsChecked, Is.False);
			Assert.That(VisiblePanes(f), Is.Empty);
		}

		[AvaloniaTest]
		public void Opening_A_Sibling_Of_The_Same_Bar_Collapses_The_Open_One()
		{
			var f = Create(("Explorer", DockZone.LeftTop, false), ("Search", DockZone.LeftTop, false));
			Click(f, Button(f, "Explorer"));
			Click(f, Button(f, "Search"));
			Assert.That(f.Anchorables["Search"].IsAutoHidden, Is.False);
			Assert.That(f.Anchorables["Explorer"].IsAutoHidden, Is.True);
			Assert.That(f.Toolboxes["Explorer"].IsOpen, Is.False);
			Assert.That(VisiblePanes(f).Count(), Is.EqualTo(1));
		}

		[AvaloniaTest]
		public void Setting_IsOpen_On_The_Toolbox_Toggles_The_Anchorable()
		{
			var f = Create(("Output", DockZone.BottomLeft, false));
			f.Toolboxes["Output"].IsOpen = true;
			DockingManagerTests.Pump(f.Window);
			Assert.That(f.Anchorables["Output"].IsAutoHidden, Is.False);
			var pane = VisiblePanes(f).Single();
			Assert.That(pane.Bounds.Height, Is.EqualTo(200).Within(2), "DefaultDockHeight");

			f.Toolboxes["Output"].IsOpen = false;
			DockingManagerTests.Pump(f.Window);
			Assert.That(f.Anchorables["Output"].IsAutoHidden, Is.True);
		}

		[AvaloniaTest]
		public void The_Minimize_Button_Of_The_Title_Collapses_The_Anchorable()
		{
			var f = Create(("Explorer", DockZone.LeftTop, true));
			var minimize = f.Manager.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "PART_MinimizeButton" && b.IsEffectivelyVisible);
			Click(f, minimize);
			Assert.That(f.Anchorables["Explorer"].IsAutoHidden, Is.True);
			Assert.That(f.Toolboxes["Explorer"].IsOpen, Is.False);
		}

		[AvaloniaTest]
		public void Dragging_A_Button_Onto_Another_Zone_Moves_The_Anchorable_There()
		{
			var f = Create(("Explorer", DockZone.LeftTop, false), ("Properties", DockZone.RightTop, false));
			var from = CenterIn(Button(f, "Explorer"), f.Window);
			f.Window.MouseDown(from, MouseButton.Left);
			f.Window.MouseMove(from + new Point(10, 10), RawInputModifiers.LeftMouseButton);
			DockingManagerTests.Pump(f.Window);
			var overlay = ToggleDockDragOverlay.Current;
			Assert.That(overlay, Is.Not.Null, "dragging a button shows the zone overlay");
			var zone = overlay.DropZones.First(z => z.Zone == DockZone.RightTop && z.Label != null);
			var target = f.Manager.TranslatePoint(zone.Rect.Center, f.Window).Value;
			f.Window.MouseMove(target, RawInputModifiers.LeftMouseButton);
			f.Window.MouseUp(target, MouseButton.Left);
			DockingManagerTests.Pump(f.Window);

			Assert.That(ToggleDockDragOverlay.Current, Is.Null);
			Assert.That(f.Manager._rightTopBar.ContainsAnchorable(f.Anchorables["Explorer"]), Is.True);
			Assert.That(f.Manager._leftTopBar.ContainsAnchorable(f.Anchorables["Explorer"]), Is.False);
			Assert.That(f.Anchorables["Explorer"].IsAutoHidden, Is.False, "moved and docked");
			var pane = VisiblePanes(f).Single();
			Assert.That(pane.TranslatePoint(default, f.Manager).Value.X, Is.GreaterThan(500), "docked on the right");
		}

		[AvaloniaTest]
		public void Hidden_Anchorables_Can_Be_Restored()
		{
			var f = Create(("Explorer", DockZone.LeftTop, false));
			var explorer = f.Anchorables["Explorer"];
			explorer.Hide();
			f.Manager.RemoveButtonFromAllBars(explorer);
			DockingManagerTests.Pump(f.Window);
			Assert.That(f.Manager.Layout.Hidden, Does.Contain(explorer));

			f.Manager.RestoreHiddenAnchorable(explorer);
			DockingManagerTests.Pump(f.Window);
			Assert.That(f.Manager._leftTopBar.ContainsAnchorable(explorer), Is.True);
			Assert.That(explorer.IsAutoHidden, Is.False);
			Assert.That(VisiblePanes(f).Count(), Is.EqualTo(1));
		}
	}
}
