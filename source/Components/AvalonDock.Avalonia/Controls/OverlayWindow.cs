using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using AvalonDock.Layout;

namespace AvalonDock.Controls
{
	/// <summary>
	/// Where the drop-target overlay is shown while a floating window is dragged.
	/// </summary>
	public enum OverlayWindowMode
	{
		/// <summary>
		/// A transparent, top-most window over the host, falling back to <see cref="InWindow"/> when the
		/// platform cannot give that window a transparent background (an X11 desktop without a compositor).
		/// </summary>
		Auto,

		/// <summary>Always a separate, transparent, top-most window - the WPF library's behaviour.</summary>
		Window,

		/// <summary>
		/// The overlay layer of the window that contains the host. Needs no transparent native window, but the
		/// dragged floating window, being a separate native window, can cover parts of it.
		/// </summary>
		InWindow,
	}

	/// <summary>
	/// The drop-target overlay shown over a <see cref="DockingManager"/> or a floating window while another
	/// floating window is dragged over it: the docking compasses and the preview of the drop.
	/// </summary>
	/// <remarks>
	/// In WPF this is itself a transparent window. Here it is a templated control, so that it can either live
	/// in a transparent top-level window (<see cref="OverlayWindowMode.Window"/>) or in the overlay layer of
	/// the host's own window (<see cref="OverlayWindowMode.InWindow"/>). All positions are computed from
	/// screen coordinates through <see cref="Extensions.ScreenToLocal(Visual, Rect)"/>, which works the same
	/// in both places.
	/// </remarks>
	public class OverlayWindow : TemplatedControl, IOverlayWindow
	{
		private Canvas _mainCanvasPanel;
		private Control _gridDockingManagerDropTargets;
		private Control _gridAnchorablePaneDropTargets;
		private Control _gridDocumentPaneDropTargets;
		private Control _gridDocumentPaneFullDropTargets;

		private Control _dockingManagerDropTargetBottom;
		private Control _dockingManagerDropTargetTop;
		private Control _dockingManagerDropTargetLeft;
		private Control _dockingManagerDropTargetRight;

		private Control _anchorablePaneDropTargetBottom;
		private Control _anchorablePaneDropTargetTop;
		private Control _anchorablePaneDropTargetLeft;
		private Control _anchorablePaneDropTargetRight;
		private Control _anchorablePaneDropTargetInto;

		private Control _documentPaneDropTargetBottom;
		private Control _documentPaneDropTargetTop;
		private Control _documentPaneDropTargetLeft;
		private Control _documentPaneDropTargetRight;
		private Control _documentPaneDropTargetInto;

		private Control _documentPaneDropTargetBottomAsAnchorablePane;
		private Control _documentPaneDropTargetTopAsAnchorablePane;
		private Control _documentPaneDropTargetLeftAsAnchorablePane;
		private Control _documentPaneDropTargetRightAsAnchorablePane;

		private Control _documentPaneFullDropTargetBottom;
		private Control _documentPaneFullDropTargetTop;
		private Control _documentPaneFullDropTargetLeft;
		private Control _documentPaneFullDropTargetRight;
		private Control _documentPaneFullDropTargetInto;

		private Path _previewBox;
		private readonly IOverlayWindowHost _host;
		private LayoutFloatingWindowControl _floatingWindow = null;
		private readonly List<IDropArea> _visibleAreas = new List<IDropArea>();
		private OverlayHostWindow _hostWindow;
		private Panel _hostLayer;

		/// <summary>Initializes a new instance of the <see cref="OverlayWindow"/> class.</summary>
		/// <param name="host">The host the overlay belongs to.</param>
		internal OverlayWindow(IOverlayWindowHost host)
		{
			_host = host;
			IsHitTestVisible = false;
			Focusable = false;
		}

		/// <summary>Gets a value indicating whether the overlay belongs to a floating window.</summary>
		public bool IsHostedInFloatingWindow => _host is LayoutFloatingWindowControl;

		/// <summary>Gets the area of the overlay on screen, in device pixels.</summary>
		internal Rect ScreenArea { get; private set; }

		/// <summary>Gets a value indicating whether the overlay is currently shown in a top-level window of its own.</summary>
		internal bool IsShownInOwnWindow => _hostWindow?.IsVisible == true;

		/// <summary>Gets the drop target that the preview is currently shown for, if any.</summary>
		internal IDropTarget CurrentPreviewTarget { get; private set; }

