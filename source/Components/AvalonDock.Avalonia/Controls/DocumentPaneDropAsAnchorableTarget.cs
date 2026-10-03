using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using AvalonDock.Layout;

namespace AvalonDock.Controls
{
	/// <summary>
	/// Represents the document pane drop as anchorable target.
	/// </summary>
	internal class DocumentPaneDropAsAnchorableTarget : DropTarget<LayoutDocumentPaneControl>
	{
		private LayoutDocumentPaneControl _targetPane;
		private int _tabIndex = -1;

		/// <summary>
		/// Initializes a new instance of the <see cref="DocumentPaneDropAsAnchorableTarget"/> class.
		/// </summary>
		/// <param name="paneControl">The pane control.</param>
		/// <param name="detectionRect">The detection rectangle.</param>
		/// <param name="type">The drop target type.</param>
		internal DocumentPaneDropAsAnchorableTarget(
			LayoutDocumentPaneControl paneControl,
			Rect detectionRect,
			DropTargetType type)
			: base(paneControl, detectionRect, type)
		{
			_targetPane = paneControl;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="DocumentPaneDropAsAnchorableTarget"/> class.
		/// </summary>
		/// <param name="paneControl">The pane control.</param>
		/// <param name="detectionRect">The detection rectangle.</param>
		/// <param name="type">The drop target type.</param>
		/// <param name="tabIndex">The tab index.</param>
		internal DocumentPaneDropAsAnchorableTarget(
			LayoutDocumentPaneControl paneControl,
			Rect detectionRect,
			DropTargetType type,
			int tabIndex)
			: base(paneControl, detectionRect, type)
		{
			_targetPane = paneControl;
			_tabIndex = tabIndex;
		}

		/// <inheritdoc/>
		protected override void Drop(LayoutAnchorableFloatingWindow floatingWindow)
		{
			ILayoutDocumentPane targetModel = _targetPane.Model as ILayoutDocumentPane;
			LayoutDocumentPaneGroup parentGroup;
			LayoutPanel parentGroupPanel;
			FindParentLayoutDocumentPane(targetModel, out parentGroup, out parentGroupPanel);

			switch (Type)
			{
				case DropTargetType.DocumentPaneDockAsAnchorableBottom:
					{
						if (parentGroupPanel != null &&
							parentGroupPanel.ChildrenCount == 1)
							parentGroupPanel.Orientation = Orientation.Vertical;

						if (parentGroupPanel != null &&
							parentGroupPanel.Orientation == Orientation.Vertical)
						{
							parentGroupPanel.Children.Insert(
								parentGroupPanel.IndexOfChild(parentGroup != null ? parentGroup : targetModel) + 1,
								floatingWindow.RootPanel);
						}
						else if (parentGroupPanel != null)
						{
							var newParentPanel = new LayoutPanel() { Orientation = Orientation.Vertical };
							parentGroupPanel.ReplaceChild(parentGroup != null ? parentGroup : targetModel, newParentPanel);
							newParentPanel.Children.Add(parentGroup != null ? parentGroup : targetModel);
							newParentPanel.Children.Add(floatingWindow.RootPanel);
						}
						else
						{
							throw new NotImplementedException();
						}
					}

					break;

				case DropTargetType.DocumentPaneDockAsAnchorableTop:
					{
						if (parentGroupPanel != null &&
							parentGroupPanel.ChildrenCount == 1)
							parentGroupPanel.Orientation = Orientation.Vertical;

						if (parentGroupPanel != null &&
							parentGroupPanel.Orientation == Orientation.Vertical)
						{
							parentGroupPanel.Children.Insert(
								parentGroupPanel.IndexOfChild(parentGroup != null ? parentGroup : targetModel),
								floatingWindow.RootPanel);
						}
						else if (parentGroupPanel != null)
						{
							var newParentPanel = new LayoutPanel() { Orientation = Orientation.Vertical };
							parentGroupPanel.ReplaceChild(parentGroup != null ? parentGroup : targetModel, newParentPanel);
							newParentPanel.Children.Add(parentGroup != null ? parentGroup : targetModel);
							newParentPanel.Children.Insert(0, floatingWindow.RootPanel);
						}
						else
						{
							throw new NotImplementedException();
						}
					}

					break;

				case DropTargetType.DocumentPaneDockAsAnchorableLeft:
					{
						if (parentGroupPanel != null &&
							parentGroupPanel.ChildrenCount == 1)
							parentGroupPanel.Orientation = Orientation.Horizontal;

						if (parentGroupPanel != null &&
							parentGroupPanel.Orientation == Orientation.Horizontal)
						{
							parentGroupPanel.Children.Insert(
								parentGroupPanel.IndexOfChild(parentGroup != null ? parentGroup : targetModel),
								floatingWindow.RootPanel);
						}
						else if (parentGroupPanel != null)
						{
							var newParentPanel = new LayoutPanel() { Orientation = Orientation.Horizontal };
							parentGroupPanel.ReplaceChild(parentGroup != null ? parentGroup : targetModel, newParentPanel);
							newParentPanel.Children.Add(parentGroup != null ? parentGroup : targetModel);
							newParentPanel.Children.Insert(0, floatingWindow.RootPanel);
						}
						else
						{
							throw new NotImplementedException();
						}
					}

					break;

				case DropTargetType.DocumentPaneDockAsAnchorableRight:
					{
						if (parentGroupPanel != null &&
							parentGroupPanel.ChildrenCount == 1)
							parentGroupPanel.Orientation = Orientation.Horizontal;

						if (parentGroupPanel != null &&
							parentGroupPanel.Orientation == Orientation.Horizontal)
						{
							parentGroupPanel.Children.Insert(
								parentGroupPanel.IndexOfChild(parentGroup != null ? parentGroup : targetModel) + 1,
								floatingWindow.RootPanel);
						}
						else if (parentGroupPanel != null)
						{
							var newParentPanel = new LayoutPanel() { Orientation = Orientation.Horizontal };
							parentGroupPanel.ReplaceChild(parentGroup != null ? parentGroup : targetModel, newParentPanel);
							newParentPanel.Children.Add(parentGroup != null ? parentGroup : targetModel);
							newParentPanel.Children.Add(floatingWindow.RootPanel);
						}
						else
						{
							throw new NotImplementedException();
						}
					}

					break;
			}

			base.Drop(floatingWindow);
		}

		/// <inheritdoc/>
		public override Geometry GetPreviewPath(OverlayWindow overlayWindow, LayoutFloatingWindow floatingWindowModel)
		{
			Rect targetScreenRect;
			ILayoutDocumentPane targetModel = _targetPane.Model as ILayoutDocumentPane;
			var manager = targetModel.Root.Manager;

			// ILayoutDocumentPane targetModel = _targetPane.Model as ILayoutDocumentPane;
			LayoutDocumentPaneGroup parentGroup;
			LayoutPanel parentGroupPanel;
			if (!FindParentLayoutDocumentPane(targetModel, out parentGroup, out parentGroupPanel))
				return null;

			// if (targetModel.Parent is LayoutDocumentPaneGroup)
			// {
			//    var parentGroup = targetModel.Parent as LayoutDocumentPaneGroup;
			//    var documentPaneGroupControl = manager.FindLogicalChildren<LayoutDocumentPaneGroupControl>().First(d => d.Model == parentGroup);
			//    targetScreenRect = documentPaneGroupControl.GetScreenArea();
			// }
			// else
			// {
			//    var documentPaneControl = manager.FindLogicalChildren<LayoutDocumentPaneControl>().First(d => d.Model == targetModel);
			//    targetScreenRect = documentPaneControl.GetScreenArea();
			// }

			// var parentPanel = targetModel.FindParent<LayoutPanel>();
			var documentPaneControl = manager.FindVisualChildren<Control>().OfType<ILayoutControl>()
				.FirstOrDefault(d => parentGroup != null ? d.Model == parentGroup : d.Model == parentGroupPanel) as Control;
			if (documentPaneControl == null) return null;
			targetScreenRect = overlayWindow.ScreenToLocal(documentPaneControl.GetScreenArea());

			switch (Type)
			{
				case DropTargetType.DocumentPaneDockAsAnchorableBottom:
					return new RectangleGeometry(new Rect(targetScreenRect.X, targetScreenRect.Y + targetScreenRect.Height - targetScreenRect.Height / 3.0, targetScreenRect.Width, targetScreenRect.Height / 3.0));

				case DropTargetType.DocumentPaneDockAsAnchorableTop:
					return new RectangleGeometry(new Rect(targetScreenRect.X, targetScreenRect.Y, targetScreenRect.Width, targetScreenRect.Height / 3.0));

				case DropTargetType.DocumentPaneDockAsAnchorableRight:
					return new RectangleGeometry(new Rect(targetScreenRect.X + targetScreenRect.Width - targetScreenRect.Width / 3.0, targetScreenRect.Y, targetScreenRect.Width / 3.0, targetScreenRect.Height));

				case DropTargetType.DocumentPaneDockAsAnchorableLeft:
					return new RectangleGeometry(new Rect(targetScreenRect.X, targetScreenRect.Y, targetScreenRect.Width / 3.0, targetScreenRect.Height));
			}

			return null;
		}

		private bool FindParentLayoutDocumentPane(ILayoutDocumentPane documentPane, out LayoutDocumentPaneGroup containerPaneGroup, out LayoutPanel containerPanel)
		{
			containerPaneGroup = null;
			containerPanel = null;

			if (documentPane.Parent is LayoutPanel)
			{
				containerPaneGroup = null;
				containerPanel = documentPane.Parent as LayoutPanel;
				return true;
			}
			else if (documentPane.Parent is LayoutDocumentPaneGroup)
			{
				var currentDocumentPaneGroup = documentPane.Parent as LayoutDocumentPaneGroup;
				while (!(currentDocumentPaneGroup.Parent is LayoutPanel))
				{
					currentDocumentPaneGroup = currentDocumentPaneGroup.Parent as LayoutDocumentPaneGroup;

					if (currentDocumentPaneGroup == null)
						break;
				}

				if (currentDocumentPaneGroup == null)
					return false;

				containerPaneGroup = currentDocumentPaneGroup;
				containerPanel = currentDocumentPaneGroup.Parent as LayoutPanel;
				return true;
			}

			return false;
		}
	}
}