using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using AvalonDock.Layout;

namespace AvalonDock.Controls
{
	/// <summary>
	/// Provides a base class for the controls that lay out a <see cref="LayoutPositionableGroup{T}"/> as a
	/// <see cref="Grid"/> of children separated by <see cref="LayoutGridResizerControl"/> splitters.
	/// </summary>
	/// <typeparam name="T">The type of the child layout elements.</typeparam>
	public abstract class LayoutGridControl<T> : Grid, ILayoutControl, IAdjustableSizeLayout
		where T : class, ILayoutPanelElement
	{
		private readonly LayoutPositionableGroup<T> _model;
		private bool _initialized;
		private ChildrenTreeChange? _asyncRefreshCalled;
		private readonly ReentrantFlag _fixingChildrenDockLengths = new ReentrantFlag();
		private LayoutGridResizerGhost _resizerGhost = null;
		private AdornerLayer _resizerAdornerLayer = null;
		private Size _resizerHostSize;
		private Vector _initialStartPoint;

		/// <summary>Initializes a new instance of the <see cref="LayoutGridControl{T}"/> class.</summary>
		/// <param name="model">The layout model.</param>
		/// <param name="orientation">The initial orientation.</param>
		internal LayoutGridControl(LayoutPositionableGroup<T> model, Orientation orientation)
		{
			_model = model ?? throw new ArgumentNullException(nameof(model));
			FlowDirection = FlowDirection.LeftToRight;
			_model.ChildrenTreeChanged += OnModelChildrenTreeChanged;
		}

		/// <inheritdoc/>
		public ILayoutElement Model => _model;

		/// <summary>Gets the orientation of the model.</summary>
		public Orientation Orientation => (_model as ILayoutOrientableGroup).Orientation;

		private bool _isUpdatingChildren;

		private bool AsyncRefreshCalled => _asyncRefreshCalled != null;

		private void OnModelChildrenTreeChanged(object sender, ChildrenTreeChangedEventArgs args)
		{
			if (args.Change != ChildrenTreeChange.DirectChildrenChanged) return;
			if (_asyncRefreshCalled.HasValue && _asyncRefreshCalled.Value == args.Change) return;
			_asyncRefreshCalled = args.Change;
			Dispatcher.UIThread.Post(
				() =>
				{
					_asyncRefreshCalled = null;
					UpdateChildren();
				},
				DispatcherPriority.Normal);
		}

		/// <summary>Makes the dock lengths of the children consistent with the orientation of the group.</summary>
		protected void FixChildrenDockLengths()
		{
			using (_fixingChildrenDockLengths.Enter())
				OnFixChildrenDockLengths();
		}

		/// <summary>Called by <see cref="FixChildrenDockLengths"/>.</summary>
		protected abstract void OnFixChildrenDockLengths();

		/// <inheritdoc/>
		protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
		{
			base.OnAttachedToVisualTree(e);

			// The first refresh normally happens on the first size change. A control that is put back into the
			// tree with the size it had before never gets one, so refresh here as well.
			if (!_initialized && _model.Root?.Manager != null)
			{
				_initialized = true;
				UpdateChildren();
			}
		}

		/// <inheritdoc/>
		protected override void OnSizeChanged(SizeChangedEventArgs e)
		{
			base.OnSizeChanged(e);
			var modelWithActualSize = _model as ILayoutPositionableElementWithActualSize;
			modelWithActualSize.ActualWidth = Bounds.Width;
			modelWithActualSize.ActualHeight = Bounds.Height;
			if (!_initialized)
			{
				_initialized = true;
				UpdateChildren();
			}

			AdjustFixedChildrenPanelSizes();
		}

		/// <summary>Rebuilds the child controls from the model.</summary>
		internal void UpdateChildren()
		{
			_isUpdatingChildren = true;
			try
			{
				UpdateChildrenCore();
			}
			finally
			{
				_isUpdatingChildren = false;
			}
		}

		private void UpdateChildrenCore()
		{
			var alreadyContainedChildren = Children.OfType<ILayoutControl>().ToArray();
			DetachOldSplitters();
			DetachPropertyChangeHandler();
			Children.Clear();
			ColumnDefinitions.Clear();
			RowDefinitions.Clear();
			var manager = _model?.Root?.Manager;
			if (manager == null) return;
			foreach (var child in _model.Children)
			{
				var foundContainedChild = alreadyContainedChildren.FirstOrDefault(chVM => chVM.Model == child);
				var control = foundContainedChild as Control ?? manager.CreateUIElementForModel(child);
				if (control != null) Children.Add(control);
			}

			CreateSplitters();
			UpdateRowColDefinitions();
			AttachNewSplitters();
			AttachPropertyChangeHandler();
		}

		private void AttachPropertyChangeHandler()
		{
			foreach (var child in Children.OfType<ILayoutControl>())
				child.Model.PropertyChanged += OnChildModelPropertyChanged;
		}

		private void DetachPropertyChangeHandler()
		{
			foreach (var child in Children.OfType<ILayoutControl>())
				child.Model.PropertyChanged -= OnChildModelPropertyChanged;
		}

		private void OnChildModelPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (AsyncRefreshCalled) return;
			if (_fixingChildrenDockLengths.CanEnter && e.PropertyName == nameof(ILayoutPositionableElement.DockWidth) && Orientation == Orientation.Horizontal)
			{
				if (ColumnDefinitions.Count != Children.Count) return;
				var changedElement = sender as ILayoutPositionableElement;
				var childFromModel = Children.OfType<ILayoutControl>().First(ch => ch.Model == changedElement) as Control;
				var indexOfChild = Children.IndexOf(childFromModel);
				ColumnDefinitions[indexOfChild].Width = changedElement.DockWidth;
			}
			else if (_fixingChildrenDockLengths.CanEnter && e.PropertyName == nameof(ILayoutPositionableElement.DockHeight) && Orientation == Orientation.Vertical)
			{
				if (RowDefinitions.Count != Children.Count) return;
				var changedElement = sender as ILayoutPositionableElement;
				var childFromModel = Children.OfType<ILayoutControl>().First(ch => ch.Model == changedElement) as Control;
				var indexOfChild = Children.IndexOf(childFromModel);
				RowDefinitions[indexOfChild].Height = changedElement.DockHeight;
			}
			else if (e.PropertyName == nameof(ILayoutPositionableElement.IsVisible))
			{
				UpdateRowColDefinitions();
			}
		}

