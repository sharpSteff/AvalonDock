using System;
using System.Linq;
using Avalonia;
using Avalonia.Layout;
using Avalonia.Media;
using AvalonDock.Layout;

namespace AvalonDock.Controls
{
	/// <summary>
	/// Represents the docking manager drop target.
	/// </summary>
	internal class DockingManagerDropTarget : DropTarget<DockingManager>
	{
		private DockingManager _manager;

		/// <summary>
		/// Initializes a new instance of the <see cref="DockingManagerDropTarget"/> class.
		/// </summary>
		/// <param name="manager">The manager.</param>
		/// <param name="detectionRect">The detection rectangle.</param>
		/// <param name="type">The drop target type.</param>
		internal DockingManagerDropTarget(
			DockingManager manager,
			Rect detectionRect,
			DropTargetType type)
			: base(manager, detectionRect, type)
		{
			_manager = manager;
		}

		/// <inheritdoc/>
		protected override void Drop(LayoutAnchorableFloatingWindow floatingWindow)
		{
			switch (Type)
			{
				case DropTargetType.DockingManagerDockLeft:
					{
						if (_manager.Layout.RootPanel.Orientation != Orientation.Horizontal &&
							_manager.Layout.RootPanel.Children.Count == 1)
							_manager.Layout.RootPanel.Orientation = Orientation.Horizontal;

						if (_manager.Layout.RootPanel.Orientation == Orientation.Horizontal)
						{
							var layoutAnchorablePaneGroup = floatingWindow.RootPanel as LayoutAnchorablePaneGroup;
							if (layoutAnchorablePaneGroup != null &&
								layoutAnchorablePaneGroup.Orientation == Orientation.Horizontal)
							{
								var childrenToTransfer = layoutAnchorablePaneGroup.Children.ToArray();
								for (int i = 0; i < childrenToTransfer.Length; i++)
									_manager.Layout.RootPanel.Children.Insert(i, childrenToTransfer[i]);
							}
							else
							{
								_manager.Layout.RootPanel.Children.Insert(0, floatingWindow.RootPanel);
							}
						}
						else
						{
							var newOrientedPanel = new LayoutPanel()
							{
								Orientation = Orientation.Horizontal
							};

							newOrientedPanel.Children.Add(floatingWindow.RootPanel);
							newOrientedPanel.Children.Add(_manager.Layout.RootPanel);

							_manager.Layout.RootPanel = newOrientedPanel;
						}
					}

					break;

				case DropTargetType.DockingManagerDockRight:
					{
						if (_manager.Layout.RootPanel.Orientation != Orientation.Horizontal &&
							_manager.Layout.RootPanel.Children.Count == 1)
							_manager.Layout.RootPanel.Orientation = Orientation.Horizontal;

						if (_manager.Layout.RootPanel.Orientation == Orientation.Horizontal)
						{
							var layoutAnchorablePaneGroup = floatingWindow.RootPanel as LayoutAnchorablePaneGroup;
							if (layoutAnchorablePaneGroup != null &&
								layoutAnchorablePaneGroup.Orientation == Orientation.Horizontal)
							{
								var childrenToTransfer = layoutAnchorablePaneGroup.Children.ToArray();
								for (int i = 0; i < childrenToTransfer.Length; i++)
									_manager.Layout.RootPanel.Children.Add(childrenToTransfer[i]);
							}
							else
							{
								_manager.Layout.RootPanel.Children.Add(floatingWindow.RootPanel);
							}
						}
						else
						{
							var newOrientedPanel = new LayoutPanel()
							{
								Orientation = Orientation.Horizontal
							};

							newOrientedPanel.Children.Add(floatingWindow.RootPanel);
							newOrientedPanel.Children.Insert(0, _manager.Layout.RootPanel);

							_manager.Layout.RootPanel = newOrientedPanel;
						}
					}

					break;

				case DropTargetType.DockingManagerDockTop:
					{
						if (_manager.Layout.RootPanel.Orientation != Orientation.Vertical &&
							_manager.Layout.RootPanel.Children.Count == 1)
							_manager.Layout.RootPanel.Orientation = Orientation.Vertical;

						if (_manager.Layout.RootPanel.Orientation == Orientation.Vertical)
						{
							var layoutAnchorablePaneGroup = floatingWindow.RootPanel as LayoutAnchorablePaneGroup;
							if (layoutAnchorablePaneGroup != null &&
								layoutAnchorablePaneGroup.Orientation == Orientation.Vertical)
							{
								var childrenToTransfer = layoutAnchorablePaneGroup.Children.ToArray();
								for (int i = 0; i < childrenToTransfer.Length; i++)
									_manager.Layout.RootPanel.Children.Insert(i, childrenToTransfer[i]);
							}
							else
							{
								_manager.Layout.RootPanel.Children.Insert(0, floatingWindow.RootPanel);
							}
						}
						else
						{
							var newOrientedPanel = new LayoutPanel()
							{
								Orientation = Orientation.Vertical
							};

							newOrientedPanel.Children.Add(floatingWindow.RootPanel);
							newOrientedPanel.Children.Add(_manager.Layout.RootPanel);

							_manager.Layout.RootPanel = newOrientedPanel;
						}
					}

					break;

				case DropTargetType.DockingManagerDockBottom:
					{
						if (_manager.Layout.RootPanel.Orientation != Orientation.Vertical &&
							_manager.Layout.RootPanel.Children.Count == 1)
							_manager.Layout.RootPanel.Orientation = Orientation.Vertical;

						if (_manager.Layout.RootPanel.Orientation == Orientation.Vertical)
						{
							var layoutAnchorablePaneGroup = floatingWindow.RootPanel as LayoutAnchorablePaneGroup;
							if (layoutAnchorablePaneGroup != null &&
								layoutAnchorablePaneGroup.Orientation == Orientation.Vertical)
							{
								var childrenToTransfer = layoutAnchorablePaneGroup.Children.ToArray();
								for (int i = 0; i < childrenToTransfer.Length; i++)
									_manager.Layout.RootPanel.Children.Add(childrenToTransfer[i]);
							}
							else
							{
								_manager.Layout.RootPanel.Children.Add(floatingWindow.RootPanel);
							}
						}
						else
						{
							var newOrientedPanel = new LayoutPanel()
							{
								Orientation = Orientation.Vertical
							};

							newOrientedPanel.Children.Add(floatingWindow.RootPanel);
							newOrientedPanel.Children.Insert(0, _manager.Layout.RootPanel);

							_manager.Layout.RootPanel = newOrientedPanel;
						}
					}

					break;
			}

			base.Drop(floatingWindow);
		}

