using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.VisualTree;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using AvalonDock.Layout;

namespace AvalonDock.Controls
{
	/// <summary>
	/// The header of a document tab. Dragging it reorders the tabs of its pane, and dragging it out of the
	/// tab strip floats the document.
	/// </summary>
	public class LayoutDocumentTabItem : TemplatedControl
	{
		/// <summary>Distance the pointer must move before a press turns into a drag.</summary>
		internal const double DragThreshold = 4.0;

		private List<Rect> _otherTabsScreenArea = null;
		private List<TabItem> _otherTabs = null;
		private Rect _parentDocumentTabPanelScreenArea;
		private DocumentPaneTabPanel _parentDocumentTabPanel;
		private bool _isMouseDown = false;
		private Point _mouseDownPoint;
		private bool _allowDrag = false;

		/// <summary><see cref="Model"/> property.</summary>
		public static readonly StyledProperty<LayoutContent> ModelProperty =
			AvaloniaProperty.Register<LayoutDocumentTabItem, LayoutContent>(nameof(Model));

		/// <summary><see cref="LayoutItem"/> property.</summary>
		public static readonly DirectProperty<LayoutDocumentTabItem, LayoutItem> LayoutItemProperty =
			AvaloniaProperty.RegisterDirect<LayoutDocumentTabItem, LayoutItem>(nameof(LayoutItem), o => o.LayoutItem);

		private LayoutItem _layoutItem;

		/// <summary>Gets or sets the content this tab belongs to.</summary>
		public LayoutContent Model
		{
			get => GetValue(ModelProperty);
			set => SetValue(ModelProperty, value);
		}

		/// <summary>Gets the layout item of <see cref="Model"/>.</summary>
		public LayoutItem LayoutItem
		{
			get => _layoutItem;
			private set => SetAndRaise(LayoutItemProperty, ref _layoutItem, value);
		}

		/// <inheritdoc/>
		protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
		{
			base.OnPropertyChanged(change);
			if (change.Property == ModelProperty) OnModelChanged();
		}

		/// <summary>Called when <see cref="Model"/> changes.</summary>
		protected virtual void OnModelChanged()
		{
			var layoutItem = Model?.Root?.Manager?.GetLayoutItemFromModel(Model);
			LayoutItem = layoutItem;
			if (layoutItem != null) Model.TabItem = this;
		}

		/// <inheritdoc/>
		protected override void OnPointerPressed(PointerPressedEventArgs e)
		{
			base.OnPointerPressed(e);
			var point = e.GetCurrentPoint(this);

			if (point.Properties.IsMiddleButtonPressed)
			{
				if (LayoutItem?.CloseCommand?.CanExecute(null) == true)
				{
					LayoutItem.CloseCommand.Execute(null);
					e.Handled = true;
				}

				return;
			}

			if (!point.Properties.IsLeftButtonPressed) return;

			_allowDrag = false;
			if (Model != null) Model.IsActive = true;
			if (Model is LayoutDocument layoutDocument && !layoutDocument.CanMove) return;
			if (e.ClickCount != 1) return;

			_mouseDownPoint = e.GetPosition(this);
			_isMouseDown = true;
			e.Pointer.Capture(this);
		}