		private void UpdateRowColDefinitions()
		{
			var root = _model.Root;
			var manager = root?.Manager;
			if (manager == null) return;

			// The child controls are refreshed asynchronously after the model's children change. Definitions
			// built in between would not match the controls, and the grid cannot measure children whose row or
			// column is out of range - so catch up with the model first.
			if (!_isUpdatingChildren && !ChildControlsMatchModel())
			{
				UpdateChildren();
				return;
			}

			FixChildrenDockLengths();
			RowDefinitions.Clear();
			ColumnDefinitions.Clear();
			if (Orientation == Orientation.Horizontal)
			{
				var iColumn = 0;
				var iChild = 0;
				for (var iChildModel = 0; iChildModel < _model.Children.Count && iChild < Children.Count; iChildModel++, iColumn++, iChild++)
				{
					var childModel = _model.Children[iChildModel] as ILayoutPositionableElement;
					ColumnDefinitions.Add(new ColumnDefinition
					{
						Width = childModel.IsVisible ? childModel.DockWidth : new GridLength(0.0, GridUnitType.Pixel),
						MinWidth = childModel.IsVisible ? childModel.CalculatedDockMinWidth() : 0.0
					});
					SetColumn(Children[iChild], iColumn);

					// append column for splitter
					if (iChild >= Children.Count - 1) continue;
					iChild++;
					iColumn++;

					var nextChildModelVisibleExist = false;
					for (var i = iChildModel + 1; i < _model.Children.Count; i++)
					{
						var nextChildModel = _model.Children[i] as ILayoutPositionableElement;
						if (!nextChildModel.IsVisible) continue;
						nextChildModelVisibleExist = true;
						break;
					}

					ColumnDefinitions.Add(new ColumnDefinition
					{
						Width = childModel.IsVisible && nextChildModelVisibleExist ? new GridLength(manager.GridSplitterWidth) : new GridLength(0.0, GridUnitType.Pixel)
					});
					SetColumn(Children[iChild], iColumn);
				}
			}
			else
			{
				var iRow = 0;
				var iChild = 0;
				for (var iChildModel = 0; iChildModel < _model.Children.Count && iChild < Children.Count; iChildModel++, iRow++, iChild++)
				{
					var childModel = _model.Children[iChildModel] as ILayoutPositionableElement;
					RowDefinitions.Add(new RowDefinition
					{
						Height = childModel.IsVisible ? childModel.DockHeight : new GridLength(0.0, GridUnitType.Pixel),
						MinHeight = childModel.IsVisible ? childModel.CalculatedDockMinHeight() : 0.0
					});
					SetRow(Children[iChild], iRow);

					// append row for splitter (if necessary)
					if (iChild >= Children.Count - 1) continue;
					iChild++;
					iRow++;

					var nextChildModelVisibleExist = false;
					for (var i = iChildModel + 1; i < _model.Children.Count; i++)
					{
						var nextChildModel = _model.Children[i] as ILayoutPositionableElement;
						if (!nextChildModel.IsVisible) continue;
						nextChildModelVisibleExist = true;
						break;
					}

					RowDefinitions.Add(new RowDefinition
					{
						Height = childModel.IsVisible && nextChildModelVisibleExist ? new GridLength(manager.GridSplitterHeight) : new GridLength(0.0, GridUnitType.Pixel)
					});
					SetRow(Children[iChild], iRow);
				}
			}

			// Collapsed children still take part in hit testing in Avalonia when their column has no width,
			// so hide them outright.
			for (var i = 0; i < Children.Count; i++)
				Children[i].IsVisible = IsChildVisible(i);
		}