		/// <inheritdoc/>
		protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
		{
			base.OnApplyTemplate(e);
			var t = e.NameScope;
			_mainCanvasPanel = t.Find<Canvas>("PART_DropTargetsContainer");
			_gridDockingManagerDropTargets = t.Find<Control>("PART_DockingManagerDropTargets");
			_gridAnchorablePaneDropTargets = t.Find<Control>("PART_AnchorablePaneDropTargets");
			_gridDocumentPaneDropTargets = t.Find<Control>("PART_DocumentPaneDropTargets");
			_gridDocumentPaneFullDropTargets = t.Find<Control>("PART_DocumentPaneFullDropTargets");

			HideGrid(_gridDockingManagerDropTargets);
			HideGrid(_gridAnchorablePaneDropTargets);
			HideGrid(_gridDocumentPaneDropTargets);
			HideGrid(_gridDocumentPaneFullDropTargets);

			_dockingManagerDropTargetBottom = t.Find<Control>("PART_DockingManagerDropTargetBottom");
			_dockingManagerDropTargetTop = t.Find<Control>("PART_DockingManagerDropTargetTop");
			_dockingManagerDropTargetLeft = t.Find<Control>("PART_DockingManagerDropTargetLeft");
			_dockingManagerDropTargetRight = t.Find<Control>("PART_DockingManagerDropTargetRight");

			_anchorablePaneDropTargetBottom = t.Find<Control>("PART_AnchorablePaneDropTargetBottom");
			_anchorablePaneDropTargetTop = t.Find<Control>("PART_AnchorablePaneDropTargetTop");
			_anchorablePaneDropTargetLeft = t.Find<Control>("PART_AnchorablePaneDropTargetLeft");
			_anchorablePaneDropTargetRight = t.Find<Control>("PART_AnchorablePaneDropTargetRight");
			_anchorablePaneDropTargetInto = t.Find<Control>("PART_AnchorablePaneDropTargetInto");

			_documentPaneDropTargetBottom = t.Find<Control>("PART_DocumentPaneDropTargetBottom");
			_documentPaneDropTargetTop = t.Find<Control>("PART_DocumentPaneDropTargetTop");
			_documentPaneDropTargetLeft = t.Find<Control>("PART_DocumentPaneDropTargetLeft");
			_documentPaneDropTargetRight = t.Find<Control>("PART_DocumentPaneDropTargetRight");
			_documentPaneDropTargetInto = t.Find<Control>("PART_DocumentPaneDropTargetInto");

			_documentPaneDropTargetBottomAsAnchorablePane = t.Find<Control>("PART_DocumentPaneDropTargetBottomAsAnchorablePane");
			_documentPaneDropTargetTopAsAnchorablePane = t.Find<Control>("PART_DocumentPaneDropTargetTopAsAnchorablePane");
			_documentPaneDropTargetLeftAsAnchorablePane = t.Find<Control>("PART_DocumentPaneDropTargetLeftAsAnchorablePane");
			_documentPaneDropTargetRightAsAnchorablePane = t.Find<Control>("PART_DocumentPaneDropTargetRightAsAnchorablePane");

			_documentPaneFullDropTargetBottom = t.Find<Control>("PART_DocumentPaneFullDropTargetBottom");
			_documentPaneFullDropTargetTop = t.Find<Control>("PART_DocumentPaneFullDropTargetTop");
			_documentPaneFullDropTargetLeft = t.Find<Control>("PART_DocumentPaneFullDropTargetLeft");
			_documentPaneFullDropTargetRight = t.Find<Control>("PART_DocumentPaneFullDropTargetRight");
			_documentPaneFullDropTargetInto = t.Find<Control>("PART_DocumentPaneFullDropTargetInto");

			_previewBox = t.Find<Path>("PART_PreviewBox");
		}

		/// <summary>Shows the overlay over <paramref name="hostVisual"/>.</summary>
		/// <param name="hostVisual">The control the overlay covers: the docking manager or the content of a floating window.</param>
		/// <param name="mode">Where the overlay is shown.</param>
		/// <param name="ownerWindow">The window that owns the overlay window in <see cref="OverlayWindowMode.Window"/> mode.</param>
		internal void ShowOver(Visual hostVisual, OverlayWindowMode mode, Window ownerWindow)
		{
			ScreenArea = hostVisual.GetScreenArea();

			if (mode != OverlayWindowMode.InWindow && TryShowInOwnWindow(hostVisual, ownerWindow, mode == OverlayWindowMode.Auto))
			{
				EnableDropTargets();
				return;
			}

			ShowInOverlayLayer(hostVisual);
			EnableDropTargets();
		}

