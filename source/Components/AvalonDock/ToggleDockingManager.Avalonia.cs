using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using AvalonDock.Controls;
using AvalonDock.Core;
using AvalonDock.Layout;
using AvalonDock.Themes;

namespace AvalonDock
{
	/// <summary>Defines which docked panes span the full size of the manager.</summary>
	public enum DockLayoutPriority
	{
		/// <summary>The bottom panes span the full width.</summary>
		BottomFullWidth,

		/// <summary>The side panes span the full height.</summary>
		SidesFullHeight,

		/// <summary>No pane is stretched.</summary>
		Default
	}

	/// <summary>
	/// A <see cref="DockingManager"/> in the style of modern IDEs: every anchorable has a button on a sidebar
	/// stripe that docks it into one of six zones or collapses it again; there is no auto-hide flyout.
	/// </summary>
	/// <remarks>
	/// Port of the WPF <c>ToggleDockingManager</c>. The sidebar panels are inserted into the grid named
	/// <c>PART_RootGrid</c> of the manager template (columns 0 and 2), and an anchorable dragged from its
	/// button or title is moved between zones with an overlay in the window rather than a topmost window.
	/// </remarks>
	public class ToggleDockingManager : DockingManager
	{
		/// <summary><see cref="LayoutPriority"/> property.</summary>
		public static readonly StyledProperty<DockLayoutPriority> LayoutPriorityProperty =
			AvaloniaProperty.Register<ToggleDockingManager, DockLayoutPriority>(nameof(LayoutPriority), DockLayoutPriority.BottomFullWidth);

		/// <summary><see cref="ButtonSize"/> property.</summary>
		public static readonly StyledProperty<double> ButtonSizeProperty =
			AvaloniaProperty.Register<ToggleDockingManager, double>(nameof(ButtonSize), 25.0);

		/// <summary><see cref="ShowHeaderMinimizeButton"/> property.</summary>
		public static readonly StyledProperty<bool> ShowHeaderMinimizeButtonProperty =
			AvaloniaProperty.Register<ToggleDockingManager, bool>(nameof(ShowHeaderMinimizeButton), true);

		/// <summary><see cref="ShowHeaderOptionsButton"/> property.</summary>
		public static readonly StyledProperty<bool> ShowHeaderOptionsButtonProperty =
			AvaloniaProperty.Register<ToggleDockingManager, bool>(nameof(ShowHeaderOptionsButton), true);

		/// <summary><see cref="DefaultDockWidth"/> property.</summary>
		public static readonly StyledProperty<double> DefaultDockWidthProperty =
			AvaloniaProperty.Register<ToggleDockingManager, double>(nameof(DefaultDockWidth), 250.0);

		/// <summary><see cref="DefaultDockHeight"/> property.</summary>
		public static readonly StyledProperty<double> DefaultDockHeightProperty =
			AvaloniaProperty.Register<ToggleDockingManager, double>(nameof(DefaultDockHeight), 200.0);

		/// <summary>The bars of the six zones.</summary>
		internal ToggleDockButtonBar _leftTopBar;
		internal ToggleDockButtonBar _leftBottomBar;
		internal ToggleDockButtonBar _rightTopBar;
		internal ToggleDockButtonBar _rightBottomBar;
		internal ToggleDockButtonBar _bottomLeftBar;
		internal ToggleDockButtonBar _bottomRightBar;

		/// <summary>The sidebar panels inserted into the template.</summary>
		internal DockPanel _injectedLeftDockPanel;
		internal DockPanel _injectedRightDockPanel;

		/// <summary>The separators between the top and bottom bar of each sidebar.</summary>
		internal Control _leftSeparator;
		internal Control _rightSeparator;

		/// <summary>The layout engine used for all layout tree operations.</summary>
		private readonly ToggleLayoutEngine _layoutEngine = new ToggleLayoutEngine();

		private readonly Dictionary<IToolbox, LayoutAnchorable> _toolboxToAnchorable = new Dictionary<IToolbox, LayoutAnchorable>();

		/// <summary>Remembers the zone each detached anchorable returns to.</summary>
		private readonly Dictionary<LayoutAnchorable, DockZone> _detachedZones = new Dictionary<LayoutAnchorable, DockZone>();

		/// <summary>Key bindings registered on the top level for toolbox shortcuts.</summary>
		private readonly List<KeyBinding> _shortcutBindings = new List<KeyBinding>();

		/// <summary>The top level that holds <see cref="_shortcutBindings"/>.</summary>
		private TopLevel _shortcutHost;

		private int _syncDepth;

		/// <summary>The grid of the template the sidebars are inserted into.</summary>
		private Grid _rootGrid;

		/// <summary>The button that opens the menu of hidden anchorables.</summary>
		private Button _showHiddenButton;

		/// <summary>Initializes a new instance of the <see cref="ToggleDockingManager"/> class.</summary>
		public ToggleDockingManager()
		{
			SupportsAutoHideFlyout = false;
			LayoutUpdateStrategy = new ToggleLayoutStrategy();
			ActiveContentChanged += OnActiveContentChangedForAnchorableState;
		}

		/// <inheritdoc/>
		public override ILayoutEngine LayoutEngine => _layoutEngine;

		/// <summary>Gets or sets which docked panes span the full size of the manager.</summary>
		public DockLayoutPriority LayoutPriority
		{
			get => GetValue(LayoutPriorityProperty);
			set => SetValue(LayoutPriorityProperty, value);
		}

		/// <summary>Gets or sets the width of the sidebar buttons.</summary>
		public double ButtonSize
		{
			get => GetValue(ButtonSizeProperty);
			set => SetValue(ButtonSizeProperty, value);
		}

		/// <summary>Gets or sets a value indicating whether the titles show a minimize button.</summary>
		public bool ShowHeaderMinimizeButton
		{
			get => GetValue(ShowHeaderMinimizeButtonProperty);
			set => SetValue(ShowHeaderMinimizeButtonProperty, value);
		}

