using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using AvalonDock.Core;
using AvalonDock.Layout;

namespace AvalonDock.Controls
{
	/// <summary>Converts a value into whether it is not <see langword="null"/>.</summary>
	public sealed class NullToFalseConverter : IValueConverter
	{
		/// <summary>The shared instance.</summary>
		public static readonly NullToFalseConverter Instance = new NullToFalseConverter();

		/// <inheritdoc/>
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value != null;

		/// <inheritdoc/>
		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
	}

	/// <summary>
	/// A stripe of <see cref="ToggleDockButton"/>s along a side of a <see cref="ToggleDockingManager"/>, one per
	/// anchorable of a <see cref="DockZone"/>.
	/// </summary>
	public class ToggleDockButtonBar : ItemsControl
	{
		/// <summary><see cref="Zone"/> property.</summary>
		public static readonly StyledProperty<DockZone> ZoneProperty =
			AvaloniaProperty.Register<ToggleDockButtonBar, DockZone>(nameof(Zone), DockZone.LeftTop);

		/// <summary><see cref="Orientation"/> property.</summary>
		public static readonly StyledProperty<Orientation> OrientationProperty =
			AvaloniaProperty.Register<ToggleDockButtonBar, Orientation>(nameof(Orientation), Orientation.Vertical);

		/// <summary>Gets or sets the zone the buttons of this bar open their anchorables in.</summary>
		public DockZone Zone
		{
			get => GetValue(ZoneProperty);
			set => SetValue(ZoneProperty, value);
		}

		/// <summary>Gets or sets the direction the buttons are stacked in.</summary>
		public Orientation Orientation
		{
			get => GetValue(OrientationProperty);
			set => SetValue(OrientationProperty, value);
		}

		/// <summary>Replaces the buttons of the bar with one per anchorable.</summary>
		/// <param name="anchorables">The anchorables.</param>
		/// <param name="zone">The zone of the anchorables.</param>
		public void SetAnchorables(IEnumerable<LayoutAnchorable> anchorables, DockZone zone)
		{
			Items.Clear();
			foreach (var anc in anchorables)
				Items.Add(new ToggleDockButton { Anchorable = anc, Zone = zone });
		}

		/// <summary>Gets a value indicating whether the bar has a button for <paramref name="anchorable"/>.</summary>
		/// <param name="anchorable">The anchorable.</param>
		/// <returns><see langword="true"/> when it has.</returns>
		internal bool ContainsAnchorable(LayoutAnchorable anchorable)
		{
			foreach (var item in Items)
			{
				if (item is ToggleDockButton btn && btn.Anchorable == anchorable)
					return true;
			}

			return false;
		}
	}

	/// <summary>
	/// The button of an anchorable on a <see cref="ToggleDockButtonBar"/>: a click docks or collapses the
	/// anchorable, a drag moves it to another zone and a right click opens its options.
	/// </summary>
	[PseudoClasses(":anchorablefocused")]
	public class ToggleDockButton : ToggleButton
	{
		/// <summary>Resource key of the foreground brush shared by all sidebar buttons.</summary>
		public const string ForegroundBrushKey = "AvalonDock_ToggleDockButtonForeground";

		/// <summary>The distance the pointer has to move before a press becomes a drag.</summary>
		private const double DragThreshold = 4;

		/// <summary><see cref="Anchorable"/> property.</summary>
		public static readonly StyledProperty<LayoutAnchorable> AnchorableProperty =
			AvaloniaProperty.Register<ToggleDockButton, LayoutAnchorable>(nameof(Anchorable));

		/// <summary><see cref="Zone"/> property.</summary>
		public static readonly StyledProperty<DockZone> ZoneProperty =
			AvaloniaProperty.Register<ToggleDockButton, DockZone>(nameof(Zone), DockZone.LeftTop);

		/// <summary><see cref="IconSource"/> property.</summary>
		public static readonly StyledProperty<IImage> IconSourceProperty =
			AvaloniaProperty.Register<ToggleDockButton, IImage>(nameof(IconSource));

		/// <summary><see cref="IconContent"/> property.</summary>
		public static readonly StyledProperty<object> IconContentProperty =
			AvaloniaProperty.Register<ToggleDockButton, object>(nameof(IconContent));

		/// <summary><see cref="IconTemplate"/> property.</summary>
		public static readonly StyledProperty<IDataTemplate> IconTemplateProperty =
			AvaloniaProperty.Register<ToggleDockButton, IDataTemplate>(nameof(IconTemplate));

		/// <summary><see cref="IsAnchorableFocused"/> property.</summary>
		public static readonly StyledProperty<bool> IsAnchorableFocusedProperty =
			AvaloniaProperty.Register<ToggleDockButton, bool>(nameof(IsAnchorableFocused));

		private Point _dragStartPoint;
		private bool _isMouseDown;
		private bool _isDragging;

		/// <summary>Gets or sets the anchorable the button stands for.</summary>
		public LayoutAnchorable Anchorable
		{
			get => GetValue(AnchorableProperty);
			set => SetValue(AnchorableProperty, value);
		}

		/// <summary>Gets or sets the zone the anchorable is docked in.</summary>
		public DockZone Zone
		{
			get => GetValue(ZoneProperty);
			set => SetValue(ZoneProperty, value);
		}

		/// <summary>Gets or sets an image shown instead of the title.</summary>
		public IImage IconSource
		{
			get => GetValue(IconSourceProperty);
			set => SetValue(IconSourceProperty, value);
		}