		private bool TryShowInOwnWindow(Visual hostVisual, Window ownerWindow, bool requireTransparency)
		{
			var topLevel = TopLevel.GetTopLevel(hostVisual);
			if (topLevel == null) return false;
			DetachFromLayer();

			if (_hostWindow == null)
			{
				_hostWindow = new OverlayHostWindow();
			}

			if (!ReferenceEquals(_hostWindow.Content, this))
			{
				if (Parent is ContentControl oldParent) oldParent.Content = null;
				_hostWindow.Content = this;
			}

			var scaling = topLevel.RenderScaling;
			_hostWindow.Position = new PixelPoint((int)System.Math.Round(ScreenArea.X), (int)System.Math.Round(ScreenArea.Y));
			_hostWindow.Width = ScreenArea.Width / scaling;
			_hostWindow.Height = ScreenArea.Height / scaling;

			if (!_hostWindow.IsVisible)
			{
				if (ownerWindow != null && ownerWindow.IsVisible) _hostWindow.Show(ownerWindow);
				else _hostWindow.Show();
			}

			if (requireTransparency && _hostWindow.ActualTransparencyLevel == WindowTransparencyLevel.None)
			{
				// An opaque overlay would hide the whole host; draw into the host's window instead.
				_hostWindow.Hide();
				_hostWindow.Content = null;
				return false;
			}

			UpdateLayout();
			return true;
		}

		private void ShowInOverlayLayer(Visual hostVisual)
		{
			if (_hostWindow != null)
			{
				_hostWindow.Hide();
				if (ReferenceEquals(_hostWindow.Content, this)) _hostWindow.Content = null;
			}

			var layer = OverlayLayer.GetOverlayLayer(hostVisual);
			if (layer == null) return;
			if (!ReferenceEquals(_hostLayer, layer))
			{
				DetachFromLayer();
				layer.Children.Add(this);
				_hostLayer = layer;
			}

			var localArea = layer.ScreenToLocal(ScreenArea);
			Canvas.SetLeft(this, localArea.X);
			Canvas.SetTop(this, localArea.Y);
			Width = localArea.Width;
			Height = localArea.Height;
			IsVisible = true;
			UpdateLayout();
		}

		private void DetachFromLayer()
		{
			if (_hostLayer == null) return;
			_hostLayer.Children.Remove(this);
			_hostLayer = null;
		}

		/// <summary>Closes the overlay and the window it may be shown in.</summary>
		internal void Close()
		{
			HideOverlay();
			DetachFromLayer();
			if (_hostWindow != null)
			{
				_hostWindow.Content = null;
				_hostWindow.Close();
				_hostWindow = null;
			}
		}

		/// <summary>Makes the drop target container visible.</summary>
		internal void EnableDropTargets()
		{
			if (_mainCanvasPanel != null) _mainCanvasPanel.IsVisible = true;
		}

		/// <summary>Hides the drop target container.</summary>
		internal void HideDropTargets()
		{
			if (_mainCanvasPanel != null) _mainCanvasPanel.IsVisible = false;
		}

		/// <summary>Hides the overlay but keeps it for the next drag.</summary>
		internal void HideOverlay()
		{
			HideDropTargets();
			HideGrid(_gridDockingManagerDropTargets);
			HideGrid(_gridAnchorablePaneDropTargets);
			HideGrid(_gridDocumentPaneDropTargets);
			HideGrid(_gridDocumentPaneFullDropTargets);
			if (_previewBox != null) _previewBox.IsVisible = false;
			CurrentPreviewTarget = null;

			// The reference to the dragged window is deliberately kept. The drag service hides this overlay
			// before it hands the drop over and before it leaves the drop areas again, and all of those
			// steps still need to know which window is being dragged.
			_visibleAreas.Clear();
			_hostWindow?.Hide();
			if (_hostLayer != null) IsVisible = false;
		}

		private static void HideGrid(Control grid)
		{
			if (grid != null) grid.IsVisible = false;
		}

		private void SetDropTargetIntoVisibility(ILayoutPositionableElement positionableElement)
		{
			if (positionableElement is LayoutAnchorablePane)
				_anchorablePaneDropTargetInto.IsVisible = true;
			else if (positionableElement is LayoutDocumentPane)
				_documentPaneDropTargetInto.IsVisible = true;

			if (positionableElement == null || _floatingWindow.Model == null || positionableElement.AllowDuplicateContent)
				return;

			// Find all content layouts in the pane (object to drop on) and in the floating window (object to drop)
			var contentLayoutsOnPositionableElementPane = positionableElement.Descendents().OfType<LayoutContent>().ToList();
			var contentLayoutsOnFloatingWindow = _floatingWindow.Model.Descendents().OfType<LayoutContent>().ToList();

			// If any of the content layouts is present in the drop area, then disable the DropTargetInto button.
			foreach (var content in contentLayoutsOnFloatingWindow)
			{
				if (!contentLayoutsOnPositionableElementPane.Any(item => item.Title == content.Title && item.ContentId == content.ContentId))
					continue;

				if (positionableElement is LayoutAnchorablePane)
					_anchorablePaneDropTargetInto.IsVisible = false;
				else if (positionableElement is LayoutDocumentPane)
					_documentPaneDropTargetInto.IsVisible = false;
				break;
			}
		}

