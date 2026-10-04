using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Avalonia;
using Avalonia.Controls.Primitives;
using AvalonDock.Layout;

namespace AvalonDock.Controls
{
	/// <summary>Represents one of the four side bars that hold the anchors of auto-hidden anchorables.</summary>
	public class LayoutAnchorSideControl : TemplatedControl, ILayoutControl
	{
		private readonly LayoutAnchorSide _model = null;
		private readonly ObservableCollection<LayoutAnchorGroupControl> _childViews = new ObservableCollection<LayoutAnchorGroupControl>();

		/// <summary><see cref="IsLeftSide"/> property.</summary>
		public static readonly DirectProperty<LayoutAnchorSideControl, bool> IsLeftSideProperty =
			AvaloniaProperty.RegisterDirect<LayoutAnchorSideControl, bool>(nameof(IsLeftSide), o => o.IsLeftSide);

		/// <summary><see cref="IsTopSide"/> property.</summary>
		public static readonly DirectProperty<LayoutAnchorSideControl, bool> IsTopSideProperty =
			AvaloniaProperty.RegisterDirect<LayoutAnchorSideControl, bool>(nameof(IsTopSide), o => o.IsTopSide);

		/// <summary><see cref="IsRightSide"/> property.</summary>
		public static readonly DirectProperty<LayoutAnchorSideControl, bool> IsRightSideProperty =
			AvaloniaProperty.RegisterDirect<LayoutAnchorSideControl, bool>(nameof(IsRightSide), o => o.IsRightSide);

		/// <summary><see cref="IsBottomSide"/> property.</summary>
		public static readonly DirectProperty<LayoutAnchorSideControl, bool> IsBottomSideProperty =
			AvaloniaProperty.RegisterDirect<LayoutAnchorSideControl, bool>(nameof(IsBottomSide), o => o.IsBottomSide);

		/// <summary>Initializes a new instance of the <see cref="LayoutAnchorSideControl"/> class.</summary>
		/// <param name="model">The side model.</param>
		internal LayoutAnchorSideControl(LayoutAnchorSide model)
		{
			_model = model ?? throw new ArgumentNullException(nameof(model));
			CreateChildrenViews();
			_model.Children.CollectionChanged += OnModelChildrenCollectionChanged;
			IsLeftSide = _model.Side == AnchorSide.Left;
			IsTopSide = _model.Side == AnchorSide.Top;
			IsRightSide = _model.Side == AnchorSide.Right;
			IsBottomSide = _model.Side == AnchorSide.Bottom;
			Classes.Add(_model.Side.ToString().ToLowerInvariant());
		}

		/// <inheritdoc/>
		public ILayoutElement Model => _model;

		/// <summary>Gets the anchor group controls of this side.</summary>
		public ObservableCollection<LayoutAnchorGroupControl> Children => _childViews;

		/// <summary>Gets a value indicating whether this is the left side.</summary>
		public bool IsLeftSide { get; private set; }

		/// <summary>Gets a value indicating whether this is the top side.</summary>
		public bool IsTopSide { get; private set; }

		/// <summary>Gets a value indicating whether this is the right side.</summary>
		public bool IsRightSide { get; private set; }

		/// <summary>Gets a value indicating whether this is the bottom side.</summary>
		public bool IsBottomSide { get; private set; }

		/// <summary>Gets the orientation in which the anchors of this side are stacked.</summary>
		public Avalonia.Layout.Orientation Orientation =>
			IsLeftSide || IsRightSide ? Avalonia.Layout.Orientation.Vertical : Avalonia.Layout.Orientation.Horizontal;

		private void CreateChildrenViews()
		{
			var manager = _model.Root?.Manager;
			if (manager == null) return;
			foreach (var childModel in _model.Children)
				_childViews.Add(manager.CreateUIElementForModel(childModel) as LayoutAnchorGroupControl);
		}