		/// <summary>Gets or sets a value indicating whether the titles show an options button.</summary>
		public bool ShowHeaderOptionsButton
		{
			get => GetValue(ShowHeaderOptionsButtonProperty);
			set => SetValue(ShowHeaderOptionsButtonProperty, value);
		}

		/// <summary>Gets or sets the width of a newly docked side pane.</summary>
		public double DefaultDockWidth
		{
			get => GetValue(DefaultDockWidthProperty);
			set => SetValue(DefaultDockWidthProperty, value);
		}

		/// <summary>Gets or sets the height of a newly docked bottom pane.</summary>
		public double DefaultDockHeight
		{
			get => GetValue(DefaultDockHeightProperty);
			set => SetValue(DefaultDockHeightProperty, value);
		}

		/// <inheritdoc/>
		protected override Type StyleKeyOverride => typeof(ToggleDockingManager);

		/// <summary>Docks a collapsed anchorable into <paramref name="zone"/>, or collapses a docked one.</summary>
		/// <param name="anchorable">The anchorable.</param>
		/// <param name="zone">The zone.</param>
		public void ToggleAnchorable(LayoutAnchorable anchorable, DockZone zone)
		{
			// While the content lives in a standalone window there is nothing to dock or collapse; the button
			// brings that window forward instead.
			if (IsDetached(anchorable))
			{
				ActivateDetachedWindow(anchorable);

				// The content stays on screen, so the toolbox stays open. Writing that back keeps a view model
				// that asked for the opposite from holding a value this manager never applied - which, because
				// change notifications are raised on change only, would leave it unable to ask again.
				SetToolboxIsOpen(anchorable);
				return;
			}

			if (anchorable.IsAutoHidden)
			{
				// Collapse whatever is docked from the same bar.
				HideDockedInBar(GetBarForZone(zone));
				DockFromAutoHide(anchorable, zone);
				FixSplitOrientation(anchorable, zone);

				if (ToggleLayoutEngine.IsBottomZone(zone))
					EnsureBottomZoneOrder();

				switch (LayoutPriority)
				{
					case DockLayoutPriority.BottomFullWidth:
						EnsureBottomFullWidth();
						break;
					case DockLayoutPriority.SidesFullHeight:
						EnsureSidesFullHeight();
						break;
				}

				// Focus the newly docked anchorable so that its button shows as focused.
				ActiveContent = anchorable.Content;
			}
			else
			{
				AutoHideFromDock(anchorable, zone);
			}

			RefreshButtonStates();
			SetToolboxIsOpen(anchorable);
		}

		/// <summary>Moves an anchorable into another zone and docks it there.</summary>
		/// <param name="anchorable">The anchorable.</param>
		/// <param name="targetZone">The zone.</param>
		public void MoveAnchorableToZone(LayoutAnchorable anchorable, DockZone targetZone)
		{
			if (anchorable == null) return;

			// A detached anchorable is not in the dock area, so only the zone it will return to changes.
			if (IsDetached(anchorable))
			{
				_detachedZones[anchorable] = targetZone;
				return;
			}

			var currentZone = GetAnchorableZone(anchorable);
			if (currentZone == targetZone)
			{
				if (anchorable.IsAutoHidden) ToggleAnchorable(anchorable, targetZone);
				return;
			}

			if (!anchorable.IsAutoHidden) AutoHideFromDock(anchorable, currentZone);
			if (anchorable.Parent is LayoutAnchorGroup oldGroup) oldGroup.RemoveChild(anchorable);

			// Always a fresh group, so DockFromAutoHide does not reuse a previous container of another zone.
			var targetGroup = new LayoutAnchorGroup();
			GetLayoutSideForZone(targetZone).Children.Add(targetGroup);
			targetGroup.Children.Add(anchorable);

			RemoveFromAllBars(anchorable);
			var targetBar = GetBarForZone(targetZone);
			if (targetBar != null)
			{
				targetBar.Items.Add(new ToggleDockButton { Anchorable = anchorable, Zone = targetZone });
				if (anchorable.Content is IToolbox movedToolbox) RegisterToolbox(movedToolbox, anchorable);
			}

			ToggleAnchorable(anchorable, targetZone);
		}

		/// <summary>Builds the options menu of an anchorable: move to another zone, change the view mode.</summary>
		/// <param name="anchorable">The anchorable.</param>
		/// <returns>The menu.</returns>
		internal ContextMenu BuildToggleContextMenu(LayoutAnchorable anchorable)
		{
			var menu = new ContextMenu();

			var moveToItem = new MenuItem { Header = "Move To" };
			var moveToItems = new List<MenuItem>();
			foreach (DockZone zone in Enum.GetValues(typeof(DockZone)))
			{
				var zoneLabel = System.Text.RegularExpressions.Regex.Replace(zone.ToString(), "(\\B[A-Z])", " $1");
				var z = zone;
				var mi = new MenuItem { Header = zoneLabel };
				mi.Click += (s, e) => MoveAnchorableToZone(anchorable, z);
				moveToItems.Add(mi);
			}

			moveToItem.ItemsSource = moveToItems;
			menu.Items.Add(moveToItem);
			menu.Items.Add(new Separator());

			// This menu is built by hand rather than from the commands, so the two manager wide switches have to
			// be honoured here as well - see AllowFloatingWindows and AllowDetachedWindows.
			var floatItem = new MenuItem { Header = "Float", IsEnabled = AllowFloatingWindows };
			floatItem.Click += (s, e) =>
			{
				ReattachAnchorable(anchorable);
				if (anchorable.IsAutoHidden) anchorable.ToggleSingleAutoHide();
				(GetLayoutItemFromModel(anchorable) as LayoutAnchorableItem)?.FloatCommand?.Execute(null);
			};

			// An ordinary, independent window - the "Window" view mode of IDE tool windows. Selecting it again
			// docks the content back.
			var windowItem = new MenuItem
			{
				Header = "Window",
				ToggleType = MenuItemToggleType.CheckBox,
				IsChecked = IsDetached(anchorable),
				IsEnabled = AllowDetachedWindows,
			};
			windowItem.Click += (s, e) =>
			{
				if (IsDetached(anchorable)) ReattachAnchorable(anchorable);
				else DetachAnchorableToWindow(anchorable);
			};

			var dockedItem = new MenuItem { Header = "Docked" };
			dockedItem.Click += (s, e) =>
			{
				if (IsDetached(anchorable))
				{
					ReattachAnchorable(anchorable);
					return;
				}

				if (anchorable.IsAutoHidden) ToggleAnchorable(anchorable, GetAnchorableZone(anchorable));
			};

			var hiddenItem = new MenuItem { Header = "Hidden" };
			hiddenItem.Click += (s, e) =>
			{
				ReattachAnchorable(anchorable);
				(GetLayoutItemFromModel(anchorable) as LayoutAnchorableItem)?.HideCommand?.Execute(null);
			};

			menu.Items.Add(new MenuItem { Header = "View Mode", ItemsSource = new[] { floatItem, windowItem, dockedItem, hiddenItem } });
			return menu;
		}