		/// <inheritdoc/>
		IEnumerable<IDropTarget> IOverlayWindow.GetTargets()
		{
			// Closing/docking detaches the floating model before the drag service releases its overlay reference.
			if (_floatingWindow?.Model == null)
				yield break;

			foreach (var visibleArea in _visibleAreas.ToArray())
			{
				switch (visibleArea.Type)
				{
					case DropAreaType.DockingManager:
						{
							var dropAreaDockingManager = visibleArea as DropArea<DockingManager>;
							yield return new DockingManagerDropTarget(dropAreaDockingManager.AreaElement, _dockingManagerDropTargetLeft.GetVisibleScreenArea(), DropTargetType.DockingManagerDockLeft);
							yield return new DockingManagerDropTarget(dropAreaDockingManager.AreaElement, _dockingManagerDropTargetTop.GetVisibleScreenArea(), DropTargetType.DockingManagerDockTop);
							yield return new DockingManagerDropTarget(dropAreaDockingManager.AreaElement, _dockingManagerDropTargetBottom.GetVisibleScreenArea(), DropTargetType.DockingManagerDockBottom);
							yield return new DockingManagerDropTarget(dropAreaDockingManager.AreaElement, _dockingManagerDropTargetRight.GetVisibleScreenArea(), DropTargetType.DockingManagerDockRight);
						}

						break;

					case DropAreaType.AnchorablePane:
						{
							var dropAreaAnchorablePane = visibleArea as DropArea<LayoutAnchorablePaneControl>;
							yield return new AnchorablePaneDropTarget(dropAreaAnchorablePane.AreaElement, _anchorablePaneDropTargetLeft.GetVisibleScreenArea(), DropTargetType.AnchorablePaneDockLeft);
							yield return new AnchorablePaneDropTarget(dropAreaAnchorablePane.AreaElement, _anchorablePaneDropTargetTop.GetVisibleScreenArea(), DropTargetType.AnchorablePaneDockTop);
							yield return new AnchorablePaneDropTarget(dropAreaAnchorablePane.AreaElement, _anchorablePaneDropTargetRight.GetVisibleScreenArea(), DropTargetType.AnchorablePaneDockRight);
							yield return new AnchorablePaneDropTarget(dropAreaAnchorablePane.AreaElement, _anchorablePaneDropTargetBottom.GetVisibleScreenArea(), DropTargetType.AnchorablePaneDockBottom);
							if (_anchorablePaneDropTargetInto.IsVisible)
								yield return new AnchorablePaneDropTarget(dropAreaAnchorablePane.AreaElement, _anchorablePaneDropTargetInto.GetVisibleScreenArea(), DropTargetType.AnchorablePaneDockInside);

							var parentPaneModel = dropAreaAnchorablePane.AreaElement.Model as LayoutAnchorablePane;
							LayoutAnchorableTabItem lastAreaTabItem = null;
							foreach (var dropAreaTabItem in dropAreaAnchorablePane.AreaElement.FindVisualChildren<LayoutAnchorableTabItem>().Where(t => t.IsEffectivelyVisible))
							{
								var tabItemModel = dropAreaTabItem.Model as LayoutAnchorable;
								lastAreaTabItem = lastAreaTabItem == null || lastAreaTabItem.GetScreenArea().Right < dropAreaTabItem.GetScreenArea().Right ?
									dropAreaTabItem : lastAreaTabItem;
								var tabIndex = parentPaneModel.Children.IndexOf(tabItemModel);
								var tabScreenArea = ClipToOverlayBounds(dropAreaTabItem.GetScreenArea());
								if (!tabScreenArea.IsEmptyArea())
									yield return new AnchorablePaneDropTarget(dropAreaAnchorablePane.AreaElement, tabScreenArea, DropTargetType.AnchorablePaneDockInside, tabIndex);
							}

							if (lastAreaTabItem != null)
							{
								var lastAreaTabItemScreenArea = lastAreaTabItem.GetScreenArea();
								var newAreaTabItemScreenArea = ClipToOverlayBounds(new Rect(lastAreaTabItemScreenArea.TopRight, new Point(lastAreaTabItemScreenArea.Right + lastAreaTabItemScreenArea.Width, lastAreaTabItemScreenArea.Bottom)));
								if (!newAreaTabItemScreenArea.IsEmptyArea() && newAreaTabItemScreenArea.Right < dropAreaAnchorablePane.AreaElement.GetScreenArea().Right)
									yield return new AnchorablePaneDropTarget(dropAreaAnchorablePane.AreaElement, newAreaTabItemScreenArea, DropTargetType.AnchorablePaneDockInside, parentPaneModel.Children.Count);
							}

							var dropAreaTitle = dropAreaAnchorablePane.AreaElement.FindVisualChildren<AnchorablePaneTitle>().FirstOrDefault(t => t.IsEffectivelyVisible);
							if (dropAreaTitle != null)
							{
								var titleScreenArea = ClipToOverlayBounds(dropAreaTitle.GetScreenArea());
								if (!titleScreenArea.IsEmptyArea())
									yield return new AnchorablePaneDropTarget(dropAreaAnchorablePane.AreaElement, titleScreenArea, DropTargetType.AnchorablePaneDockInside);
							}
						}

						break;

					case DropAreaType.DocumentPane:
						{
							var isDraggingAnchorables = _floatingWindow.Model is LayoutAnchorableFloatingWindow;
							var dropAreaDocumentPane = visibleArea as DropArea<LayoutDocumentPaneControl>;
							if (isDraggingAnchorables && _gridDocumentPaneFullDropTargets != null)
							{
								// An anchorable is dragged over a document pane: 9 buttons
								if (_documentPaneFullDropTargetLeft.IsVisible)
									yield return new DocumentPaneDropTarget(dropAreaDocumentPane.AreaElement, _documentPaneFullDropTargetLeft.GetVisibleScreenArea(), DropTargetType.DocumentPaneDockLeft);
								if (_documentPaneFullDropTargetTop.IsVisible)
									yield return new DocumentPaneDropTarget(dropAreaDocumentPane.AreaElement, _documentPaneFullDropTargetTop.GetVisibleScreenArea(), DropTargetType.DocumentPaneDockTop);
								if (_documentPaneFullDropTargetRight.IsVisible)
									yield return new DocumentPaneDropTarget(dropAreaDocumentPane.AreaElement, _documentPaneFullDropTargetRight.GetVisibleScreenArea(), DropTargetType.DocumentPaneDockRight);
								if (_documentPaneFullDropTargetBottom.IsVisible)
									yield return new DocumentPaneDropTarget(dropAreaDocumentPane.AreaElement, _documentPaneFullDropTargetBottom.GetVisibleScreenArea(), DropTargetType.DocumentPaneDockBottom);
								if (_documentPaneFullDropTargetInto.IsVisible)
									yield return new DocumentPaneDropTarget(dropAreaDocumentPane.AreaElement, _documentPaneFullDropTargetInto.GetVisibleScreenArea(), DropTargetType.DocumentPaneDockInside);
							}
							else
							{
								// A document is dragged over a document pane: 5 centered buttons
								if (_documentPaneDropTargetLeft.IsVisible)
									yield return new DocumentPaneDropTarget(dropAreaDocumentPane.AreaElement, _documentPaneDropTargetLeft.GetVisibleScreenArea(), DropTargetType.DocumentPaneDockLeft);
								if (_documentPaneDropTargetTop.IsVisible)
									yield return new DocumentPaneDropTarget(dropAreaDocumentPane.AreaElement, _documentPaneDropTargetTop.GetVisibleScreenArea(), DropTargetType.DocumentPaneDockTop);
								if (_documentPaneDropTargetRight.IsVisible)
									yield return new DocumentPaneDropTarget(dropAreaDocumentPane.AreaElement, _documentPaneDropTargetRight.GetVisibleScreenArea(), DropTargetType.DocumentPaneDockRight);
								if (_documentPaneDropTargetBottom.IsVisible)
									yield return new DocumentPaneDropTarget(dropAreaDocumentPane.AreaElement, _documentPaneDropTargetBottom.GetVisibleScreenArea(), DropTargetType.DocumentPaneDockBottom);
								if (_documentPaneDropTargetInto.IsVisible)
									yield return new DocumentPaneDropTarget(dropAreaDocumentPane.AreaElement, _documentPaneDropTargetInto.GetVisibleScreenArea(), DropTargetType.DocumentPaneDockInside);
							}

							var parentPaneModel = dropAreaDocumentPane.AreaElement.Model as LayoutDocumentPane;
							LayoutDocumentTabItem lastAreaTabItem = null;
							foreach (var dropAreaTabItem in dropAreaDocumentPane.AreaElement.FindVisualChildren<LayoutDocumentTabItem>().Where(t => t.IsEffectivelyVisible))
							{
								var tabItemModel = dropAreaTabItem.Model;
								lastAreaTabItem = lastAreaTabItem == null || lastAreaTabItem.GetScreenArea().Right < dropAreaTabItem.GetScreenArea().Right ?
									dropAreaTabItem : lastAreaTabItem;
								var tabIndex = parentPaneModel.Children.IndexOf(tabItemModel);
								var tabScreenArea = ClipToOverlayBounds(dropAreaTabItem.GetScreenArea());
								if (!tabScreenArea.IsEmptyArea())
									yield return new DocumentPaneDropTarget(dropAreaDocumentPane.AreaElement, tabScreenArea, DropTargetType.DocumentPaneDockInside, tabIndex);
							}

							if (lastAreaTabItem != null)
							{
								var lastAreaTabItemScreenArea = lastAreaTabItem.GetScreenArea();
								var newAreaTabItemScreenArea = ClipToOverlayBounds(new Rect(lastAreaTabItemScreenArea.TopRight, new Point(lastAreaTabItemScreenArea.Right + lastAreaTabItemScreenArea.Width, lastAreaTabItemScreenArea.Bottom)));
								if (!newAreaTabItemScreenArea.IsEmptyArea() && newAreaTabItemScreenArea.Right < dropAreaDocumentPane.AreaElement.GetScreenArea().Right)
									yield return new DocumentPaneDropTarget(dropAreaDocumentPane.AreaElement, newAreaTabItemScreenArea, DropTargetType.DocumentPaneDockInside, parentPaneModel.Children.Count);
							}

							if (isDraggingAnchorables && _gridDocumentPaneFullDropTargets != null)
							{
								if (_documentPaneDropTargetLeftAsAnchorablePane.IsVisible)
									yield return new DocumentPaneDropAsAnchorableTarget(dropAreaDocumentPane.AreaElement, _documentPaneDropTargetLeftAsAnchorablePane.GetVisibleScreenArea(), DropTargetType.DocumentPaneDockAsAnchorableLeft);
								if (_documentPaneDropTargetTopAsAnchorablePane.IsVisible)
									yield return new DocumentPaneDropAsAnchorableTarget(dropAreaDocumentPane.AreaElement, _documentPaneDropTargetTopAsAnchorablePane.GetVisibleScreenArea(), DropTargetType.DocumentPaneDockAsAnchorableTop);
								if (_documentPaneDropTargetRightAsAnchorablePane.IsVisible)
									yield return new DocumentPaneDropAsAnchorableTarget(dropAreaDocumentPane.AreaElement, _documentPaneDropTargetRightAsAnchorablePane.GetVisibleScreenArea(), DropTargetType.DocumentPaneDockAsAnchorableRight);
								if (_documentPaneDropTargetBottomAsAnchorablePane.IsVisible)
									yield return new DocumentPaneDropAsAnchorableTarget(dropAreaDocumentPane.AreaElement, _documentPaneDropTargetBottomAsAnchorablePane.GetVisibleScreenArea(), DropTargetType.DocumentPaneDockAsAnchorableBottom);
							}
						}

						break;

					case DropAreaType.DocumentPaneGroup:
						{
							var dropAreaDocumentPane = visibleArea as DropArea<LayoutDocumentPaneGroupControl>;
							if (_documentPaneDropTargetInto.IsVisible)
								yield return new DocumentPaneGroupDropTarget(dropAreaDocumentPane.AreaElement, _documentPaneDropTargetInto.GetVisibleScreenArea(), DropTargetType.DocumentPaneGroupDockInside);
						}

						break;
				}
			}
		}

