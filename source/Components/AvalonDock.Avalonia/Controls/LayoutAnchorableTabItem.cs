using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using AvalonDock.Layout;

namespace AvalonDock.Controls
{
	/// <summary>
	/// The header of an anchorable tab. Dragging it over the other tabs of its pane reorders them, and dragging
	/// it out of the tab strip floats the anchorable.
	/// </summary>
	public class LayoutAnchorableTabItem : TemplatedControl
	{
		private bool _isMouseDown;
		private bool _isDragging;
		private Point _mouseDownPoint;

		/// <summary><see cref="Model"/> property.</summary>
		public static readonly StyledProperty<LayoutContent> ModelProperty =
			AvaloniaProperty.Register<LayoutAnchorableTabItem, LayoutContent>(nameof(Model));

		/// <summary><see cref="LayoutItem"/> property.</summary>
		public static readonly DirectProperty<LayoutAnchorableTabItem, LayoutItem> LayoutItemProperty =
			AvaloniaProperty.RegisterDirect<LayoutAnchorableTabItem, LayoutItem>(nameof(LayoutItem), o => o.LayoutItem);

		private LayoutItem _layoutItem;

		/// <summary>Gets or sets the anchorable this tab belongs to.</summary>
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
			if (change.Property == ModelProperty)
				LayoutItem = Model?.Root?.Manager?.GetLayoutItemFromModel(Model);
		}

		/// <inheritdoc/>
		protected override void OnPointerPressed(PointerPressedEventArgs e)
		{
			base.OnPointerPressed(e);
			if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
			if (Model is LayoutAnchorable anchorable && !anchorable.CanMove) return;
			_isMouseDown = true;
			_isDragging = false;
			_mouseDownPoint = e.GetPosition(this);
			e.Pointer.Capture(this);
		}

		/// <inheritdoc/>
		protected override void OnPointerMoved(PointerEventArgs e)
		{
			base.OnPointerMoved(e);
			if (!_isMouseDown || !ReferenceEquals(e.Pointer.Captured, this)) return;
			if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
			{
				_isMouseDown = false;
				return;
			}

			var position = e.GetPosition(this);
			if (!_isDragging)
			{
				if (Math.Abs(position.X - _mouseDownPoint.X) <= LayoutDocumentTabItem.DragThreshold &&
					Math.Abs(position.Y - _mouseDownPoint.Y) <= LayoutDocumentTabItem.DragThreshold)
					return;
				_isDragging = true;
			}

			var panel = this.FindVisualAncestor<AnchorablePaneTabPanel>();
			var screenPoint = this.LocalToScreen(position);
			var panelArea = panel?.GetScreenArea() ?? default;
			if (panel == null || !panelArea.Inflate(new Thickness(0, panelArea.Height / 2)).Contains(screenPoint))
			{
				_isMouseDown = false;
				var manager = Model?.Root?.Manager;
				if (manager == null) return;
				var dragStart = DragStartInfo.FromPointerEvent(e, this, new Vector(position.X + 8, position.Y + 4));
				manager.StartDraggingFloatingWindowForContent(Model, dragStart);
				return;
			}

			// Reorder: move this anchorable to the slot of the tab under the pointer.
			var targetTab = panel.Children.OfType<Control>()
				.Select(c => c.FindVisualChildren<LayoutAnchorableTabItem>().FirstOrDefault())
				.FirstOrDefault(t => t != null && t != this && t.GetScreenArea().Contains(screenPoint));
			if (targetTab?.Model == null) return;
			var containerPane = Model.Parent as ILayoutPane;
			if (containerPane is LayoutAnchorablePane layoutAnchorablePane && !layoutAnchorablePane.CanRepositionItems) return;
			if (containerPane?.Parent is LayoutAnchorablePaneGroup layoutAnchorablePaneGroup && !layoutAnchorablePaneGroup.CanRepositionItems) return;
			var childrenList = Model.Parent.Children.ToList();
			var oldIndex = childrenList.IndexOf(Model);
			var newIndex = childrenList.IndexOf(targetTab.Model);
			if (oldIndex > -1 && newIndex > -1 && newIndex < containerPane.ChildrenCount) containerPane.MoveChild(oldIndex, newIndex);
		}

		/// <inheritdoc/>
		protected override void OnPointerReleased(PointerReleasedEventArgs e)
		{
			var wasClick = _isMouseDown && !_isDragging;
			_isMouseDown = false;
			_isDragging = false;
			if (ReferenceEquals(e.Pointer.Captured, this)) e.Pointer.Capture(null);
			base.OnPointerReleased(e);
			if (wasClick && Model != null) Model.IsActive = true;
		}

		/// <inheritdoc/>
		protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
		{
			_isMouseDown = false;
			_isDragging = false;
			base.OnPointerCaptureLost(e);
		}
	}
}