		private bool ChildControlsMatchModel()
		{
			var controls = Children.OfType<ILayoutControl>().ToList();
			if (controls.Count != _model.Children.Count) return false;
			for (var i = 0; i < controls.Count; i++)
			{
				if (!ReferenceEquals(controls[i].Model, _model.Children[i])) return false;
			}

			return true;
		}

		private void CreateSplitters()
		{
			for (var iChild = 1; iChild < Children.Count; iChild++)
			{
				var splitter = new LayoutGridResizerControl();
				var manager = _model.Root?.Manager;

				if (Orientation == Orientation.Horizontal)
				{
					splitter.Cursor = new Cursor(StandardCursorType.SizeWestEast);
					splitter.Classes.Add("vertical");
					if (manager?.GridSplitterVerticalStyle != null) splitter.Theme = manager.GridSplitterVerticalStyle;
				}
				else
				{
					splitter.Cursor = new Cursor(StandardCursorType.SizeNorthSouth);
					splitter.Classes.Add("horizontal");
					if (manager?.GridSplitterHorizontalStyle != null) splitter.Theme = manager.GridSplitterHorizontalStyle;
				}

				Children.Insert(iChild, splitter);
				iChild++;
			}
		}

		private void DetachOldSplitters()
		{
			foreach (var splitter in Children.OfType<LayoutGridResizerControl>())
			{
				splitter.DragStarted -= OnSplitterDragStarted;
				splitter.DragDelta -= OnSplitterDragDelta;
				splitter.DragCompleted -= OnSplitterDragCompleted;
			}
		}

		private void AttachNewSplitters()
		{
			foreach (var splitter in Children.OfType<LayoutGridResizerControl>())
			{
				splitter.DragStarted += OnSplitterDragStarted;
				splitter.DragDelta += OnSplitterDragDelta;
				splitter.DragCompleted += OnSplitterDragCompleted;
			}
		}

		private void OnSplitterDragStarted(object sender, VectorEventArgs e) => ShowResizerGhost(sender as LayoutGridResizerControl);