		/// <inheritdoc/>
		void IOverlayWindow.DragEnter(LayoutFloatingWindowControl floatingWindow)
		{
			_floatingWindow = floatingWindow;
			EnableDropTargets();
		}

		/// <inheritdoc/>
		void IOverlayWindow.DragLeave(LayoutFloatingWindowControl floatingWindow)
		{
			HideOverlay();
			_floatingWindow = null;
		}

		/// <inheritdoc/>
		void IOverlayWindow.DragEnter(IDropArea area)
		{
			// Several races upstream (a drag whose end was missed, a model detached mid-drag) can leave the
			// overlay without a live dragged window; it must then simply do nothing.
			if (_floatingWindow?.Model?.Root == null)
				return;
			var floatingWindowManager = _floatingWindow.Model.Root.Manager;

			_visibleAreas.Add(area);

			Control areaElement;
			switch (area.Type)
			{
				case DropAreaType.DockingManager:
					var dropAreaDockingManager = area as DropArea<DockingManager>;
					if (dropAreaDockingManager.AreaElement != floatingWindowManager)
					{
						_visibleAreas.Remove(area);
						return;
					}

					areaElement = _gridDockingManagerDropTargets;
					break;

				case DropAreaType.AnchorablePane:
					areaElement = _gridAnchorablePaneDropTargets;
					var dropAreaAnchorablePaneGroup = area as DropArea<LayoutAnchorablePaneControl>;
					var layoutAnchorablePane = dropAreaAnchorablePaneGroup.AreaElement.Model as LayoutAnchorablePane;
					if (layoutAnchorablePane.Root?.Manager != floatingWindowManager)
					{
						_visibleAreas.Remove(area);
						return;
					}

					SetDropTargetIntoVisibility(layoutAnchorablePane);
					break;

				case DropAreaType.DocumentPaneGroup:
					{
						areaElement = _gridDocumentPaneDropTargets;
						var dropAreaDocumentPaneGroup = area as DropArea<LayoutDocumentPaneGroupControl>;
						var group = dropAreaDocumentPaneGroup.AreaElement.Model as LayoutDocumentPaneGroup;
						if (group?.Root?.Manager != floatingWindowManager)
						{
							_visibleAreas.Remove(area);
							return;
						}

						_documentPaneDropTargetLeft.IsVisible = false;
						_documentPaneDropTargetRight.IsVisible = false;
						_documentPaneDropTargetTop.IsVisible = false;
						_documentPaneDropTargetBottom.IsVisible = false;
						_documentPaneDropTargetInto.IsVisible = true;
					}

					break;

				case DropAreaType.DocumentPane:
				default:
					areaElement = PrepareDocumentPaneTargets(area, floatingWindowManager);
					if (areaElement == null)
					{
						_visibleAreas.Remove(area);
						return;
					}

					break;
			}

			if (areaElement == null)
				return;

			var localArea = this.ScreenToLocal(area.DetectionRect);
			Canvas.SetLeft(areaElement, localArea.X);
			Canvas.SetTop(areaElement, localArea.Y);
			areaElement.Width = localArea.Width;
			areaElement.Height = localArea.Height;
			areaElement.IsVisible = true;

			// The drag service looks for the targets of this area right away, so their screen positions must
			// be up to date now rather than after the next layout pass.
			UpdateLayout();
		}

