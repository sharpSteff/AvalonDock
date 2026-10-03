using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using AvalonDock.Core;
using AvalonDock.Layout;

namespace AvalonDock.Controls
{
	/// <summary>
	/// Shows the zones of a <see cref="ToggleDockingManager"/> while an anchorable is dragged from its sidebar
	/// button or its title, and moves the anchorable into the zone it is dropped on.
	/// </summary>
	/// <remarks>
	/// The WPF library uses a transparent topmost window that captures the mouse. Here the overlay is a control
	/// in the overlay layer of the manager's window - transparent windows are not available everywhere - and the
	/// pointer is captured by the top level, which does not move during the drag. All coordinates are in the
	/// coordinate space of the manager, which the overlay covers exactly.
	/// </remarks>
	internal sealed class ToggleDockDragOverlay : Control
	{
		private static readonly IBrush NormalBrush = new SolidColorBrush(Color.FromArgb(0x30, 0x00, 0x7A, 0xCC));
		private static readonly IBrush HoverBrush = new SolidColorBrush(Color.FromArgb(0x60, 0x00, 0x7A, 0xCC));
		private static readonly IBrush BarHoverBrush = new SolidColorBrush(Color.FromArgb(0x20, 0x00, 0x7A, 0xCC));
		private static readonly IBrush LineBrush = new SolidColorBrush(Color.FromArgb(0xCC, 0x00, 0x7A, 0xCC));
		private static readonly IPen BorderPen = new Pen(new SolidColorBrush(Color.FromArgb(0x80, 0x00, 0x7A, 0xCC)), 1.5);
		private static readonly IPen LinePen = new Pen(LineBrush, 3, lineCap: PenLineCap.Round);
		private static readonly IPen GhostPen = new Pen(new SolidColorBrush(Color.FromRgb(0x00, 0x7A, 0xCC)), 1);

		private readonly LayoutAnchorable _sourceAnchorable;
		private readonly ToggleDockingManager _manager;
		private readonly List<DropZone> _dropZones = new List<DropZone>();
		private readonly IBrush _ghostBrush;
		private readonly Size _ghostSize;

		private IPointer _pointer;
		private TopLevel _captureTarget;
		private Point _pointerPosition;
		private bool _isClosed;

		private ToggleDockDragOverlay(LayoutAnchorable anchorable, Visual ghostVisual, Size ghostSize, ToggleDockingManager manager)
		{
			_sourceAnchorable = anchorable;
			_manager = manager;
			_ghostBrush = ghostVisual == null ? null : new VisualBrush(ghostVisual) { Opacity = 0.6 };
			_ghostSize = ghostSize;
			IsHitTestVisible = false;
			ClipToBounds = true;
		}

		/// <summary>Gets the overlay of the drag in progress, if any; used by tests.</summary>
		internal static ToggleDockDragOverlay Current { get; private set; }

		/// <summary>Gets the zones the anchorable can be dropped on, in manager coordinates.</summary>
		internal IReadOnlyList<DropZone> DropZones => _dropZones;

		/// <summary>A zone the anchorable can be dropped on.</summary>
		internal struct DropZone
		{
			/// <summary>The area, in manager coordinates.</summary>
			public Rect Rect;

			/// <summary>The zone.</summary>
			public DockZone Zone;

			/// <summary>The label shown in the area; <see langword="null"/> for the areas over the sidebars.</summary>
			public string Label;

			/// <summary>The start of the line marking where the button is inserted.</summary>
			public Point? InsertionLineStart;

			/// <summary>The end of the line marking where the button is inserted.</summary>
			public Point? InsertionLineEnd;
		}

		/// <summary>Starts dragging an anchorable from its sidebar button.</summary>
		/// <param name="anchorable">The anchorable.</param>
		/// <param name="source">The button, drawn as a ghost under the pointer.</param>
		/// <param name="ghostSize">The size of the ghost.</param>
		/// <param name="manager">The manager.</param>
		/// <param name="e">The pointer event that started the drag.</param>
		internal static void StartDrag(LayoutAnchorable anchorable, Visual source, Size ghostSize, ToggleDockingManager manager, PointerEventArgs e)
		{
			if (anchorable == null || manager == null || e == null) return;
			new ToggleDockDragOverlay(anchorable, source, ghostSize, manager).Begin(e.Pointer, e.GetPosition(manager));
		}