		private void OnModelChildrenCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			if (e.OldItems != null && (e.Action == NotifyCollectionChangedAction.Remove || e.Action == NotifyCollectionChangedAction.Replace))
			{
				foreach (var childModel in e.OldItems)
				{
					var view = _childViews.FirstOrDefault(cv => cv.Model == childModel);
					if (view != null) _childViews.Remove(view);
				}
			}

			if (e.Action == NotifyCollectionChangedAction.Reset)
				_childViews.Clear();

			if (e.NewItems != null && (e.Action == NotifyCollectionChangedAction.Add || e.Action == NotifyCollectionChangedAction.Replace))
			{
				var manager = _model.Root?.Manager;
				if (manager == null) return;
				var insertIndex = Math.Max(0, Math.Min(e.NewStartingIndex, _childViews.Count));
				foreach (LayoutAnchorGroup childModel in e.NewItems)
					_childViews.Insert(insertIndex++, manager.CreateUIElementForModel(childModel) as LayoutAnchorGroupControl);
			}
		}
	}

	/// <summary>Represents a group of anchors on a side bar - the anchorables that were auto-hidden together.</summary>
	public class LayoutAnchorGroupControl : TemplatedControl, ILayoutControl
	{
		private readonly ObservableCollection<LayoutAnchorControl> _childViews = new ObservableCollection<LayoutAnchorControl>();
		private readonly LayoutAnchorGroup _model;

		/// <summary>Initializes a new instance of the <see cref="LayoutAnchorGroupControl"/> class.</summary>
		/// <param name="model">The group model.</param>
		internal LayoutAnchorGroupControl(LayoutAnchorGroup model)
		{
			_model = model;
			CreateChildrenViews();
			_model.Children.CollectionChanged += (s, e) => OnModelChildrenCollectionChanged(e);
			var side = (_model.Parent as LayoutAnchorSide)?.Side ?? AnchorSide.Left;
			Classes.Add(side.ToString().ToLowerInvariant());
		}

		/// <summary>Gets the anchor controls of this group.</summary>
		public ObservableCollection<LayoutAnchorControl> Children => _childViews;

		/// <inheritdoc/>
		public ILayoutElement Model => _model;

		/// <summary>Gets a value indicating whether the group sits on the left or right side bar, whose anchors are rotated.</summary>
		public bool IsVerticalSide => _model.Parent is LayoutAnchorSide side && (side.Side == AnchorSide.Left || side.Side == AnchorSide.Right);

		private LayoutAnchorControl CreateAnchor(LayoutAnchorable childModel)
		{
			var manager = _model.Root?.Manager;
			var lac = new LayoutAnchorControl(childModel);
			manager?.ApplyTemplateProperty(lac, DockingManager.AnchorTemplateProperty);
			return lac;
		}

		private void CreateChildrenViews()
		{
			foreach (var childModel in _model.Children)
				_childViews.Add(CreateAnchor(childModel));
		}

		private void OnModelChildrenCollectionChanged(NotifyCollectionChangedEventArgs e)
		{
			if ((e.Action == NotifyCollectionChangedAction.Remove || e.Action == NotifyCollectionChangedAction.Replace) && e.OldItems != null)
			{
				foreach (var childModel in e.OldItems)
				{
					var view = _childViews.FirstOrDefault(cv => cv.Model == childModel);
					if (view != null) _childViews.Remove(view);
				}
			}

			if (e.Action == NotifyCollectionChangedAction.Reset)
				_childViews.Clear();

			if ((e.Action == NotifyCollectionChangedAction.Add || e.Action == NotifyCollectionChangedAction.Replace) && e.NewItems != null)
			{
				var insertIndex = Math.Max(0, Math.Min(e.NewStartingIndex, _childViews.Count));
				foreach (LayoutAnchorable childModel in e.NewItems)
					_childViews.Insert(insertIndex++, CreateAnchor(childModel));
			}
		}
	}
}