		private Control PrepareDocumentPaneTargets(IDropArea area, DockingManager floatingWindowManager)
		{
			var isDraggingAnchorables = _floatingWindow.Model is LayoutAnchorableFloatingWindow;
			var dropAreaDocumentPane = area as DropArea<LayoutDocumentPaneControl>;
			var layoutDocumentPane = dropAreaDocumentPane.AreaElement.Model as LayoutDocumentPane;
			var parentDocumentPaneGroup = layoutDocumentPane.Parent as LayoutDocumentPaneGroup;
			if (layoutDocumentPane.Root?.Manager != floatingWindowManager)
				return null;

			SetDropTargetIntoVisibility(layoutDocumentPane);

			Control left, right, top, bottom;
			Control areaElement;
			if (isDraggingAnchorables && _gridDocumentPaneFullDropTargets != null)
			{
				areaElement = _gridDocumentPaneFullDropTargets;
				left = _documentPaneFullDropTargetLeft;
				right = _documentPaneFullDropTargetRight;
				top = _documentPaneFullDropTargetTop;
				bottom = _documentPaneFullDropTargetBottom;
				_documentPaneFullDropTargetInto.IsVisible = _documentPaneDropTargetInto.IsVisible;
			}
			else
			{
				areaElement = _gridDocumentPaneDropTargets;
				left = _documentPaneDropTargetLeft;
				right = _documentPaneDropTargetRight;
				top = _documentPaneDropTargetTop;
				bottom = _documentPaneDropTargetBottom;
			}

			if (parentDocumentPaneGroup != null && parentDocumentPaneGroup.Children.Count(c => c.IsVisible) > 1)
			{
				var manager = parentDocumentPaneGroup.Root.Manager;
				if (!manager.AllowMixedOrientation)
				{
					left.IsVisible = parentDocumentPaneGroup.Orientation == Orientation.Horizontal;
					right.IsVisible = parentDocumentPaneGroup.Orientation == Orientation.Horizontal;
					top.IsVisible = parentDocumentPaneGroup.Orientation == Orientation.Vertical;
					bottom.IsVisible = parentDocumentPaneGroup.Orientation == Orientation.Vertical;
				}
				else
				{
					left.IsVisible = right.IsVisible = top.IsVisible = bottom.IsVisible = true;
				}
			}
			else if (parentDocumentPaneGroup == null && layoutDocumentPane.ChildrenCount == 0)
			{
				left.IsVisible = right.IsVisible = top.IsVisible = bottom.IsVisible = false;
			}
			else
			{
				left.IsVisible = right.IsVisible = top.IsVisible = bottom.IsVisible = true;
			}

			if (!(isDraggingAnchorables && _gridDocumentPaneFullDropTargets != null)) return areaElement;

			if (layoutDocumentPane.IsHostedInFloatingWindow)
			{
				// The outer buttons dock next to the document area of the manager, so they only make sense there.
				_documentPaneDropTargetBottomAsAnchorablePane.IsVisible = false;
				_documentPaneDropTargetLeftAsAnchorablePane.IsVisible = false;
				_documentPaneDropTargetRightAsAnchorablePane.IsVisible = false;
				_documentPaneDropTargetTopAsAnchorablePane.IsVisible = false;
			}
			else if (parentDocumentPaneGroup != null && parentDocumentPaneGroup.Children.Count(c => c.IsVisible) > 1)
			{
				var visibleChildren = parentDocumentPaneGroup.Children.Where(ch => ch.IsVisible).ToList();
				var indexOfDocumentPane = visibleChildren.IndexOf(layoutDocumentPane);
				var isFirstChild = indexOfDocumentPane == 0;
				var isLastChild = indexOfDocumentPane == visibleChildren.Count - 1;

				var manager = parentDocumentPaneGroup.Root.Manager;
				if (!manager.AllowMixedOrientation)
				{
					_documentPaneDropTargetBottomAsAnchorablePane.IsVisible = parentDocumentPaneGroup.Orientation == Orientation.Vertical && isLastChild;
					_documentPaneDropTargetTopAsAnchorablePane.IsVisible = parentDocumentPaneGroup.Orientation == Orientation.Vertical && isFirstChild;
					_documentPaneDropTargetLeftAsAnchorablePane.IsVisible = parentDocumentPaneGroup.Orientation == Orientation.Horizontal && isFirstChild;
					_documentPaneDropTargetRightAsAnchorablePane.IsVisible = parentDocumentPaneGroup.Orientation == Orientation.Horizontal && isLastChild;
				}
				else
				{
					_documentPaneDropTargetBottomAsAnchorablePane.IsVisible = true;
					_documentPaneDropTargetLeftAsAnchorablePane.IsVisible = true;
					_documentPaneDropTargetRightAsAnchorablePane.IsVisible = true;
					_documentPaneDropTargetTopAsAnchorablePane.IsVisible = true;
				}
			}
			else
			{
				_documentPaneDropTargetBottomAsAnchorablePane.IsVisible = true;
				_documentPaneDropTargetLeftAsAnchorablePane.IsVisible = true;
				_documentPaneDropTargetRightAsAnchorablePane.IsVisible = true;
				_documentPaneDropTargetTopAsAnchorablePane.IsVisible = true;
			}

			return areaElement;
		}

