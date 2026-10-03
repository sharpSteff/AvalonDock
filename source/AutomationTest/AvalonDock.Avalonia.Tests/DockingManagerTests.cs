using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvalonDock.Controls;
using AvalonDock.Layout;
using NUnit.Framework;

namespace AvalonDock.Avalonia.Tests
{
	[TestFixture]
	public class DockingManagerTests
	{
		internal sealed class Fixture
		{
			public Window Window;
			public DockingManager Manager;
			public LayoutDocumentPane DocumentPane;
			public LayoutAnchorablePane AnchorablePane;
			public LayoutDocument Doc1;
			public LayoutDocument Doc2;
			public LayoutAnchorable Tool1;
			public LayoutAnchorable Tool2;
		}

		internal static Fixture Create()
		{
			var f = new Fixture();
			f.Doc1 = new LayoutDocument { Title = "Doc 1", ContentId = "doc1", Content = new TextBlock { Text = "Content of doc 1" } };
			f.Doc2 = new LayoutDocument { Title = "Doc 2", ContentId = "doc2", Content = new TextBlock { Text = "Content of doc 2" } };
			f.Tool1 = new LayoutAnchorable { Title = "Tool 1", ContentId = "tool1", Content = new TextBlock { Text = "Content of tool 1" } };
			f.Tool2 = new LayoutAnchorable { Title = "Tool 2", ContentId = "tool2", Content = new TextBlock { Text = "Content of tool 2" } };
			f.DocumentPane = new LayoutDocumentPane();
			f.DocumentPane.Children.Add(f.Doc1);
			f.DocumentPane.Children.Add(f.Doc2);
			f.AnchorablePane = new LayoutAnchorablePane { DockWidth = new GridLength(200) };
			f.AnchorablePane.Children.Add(f.Tool1);
			f.AnchorablePane.Children.Add(f.Tool2);
			var panel = new LayoutPanel { Orientation = global::Avalonia.Layout.Orientation.Horizontal };
			panel.Children.Add(f.AnchorablePane);
			panel.Children.Add(new LayoutDocumentPaneGroup(f.DocumentPane));
			f.Manager = new DockingManager { Layout = new LayoutRoot { RootPanel = panel } };
			f.Window = new Window { Width = 1000, Height = 700, Content = f.Manager };
			f.Window.Show();
			Pump(f.Window);
			return f;
		}

		internal static void Pump(Window window)
		{
			for (var i = 0; i < 3; i++)
			{
				Dispatcher.UIThread.RunJobs();
				window.UpdateLayout();
			}
		}

		internal static bool ShowsText(Visual root, string text)
			=> root.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == text && t.IsEffectivelyVisible);

		[AvaloniaTest]
		public void Layout_Is_Rendered_With_Panes_Tabs_And_Selected_Content()
		{
			var f = Create();
			var docPane = f.Manager.GetVisualDescendants().OfType<LayoutDocumentPaneControl>().Single();
			var toolPane = f.Manager.GetVisualDescendants().OfType<LayoutAnchorablePaneControl>().Single();
			Assert.That(docPane.Bounds.Width, Is.GreaterThan(500));
			Assert.That(toolPane.Bounds.Width, Is.EqualTo(200).Within(1));

			Assert.That(f.Manager.GetVisualDescendants().OfType<LayoutDocumentTabItem>().Count(), Is.EqualTo(2));
			Assert.That(f.Manager.GetVisualDescendants().OfType<LayoutAnchorableTabItem>().Count(), Is.EqualTo(2));
			Assert.That(ShowsText(f.Manager, "Doc 1"), Is.True, "document tab header");
			Assert.That(ShowsText(f.Manager, "Content of doc 1"), Is.True, "selected document content");
			Assert.That(ShowsText(f.Manager, "Content of doc 2"), Is.False);
			Assert.That(ShowsText(f.Manager, "Content of tool 1"), Is.True, "selected anchorable content");
			Assert.That(f.Manager.GetVisualDescendants().OfType<AnchorablePaneTitle>().Any(t => t.IsEffectivelyVisible), Is.True);
		}

		[AvaloniaTest]
		public void Selecting_A_Document_In_The_Model_Switches_The_Shown_Content()
		{
			var f = Create();
			f.Doc2.IsSelected = true;
			Pump(f.Window);
			Assert.That(ShowsText(f.Manager, "Content of doc 2"), Is.True);
			Assert.That(ShowsText(f.Manager, "Content of doc 1"), Is.False);

			f.Doc1.IsActive = true;
			Pump(f.Window);
			Assert.That(f.Manager.ActiveContent, Is.SameAs(f.Doc1.Content));
			Assert.That(ShowsText(f.Manager, "Content of doc 1"), Is.True);
		}

		[AvaloniaTest]
		public void Closing_A_Document_Through_Its_LayoutItem_Removes_It()
		{
			var f = Create();
			var item = f.Manager.GetLayoutItemFromModel(f.Doc1);
			Assert.That(item.CloseCommand.CanExecute(null), Is.True);
			item.CloseCommand.Execute(null);
			Pump(f.Window);
			Assert.That(f.DocumentPane.Children, Does.Not.Contain(f.Doc1));
			Assert.That(f.Manager.GetVisualDescendants().OfType<LayoutDocumentTabItem>().Count(), Is.EqualTo(1));
			Assert.That(ShowsText(f.Manager, "Content of doc 2"), Is.True);
		}