		private void OnSplitterDragDelta(object sender, VectorEventArgs e)
		{
			if (_resizerGhost == null) return;
			var splitter = (LayoutGridResizerControl)sender;
			if (Orientation == Orientation.Horizontal)
				_resizerGhost.Position = new Point(MathHelper.MinMax(_initialStartPoint.X + e.Vector.X, 0.0, Math.Max(0.0, _resizerHostSize.Width - splitter.Bounds.Width)) - _initialStartPoint.X, 0);
			else
				_resizerGhost.Position = new Point(0, MathHelper.MinMax(_initialStartPoint.Y + e.Vector.Y, 0.0, Math.Max(0.0, _resizerHostSize.Height - splitter.Bounds.Height)) - _initialStartPoint.Y);
		}

		private void OnSplitterDragCompleted(object sender, VectorEventArgs e)
		{
			var splitter = sender as LayoutGridResizerControl;
			if (_resizerGhost == null) return;

			var delta = Orientation == Orientation.Horizontal ? _resizerGhost.Position.X : _resizerGhost.Position.Y;
			HideResizerGhost();
			ResizeChildrenAround(splitter, delta);
		}

		/// <summary>
		/// Moves the splitter at <paramref name="splitter"/> by <paramref name="delta"/>, resizing the visible
		/// children on either side of it.
		/// </summary>
		/// <param name="splitter">The splitter that was dragged.</param>
		/// <param name="delta">The distance it was dragged, in device independent pixels.</param>
		internal void ResizeChildrenAround(LayoutGridResizerControl splitter, double delta)
		{
			var indexOfResizer = Children.IndexOf(splitter);
			if (indexOfResizer <= 0) return;

			var prevChild = Children[indexOfResizer - 1];
			var nextChild = GetNextVisibleChild(indexOfResizer);
			if (nextChild == null) return;

			var prevChildActualSize = prevChild.Bounds.Size;
			var nextChildActualSize = nextChild.Bounds.Size;

			var prevChildModel = (ILayoutPositionableElement)(prevChild as ILayoutControl).Model;
			var nextChildModel = (ILayoutPositionableElement)(nextChild as ILayoutControl).Model;

			if (Orientation == Orientation.Horizontal)
			{
				if (prevChildModel.DockWidth.IsStar)
				{
					prevChildModel.DockWidth = new GridLength(prevChildModel.DockWidth.Value * (prevChildActualSize.Width + delta) / prevChildActualSize.Width, GridUnitType.Star);
				}
				else
				{
					var width = prevChildModel.DockWidth.IsAuto ? prevChildActualSize.Width : prevChildModel.DockWidth.Value;
					var resizedWidth = width + delta;
					prevChildModel.DockWidth = new GridLength(double.IsNaN(resizedWidth) ? width : resizedWidth, GridUnitType.Pixel);
				}

				if (nextChildModel.DockWidth.IsStar)
				{
					nextChildModel.DockWidth = new GridLength(nextChildModel.DockWidth.Value * (nextChildActualSize.Width - delta) / nextChildActualSize.Width, GridUnitType.Star);
				}
				else
				{
					var width = nextChildModel.DockWidth.IsAuto ? nextChildActualSize.Width : nextChildModel.DockWidth.Value;
					var resizedWidth = width - delta;
					nextChildModel.DockWidth = new GridLength(double.IsNaN(resizedWidth) ? width : resizedWidth, GridUnitType.Pixel);
				}
			}
			else
			{
				if (prevChildModel.DockHeight.IsStar)
				{
					prevChildModel.DockHeight = new GridLength(prevChildModel.DockHeight.Value * (prevChildActualSize.Height + delta) / prevChildActualSize.Height, GridUnitType.Star);
				}
				else
				{
					var height = prevChildModel.DockHeight.IsAuto ? prevChildActualSize.Height : prevChildModel.DockHeight.Value;
					var resizedHeight = height + delta;
					prevChildModel.DockHeight = new GridLength(double.IsNaN(resizedHeight) ? height : resizedHeight, GridUnitType.Pixel);
				}

				if (nextChildModel.DockHeight.IsStar)
				{
					nextChildModel.DockHeight = new GridLength(nextChildModel.DockHeight.Value * (nextChildActualSize.Height - delta) / nextChildActualSize.Height, GridUnitType.Star);
				}
				else
				{
					var height = nextChildModel.DockHeight.IsAuto ? nextChildActualSize.Height : nextChildModel.DockHeight.Value;
					var resizedHeight = height - delta;
					nextChildModel.DockHeight = new GridLength(double.IsNaN(resizedHeight) ? height : resizedHeight, GridUnitType.Pixel);
				}
			}
		}

