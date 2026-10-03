using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using AvalonDock.Controls;
using AvalonDock.Layout;
using NUnit.Framework;

namespace AvalonDock.Avalonia.Tests
{
	/// <summary>Drives the docking controls with (headless) mouse input, the way a user would.</summary>
	[TestFixture]
	public class PointerTests
	{
		private static Point CenterIn(Visual visual, Visual root)
			=> visual.TranslatePoint(new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2), root).Value;

		[AvaloniaTest]
		public void Clicking_A_Document_Tab_Selects_It()
		{
			var f = DockingManagerTests.Create();
			var tab = f.Manager.GetVisualDescendants().OfType<LayoutDocumentTabItem>().Single(t => t.Model == f.Doc2);
			var p = CenterIn(tab, f.Window);
			f.Window.MouseDown(p, MouseButton.Left);
			f.Window.MouseUp(p, MouseButton.Left);
			DockingManagerTests.Pump(f.Window);
			Assert.That(f.Doc2.IsSelected, Is.True);
			Assert.That(f.Doc2.IsActive, Is.True);
			Assert.That(DockingManagerTests.ShowsText(f.Manager, "Content of doc 2"), Is.True);
		}

		[AvaloniaTest]
		public void Dragging_A_Document_Tab_Out_Of_The_Strip_Floats_It()
		{
			var f = DockingManagerTests.Create();
			var tab = f.Manager.GetVisualDescendants().OfType<LayoutDocumentTabItem>().Single(t => t.Model == f.Doc2);
			var p = CenterIn(tab, f.Window);
			f.Window.MouseDown(p, MouseButton.Left);
			f.Window.MouseMove(p + new Point(5, 40), RawInputModifiers.LeftMouseButton);
			f.Window.MouseMove(p + new Point(30, 250), RawInputModifiers.LeftMouseButton);
			DockingManagerTests.Pump(f.Window);
			var fw = f.Manager.FloatingWindows.SingleOrDefault();
			Assert.That(fw, Is.InstanceOf<LayoutDocumentFloatingWindowControl>(), "tearing the tab out creates a floating window");
			Assert.That(fw.IsDragging, Is.True, "the floating window follows the pointer");

			f.Window.MouseMove(p + new Point(60, 300), RawInputModifiers.LeftMouseButton);
			f.Window.MouseUp(p + new Point(60, 300), MouseButton.Left);
			DockingManagerTests.Pump(f.Window);
			Assert.That(fw.IsDragging, Is.False);
			Assert.That(f.Doc2.IsFloating, Is.True);
			Assert.That(f.DocumentPane.Children, Does.Not.Contain(f.Doc2));
		}

		[AvaloniaTest]
		public void Dragging_A_Document_Tab_Along_The_Strip_Reorders_It()
		{
			var f = DockingManagerTests.Create();
			var tab1 = f.Manager.GetVisualDescendants().OfType<LayoutDocumentTabItem>().Single(t => t.Model == f.Doc1);
			var tab2 = f.Manager.GetVisualDescendants().OfType<LayoutDocumentTabItem>().Single(t => t.Model == f.Doc2);
			var from = CenterIn(tab2, f.Window);
			var to = CenterIn(tab1, f.Window);
			f.Window.MouseDown(from, MouseButton.Left);
			f.Window.MouseMove(new Point((from.X + to.X) / 2, from.Y), RawInputModifiers.LeftMouseButton);
			f.Window.MouseMove(to, RawInputModifiers.LeftMouseButton);
			f.Window.MouseUp(to, MouseButton.Left);
			DockingManagerTests.Pump(f.Window);
			Assert.That(f.DocumentPane.Children.IndexOf(f.Doc2), Is.EqualTo(0));
			Assert.That(f.Manager.FloatingWindows, Is.Empty);
		}

		[AvaloniaTest]
		public void Dragging_A_Splitter_Resizes_The_Panes()
		{
			var f = DockingManagerTests.Create();
			var splitter = f.Manager.GetVisualDescendants().OfType<LayoutGridResizerControl>().Single(s => s.IsEffectivelyVisible);
			var p = CenterIn(splitter, f.Window);
			f.Window.MouseDown(p, MouseButton.Left);
			f.Window.MouseMove(p + new Point(50, 0), RawInputModifiers.LeftMouseButton);
			f.Window.MouseMove(p + new Point(100, 0), RawInputModifiers.LeftMouseButton);
			f.Window.MouseUp(p + new Point(100, 0), MouseButton.Left);
			DockingManagerTests.Pump(f.Window);
			var toolPane = f.Manager.GetVisualDescendants().OfType<LayoutAnchorablePaneControl>().Single();
			Assert.That(toolPane.Bounds.Width, Is.EqualTo(300).Within(2));
			Assert.That(f.AnchorablePane.DockWidth.Value, Is.EqualTo(300).Within(2));
		}