		/// <inheritdoc/>
		void IOverlayWindow.DragLeave(IDropArea area)
		{
			_visibleAreas.Remove(area);

			Control areaElement;
			switch (area.Type)
			{
				case DropAreaType.DockingManager:
					areaElement = _gridDockingManagerDropTargets;
					break;

				case DropAreaType.AnchorablePane:
					areaElement = _gridAnchorablePaneDropTargets;
					break;

				case DropAreaType.DocumentPaneGroup:
					areaElement = _gridDocumentPaneDropTargets;
					break;

				case DropAreaType.DocumentPane:
				default:
					{
						var isDraggingAnchorables = _floatingWindow?.Model is LayoutAnchorableFloatingWindow;
						areaElement = isDraggingAnchorables && _gridDocumentPaneFullDropTargets != null
							? _gridDocumentPaneFullDropTargets
							: _gridDocumentPaneDropTargets;
					}

					break;
			}

			HideGrid(areaElement);
		}

		/// <inheritdoc/>
		void IOverlayWindow.DragEnter(IDropTarget target)
		{
			if (_floatingWindow?.Model is not LayoutFloatingWindow floatingWindowModel || _previewBox == null) return;
			var previewBoxPath = target.GetPreviewPath(this, floatingWindowModel);
			if (previewBoxPath == null) return;
			_previewBox.Data = previewBoxPath;
			_previewBox.IsVisible = true;
			CurrentPreviewTarget = target;
		}