		/// <summary>Starts dragging an anchorable from its title.</summary>
		/// <param name="anchorable">The anchorable.</param>
		/// <param name="manager">The manager.</param>
		/// <param name="dragStart">The drag that was started on the title.</param>
		/// <returns><see langword="true"/> when the drag started.</returns>
		internal static bool StartDragFromPane(LayoutAnchorable anchorable, ToggleDockingManager manager, DragStartInfo dragStart)
		{
			if (anchorable == null || manager == null || dragStart?.Pointer == null) return false;
			var position = manager.ScreenToLocal(dragStart.ScreenPoint);
			return new ToggleDockDragOverlay(anchorable, null, new Size(24, 24), manager).Begin(dragStart.Pointer, position);
		}

		/// <summary>Moves the virtual pointer of the drag in progress; used by tests and keyboard moves.</summary>
		/// <param name="position">The position in manager coordinates.</param>
		internal void MoveTo(Point position)
		{
			_pointerPosition = position;
			InvalidateVisual();
		}

		/// <summary>Ends the drag in progress, dropping the anchorable into the zone under <paramref name="position"/>.</summary>
		/// <param name="position">The position in manager coordinates.</param>
		internal void DropAt(Point position)
		{
			_pointerPosition = position;
			var hitZone = HitTestZone(position);
			Close();
			if (hitZone.HasValue && _sourceAnchorable != null)
				_manager.MoveAnchorableToZone(_sourceAnchorable, hitZone.Value.Zone);
		}

		/// <summary>Ends the drag in progress without moving the anchorable.</summary>
		internal void Cancel() => Close();

		/// <inheritdoc/>
		public override void Render(DrawingContext context)
		{
			base.Render(context);
			var hoveredIndex = IndexOfZoneAt(_pointerPosition);
			var typeface = new Typeface(FontFamily.Default);

			for (var i = 0; i < _dropZones.Count; i++)
			{
				var zone = _dropZones[i];
				var isHovered = i == hoveredIndex;

				if (zone.Label == null)
				{
					// Areas over the sidebars only show where the button would be inserted, and only when hovered.
					if (!isHovered) continue;
					context.DrawRectangle(BarHoverBrush, null, zone.Rect, 2, 2);
					if (zone.InsertionLineStart.HasValue && zone.InsertionLineEnd.HasValue)
					{
						context.DrawLine(LinePen, zone.InsertionLineStart.Value, zone.InsertionLineEnd.Value);
						context.DrawEllipse(LineBrush, null, zone.InsertionLineStart.Value, 3, 3);
						context.DrawEllipse(LineBrush, null, zone.InsertionLineEnd.Value, 3, 3);
					}
					else
					{
						context.DrawRectangle(HoverBrush, BorderPen, zone.Rect, 2, 2);
					}

					continue;
				}

				// Areas over the content are always shown, with their name.
				context.DrawRectangle(isHovered ? HoverBrush : NormalBrush, BorderPen, zone.Rect, 4, 4);
				var text = new FormattedText(
					zone.Label,
					CultureInfo.CurrentCulture,
					FlowDirection.LeftToRight,
					typeface,
					14,
					new SolidColorBrush(Color.FromArgb(isHovered ? (byte)0xCC : (byte)0x66, 0x00, 0x7A, 0xCC)));
				context.DrawText(text, new Point(
					zone.Rect.X + (zone.Rect.Width - text.Width) / 2,
					zone.Rect.Y + (zone.Rect.Height - text.Height) / 2));
			}

			if (_ghostBrush != null)
			{
				var ghostRect = new Rect(_pointerPosition.X + 12, _pointerPosition.Y - _ghostSize.Height / 2, _ghostSize.Width, _ghostSize.Height);
				context.DrawRectangle(_ghostBrush, GhostPen, ghostRect);
			}
		}