		/// <summary>Restores a hidden anchorable onto its bar and docks it.</summary>
		/// <param name="anchorable">The hidden anchorable.</param>
		internal void RestoreHiddenAnchorable(LayoutAnchorable anchorable)
		{
			if (anchorable == null) return;

			var zone = anchorable.Content is IToolbox toolbox ? toolbox.Zone : DockZone.LeftTop;
			if (Layout.Hidden.Contains(anchorable)) Layout.Hidden.Remove(anchorable);

			var group = new LayoutAnchorGroup();
			GetLayoutSideForZone(zone).Children.Add(group);
			group.Children.Add(anchorable);

			var bar = GetBarForZone(zone);
			if (bar != null)
			{
				bar.Items.Add(new ToggleDockButton { Anchorable = anchorable, Zone = zone });
				if (anchorable.Content is IToolbox restoredToolbox) RegisterToolbox(restoredToolbox, anchorable);
			}

			RefreshButtonStates();
			ToggleAnchorable(anchorable, zone);
		}

		/// <summary>Removes the button of <paramref name="anchorable"/> from every bar.</summary>
		/// <param name="anchorable">The anchorable.</param>
		internal void RemoveButtonFromAllBars(LayoutAnchorable anchorable) => RemoveFromAllBars(anchorable);

		/// <summary>Gets the bar of a zone.</summary>
		/// <param name="zone">The zone.</param>
		/// <returns>The bar, or <see langword="null"/> before the bars were created.</returns>
		internal ToggleDockButtonBar GetBarForZone(DockZone zone)
		{
			switch (zone)
			{
				case DockZone.LeftTop: return _leftTopBar;
				case DockZone.LeftBottom: return _leftBottomBar;
				case DockZone.RightTop: return _rightTopBar;
				case DockZone.RightBottom: return _rightBottomBar;
				case DockZone.BottomLeft: return _bottomLeftBar;
				case DockZone.BottomRight: return _bottomRightBar;
				default: return _leftTopBar;
			}
		}

		/// <summary>Links a toolbox to its anchorable, so that <see cref="IToolbox.IsOpen"/> toggles it.</summary>
		/// <param name="toolbox">The toolbox.</param>
		/// <param name="anchorable">The anchorable.</param>
		internal void RegisterToolbox(IToolbox toolbox, LayoutAnchorable anchorable)
		{
			var isNew = !_toolboxToAnchorable.ContainsKey(toolbox);
			_toolboxToAnchorable[toolbox] = anchorable;
			if (!isNew) return;
			if (toolbox is INotifyPropertyChanged npc) npc.PropertyChanged += OnToolboxPropertyChanged;
			RefreshShortcuts();
		}

		/// <summary>Removes the link of a toolbox to its anchorable.</summary>
		/// <param name="toolbox">The toolbox.</param>
		internal void UnregisterToolbox(IToolbox toolbox)
		{
			_toolboxToAnchorable.Remove(toolbox);
			if (toolbox is INotifyPropertyChanged npc) npc.PropertyChanged -= OnToolboxPropertyChanged;
			RefreshShortcuts();
		}

		/// <inheritdoc/>
		protected override void OnDockLayoutChanged(Core.IRootDock oldValue, Core.IRootDock newValue)
		{
			base.OnDockLayoutChanged(oldValue, newValue);
			if (!IsLoaded) return;
			SetupToggleDockButtonBars();
			Dispatcher.UIThread.Post(
				() =>
				{
					ApplyInitialToolboxState();
					RefreshButtonStates();
				},
				DispatcherPriority.Loaded);
		}

