using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using AvalonDock.Layout;

namespace AvalonDock.Controls
{
	/// <summary>
	/// The title bar of an anchorable. Dragging it floats the pane (or moves the floating window it is the only
	/// pane of).
	/// </summary>
	public class AnchorablePaneTitle : TemplatedControl
	{
		private bool _isMouseDown = false;
		private Point _mouseDownPoint;

		/// <summary><see cref="Model"/> property.</summary>
		public static readonly StyledProperty<LayoutAnchorable> ModelProperty =
			AvaloniaProperty.Register<AnchorablePaneTitle, LayoutAnchorable>(nameof(Model));

		/// <summary><see cref="LayoutItem"/> property.</summary>
		public static readonly DirectProperty<AnchorablePaneTitle, LayoutItem> LayoutItemProperty =
			AvaloniaProperty.RegisterDirect<AnchorablePaneTitle, LayoutItem>(nameof(LayoutItem), o => o.LayoutItem);

		private LayoutItem _layoutItem;

		/// <summary>Initializes a new instance of the <see cref="AnchorablePaneTitle"/> class.</summary>
		public AnchorablePaneTitle()
		{
			Focusable = false;
		}

		/// <summary>Gets or sets the anchorable this title belongs to.</summary>
		public LayoutAnchorable Model
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
		protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
		{
			base.OnAttachedToVisualTree(e);
			if (LayoutItem == null && Model != null) LayoutItem = Model.Root?.Manager?.GetLayoutItemFromModel(Model);
		}

		/// <inheritdoc/>
		protected override void OnPointerPressed(PointerPressedEventArgs e)
		{
			base.OnPointerPressed(e);
			if (e.Handled || Model == null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
			if (!Model.CanMove) return;
			var manager = Model.Root?.Manager;
			if (manager == null || manager.IsDetached(Model)) return;

			var parentFloatingWindow = Model.FindParent<LayoutAnchorableFloatingWindow>();
			if (parentFloatingWindow != null && parentFloatingWindow.Descendents().OfType<LayoutAnchorablePane>().Count() == 1)
			{
				// The pane is the only one of its floating window: dragging the title moves the window itself.
				var floatingWndControl = manager.FloatingWindows.FirstOrDefault(fwc => fwc.Model == parentFloatingWindow);
				floatingWndControl?.BeginCaptionDrag(e);
				return;
			}

			_isMouseDown = true;
			_mouseDownPoint = e.GetPosition(this);
			e.Pointer.Capture(this);
		}

		/// <inheritdoc/>
		protected override void OnPointerMoved(PointerEventArgs e)
		{
			base.OnPointerMoved(e);
			if (!_isMouseDown) return;
			if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
			{
				_isMouseDown = false;
				return;
			}

			var position = e.GetPosition(this);
			if (Math.Abs(position.X - _mouseDownPoint.X) <= LayoutDocumentTabItem.DragThreshold &&
				Math.Abs(position.Y - _mouseDownPoint.Y) <= LayoutDocumentTabItem.DragThreshold)
				return;

			_isMouseDown = false;
			var manager = Model?.Root?.Manager;
			if (manager == null || manager.IsDetached(Model)) return;
			var dragStart = DragStartInfo.FromPointerEvent(e, this, new Vector(position.X, position.Y));
			var pane = this.FindVisualAncestor<LayoutAnchorablePaneControl>();
			if (pane != null)
			{
				manager.StartDraggingFloatingWindowForPane(pane.Model as LayoutAnchorablePane, dragStart);
			}
			else
			{
				// Dragging the title of an auto-hidden anchorable shown in the flyout.
				manager.StartDraggingFloatingWindowForContent(Model, dragStart);
			}
		}

		/// <inheritdoc/>
		protected override void OnPointerReleased(PointerReleasedEventArgs e)
		{
			var wasPressed = _isMouseDown;
			_isMouseDown = false;
			if (ReferenceEquals(e.Pointer.Captured, this)) e.Pointer.Capture(null);
			base.OnPointerReleased(e);
			if (wasPressed && Model != null) Model.IsActive = true;
		}

		/// <inheritdoc/>
		protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
		{
			_isMouseDown = false;
			base.OnPointerCaptureLost(e);
		}
	}
}