		private bool Begin(IPointer pointer, Point position)
		{
			var layer = OverlayLayer.GetOverlayLayer(_manager);
			var topLevel = TopLevel.GetTopLevel(_manager);
			if (layer == null || topLevel == null) return false;

			Current?.Cancel();
			Current = this;

			// Cover the manager exactly, so manager coordinates are overlay coordinates.
			var origin = _manager.TranslatePoint(default, layer) ?? default;
			Canvas.SetLeft(this, origin.X);
			Canvas.SetTop(this, origin.Y);
			Width = _manager.Bounds.Width;
			Height = _manager.Bounds.Height;
			Cursor = new Cursor(StandardCursorType.Hand);

			BuildDropZones();
			_pointerPosition = position;
			layer.Children.Add(this);

			_pointer = pointer;
			_captureTarget = topLevel;
			topLevel.AddHandler(PointerMovedEvent, OnPointerMovedWhileDragging, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
			topLevel.AddHandler(PointerReleasedEvent, OnPointerReleasedWhileDragging, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
			topLevel.AddHandler(PointerCaptureLostEvent, OnPointerCaptureLostWhileDragging, RoutingStrategies.Direct | RoutingStrategies.Bubble, handledEventsToo: true);
			topLevel.AddHandler(KeyDownEvent, OnKeyDownWhileDragging, RoutingStrategies.Tunnel, handledEventsToo: true);
			pointer?.Capture(topLevel);
			return true;
		}

		private void Close()
		{
			if (_isClosed) return;
			_isClosed = true;
			if (Current == this) Current = null;

			var topLevel = _captureTarget;
			_captureTarget = null;
			if (topLevel != null)
			{
				topLevel.RemoveHandler(PointerMovedEvent, OnPointerMovedWhileDragging);
				topLevel.RemoveHandler(PointerReleasedEvent, OnPointerReleasedWhileDragging);
				topLevel.RemoveHandler(PointerCaptureLostEvent, OnPointerCaptureLostWhileDragging);
				topLevel.RemoveHandler(KeyDownEvent, OnKeyDownWhileDragging);
				if (_pointer != null && ReferenceEquals(_pointer.Captured, topLevel)) _pointer.Capture(null);
			}

			(Parent as Panel)?.Children.Remove(this);
		}

		private void OnPointerMovedWhileDragging(object sender, PointerEventArgs e)
		{
			if (!ReferenceEquals(e.Pointer, _pointer)) return;
			MoveTo(e.GetPosition(_manager));
			e.Handled = true;
		}

		private void OnPointerReleasedWhileDragging(object sender, PointerReleasedEventArgs e)
		{
			if (!ReferenceEquals(e.Pointer, _pointer)) return;
			e.Handled = true;
			if (e.InitialPressMouseButton != MouseButton.Left)
			{
				Close();
				return;
			}

			DropAt(e.GetPosition(_manager));
		}

		private void OnPointerCaptureLostWhileDragging(object sender, PointerCaptureLostEventArgs e)
		{
			if (ReferenceEquals(e.Pointer, _pointer)) Close();
		}

		private void OnKeyDownWhileDragging(object sender, KeyEventArgs e)
		{
			if (e.Key != Key.Escape) return;
			Close();
			e.Handled = true;
		}

		private DropZone? HitTestZone(Point position)
		{
			var index = IndexOfZoneAt(position);
			return index < 0 ? (DropZone?)null : _dropZones[index];
		}

		// The zones added last (the sidebars) win where zones overlap.
		private int IndexOfZoneAt(Point position)
		{
			for (var i = _dropZones.Count - 1; i >= 0; i--)
			{
				if (_dropZones[i].Rect.Contains(position)) return i;
			}

			return -1;
		}

		private void BuildDropZones()
		{
			var totalW = _manager.Bounds.Width;
			var totalH = _manager.Bounds.Height;

			var leftBarWidth = _manager._injectedLeftDockPanel?.IsEffectivelyVisible == true ? _manager._injectedLeftDockPanel.Bounds.Width : 0;
			var rightBarWidth = _manager._rightTopBar?.IsEffectivelyVisible == true ? _manager._rightTopBar.Bounds.Width : 0;

			var contentX = leftBarWidth;
			var contentW = totalW - leftBarWidth - rightBarWidth;
			var contentH = totalH;
			if (contentW < 50 || contentH < 50) return;

			var leftW = GetOpenDockSize(AnchorSide.Left, contentW * 0.25, horizontal: true);
			var rightW = GetOpenDockSize(AnchorSide.Right, contentW * 0.25, horizontal: true);
			var bottomH = GetOpenDockSize(AnchorSide.Bottom, contentH * 0.25, horizontal: false);
			var sideH = contentH - bottomH;
			var halfSideH = sideH / 2.0;

			_dropZones.Add(new DropZone { Rect = new Rect(contentX, 0, leftW, halfSideH), Zone = DockZone.LeftTop, Label = "Left Top" });
			_dropZones.Add(new DropZone { Rect = new Rect(contentX, halfSideH, leftW, halfSideH), Zone = DockZone.LeftBottom, Label = "Left Bottom" });
			_dropZones.Add(new DropZone { Rect = new Rect(contentX + contentW - rightW, 0, rightW, halfSideH), Zone = DockZone.RightTop, Label = "Right Top" });
			_dropZones.Add(new DropZone { Rect = new Rect(contentX + contentW - rightW, halfSideH, rightW, halfSideH), Zone = DockZone.RightBottom, Label = "Right Bottom" });
			_dropZones.Add(new DropZone { Rect = new Rect(contentX, sideH, contentW / 2, bottomH), Zone = DockZone.BottomLeft, Label = "Bottom Left" });
			_dropZones.Add(new DropZone { Rect = new Rect(contentX + contentW / 2, sideH, contentW / 2, bottomH), Zone = DockZone.BottomRight, Label = "Bottom Right" });

			// The sidebars: the whole panel, split at the separator between the top and bottom bar.
			AddSidebarPanelDropZones(_manager._injectedLeftDockPanel, DockZone.LeftTop, DockZone.LeftBottom, _manager._bottomLeftBar, _manager._leftSeparator);
			AddSidebarPanelDropZones(_manager._injectedRightDockPanel, DockZone.RightTop, DockZone.RightBottom, _manager._bottomRightBar, _manager._rightSeparator);

			// The bottom bars sit at the bottom of the side panels.
			AddBarDropZone(_manager._bottomLeftBar, DockZone.BottomLeft);
			AddBarDropZone(_manager._bottomRightBar, DockZone.BottomRight);
		}

		private void AddSidebarPanelDropZones(Control panel, DockZone topZone, DockZone bottomZone, Control bottomDockBar, Control separator)
		{
			if (panel == null || !panel.IsEffectivelyVisible) return;
			var panelArea = AreaInManager(panel);
			var panelRect = new Rect(panelArea.X, panelArea.Y, Math.Max(panelArea.Width, 20), Math.Max(panelArea.Height, 20));

			// The bottom-dock bar at the end of the panel is a zone of its own.
			var bottomBarHeight = bottomDockBar != null && bottomDockBar.IsEffectivelyVisible ? bottomDockBar.Bounds.Height : 0;
			var usableHeight = panelRect.Height - bottomBarHeight;
			if (usableHeight < 10) return;

			double splitY;
			if (separator != null && separator.IsEffectivelyVisible)
			{
				var sepArea = AreaInManager(separator);
				splitY = sepArea.Top + sepArea.Height / 2.0;
			}
			else
			{
				splitY = panelRect.Y + usableHeight / 2.0;
			}

			var lineStart = new Point(panelRect.X + 2, splitY);
			var lineEnd = new Point(panelRect.X + panelRect.Width - 2, splitY);
			_dropZones.Add(new DropZone { Rect = new Rect(panelRect.X, panelRect.Y, panelRect.Width, splitY - panelRect.Y), Zone = topZone, InsertionLineStart = lineStart, InsertionLineEnd = lineEnd });
			_dropZones.Add(new DropZone { Rect = new Rect(panelRect.X, splitY, panelRect.Width, panelRect.Y + usableHeight - splitY), Zone = bottomZone, InsertionLineStart = lineStart, InsertionLineEnd = lineEnd });
		}

		private void AddBarDropZone(Control bar, DockZone zone)
		{
			if (bar == null || !bar.IsEffectivelyVisible) return;
			var barArea = AreaInManager(bar);
			_dropZones.Add(new DropZone { Rect = new Rect(barArea.X, barArea.Y, Math.Max(barArea.Width, 20), Math.Max(barArea.Height, 20)), Zone = zone });
		}

		private Rect AreaInManager(Visual visual)
		{
			var origin = visual.TranslatePoint(default, _manager) ?? default;
			return new Rect(origin, visual.Bounds.Size);
		}

		// The size of a docked pane on this side, so the zone matches what is on screen.
		private double GetOpenDockSize(AnchorSide side, double fallback, bool horizontal)
		{
			foreach (var paneCtrl in _manager.FindVisualChildren<LayoutAnchorablePaneControl>())
			{
				var size = horizontal ? paneCtrl.Bounds.Width : paneCtrl.Bounds.Height;
				if (paneCtrl.Model is LayoutAnchorablePane pane
					&& pane.GetSide() == side
					&& pane.Children.Any(a => !a.IsAutoHidden)
					&& size > 10)
					return size;
			}

			return fallback;
		}
	}
}