		[AvaloniaTest]
		public void Floating_An_Anchorable_Opens_A_Floating_Window_And_Docking_Returns_It()
		{
			var f = Create();
			f.Tool1.Float();
			Pump(f.Window);
			var fw = f.Manager.FloatingWindows.Single();
			Assert.That(fw, Is.InstanceOf<LayoutAnchorableFloatingWindowControl>());
			Assert.That(fw.IsVisible, Is.True);
			Pump(fw);
			Assert.That(ShowsText(fw, "Content of tool 1"), Is.True, "content moved into the floating window");
			Assert.That(ShowsText(fw, "Tool 1"), Is.True, "caption title");

			f.Tool1.Dock();
			Pump(f.Window);
			Assert.That(f.Manager.FloatingWindows, Is.Empty);
			Assert.That(f.Tool1.Parent, Is.InstanceOf<LayoutAnchorablePane>());
			Assert.That(f.Manager.Layout.FloatingWindows, Is.Empty);
		}

		[AvaloniaTest]
		public void AutoHide_Moves_The_Anchorable_To_A_Side_And_Shows_An_Anchor()
		{
			var f = Create();
			f.Tool1.ToggleAutoHide();
			Pump(f.Window);
			Assert.That(f.Tool1.IsAutoHidden, Is.True);
			var anchors = f.Manager.GetVisualDescendants().OfType<LayoutAnchorControl>().ToList();
			Assert.That(anchors.Count, Is.EqualTo(2), "both anchorables of the pane are auto-hidden together");
			Assert.That(anchors.All(a => a.IsEffectivelyVisible), Is.True);
			// Like in WPF the emptied pane stays in the layout as the place to restore to, but it is hidden.
			Assert.That(f.Manager.GetVisualDescendants().OfType<LayoutAnchorablePaneControl>().Where(p => p.IsEffectivelyVisible), Is.Empty);

			f.Tool1.IsActive = true;
			Pump(f.Window);
			Assert.That(f.Manager.AutoHideWindow.IsVisible, Is.True, "activating an auto-hidden anchorable shows the flyout");
			Assert.That(ShowsText(f.Manager.AutoHideWindow, "Content of tool 1"), Is.True);

			f.Tool1.ToggleAutoHide();
			Pump(f.Window);
			Assert.That(f.Tool1.IsAutoHidden, Is.False);
			Assert.That(f.Manager.GetVisualDescendants().OfType<LayoutAnchorControl>(), Is.Empty);
			Assert.That(f.Manager.GetVisualDescendants().OfType<LayoutAnchorablePaneControl>().Count(p => p.IsEffectivelyVisible), Is.EqualTo(1));
		}

		[AvaloniaTest]
		public void Dragging_A_Floating_Window_Onto_The_Document_Pane_Docks_It()
		{
			var f = Create();
			f.Tool2.Float();
			Pump(f.Window);
			var fw = f.Manager.FloatingWindows.Single();
			Pump(fw);

			var docPane = f.Manager.GetVisualDescendants().OfType<LayoutDocumentPaneControl>().Single();
			var area = docPane.GetScreenArea();
			var center = area.Center;

			fw.BeginProgrammaticDrag(new Point(center.X - 300, center.Y - 300));
			fw.DragTo(new Point(center.X - 10, center.Y - 10));
			fw.DragTo(center);
			Pump(f.Window);

			// The "into" indicator of the document pane is in the centre of the pane.
			var docked = fw.EndDrag(true);
			Pump(f.Window);
			Assert.That(docked, Is.True, "the drop target under the pointer accepted the window");
			Assert.That(f.Tool2.Parent, Is.SameAs(f.DocumentPane));
			Dispatcher.UIThread.RunJobs();
			Assert.That(f.Manager.Layout.FloatingWindows, Is.Empty);
		}

		[AvaloniaTest]
		public void Replacing_The_Layout_Releases_The_Controls_Of_The_Old_Layout()
		{
			var f = Create();
			var oldPane = CollectPaneReference(f);
			f.Doc1 = f.Doc2 = null;
			f.Tool1 = f.Tool2 = null;
			f.DocumentPane = null;
			f.AnchorablePane = null;
			f.Manager.Layout = new LayoutRoot();
			Pump(f.Window);
			for (var i = 0; i < 3; i++)
			{
				System.GC.Collect();
				System.GC.WaitForPendingFinalizers();
				Dispatcher.UIThread.RunJobs();
			}

			Assert.That(oldPane.TryGetTarget(out _), Is.False, "the manager must not keep pane controls of a discarded layout alive");
		}

		[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
		private static System.WeakReference<LayoutDocumentPaneControl> CollectPaneReference(Fixture f)
			=> new System.WeakReference<LayoutDocumentPaneControl>(f.Manager.GetVisualDescendants().OfType<LayoutDocumentPaneControl>().Single());
	}
}