		/// <inheritdoc/>
		public override Geometry GetPreviewPath(
			OverlayWindow overlayWindow,
			LayoutFloatingWindow floatingWindowModel)
		{
			var anchorableFloatingWindowModel = floatingWindowModel as LayoutAnchorableFloatingWindow;
			var layoutAnchorablePane = anchorableFloatingWindowModel.RootPanel as ILayoutPositionableElement;
			var layoutAnchorablePaneWithActualSize = anchorableFloatingWindowModel.RootPanel as ILayoutPositionableElementWithActualSize;

			// Work in the device independent coordinates of the overlay: the preferred sizes below are DIPs.
			// The preview targets the area of the root panel, which is where a pane docked at the outer edge
			// of the manager ends up; the side bars around it are not part of that area.
			var targetElement = TargetElement.LayoutRootPanel is Visual rootPanel && rootPanel.IsEffectivelyVisible
				? rootPanel
				: (Visual)TargetElement;
			var targetScreenRect = overlayWindow.ScreenToLocal(targetElement.GetScreenArea());

			// Preferred dock size used by the outer-edge rules: width for Left/Right, height for Top/Bottom.
			// The rendered actual size is preferred, but on backends where a Star pane is not yet laid
			// out (LibreWPF collapses it to its minimum while a floating window is being dragged) it is
			// unusable, so fall back to the floating window's size kept on the model - otherwise the
			// preview box collapses to a sliver that never matches the pane after the drop.
			var forVertical = Type == DropTargetType.DockingManagerDockTop || Type == DropTargetType.DockingManagerDockBottom;
			var dockSize = forVertical ? layoutAnchorablePane.DockHeight : layoutAnchorablePane.DockWidth;
			var actualSize = forVertical ? layoutAnchorablePaneWithActualSize.ActualHeight : layoutAnchorablePaneWithActualSize.ActualWidth;
			var minSize = forVertical ? layoutAnchorablePane.CalculatedDockMinHeight() : layoutAnchorablePane.CalculatedDockMinWidth();
			var floatingSize = forVertical ? layoutAnchorablePaneWithActualSize.FloatingHeight : layoutAnchorablePaneWithActualSize.FloatingWidth;
			var preferredSize = dockSize.IsAbsolute
				? dockSize.Value
				: (actualSize > minSize ? actualSize : floatingSize);

			if (OverlayPreviewRules.TryComputeManagerPreviewRect(
				Type,
				targetScreenRect.Width,
				targetScreenRect.Height,
				preferredSize,
				out var left,
				out var top,
				out var width,
				out var height))
			{
				var previewBoxRect = new Rect(
					targetScreenRect.Left + left,
					targetScreenRect.Top + top,
					width,
					height);

				return new RectangleGeometry(previewBoxRect);
			}

			throw new InvalidOperationException();
		}
	}
}