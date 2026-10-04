using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using AvalonDock.Layout;

namespace AvalonDock.Controls
{
	/// <summary>
	/// The flyout that slides an auto-hidden anchorable out of its side bar.
	/// </summary>
	/// <remarks>
	/// The WPF library hosts the flyout in a child HWND so it can float above other HWND hosted content. Here it
	/// is an ordinary control in the <see cref="DockingManager"/> template (<c>PART_AutoHideArea</c>), aligned
	/// to the side its anchor belongs to - the same in-tree hosting the LibreWPF port had to fall back to, since
	/// Avalonia renders everything of a window in one surface.
	/// </remarks>
	public class LayoutAutoHideWindowControl : Border, ILayoutControl
	{
		private LayoutAnchorControl _anchor;
		private LayoutAnchorable _model;
		private Grid _internalGrid = null;
		private AnchorSide _side;
		private LayoutGridResizerControl _resizer = null;
		private DockingManager _manager;
		private double _sizeAtDragStart;

		/// <summary><see cref="AnchorableStyle"/> property.</summary>
		public static readonly StyledProperty<ControlTheme> AnchorableStyleProperty =
			AvaloniaProperty.Register<LayoutAutoHideWindowControl, ControlTheme>(nameof(AnchorableStyle));

		/// <summary>Initializes a new instance of the <see cref="LayoutAutoHideWindowControl"/> class.</summary>
		internal LayoutAutoHideWindowControl()
		{
			IsVisible = false;
			Focusable = true;
			ZIndex = 100;
		}

		/// <summary>Gets or sets the theme applied to the <see cref="LayoutAnchorableControl"/> hosted by the flyout.</summary>
		public ControlTheme AnchorableStyle
		{
			get => GetValue(AnchorableStyleProperty);
			set => SetValue(AnchorableStyleProperty, value);
		}

		/// <inheritdoc/>
		public ILayoutElement Model => _model;

		/// <summary>Gets the anchorable control currently shown in the flyout.</summary>
		internal LayoutAnchorableControl InternalHost { get; private set; }

		/// <summary>Gets a value indicating whether the flyout is being resized.</summary>
		internal bool IsResizing { get; private set; }

		/// <summary>Gets a value indicating whether the pointer is over the flyout or over the anchor it belongs to.</summary>
		internal bool IsPointerOverFlyoutOrAnchor => IsPointerOver || (_anchor?.IsPointerOver ?? false);

		/// <summary>Shows the flyout for the given anchor.</summary>
		/// <param name="anchor">The anchor whose anchorable is shown.</param>
		internal void Show(LayoutAnchorControl anchor)
		{
			if (_model != null) Hide();

			// The anchorable can be selected while the layout is being restructured and its parent chain
			// is not (or no longer) attached to an anchor side; showing the flyout is not possible then.
			var anchorSide = anchor.Model?.Parent?.Parent as LayoutAnchorSide;
			var manager = anchor.Model?.Root?.Manager;
			if (anchorSide == null || manager == null)
				return;

			_anchor = anchor;
			_model = anchor.Model as LayoutAnchorable;
			_side = anchorSide.Side;
			_manager = manager;
			CreateInternalGrid();
			_model.PropertyChanged += Model_PropertyChanged;
			IsVisible = true;
			InvalidateMeasure();
		}

		/// <summary>Hides the flyout.</summary>
		internal void Hide()
		{
			if (_model == null) return;
			_model.PropertyChanged -= Model_PropertyChanged;
			RemoveInternalGrid();
			_anchor = null;
			_model = null;
			_manager = null;
			IsVisible = false;
		}

		private void Model_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (e.PropertyName != nameof(LayoutAnchorable.IsAutoHidden)) return;
			if (!_model.IsAutoHidden) _manager.HideAutoHideWindow(_anchor);
		}