		/// <inheritdoc/>
		public virtual void AdjustFixedChildrenPanelSizes(Size? parentSize = null)
		{
			var visibleChildren = GetVisibleChildren();
			if (visibleChildren.Count == 0) return;

			var layoutChildrenModels = visibleChildren.OfType<ILayoutControl>()
			  .Select(child => child.Model)
			  .OfType<ILayoutPositionableElementWithActualSize>()
			  .ToList();
			if (layoutChildrenModels.Count == 0) return;

			var splitterChildren = visibleChildren.OfType<LayoutGridResizerControl>().ToList();
			List<ILayoutPositionableElementWithActualSize> fixedPanels;
			List<ILayoutPositionableElementWithActualSize> relativePanels;

			// Get current available size of panel.
			var availableSize = parentSize ?? Bounds.Size;

			// Calculate minimum required size and current size of children.
			double minimumWidth = 0, minimumHeight = 0, currentWidth = 0, currentHeight = 0, preferredMinimumWidth = 0, preferredMinimumHeight = 0;
			if (Orientation == Orientation.Vertical)
			{
				fixedPanels = layoutChildrenModels.Where(child => child.DockHeight.IsAbsolute).ToList();
				relativePanels = layoutChildrenModels.Where(child => !child.DockHeight.IsAbsolute).ToList();
				minimumWidth += layoutChildrenModels.Max(child => child.CalculatedDockMinWidth());
				minimumHeight += layoutChildrenModels.Sum(child => child.CalculatedDockMinHeight());
				minimumHeight += splitterChildren.Sum(child => child.Bounds.Height);
				currentWidth += layoutChildrenModels.Max(child => child.ActualWidth);
				currentHeight += layoutChildrenModels.Sum(child => child.ActualHeight);
				currentHeight += splitterChildren.Sum(child => child.Bounds.Height);
				preferredMinimumWidth += layoutChildrenModels.Max(child => child.CalculatedDockMinWidth());
				preferredMinimumHeight += minimumHeight + fixedPanels.Sum(child => child.FixedDockHeight) - fixedPanels.Sum(child => child.CalculatedDockMinHeight());
			}
			else
			{
				fixedPanels = layoutChildrenModels.Where(child => child.DockWidth.IsAbsolute).ToList();
				relativePanels = layoutChildrenModels.Where(child => !child.DockWidth.IsAbsolute).ToList();
				minimumWidth += layoutChildrenModels.Sum(child => child.CalculatedDockMinWidth());
				minimumHeight += layoutChildrenModels.Max(child => child.CalculatedDockMinHeight());
				minimumWidth += splitterChildren.Sum(child => child.Bounds.Width);
				currentWidth += layoutChildrenModels.Sum(child => child.ActualWidth);
				currentHeight += layoutChildrenModels.Max(child => child.ActualHeight);
				currentWidth += splitterChildren.Sum(child => child.Bounds.Width);
				preferredMinimumHeight += layoutChildrenModels.Max(child => child.CalculatedDockMinHeight());
				preferredMinimumWidth += minimumWidth + fixedPanels.Sum(child => child.FixedDockWidth) - fixedPanels.Sum(child => child.CalculatedDockMinWidth());
			}

			// Apply corrected sizes for fixed panels.
			if (Orientation == Orientation.Vertical)
			{
				var delta = availableSize.Height - currentHeight;
				var relativeDelta = relativePanels.Sum(child => child.ActualHeight - child.CalculatedDockMinHeight());
				delta += relativeDelta;
				foreach (var fixedChild in fixedPanels)
				{
					if (minimumHeight >= availableSize.Height)
					{
						fixedChild.ResizableAbsoluteDockHeight = fixedChild.CalculatedDockMinHeight();
					}
					else if (preferredMinimumHeight <= availableSize.Height)
					{
						fixedChild.ResizableAbsoluteDockHeight = fixedChild.FixedDockHeight;
					}
					else if (relativePanels.All(child => Math.Abs(child.ActualHeight - child.CalculatedDockMinHeight()) <= 1))
					{
						double panelFraction;
						var indexOfChild = fixedPanels.IndexOf(fixedChild);
						if (delta < 0)
						{
							var availableHeightLeft = fixedPanels.Where(child => fixedPanels.IndexOf(child) >= indexOfChild)
							  .Sum(child => child.ActualHeight - child.CalculatedDockMinHeight());
							panelFraction = (fixedChild.ActualHeight - fixedChild.CalculatedDockMinHeight()) / (availableHeightLeft > 0 ? availableHeightLeft : 1);
						}
						else
						{
							var fixedHeightLeft = fixedPanels.Where(child => fixedPanels.IndexOf(child) >= indexOfChild)
							  .Sum(child => child.FixedDockHeight);
							panelFraction = fixedChild.FixedDockHeight / (fixedHeightLeft > 0 ? fixedHeightLeft : 1);
						}

						var childActualHeight = fixedChild.ActualHeight;
						var heightToSet = Math.Max(Math.Round(delta * panelFraction + fixedChild.ActualHeight), fixedChild.CalculatedDockMinHeight());
						fixedChild.ResizableAbsoluteDockHeight = heightToSet;
						delta -= heightToSet - childActualHeight;
					}
				}
			}
			else
			{
				var delta = availableSize.Width - currentWidth;
				var relativeDelta = relativePanels.Sum(child => child.ActualWidth - child.CalculatedDockMinWidth());
				delta += relativeDelta;
				foreach (var fixedChild in fixedPanels)
				{
					if (minimumWidth >= availableSize.Width)
					{
						fixedChild.ResizableAbsoluteDockWidth = fixedChild.CalculatedDockMinWidth();
					}
					else if (preferredMinimumWidth <= availableSize.Width)
					{
						fixedChild.ResizableAbsoluteDockWidth = fixedChild.FixedDockWidth;
					}
					else
					{
						double panelFraction;
						var indexOfChild = fixedPanels.IndexOf(fixedChild);
						if (delta < 0)
						{
							var availableWidthLeft = fixedPanels.Where(child => fixedPanels.IndexOf(child) >= indexOfChild)
							  .Sum(child => child.ActualWidth - child.CalculatedDockMinWidth());
							panelFraction = (fixedChild.ActualWidth - fixedChild.CalculatedDockMinWidth()) / (availableWidthLeft > 0 ? availableWidthLeft : 1);
						}
						else
						{
							var fixedWidthLeft = fixedPanels.Where(child => fixedPanels.IndexOf(child) >= indexOfChild)
							  .Sum(child => child.FixedDockWidth);
							panelFraction = fixedChild.FixedDockWidth / (fixedWidthLeft > 0 ? fixedWidthLeft : 1);
						}

						var childActualWidth = fixedChild.ActualWidth;
						var widthToSet = Math.Max(Math.Round(delta * panelFraction + fixedChild.ActualWidth), fixedChild.CalculatedDockMinWidth());
						fixedChild.ResizableAbsoluteDockWidth = widthToSet;
						delta -= widthToSet - childActualWidth;
					}
				}
			}

			foreach (var child in Children.OfType<IAdjustableSizeLayout>())
				child.AdjustFixedChildrenPanelSizes(availableSize);
		}