		/// <inheritdoc/>
		void IOverlayWindow.DragLeave(IDropTarget target)
		{
			if (_previewBox != null) _previewBox.IsVisible = false;
			CurrentPreviewTarget = null;
		}

		/// <inheritdoc/>
		void IOverlayWindow.DragDrop(IDropTarget target)
		{
			if (_floatingWindow?.Model is LayoutFloatingWindow floatingWindowModel)
				target.Drop(floatingWindowModel);
		}

		private Rect ClipToOverlayBounds(Rect screenArea)
		{
			if (screenArea.IsEmptyArea())
				return default;
			return screenArea.IntersectOrEmpty(ScreenArea);
		}

		/// <summary>The transparent, top-most window that shows the overlay in <see cref="OverlayWindowMode.Window"/> mode.</summary>
		private sealed class OverlayHostWindow : Window
		{
			public OverlayHostWindow()
			{
				WindowDecorations = WindowDecorations.None;
				ShowInTaskbar = false;
				ShowActivated = false;
				Topmost = true;
				CanResize = false;
				Background = Brushes.Transparent;
				TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
				IsHitTestVisible = false;
				Focusable = false;
				SizeToContent = SizeToContent.Manual;
				Title = "AvalonDock drop targets";
			}

			protected override System.Type StyleKeyOverride => typeof(Window);
		}
	}
}
