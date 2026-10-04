using System;
using Avalonia;
using Avalonia.VisualTree;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Threading;
using AvalonDock.Layout;

namespace AvalonDock.Controls
{
	/// <summary>
	/// The anchor (side tab) of an auto-hidden anchorable. Clicking it, or hovering it when the anchorable allows
	/// that, slides the anchorable out in the auto-hide flyout.
	/// </summary>
	public class LayoutAnchorControl : TemplatedControl, ILayoutControl
	{
		private readonly LayoutAnchorable _model;
		private DispatcherTimer _openUpTimer = null;

		/// <summary><see cref="Side"/> property.</summary>
		public static readonly DirectProperty<LayoutAnchorControl, AnchorSide> SideProperty =
			AvaloniaProperty.RegisterDirect<LayoutAnchorControl, AnchorSide>(nameof(Side), o => o.Side);

		private AnchorSide _side;

		/// <summary>Initializes a new instance of the <see cref="LayoutAnchorControl"/> class.</summary>
		/// <param name="model">The anchorable.</param>
		internal LayoutAnchorControl(LayoutAnchorable model)
		{
			_model = model;
			_model.IsActiveChanged += Model_IsActiveChanged;
			_model.IsSelectedChanged += Model_IsSelectedChanged;
			Side = _model.FindParent<LayoutAnchorSide>()?.Side ?? AnchorSide.Left;
			Classes.Add(Side.ToString().ToLowerInvariant());
		}

		/// <inheritdoc/>
		public ILayoutElement Model => _model;

		/// <summary>Gets the side bar the anchor sits on.</summary>
		public AnchorSide Side
		{
			get => _side;
			private set => SetAndRaise(SideProperty, ref _side, value);
		}

		/// <inheritdoc/>
		protected override void OnPointerPressed(PointerPressedEventArgs e)
		{
			base.OnPointerPressed(e);
			if (e.Handled) return;
			var point = e.GetCurrentPoint(this);
			var manager = _model.Root?.Manager;
			if (manager == null) return;

			if (point.Properties.IsLeftButtonPressed && e.ClickCount == 2 && manager.AllowAnchorDoubleClickDock)
			{
				manager.ExecuteAutoHideCommand(_model);
				e.Handled = true;
				return;
			}

			if (point.Properties.IsLeftButtonPressed)
			{
				manager.ShowAutoHideWindow(this);
				_model.IsActive = true;
			}
		}

		/// <inheritdoc/>
		protected override void OnPointerReleased(PointerReleasedEventArgs e)
		{
			base.OnPointerReleased(e);
			if (e.Handled || e.InitialPressMouseButton != MouseButton.Right) return;
			var manager = _model.Root?.Manager;
			if (manager == null || !manager.AllowAnchorRightClickContextMenu) return;
			var layoutItem = manager.GetLayoutItemFromModel(_model);
			var contextMenu = manager.AnchorableContextMenu;
			if (contextMenu == null || layoutItem == null) return;
			contextMenu.Placement = PlacementMode.Pointer;
			contextMenu.DataContext = layoutItem;
			contextMenu.Open(this);
			e.Handled = true;
		}

		/// <inheritdoc/>
		protected override void OnPointerEntered(PointerEventArgs e)
		{
			base.OnPointerEntered(e);

			// If the model wants to auto-show itself on hover then initiate the show action
			if (!e.Handled && _model.CanShowOnHover)
			{
				StopOpenUpTimer();
				_openUpTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(400), DispatcherPriority.Background, OpenUpTimer_Tick);
				_openUpTimer.Start();
			}
		}

		/// <inheritdoc/>
		protected override void OnPointerExited(PointerEventArgs e)
		{
			StopOpenUpTimer();
			base.OnPointerExited(e);
		}

		private void StopOpenUpTimer()
		{
			if (_openUpTimer == null) return;
			_openUpTimer.Stop();
			_openUpTimer.Tick -= OpenUpTimer_Tick;
			_openUpTimer = null;
		}

		private void Model_IsSelectedChanged(object sender, EventArgs e)
		{
			if (!_model.IsAutoHidden)
			{
				_model.IsSelectedChanged -= Model_IsSelectedChanged;
			}
			else if (_model.IsSelected)
			{
				if (CanShowAutoHideWindow())
					_model.Root.Manager.ShowAutoHideWindow(this);
				_model.IsSelected = false;
			}
		}

		private void Model_IsActiveChanged(object sender, EventArgs e)
		{
			if (!_model.IsAutoHidden)
				_model.IsActiveChanged -= Model_IsActiveChanged;
			else if (_model.IsActive && CanShowAutoHideWindow())
				_model.Root.Manager.ShowAutoHideWindow(this);
		}

		private bool CanShowAutoHideWindow()
		{
			var manager = _model.Root?.Manager;
			if (manager == null || !manager.SupportsAutoHideFlyout)
				return false;

			return _model.Parent?.Parent is LayoutAnchorSide && this.IsAttachedToVisualTree();
		}

		private void OpenUpTimer_Tick(object sender, EventArgs e)
		{
			StopOpenUpTimer();
			_model.Root?.Manager?.ShowAutoHideWindow(this);
		}
	}
}