		/// <inheritdoc/>
		/// <remarks>
		/// <para>
		/// Replacing the layout - deserializing a stored layout, say - swaps in a new set of anchorables. The
		/// sidebar buttons still reference the anchorables of the old tree, so the bars are rebuilt from the new
		/// layout, which also drops the toolbox registrations of the old tree.
		/// </para>
		/// <para>
		/// The anchorables that were docked in the restored layout are re-opened afterwards, so a stored layout
		/// keeps deciding which toolboxes are visible. <see cref="IToolbox.IsOpenByDefault"/> is deliberately not
		/// applied here: it is the default for a fresh layout, not something that should override what the user
		/// saved.
		/// </para>
		/// </remarks>
		protected override void OnLayoutChanged(LayoutRoot oldLayout, LayoutRoot newLayout)
		{
			// Collected before the base call: building the bars collapses everything onto the stripes first.
			var restoreDocked = CollectDockedAnchorables(newLayout);

			base.OnLayoutChanged(oldLayout, newLayout);

			// The base constructor assigns the initial layout before the field initializers of this class ran.
			if (oldLayout == null || _detachedZones == null) return;

			// The entries reference anchorables of the replaced layout.
			_detachedZones.Clear();

			if (!IsLoaded)
			{
				// Without a template there is nowhere to insert the bars; drop the stale ones so that nothing
				// survives the swap. OnLoaded builds them from the new layout.
				RemoveToggleDockButtonBars();
				return;
			}

			SetupToggleDockButtonBars();
			foreach (var anchorable in restoreDocked)
			{
				if (anchorable.Root == newLayout && anchorable.IsAutoHidden)
					ToggleAnchorable(anchorable, GetAnchorableZone(anchorable));
			}

			SyncToolboxStateToLayout();
			Dispatcher.UIThread.Post(RefreshButtonStates, DispatcherPriority.Loaded);
		}

		/// <inheritdoc/>
		protected override void OnThemeChanged(Theme oldTheme, Theme newTheme)
		{
			base.OnThemeChanged(oldTheme, newTheme);

			// Re-insert the sidebars once the new theme's resources are in place.
			if (IsLoaded) Dispatcher.UIThread.Post(ReapplyThemeStyles, DispatcherPriority.Loaded);
		}

		/// <inheritdoc/>
		protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
		{
			base.OnApplyTemplate(e);
			_rootGrid = e.NameScope.Find<Grid>("PART_RootGrid");

			// A new template (theme change) needs the sidebars inserted again.
			if (IsLoaded) ReapplyThemeStyles();
		}

		/// <inheritdoc/>
		protected override void OnLoaded(RoutedEventArgs e)
		{
			base.OnLoaded(e);
			SetupToggleDockButtonBars();
			ApplyInitialToolboxState();
			RefreshShortcuts();
		}

		/// <inheritdoc/>
		protected override void OnUnloaded(RoutedEventArgs e)
		{
			RemoveShortcuts();
			base.OnUnloaded(e);
		}

		/// <inheritdoc/>
		/// <remarks>
		/// Collapses the anchorable onto its sidebar rather than hiding it, so its toggle button stays available
		/// and layout serialization keeps seeing an ordinary auto hidden entry.
		/// </remarks>
		protected override object DetachFromLayout(LayoutAnchorable anchorable)
		{
			var zone = GetAnchorableZone(anchorable);
			_detachedZones[anchorable] = zone;
			if (!anchorable.IsAutoHidden) AutoHideFromDock(anchorable, zone);
			return zone;
		}

		/// <inheritdoc/>
		protected override void ReturnToLayout(LayoutAnchorable anchorable, object restoreState)
		{
			var zone = _detachedZones.TryGetValue(anchorable, out var remembered)
				? remembered
				: restoreState as DockZone? ?? GetAnchorableZone(anchorable);
			_detachedZones.Remove(anchorable);
			if (anchorable.IsAutoHidden) ToggleAnchorable(anchorable, zone);
		}

		/// <inheritdoc/>
		/// <remarks>Keeps the options menu available while the content lives in its own window.</remarks>
		protected override Control CreateDetachedWindowHeader(LayoutAnchorable anchorable) => new ToggleAnchorablePaneTitle { Model = anchorable };

		/// <inheritdoc/>
		protected override void OnDetachedAnchorablesChanged(LayoutAnchorable anchorable)
		{
			SetToolboxIsOpen(anchorable);
			RefreshButtonStates();
		}

		/// <inheritdoc/>
		internal override void ExecuteAutoHideCommand(LayoutAnchorable anchorable)
		{
			if (anchorable == null) return;
			ToggleAnchorable(anchorable, GetAnchorableZone(anchorable));
		}

		/// <inheritdoc/>
		internal override void StartDraggingFloatingWindowForPane(LayoutAnchorablePane paneModel, DragStartInfo dragStart = null)
		{
			if (paneModel == null) return;
			var anchorable = paneModel.Children.OfType<LayoutAnchorable>().FirstOrDefault();
			if (anchorable != null && ToggleDockDragOverlay.StartDragFromPane(anchorable, this, dragStart)) return;
			base.StartDraggingFloatingWindowForPane(paneModel, dragStart);
		}

		/// <inheritdoc/>
		internal override void StartDraggingFloatingWindowForContent(LayoutContent contentModel, DragStartInfo dragStart)
		{
			if (contentModel is LayoutAnchorable anchorable && ToggleDockDragOverlay.StartDragFromPane(anchorable, this, dragStart)) return;
			base.StartDraggingFloatingWindowForContent(contentModel, dragStart);
		}

		private static List<LayoutAnchorable> CollectDockedAnchorables(LayoutRoot layout)
		{
			if (layout == null) return new List<LayoutAnchorable>();
			return layout.Descendents()
				.OfType<LayoutAnchorable>()
				.Where(a => a.Parent is LayoutAnchorablePane && !a.IsAutoHidden && !a.IsFloating && !a.IsHidden)
				.ToList();
		}