		private Control GetNextVisibleChild(int index)
		{
			for (var i = index + 1; i < Children.Count; i++)
			{
				if (Children[i] is LayoutGridResizerControl) continue;
				if (IsChildVisible(i)) return Children[i];
			}

			return null;
		}

		private List<Control> GetVisibleChildren()
		{
			var visibleChildren = new List<Control>();
			for (var i = 0; i < Children.Count; i++)
			{
				if (IsChildVisible(i))
					visibleChildren.Add(Children[i]);
			}

			return visibleChildren;
		}

		private bool IsChildVisible(int index)
		{
			if (Orientation == Orientation.Horizontal)
			{
				if (index < ColumnDefinitions.Count)
					return ColumnDefinitions[index].Width.IsStar || ColumnDefinitions[index].Width.Value > 0;
			}
			else if (index < RowDefinitions.Count)
			{
				return RowDefinitions[index].Height.IsStar || RowDefinitions[index].Height.Value > 0;
			}

			return false;
		}

		private void ShowResizerGhost(LayoutGridResizerControl splitter)
		{
			var indexOfResizer = Children.IndexOf(splitter);

			var prevChild = Children[indexOfResizer - 1];
			var nextChild = GetNextVisibleChild(indexOfResizer);
			if (nextChild == null) return;

			var prevChildActualSize = prevChild.Bounds.Size;
			var nextChildActualSize = nextChild.Bounds.Size;

			var prevChildModel = (ILayoutPositionableElement)(prevChild as ILayoutControl).Model;
			var nextChildModel = (ILayoutPositionableElement)(nextChild as ILayoutControl).Model;

			Size actualSize;
			Size ghostSize;

			if (Orientation == Orientation.Horizontal)
			{
				actualSize = new Size(
					Math.Max(0, prevChildActualSize.Width - prevChildModel.CalculatedDockMinWidth() + splitter.Bounds.Width + nextChildActualSize.Width - nextChildModel.CalculatedDockMinWidth()),
					nextChildActualSize.Height);

				ghostSize = new Size(splitter.Bounds.Width, actualSize.Height);
				_initialStartPoint = new Vector(prevChildActualSize.Width - prevChildModel.CalculatedDockMinWidth(), 0);
			}
			else
			{
				actualSize = new Size(
					prevChildActualSize.Width,
					Math.Max(0, prevChildActualSize.Height - prevChildModel.CalculatedDockMinHeight() + splitter.Bounds.Height + nextChildActualSize.Height - nextChildModel.CalculatedDockMinHeight()));

				ghostSize = new Size(actualSize.Width, splitter.Bounds.Height);
				_initialStartPoint = new Vector(0, prevChildActualSize.Height - prevChildModel.CalculatedDockMinHeight());
			}

			_resizerHostSize = actualSize;
			_resizerGhost = new LayoutGridResizerGhost(splitter.BackgroundWhileDragging, splitter.OpacityWhileDragging, ghostSize);

			// Splitter feedback stays inside the window (an adorner) rather than in a separate window opened in the
			// middle of a pointer drag. Without an adorner layer the drag still works, only without the ghost.
			_resizerAdornerLayer = AdornerLayer.GetAdornerLayer(splitter);
			if (_resizerAdornerLayer != null)
			{
				AdornerLayer.SetAdornedElement(_resizerGhost, splitter);
				_resizerAdornerLayer.Children.Add(_resizerGhost);
			}
		}

		private void HideResizerGhost()
		{
			if (_resizerGhost == null) return;
			_resizerAdornerLayer?.Children.Remove(_resizerGhost);
			_resizerGhost = null;
			_resizerAdornerLayer = null;
		}

		/// <summary>The rectangle that follows the pointer while a splitter is dragged.</summary>
		private sealed class LayoutGridResizerGhost : Control
		{
			private readonly IBrush _brush;
			private readonly Size _ghostSize;
			private Point _position;

			internal LayoutGridResizerGhost(IBrush brush, double opacity, Size ghostSize)
			{
				_brush = brush ?? Brushes.Black;
				_ghostSize = ghostSize;
				Opacity = opacity;
				IsHitTestVisible = false;
				ClipToBounds = false;
			}

			internal Point Position
			{
				get => _position;
				set
				{
					_position = value;
					InvalidateVisual();
				}
			}

			public override void Render(DrawingContext context) => context.FillRectangle(_brush, new Rect(_position, _ghostSize));
		}
	}
}