		/// <summary>Gets or sets arbitrary content (a path, a glyph) shown instead of the title.</summary>
		public object IconContent
		{
			get => GetValue(IconContentProperty);
			set => SetValue(IconContentProperty, value);
		}

		/// <summary>Gets or sets the template of <see cref="IconContent"/>.</summary>
		public IDataTemplate IconTemplate
		{
			get => GetValue(IconTemplateProperty);
			set => SetValue(IconTemplateProperty, value);
		}

		/// <summary>Gets or sets a value indicating whether the anchorable is docked and holds the active content.</summary>
		public bool IsAnchorableFocused
		{
			get => GetValue(IsAnchorableFocusedProperty);
			set => SetValue(IsAnchorableFocusedProperty, value);
		}

		/// <inheritdoc/>
		protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
		{
			base.OnPropertyChanged(change);
			if (change.Property == AnchorableProperty)
				OnAnchorableChanged(change.GetNewValue<LayoutAnchorable>());
			else if (change.Property == IconSourceProperty || change.Property == IconContentProperty)
				Classes.Set("icon", IconSource != null || IconContent != null);
			else if (change.Property == IsAnchorableFocusedProperty)
				PseudoClasses.Set(":anchorablefocused", IsAnchorableFocused);
		}

		private void OnAnchorableChanged(LayoutAnchorable anc)
		{
			if (anc == null) return;

			// The title, as on WPF; the template shows it through the header template, not through Content.
			Content = anc.Title;
			IsChecked = !anc.IsAutoHidden;

			// Icon: prefer the attached ToggleDock.Icon, then the icon of the IToolbox view model (anchorables
			// restored by layout deserialization never pass the ToggleLayoutStrategy that copies it to the
			// attached property), and fall back to LayoutAnchorable.IconSource.
			var attachedIcon = ToggleDock.GetIcon(anc);
			if (attachedIcon != null)
				IconContent = attachedIcon;
			else if (anc.Content is IToolbox toolboxWithIcon && toolboxWithIcon.Icon != null)
				IconContent = toolboxWithIcon.Icon;
			else if (anc.IconSource != null)
				IconSource = anc.IconSource;

			var attachedIconTemplate = ToggleDock.GetIconTemplate(anc);
			if (attachedIconTemplate != null)
				IconTemplate = attachedIconTemplate;

			// ToolTip: prefer the attached ToggleDock.ToolTip, fall back to the title; append the shortcut.
			var baseToolTip = ToggleDock.GetToolTip(anc) ?? anc.Title;
			ToolTip.SetTip(this, anc.Content is IToolbox toolboxForTip && !string.IsNullOrWhiteSpace(toolboxForTip.Shortcut)
				? $"{baseToolTip} ({toolboxForTip.Shortcut})"
				: baseToolTip);
		}

		/// <inheritdoc/>
		protected override void OnClick()
		{
			// A drag that ended on the button is not a click.
			if (_isDragging)
			{
				_isDragging = false;
				return;
			}

			base.OnClick();
			if (Anchorable?.Root?.Manager is ToggleDockingManager manager)
				manager.ToggleAnchorable(Anchorable, Zone);
		}

		/// <inheritdoc/>
		protected override void OnPointerPressed(PointerPressedEventArgs e)
		{
			base.OnPointerPressed(e);
			if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
			_dragStartPoint = e.GetPosition(this);
			_isMouseDown = true;
			_isDragging = false;
		}

		/// <inheritdoc/>
		protected override void OnPointerMoved(PointerEventArgs e)
		{
			base.OnPointerMoved(e);
			if (!_isMouseDown || _isDragging || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

			var diff = e.GetPosition(this) - _dragStartPoint;
			if (Math.Abs(diff.X) <= DragThreshold && Math.Abs(diff.Y) <= DragThreshold) return;

			_isMouseDown = false;
			_isDragging = true;
			if (Anchorable?.Root?.Manager is ToggleDockingManager manager)
				ToggleDockDragOverlay.StartDrag(Anchorable, this, Bounds.Size, manager, e);
		}

		/// <inheritdoc/>
		protected override void OnPointerReleased(PointerReleasedEventArgs e)
		{
			_isMouseDown = false;
			base.OnPointerReleased(e);
			_isDragging = false;

			if (e.InitialPressMouseButton != MouseButton.Right || Anchorable == null) return;
			if (!(Anchorable.Root?.Manager is ToggleDockingManager toggleManager)) return;

			var anchorable = Anchorable;
			var menu = toggleManager.BuildToggleContextMenu(anchorable);

			// "Hide" goes first, followed by a separator.
			var hideItem = new MenuItem { Header = "Hide" };
			hideItem.Click += (s, ev) =>
			{
				var layoutItem = toggleManager.GetLayoutItemFromModel(anchorable) as LayoutAnchorableItem;
				layoutItem?.HideCommand?.Execute(null);
				toggleManager.RemoveButtonFromAllBars(anchorable);
			};

			menu.Items.Insert(0, hideItem);
			menu.Items.Insert(1, new Separator());
			menu.Placement = PlacementMode.Bottom;
			menu.Open(this);
			e.Handled = true;
		}

		/// <inheritdoc/>
		protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
		{
			// The drag overlay takes the pointer over; the press must not end as a click then.
			_isMouseDown = false;
			base.OnPointerCaptureLost(e);
		}
	}
}