		private void CreateInternalGrid()
		{
			_internalGrid = new Grid { FlowDirection = FlowDirection.LeftToRight };
			_internalGrid[!Panel.BackgroundProperty] = this[!BackgroundProperty];

			InternalHost = new LayoutAnchorableControl { Model = _model };
			if (AnchorableStyle != null) InternalHost.Theme = AnchorableStyle;

			KeyboardNavigation.SetTabNavigation(_internalGrid, KeyboardNavigationMode.Cycle);
			_resizer = new LayoutGridResizerControl();
			_resizer.DragStarted += OnResizerDragStarted;
			_resizer.DragDelta += OnResizerDragDelta;
			_resizer.DragCompleted += OnResizerDragCompleted;

			var width = _model.AutoHideWidth == 0.0 ? Math.Max(_model.AutoHideMinWidth, 150) : _model.AutoHideWidth;
			var height = _model.AutoHideHeight == 0.0 ? Math.Max(_model.AutoHideMinHeight, 100) : _model.AutoHideHeight;

			switch (_side)
			{
				case AnchorSide.Right:
					_internalGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(_manager.GridSplitterWidth) });
					_internalGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width, GridUnitType.Pixel) });
					Grid.SetColumn(_resizer, 0);
					Grid.SetColumn(InternalHost, 1);
					_resizer.Cursor = new Cursor(StandardCursorType.SizeWestEast);
					HorizontalAlignment = HorizontalAlignment.Right;
					VerticalAlignment = VerticalAlignment.Stretch;
					break;

				case AnchorSide.Left:
					_internalGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width, GridUnitType.Pixel) });
					_internalGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(_manager.GridSplitterWidth) });
					Grid.SetColumn(InternalHost, 0);
					Grid.SetColumn(_resizer, 1);
					_resizer.Cursor = new Cursor(StandardCursorType.SizeWestEast);
					HorizontalAlignment = HorizontalAlignment.Left;
					VerticalAlignment = VerticalAlignment.Stretch;
					break;

				case AnchorSide.Top:
					_internalGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(height, GridUnitType.Pixel) });
					_internalGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(_manager.GridSplitterHeight) });
					Grid.SetRow(InternalHost, 0);
					Grid.SetRow(_resizer, 1);
					_resizer.Cursor = new Cursor(StandardCursorType.SizeNorthSouth);
					VerticalAlignment = VerticalAlignment.Top;
					HorizontalAlignment = HorizontalAlignment.Stretch;
					break;

				case AnchorSide.Bottom:
					_internalGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(_manager.GridSplitterHeight) });
					_internalGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(height, GridUnitType.Pixel) });
					Grid.SetRow(_resizer, 0);
					Grid.SetRow(InternalHost, 1);
					_resizer.Cursor = new Cursor(StandardCursorType.SizeNorthSouth);
					VerticalAlignment = VerticalAlignment.Bottom;
					HorizontalAlignment = HorizontalAlignment.Stretch;
					break;
			}

			_internalGrid.Children.Add(_resizer);
			_internalGrid.Children.Add(InternalHost);
			Child = _internalGrid;
		}

		private void RemoveInternalGrid()
		{
			if (_resizer != null)
			{
				_resizer.DragStarted -= OnResizerDragStarted;
				_resizer.DragDelta -= OnResizerDragDelta;
				_resizer.DragCompleted -= OnResizerDragCompleted;
			}

			Child = null;
			InternalHost = null;
			_internalGrid = null;
			_resizer = null;
		}

		private bool IsHorizontalSide => _side == AnchorSide.Left || _side == AnchorSide.Right;

		private void OnResizerDragStarted(object sender, VectorEventArgs e)
		{
			IsResizing = true;
			_sizeAtDragStart = IsHorizontalSide ? InternalHost.Bounds.Width : InternalHost.Bounds.Height;
		}

		private void OnResizerDragDelta(object sender, VectorEventArgs e) => ApplyResize(e.Vector, false);

		private void OnResizerDragCompleted(object sender, VectorEventArgs e)
		{
			ApplyResize(e.Vector, true);
			IsResizing = false;
		}

		// Avalonia renders the flyout in the window's own surface, so it is resized live instead of through
		// the ghost window the WPF library has to show over its child HWND.
		private void ApplyResize(Vector delta, bool commit)
		{
			if (_model == null || _internalGrid == null) return;
			var available = Parent is Visual parent ? parent.Bounds.Size : Bounds.Size;
			double newSize;
			switch (_side)
			{
				case AnchorSide.Right:
					newSize = MathHelper.MinMax(_sizeAtDragStart - delta.X, _model.AutoHideMinWidth, Math.Max(_model.AutoHideMinWidth, available.Width - 25));
					_internalGrid.ColumnDefinitions[1].Width = new GridLength(newSize, GridUnitType.Pixel);
					if (commit) _model.AutoHideWidth = newSize;
					break;

				case AnchorSide.Left:
					newSize = MathHelper.MinMax(_sizeAtDragStart + delta.X, _model.AutoHideMinWidth, Math.Max(_model.AutoHideMinWidth, available.Width - 25));
					_internalGrid.ColumnDefinitions[0].Width = new GridLength(newSize, GridUnitType.Pixel);
					if (commit) _model.AutoHideWidth = newSize;
					break;

				case AnchorSide.Top:
					newSize = MathHelper.MinMax(_sizeAtDragStart + delta.Y, _model.AutoHideMinHeight, Math.Max(_model.AutoHideMinHeight, available.Height - 25));
					_internalGrid.RowDefinitions[0].Height = new GridLength(newSize, GridUnitType.Pixel);
					if (commit) _model.AutoHideHeight = newSize;
					break;

				case AnchorSide.Bottom:
					newSize = MathHelper.MinMax(_sizeAtDragStart - delta.Y, _model.AutoHideMinHeight, Math.Max(_model.AutoHideMinHeight, available.Height - 25));
					_internalGrid.RowDefinitions[1].Height = new GridLength(newSize, GridUnitType.Pixel);
					if (commit) _model.AutoHideHeight = newSize;
					break;
			}
		}
	}
}