		/// <inheritdoc/>
		protected override void OnPointerMoved(PointerEventArgs e)
		{
			base.OnPointerMoved(e);
			var leftPressed = e.GetCurrentPoint(this).Properties.IsLeftButtonPressed;
			_isMouseDown = leftPressed && _isMouseDown;
			if (_isMouseDown)
			{
				var ptMouseMove = e.GetPosition(this);
				if (Math.Abs(ptMouseMove.X - _mouseDownPoint.X) > DragThreshold || Math.Abs(ptMouseMove.Y - _mouseDownPoint.Y) > DragThreshold)
				{
					UpdateDragDetails();
					_isMouseDown = false;
					_allowDrag = _parentDocumentTabPanel != null;
				}
			}

			if (!leftPressed || !_allowDrag || !ReferenceEquals(e.Pointer.Captured, this)) return;

			var mousePosInScreenCoord = this.LocalToScreen(e.GetPosition(this));
			if (!_parentDocumentTabPanelScreenArea.Contains(mousePosInScreenCoord))
			{
				StartDraggingFloatingWindowForContent(e);
			}
			else
			{
				var indexOfTabItemWithMouseOver = _otherTabsScreenArea.FindIndex(r => r.Contains(mousePosInScreenCoord));
				if (indexOfTabItemWithMouseOver < 0) return;
				var targetModel = _otherTabs[indexOfTabItemWithMouseOver].DataContext as LayoutContent;
				var container = Model.Parent as ILayoutContainer;
				var containerPane = Model.Parent as ILayoutPane;

				if (containerPane is LayoutDocumentPane layoutDocumentPane && !layoutDocumentPane.CanRepositionItems) return;
				if (containerPane?.Parent is LayoutDocumentPaneGroup layoutDocumentPaneGroup && !layoutDocumentPaneGroup.CanRepositionItems) return;
				if (targetModel == null || targetModel == Model) return;

				var model = Model;
				var pointer = e.Pointer;
				var tabPanel = _parentDocumentTabPanel;
				var childrenList = container.Children.ToList();
				containerPane?.MoveChild(childrenList.IndexOf(model), childrenList.IndexOf(targetModel));
				model.IsActive = true;
				tabPanel.UpdateLayout();

				// Moving the item makes the tab control create a new container for it, so the tab item that now
				// shows the model takes over the drag.
				var newTabItem = model.TabItem;
				if (newTabItem != null && !ReferenceEquals(newTabItem, this) && newTabItem.IsAttachedToVisualTree())
				{
					_allowDrag = false;
					newTabItem.ContinueReorderDrag(pointer);
				}
				else
				{
					UpdateDragDetails();
				}
			}
		}

		/// <summary>Takes over a reorder drag from the tab item that showed the model before it was moved.</summary>
		/// <param name="pointer">The dragging pointer.</param>
		private void ContinueReorderDrag(IPointer pointer)
		{
			UpdateDragDetails();
			_isMouseDown = false;
			_allowDrag = _parentDocumentTabPanel != null;
			if (_allowDrag) pointer.Capture(this);
		}

		/// <inheritdoc/>
		protected override void OnPointerReleased(PointerReleasedEventArgs e)
		{
			_isMouseDown = false;
			_allowDrag = false;
			if (ReferenceEquals(e.Pointer.Captured, this)) e.Pointer.Capture(null);
			base.OnPointerReleased(e);
		}

		/// <inheritdoc/>
		protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
		{
			_isMouseDown = false;
			_allowDrag = false;
			base.OnPointerCaptureLost(e);
		}

		private void UpdateDragDetails()
		{
			_parentDocumentTabPanel = this.FindVisualAncestor<DocumentPaneTabPanel>();
			if (_parentDocumentTabPanel == null) return;
			var area = _parentDocumentTabPanel.GetScreenArea();

			// Add vertical buffer to prevent accidental floating when reordering tabs
			_parentDocumentTabPanelScreenArea = area.Inflate(new Thickness(0, area.Height / 2));
			_otherTabs = _parentDocumentTabPanel.Children.OfType<TabItem>().Where(ch => ch.IsVisible && ch.Opacity > 0).ToList();
			var currentTab = this.FindVisualAncestor<TabItem>();
			var currentTabScreenArea = currentTab?.GetScreenArea() ?? this.GetScreenArea();
			_otherTabsScreenArea = _otherTabs.Select(ti =>
			{
				var screenArea = ti.GetScreenArea();
				return new Rect(screenArea.Left, screenArea.Top, currentTabScreenArea.Width, screenArea.Height);
			}).ToList();
		}

		private void StartDraggingFloatingWindowForContent(PointerEventArgs e)
		{
			_allowDrag = false;
			var manager = Model?.Root?.Manager;
			if (manager == null) return;
			var dragStart = DragStartInfo.FromPointerEvent(e, this, new Vector(e.GetPosition(this).X + 8, e.GetPosition(this).Y + 4));
			manager.StartDraggingFloatingWindowForContent(Model, dragStart);
		}
	}
}