		[AvaloniaTest]
		public void Clicking_An_Anchor_Shows_The_AutoHide_Flyout_And_Its_Pin_Docks_It()
		{
			var f = DockingManagerTests.Create();
			f.Tool1.ToggleAutoHide();
			DockingManagerTests.Pump(f.Window);
			var anchor = f.Manager.GetVisualDescendants().OfType<LayoutAnchorControl>().Single(a => a.Model == f.Tool1);
			var p = CenterIn(anchor, f.Window);
			f.Window.MouseDown(p, MouseButton.Left);
			f.Window.MouseUp(p, MouseButton.Left);
			DockingManagerTests.Pump(f.Window);
			Assert.That(f.Manager.AutoHideWindow.IsVisible, Is.True);

			var pin = f.Manager.AutoHideWindow.GetVisualDescendants().OfType<global::Avalonia.Controls.Button>().Single(b => b.Name == "PART_AutoHidePin");
			var pp = CenterIn(pin, f.Window);
			f.Window.MouseDown(pp, MouseButton.Left);
			f.Window.MouseUp(pp, MouseButton.Left);
			DockingManagerTests.Pump(f.Window);
			Assert.That(f.Tool1.IsAutoHidden, Is.False);
			Assert.That(f.Tool1.Parent, Is.InstanceOf<LayoutAnchorablePane>());
		}

		[AvaloniaTest]
		public void Dragging_An_Anchorable_Tab_Along_The_Strip_Reorders_It()
		{
			var f = DockingManagerTests.Create();
			var tab1 = f.Manager.GetVisualDescendants().OfType<LayoutAnchorableTabItem>().Single(t => t.Model == f.Tool1);
			var tab2 = f.Manager.GetVisualDescendants().OfType<LayoutAnchorableTabItem>().Single(t => t.Model == f.Tool2);
			var from = CenterIn(tab2, f.Window);
			var to = CenterIn(tab1, f.Window);
			f.Window.MouseDown(from, MouseButton.Left);
			f.Window.MouseMove(new Point((from.X + to.X) / 2, from.Y), RawInputModifiers.LeftMouseButton);
			f.Window.MouseMove(to, RawInputModifiers.LeftMouseButton);
			f.Window.MouseMove(to + new Point(-2, 0), RawInputModifiers.LeftMouseButton);
			f.Window.MouseUp(to, MouseButton.Left);
			DockingManagerTests.Pump(f.Window);
			Assert.That(f.AnchorablePane.Children.IndexOf(f.Tool2), Is.EqualTo(0));
			Assert.That(f.Manager.FloatingWindows, Is.Empty);
		}

		[AvaloniaTest]
		public void Tearing_Out_The_Last_Document_Of_A_Pane_Collapses_The_Group_Without_Errors()
		{
			// The sequence that crashed on X11: a document docked to the left of the other through the drop
			// targets, then the other one torn out - which empties its pane and collapses the group while the
			// drag is starting.
			var f = DockingManagerTests.Create();
			var tab2 = f.Manager.GetVisualDescendants().OfType<LayoutDocumentTabItem>().Single(t => t.Model == f.Doc2);
			var p = CenterIn(tab2, f.Window);
			f.Window.MouseDown(p, MouseButton.Left);
			f.Window.MouseMove(p + new Point(5, 40), RawInputModifiers.LeftMouseButton);
			f.Window.MouseMove(p + new Point(50, 250), RawInputModifiers.LeftMouseButton);
			DockingManagerTests.Pump(f.Window);
			var fw = f.Manager.FloatingWindows.Single();
			var left = fw.CurrentDragService.CurrentOverlayWindow is OverlayWindow overlay
				? overlay.GetVisualDescendants().OfType<Control>().Single(c => c.Name == "PART_DocumentPaneDropTargetLeft")
				: null;
			Assert.That(left?.IsEffectivelyVisible, Is.True, "the document pane drop targets are shown");
			var target = f.Window.PointToClient(new PixelPoint((int)left.GetScreenArea().Center.X, (int)left.GetScreenArea().Center.Y));
			f.Window.MouseMove(target, RawInputModifiers.LeftMouseButton);
			f.Window.MouseUp(target, MouseButton.Left);
			DockingManagerTests.Pump(f.Window);
			Assert.That(f.Doc2.IsFloating, Is.False, "docked to the left");
			Assert.That(f.Manager.GetVisualDescendants().OfType<LayoutDocumentPaneControl>().Count(c => c.IsEffectivelyVisible), Is.EqualTo(2));

			var tab1 = f.Manager.GetVisualDescendants().OfType<LayoutDocumentTabItem>().Single(t => t.Model == f.Doc1);
			p = CenterIn(tab1, f.Window);
			f.Window.MouseDown(p, MouseButton.Left);
			f.Window.MouseMove(p + new Point(5, 40), RawInputModifiers.LeftMouseButton);
			f.Window.MouseMove(p + new Point(100, 250), RawInputModifiers.LeftMouseButton);
			DockingManagerTests.Pump(f.Window);
			f.Window.MouseMove(p + new Point(110, 260), RawInputModifiers.LeftMouseButton);
			DockingManagerTests.Pump(f.Window);
			f.Window.MouseUp(p + new Point(110, 260), MouseButton.Left);
			DockingManagerTests.Pump(f.Window);

			Assert.That(f.Doc1.IsFloating || f.Doc1.Parent != null, Is.True);
			Assert.That(f.Manager.GetVisualDescendants().OfType<LayoutDocumentPaneControl>().Count(c => c.IsEffectivelyVisible), Is.GreaterThanOrEqualTo(1));
		}
	}
}