		private static Control CreateSeparator() => new Border
		{
			Height = 1,
			Margin = new Thickness(4, 6, 4, 6),
			Background = new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99)),
		};

		private static List<LayoutAnchorable> CollectAnchorables(LayoutAnchorSide side)
		{
			var result = new List<LayoutAnchorable>();
			if (side == null) return result;
			foreach (var group in side.Children)
				result.AddRange(group.Children);
			return result;
		}

		private static void ClearBarIconContent(ToggleDockButtonBar bar)
		{
			if (bar == null) return;
			foreach (var item in bar.Items)
			{
				if (item is ToggleDockButton btn)
				{
					btn.IconContent = null;
					btn.IconSource = null;
				}
			}
		}

		private static void RemoveFromBar(ToggleDockButtonBar bar, LayoutAnchorable anchorable)
		{
			if (bar == null) return;
			for (var i = bar.Items.Count - 1; i >= 0; i--)
			{
				if (bar.Items[i] is ToggleDockButton btn && btn.Anchorable == anchorable)
				{
					bar.Items.RemoveAt(i);
					return;
				}
			}
		}

		private static void RefreshBarStates(ToggleDockButtonBar bar, object activeContent)
		{
			if (bar == null) return;
			foreach (var item in bar.Items)
			{
				if (item is ToggleDockButton btn && btn.Anchorable != null)
				{
					btn.IsChecked = !btn.Anchorable.IsAutoHidden;
					btn.IsAnchorableFocused = !btn.Anchorable.IsAutoHidden && activeContent != null && activeContent == btn.Anchorable.Content;
				}
			}
		}

		private static KeyGesture TryParseGesture(string text)
		{
			if (string.IsNullOrWhiteSpace(text)) return null;
			try
			{
				return KeyGesture.Parse(text);
			}
			catch (ArgumentException)
			{
				return null;
			}
			catch (FormatException)
			{
				return null;
			}
		}

		private void ReapplyThemeStyles()
		{
			SetupToggleDockButtonBars();

			// Rebuilding the bars collapses every docked anchorable onto its stripe. The toolboxes still carry the
			// state they were in, so re-applying it brings the open ones back instead of closing them.
			ApplyInitialToolboxState();
			RefreshButtonStates();
		}

		/// <summary>Docks the toolboxes that ask to be showing, and reconciles the state of the ones that do not.</summary>
		/// <remarks>
		/// <see cref="IToolbox.IsOpenByDefault"/> seeds <see cref="IToolbox.IsOpen"/>, which from then on is the
		/// single answer to whether a toolbox is showing; reading it here lets a value the application set before
		/// the manager was loaded take effect. Anchorables that already show are left where they are, so running
		/// this again never collapses what is open.
		/// </remarks>
		private void ApplyInitialToolboxState()
		{
			if (Layout == null) return;
			foreach (var anchorable in Layout.Descendents().OfType<LayoutAnchorable>().ToList())
			{
				if (!(anchorable.Content is IToolbox toolbox)) continue;
				if (!toolbox.IsOpen && !toolbox.IsOpenByDefault) continue;

				if (anchorable.IsAutoHidden && !IsDetached(anchorable))
					ToggleAnchorable(anchorable, toolbox.Zone);
				else
					SetToolboxIsOpen(anchorable);
			}
		}

		/// <summary>Writes the state every registered anchorable is actually in back onto its toolbox.</summary>
		private void SyncToolboxStateToLayout()
		{
			foreach (var anchorable in _toolboxToAnchorable.Values.ToList())
				SetToolboxIsOpen(anchorable);
		}

		private void FixSplitOrientation(LayoutAnchorable anchorable, DockZone zone) => _layoutEngine.FixSplitOrientationForZone(anchorable, zone);

		private void EnsureBottomFullWidth() => _layoutEngine.EnsureBottomFullWidth(Layout);

		private void EnsureSidesFullHeight() => _layoutEngine.EnsureSidesFullHeight(Layout);

		/// <summary>Orders the panes of the bottom group: the BottomLeft panes before the BottomRight panes.</summary>
		private void EnsureBottomZoneOrder()
		{
			var root = Layout;
			if (root?.RootPanel == null) return;

			var group = root.RootPanel.Children.OfType<LayoutAnchorablePaneGroup>().FirstOrDefault(g => g.Orientation == Orientation.Horizontal);
			if (group == null || group.Children.Count < 2) return;

			var needsReorder = false;
			var seenRight = false;
			foreach (var child in group.Children.OfType<LayoutAnchorablePane>())
			{
				var anc = child.Children.FirstOrDefault();
				if (anc == null) continue;
				var zone = GetAnchorableZone(anc);
				if (zone == DockZone.BottomRight)
				{
					seenRight = true;
				}
				else if (zone == DockZone.BottomLeft && seenRight)
				{
					needsReorder = true;
					break;
				}
			}

			if (!needsReorder) return;

			var leftPanes = new List<ILayoutAnchorablePane>();
			var rightPanes = new List<ILayoutAnchorablePane>();
			foreach (var child in group.Children.ToList())
			{
				if (child is LayoutAnchorablePane pane && pane.Children.FirstOrDefault() is LayoutAnchorable anc && GetAnchorableZone(anc) == DockZone.BottomRight)
					rightPanes.Add(child);
				else
					leftPanes.Add(child);
			}

			while (group.Children.Count > 0)
				group.Children.RemoveAt(group.Children.Count - 1);
			foreach (var p in leftPanes)
				group.Children.Add(p);
			foreach (var p in rightPanes)
				group.Children.Add(p);
		}

		private void DockFromAutoHide(LayoutAnchorable anchorable, DockZone zone)
		{
			if (!(anchorable.Parent is LayoutAnchorGroup parentGroup)) return;
			if (!(parentGroup.Parent is LayoutAnchorSide parentSide)) return;

			var previousContainer = ((ILayoutPreviousContainer)parentGroup).PreviousContainer as LayoutAnchorablePane;
			if (previousContainer != null && previousContainer.Root == null) previousContainer = null;

			var root = parentGroup.Root as LayoutRoot;
			if (previousContainer == null)
			{
				var side = ToggleLayoutEngine.ZoneToAnchorSide(zone);
				previousContainer = new LayoutAnchorablePane
				{
					DockMinWidth = anchorable.AutoHideMinWidth,
					DockMinHeight = anchorable.AutoHideMinHeight,
				};

				if (side == AnchorSide.Left || side == AnchorSide.Right)
					previousContainer.DockWidth = new GridLength(DefaultDockWidth);
				else
					previousContainer.DockHeight = new GridLength(DefaultDockHeight);

				_layoutEngine.InsertPaneForZone(root, previousContainer, zone);
			}
			else
			{
				// Re-insert at the position of the zone, to keep the zones in order.
				((ILayoutContainer)previousContainer.Parent)?.RemoveChild(previousContainer);
				_layoutEngine.InsertPaneForZone(root, previousContainer, zone);
			}

			parentGroup.Children.Remove(anchorable);
			previousContainer.Children.Add(anchorable);
			if (parentGroup.Children.Count == 0) parentSide.Children.Remove(parentGroup);
		}

		private void AutoHideFromDock(LayoutAnchorable anchorable, DockZone zone)
		{
			if (!(anchorable.Parent is LayoutAnchorablePane parentPane)) return;
			var root = anchorable.Root;
			if (root == null) return;

			var newAnchorGroup = new LayoutAnchorGroup();
			((ILayoutPreviousContainer)newAnchorGroup).PreviousContainer = parentPane;
			parentPane.Children.Remove(anchorable);
			newAnchorGroup.Children.Add(anchorable);

			switch (ToggleLayoutEngine.ZoneToAnchorSide(zone))
			{
				case AnchorSide.Right: root.RightSide?.Children.Add(newAnchorGroup); break;
				case AnchorSide.Left: root.LeftSide?.Children.Add(newAnchorGroup); break;
				case AnchorSide.Bottom: root.BottomSide?.Children.Add(newAnchorGroup); break;
			}
		}

		private void SetupToggleDockButtonBars()
		{
			RemoveToggleDockButtonBars();
			if (Layout == null) return;

			// The bars replace the standard auto-hide sides.
			if (LeftSidePanel != null) LeftSidePanel.IsVisible = false;
			if (RightSidePanel != null) RightSidePanel.IsVisible = false;
			if (TopSidePanel != null) TopSidePanel.IsVisible = false;
			if (BottomSidePanel != null) BottomSidePanel.IsVisible = false;

			AutoHideAllDockedAnchorables();

			// Distribute the anchorables of each side to its two zones by IToolbox.Zone.
			var leftTop = new List<LayoutAnchorable>();
			var leftBottom = new List<LayoutAnchorable>();
			foreach (var anc in CollectAnchorables(Layout.LeftSide))
				(anc.Content is IToolbox t && t.Zone == DockZone.LeftBottom ? leftBottom : leftTop).Add(anc);

			var rightTop = new List<LayoutAnchorable>();
			var rightBottom = new List<LayoutAnchorable>();
			foreach (var anc in CollectAnchorables(Layout.RightSide))
				(anc.Content is IToolbox t && t.Zone == DockZone.RightBottom ? rightBottom : rightTop).Add(anc);

			var bottomLeft = new List<LayoutAnchorable>();
			var bottomRight = new List<LayoutAnchorable>();
			foreach (var anc in CollectAnchorables(Layout.BottomSide))
				(anc.Content is IToolbox t && t.Zone == DockZone.BottomRight ? bottomRight : bottomLeft).Add(anc);

			_leftTopBar = CreateBar(DockZone.LeftTop, leftTop);
			_leftBottomBar = CreateBar(DockZone.LeftBottom, leftBottom);
			_rightTopBar = CreateBar(DockZone.RightTop, rightTop);
			_rightBottomBar = CreateBar(DockZone.RightBottom, rightBottom);
			_bottomLeftBar = CreateBar(DockZone.BottomLeft, bottomLeft);
			_bottomRightBar = CreateBar(DockZone.BottomRight, bottomRight);

			RegisterToolboxesFromBars();

			var rootGrid = _rootGrid;
			if (rootGrid == null) return;

			// Left sidebar: [LeftTop] - separator - [LeftBottom] - hidden panels button - gap - [BottomLeft]
			var leftPanel = new DockPanel { Name = "PART_ToggleLeftSidebar" };
			DockPanel.SetDock(_leftTopBar, Dock.Top);
			DockPanel.SetDock(_bottomLeftBar, Dock.Bottom);
			_leftSeparator = CreateSeparator();
			DockPanel.SetDock(_leftSeparator, Dock.Top);
			DockPanel.SetDock(_leftBottomBar, Dock.Top);
			leftPanel.Children.Add(_leftTopBar);
			leftPanel.Children.Add(_leftSeparator);
			leftPanel.Children.Add(_leftBottomBar);
			leftPanel.Children.Add(CreateShowHiddenButton());
			leftPanel.Children.Add(_bottomLeftBar);
			leftPanel.Children.Add(new Border());
			Grid.SetRow(leftPanel, 0);
			Grid.SetRowSpan(leftPanel, 3);
			Grid.SetColumn(leftPanel, 0);

			// Right sidebar: [RightTop] - separator - [RightBottom] - gap - [BottomRight]
			var rightPanel = new DockPanel { Name = "PART_ToggleRightSidebar" };
			DockPanel.SetDock(_rightTopBar, Dock.Top);
			DockPanel.SetDock(_bottomRightBar, Dock.Bottom);
			_rightSeparator = CreateSeparator();
			DockPanel.SetDock(_rightSeparator, Dock.Top);
			DockPanel.SetDock(_rightBottomBar, Dock.Top);
			rightPanel.Children.Add(_rightTopBar);
			rightPanel.Children.Add(_rightSeparator);
			rightPanel.Children.Add(_rightBottomBar);
			rightPanel.Children.Add(_bottomRightBar);
			rightPanel.Children.Add(new Border());
			Grid.SetRow(rightPanel, 0);
			Grid.SetRowSpan(rightPanel, 3);
			Grid.SetColumn(rightPanel, 2);

			rootGrid.Children.Add(leftPanel);
			rootGrid.Children.Add(rightPanel);
			_injectedLeftDockPanel = leftPanel;
			_injectedRightDockPanel = rightPanel;
		}

		private ToggleDockButtonBar CreateBar(DockZone zone, List<LayoutAnchorable> anchorables)
		{
			var bar = new ToggleDockButtonBar { Orientation = Orientation.Vertical, Zone = zone, Name = "ToggleDockBar_" + zone };
			Avalonia.Automation.AutomationProperties.SetAutomationId(bar, "ToggleDockBar_" + zone);
			bar.SetAnchorables(anchorables, zone);
			return bar;
		}

		private void RemoveToggleDockButtonBars()
		{
			foreach (var toolbox in _toolboxToAnchorable.Keys.ToList())
				UnregisterToolbox(toolbox);

			// Icons are content that may only have one parent; let go of them before the buttons are dropped,
			// so the next set of buttons can show them.
			ClearBarIconContent(_leftTopBar);
			ClearBarIconContent(_leftBottomBar);
			ClearBarIconContent(_rightTopBar);
			ClearBarIconContent(_rightBottomBar);
			ClearBarIconContent(_bottomLeftBar);
			ClearBarIconContent(_bottomRightBar);

			if (_injectedLeftDockPanel != null)
			{
				(_injectedLeftDockPanel.Parent as Panel)?.Children.Remove(_injectedLeftDockPanel);
				_injectedLeftDockPanel = null;
			}

			if (_injectedRightDockPanel != null)
			{
				(_injectedRightDockPanel.Parent as Panel)?.Children.Remove(_injectedRightDockPanel);
				_injectedRightDockPanel = null;
			}

			_leftTopBar = null;
			_leftBottomBar = null;
			_rightTopBar = null;
			_rightBottomBar = null;
			_bottomLeftBar = null;
			_bottomRightBar = null;
		}

		private void AutoHideAllDockedAnchorables()
		{
			var docked = Layout.Descendents()
				.OfType<LayoutAnchorable>()
				.Where(a => a.Parent is LayoutAnchorablePane && !a.IsFloating)
				.ToList();
			foreach (var a in docked)
				a.ToggleSingleAutoHide();
		}

		private void HideDockedInBar(ToggleDockButtonBar bar)
		{
			if (bar == null) return;
			foreach (var item in bar.Items.ToList())
			{
				if (item is ToggleDockButton btn && btn.Anchorable != null && !btn.Anchorable.IsAutoHidden)
				{
					AutoHideFromDock(btn.Anchorable, bar.Zone);

					// This collapse is a side effect of opening a sibling, so nothing else writes it back.
					SetToolboxIsOpen(btn.Anchorable);
				}
			}
		}

		private void RefreshButtonStates()
		{
			var activeContent = ActiveContent;
			RefreshBarStates(_leftTopBar, activeContent);
			RefreshBarStates(_leftBottomBar, activeContent);
			RefreshBarStates(_rightTopBar, activeContent);
			RefreshBarStates(_rightBottomBar, activeContent);
			RefreshBarStates(_bottomLeftBar, activeContent);
			RefreshBarStates(_bottomRightBar, activeContent);
		}

		private DockZone GetAnchorableZone(LayoutAnchorable anchorable)
		{
			foreach (DockZone zone in Enum.GetValues(typeof(DockZone)))
			{
				if (GetBarForZone(zone)?.ContainsAnchorable(anchorable) == true) return zone;
			}

			if (anchorable.Parent is LayoutAnchorGroup group && group.Parent is LayoutAnchorSide side)
			{
				switch (side.Side)
				{
					case AnchorSide.Left: return DockZone.LeftTop;
					case AnchorSide.Right: return DockZone.RightTop;
					case AnchorSide.Bottom: return DockZone.BottomLeft;
				}
			}

			return DockZone.LeftTop;
		}

		private LayoutAnchorSide GetLayoutSideForZone(DockZone zone)
		{
			switch (zone)
			{
				case DockZone.RightTop:
				case DockZone.RightBottom:
					return Layout.RightSide;
				case DockZone.BottomLeft:
				case DockZone.BottomRight:
					return Layout.BottomSide;
				default:
					return Layout.LeftSide;
			}
		}

		private Button CreateShowHiddenButton()
		{
			_showHiddenButton = new Button
			{
				Name = "PART_ShowHiddenButton",
				Width = ButtonSize,
				Height = ButtonSize,
				Padding = default,
				Margin = new Thickness(2),
				Focusable = false,
				Background = Brushes.Transparent,
				BorderThickness = default,
				HorizontalContentAlignment = HorizontalAlignment.Center,
				VerticalContentAlignment = VerticalAlignment.Center,
				Content = new Path
				{
					Data = Geometry.Parse("M8 256a56 56 0 1 1 112 0A56 56 0 1 1 8 256zm160 0a56 56 0 1 1 112 0 56 56 0 1 1 -112 0zm216-56a56 56 0 1 1 0 112 56 56 0 1 1 0-112z"),
					Stretch = Stretch.Uniform,
					Width = 12,
					Height = 12,
					[!Shape.FillProperty] = new Avalonia.Data.Binding("Foreground") { RelativeSource = new Avalonia.Data.RelativeSource(Avalonia.Data.RelativeSourceMode.FindAncestor) { AncestorType = typeof(Button) } },
				},
			};
			ToolTip.SetTip(_showHiddenButton, "Hidden Panels");
			_showHiddenButton[!ForegroundProperty] = _showHiddenButton.GetResourceObservable(ToggleDockButton.ForegroundBrushKey).ToBinding();
			DockPanel.SetDock(_showHiddenButton, Dock.Top);

			_showHiddenButton.Click += (s, e) =>
			{
				var menu = BuildShowHiddenContextMenu();
				if (menu == null) return;
				menu.Placement = PlacementMode.Right;
				menu.Open(_showHiddenButton);
			};

			return _showHiddenButton;
		}

		/// <summary>Builds the menu of hidden anchorables that can be shown again, or <see langword="null"/> when there are none.</summary>
		/// <remarks>
		/// Anchorables without content are skipped: deserializing a stored layout hides every anchorable whose
		/// content could not be resolved, and restoring one of those would open a pane showing nothing.
		/// </remarks>
		private ContextMenu BuildShowHiddenContextMenu()
		{
			var hidden = Layout?.Hidden?.Where(a => a.Content != null).ToList();
			if (hidden == null || hidden.Count == 0) return null;

			var menu = new ContextMenu();
			foreach (var anchorable in hidden)
			{
				var mi = new MenuItem { Header = anchorable.Title };
				if (ToggleDock.GetIcon(anchorable) is Visual iconVisual)
					mi.Icon = new Rectangle { Width = 16, Height = 16, Fill = new VisualBrush(iconVisual) { Stretch = Stretch.Uniform } };
				else if (anchorable.IconSource != null)
					mi.Icon = new Image { Source = anchorable.IconSource, Width = 16, Height = 16 };

				var anc = anchorable;
				mi.Click += (s, ev) => RestoreHiddenAnchorable(anc);
				menu.Items.Add(mi);
			}

			return menu;
		}

		private void RemoveFromAllBars(LayoutAnchorable anchorable)
		{
			RemoveFromBar(_leftTopBar, anchorable);
			RemoveFromBar(_leftBottomBar, anchorable);
			RemoveFromBar(_rightTopBar, anchorable);
			RemoveFromBar(_rightBottomBar, anchorable);
			RemoveFromBar(_bottomLeftBar, anchorable);
			RemoveFromBar(_bottomRightBar, anchorable);
		}

		private void OnActiveContentChangedForAnchorableState(object sender, EventArgs e) => RefreshButtonStates();

		private void RegisterToolboxesFromBars()
		{
			foreach (var bar in new[] { _leftTopBar, _leftBottomBar, _rightTopBar, _rightBottomBar, _bottomLeftBar, _bottomRightBar })
			{
				if (bar == null) continue;
				foreach (var item in bar.Items)
				{
					if (item is ToggleDockButton btn && btn.Anchorable?.Content is IToolbox toolbox)
						RegisterToolbox(toolbox, btn.Anchorable);
				}
			}
		}

		private void OnToolboxPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName != nameof(IToolbox.IsOpen) || _syncDepth > 0) return;
			if (!(sender is IToolbox toolbox) || !_toolboxToAnchorable.TryGetValue(toolbox, out var anchorable)) return;

			// A detached anchorable sits collapsed on its stripe while its content is on screen in a standalone
			// window, so IsAutoHidden on its own does not say whether the toolbox is showing.
			var isOpen = !anchorable.IsAutoHidden || IsDetached(anchorable);
			if (toolbox.IsOpen == isOpen) return;

			// ToggleAnchorable is the one implementation of this transition, and it writes the resulting state back
			// onto the toolbox; the _syncDepth guard keeps that write-back from re-entering this handler.
			_syncDepth++;
			try
			{
				ToggleAnchorable(anchorable, GetAnchorableZone(anchorable));
			}
			finally
			{
				_syncDepth--;
			}
		}

		/// <summary>
		/// Rebuilds the key bindings of the top level from the <see cref="IToolbox.Shortcut"/> of every registered
		/// toolbox. A no-op until the manager is in a top level.
		/// </summary>
		private void RefreshShortcuts()
		{
			RemoveShortcuts();
			var topLevel = TopLevel.GetTopLevel(this);
			if (topLevel == null) return;

			_shortcutHost = topLevel;
			foreach (var pair in _toolboxToAnchorable)
			{
				var gesture = TryParseGesture(pair.Key.Shortcut);
				if (gesture == null) continue;
				var binding = new KeyBinding { Gesture = gesture, Command = new ToggleToolboxCommand(this, pair.Key) };
				topLevel.KeyBindings.Add(binding);
				_shortcutBindings.Add(binding);
			}
		}

		private void RemoveShortcuts()
		{
			if (_shortcutHost != null)
			{
				foreach (var binding in _shortcutBindings)
					_shortcutHost.KeyBindings.Remove(binding);
			}

			_shortcutBindings.Clear();
			_shortcutHost = null;
		}

		/// <summary>Writes the state of <paramref name="anchorable"/> onto its toolbox.</summary>
		private void SetToolboxIsOpen(LayoutAnchorable anchorable)
		{
			if (!(anchorable.Content is IToolbox toolbox)) return;
			_syncDepth++;
			try
			{
				// A detached anchorable is collapsed onto its stripe but its content is on screen in a standalone
				// window, so it counts as open.
				toolbox.IsOpen = !anchorable.IsAutoHidden || IsDetached(anchorable);
			}
			finally
			{
				_syncDepth--;
			}
		}

		/// <summary>Toggles the anchorable of a toolbox like a click on its sidebar button; the command of a shortcut.</summary>
		private sealed class ToggleToolboxCommand : ICommand
		{
			private readonly ToggleDockingManager _manager;
			private readonly IToolbox _toolbox;

			public ToggleToolboxCommand(ToggleDockingManager manager, IToolbox toolbox)
			{
				_manager = manager;
				_toolbox = toolbox;
			}

			event EventHandler ICommand.CanExecuteChanged
			{
				add { }
				remove { }
			}

			public bool CanExecute(object parameter) => true;

			public void Execute(object parameter)
			{
				if (!_manager._toolboxToAnchorable.TryGetValue(_toolbox, out var anchorable)) return;
				_manager.ToggleAnchorable(anchorable, _manager.GetAnchorableZone(anchorable));
			}
		}
	}
}
