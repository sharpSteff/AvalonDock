using Avalonia.Styling;
using Avalonia.Controls.Documents;
using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvalonDock.Layout;
using AvalonDock.Themes;

namespace AvalonDock.Controls
{
	/// <summary>
	/// Provides a base class for the top-level windows that host floating anchorables and documents.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Floating windows draw their own chrome (<see cref="Window.WindowDecorations"/> is
	/// <see cref="WindowDecorations.None"/>) and are moved by a managed drag rather than a native window move,
	/// because the drag must feed every pointer position into the <see cref="DragService"/> that shows the drop
	/// targets and must know reliably when the drag ends. A native move loop reports neither on every platform.
	/// </para>
	/// <para>
	/// While a drag that started on a tab or title in another window is in progress, the pointer stays captured
	/// by that (stationary) window, see <see cref="DragStartInfo"/>. Only a drag on this window's own caption
	/// is driven from the moving window itself.
	/// </para>
	/// </remarks>
	public abstract class LayoutFloatingWindowControl : Window, ILayoutControl
	{
		/// <summary>Thickness of the invisible border along which the window can be resized.</summary>
		private const double ResizeBorder = 6.0;

		/// <summary>Step of a keyboard move, in device independent pixels.</summary>
		private const double KeyboardMoveStep = 10.0;

		/// <summary>The size of the window and its distance from the pointer in <see cref="SetCompactDrag"/> mode.</summary>
		private const double CompactDragWidth = 200.0;
		private const double CompactDragHeight = 28.0;
		private const double CompactDragGap = 16.0;

		/// <summary>How far, in device independent pixels, the pointer moves before a caption press becomes a drag.</summary>
		private const double CaptionDragThreshold = 4.0;

		private Size? _sizeBeforeCompactDrag;
		private Vector _grabOffsetBeforeCompactDrag;

		private readonly ILayoutElement _model;
		private bool _internalCloseFlag = false;
		private bool _isClosing = false;
		private DragService _dragService;
		private Control _dragCaptureTarget;
		private IPointer _dragPointer;
		private Vector _dragGrabOffset;
		private Point _lastDragScreenPoint;
		private Point? _pendingCaptionDragStart;
		private bool _suppressFloatingPropertiesUpdate;
		private IStyle _appliedThemeStyles;
		private bool _isPositionInitialized;
		private bool _isSizedToContent;
		private readonly System.Collections.Generic.HashSet<AvaloniaProperty> _mirroredProperties = new System.Collections.Generic.HashSet<AvaloniaProperty>();

		/// <summary><see cref="IsContentImmutable"/> property.</summary>
		public static readonly StyledProperty<bool> IsContentImmutableProperty =
			AvaloniaProperty.Register<LayoutFloatingWindowControl, bool>(nameof(IsContentImmutable));

		/// <summary><see cref="IsDragging"/> property.</summary>
		public static readonly DirectProperty<LayoutFloatingWindowControl, bool> IsDraggingProperty =
			AvaloniaProperty.RegisterDirect<LayoutFloatingWindowControl, bool>(nameof(IsDragging), o => o.IsDragging);

		/// <summary><see cref="OwnedByDockingManagerWindow"/> property.</summary>
		public static readonly StyledProperty<bool> OwnedByDockingManagerWindowProperty =
			AvaloniaProperty.Register<LayoutFloatingWindowControl, bool>(nameof(OwnedByDockingManagerWindow), true);

		/// <summary><see cref="AllowMinimize"/> property.</summary>
		public static readonly StyledProperty<bool> AllowMinimizeProperty =
			AvaloniaProperty.Register<LayoutFloatingWindowControl, bool>(nameof(AllowMinimize));

		/// <summary><see cref="IsMaximized"/> property.</summary>
		public static readonly DirectProperty<LayoutFloatingWindowControl, bool> IsMaximizedProperty =
			AvaloniaProperty.RegisterDirect<LayoutFloatingWindowControl, bool>(nameof(IsMaximized), o => o.IsMaximized);

		private bool _isDragging;
		private bool _isMaximized;

		/// <summary>Initializes a new instance of the <see cref="LayoutFloatingWindowControl"/> class.</summary>
		/// <param name="model">The layout model.</param>
		/// <param name="isContentImmutable">Whether the content may change while floating.</param>
		protected LayoutFloatingWindowControl(ILayoutElement model, bool isContentImmutable)
		{
			_model = model;
			IsContentImmutable = isContentImmutable;
			WindowDecorations = WindowDecorations.None;
			ShowInTaskbar = false;
			CanResize = true;
			SizeToContent = SizeToContent.Manual;
			WindowStartupLocation = WindowStartupLocation.Manual;
			PositionChanged += OnPositionChanged;
			AddHandler(PointerPressedEvent, OnPreviewPointerPressed, RoutingStrategies.Tunnel);
			AddHandler(PointerMovedEvent, OnPreviewPointerMovedForCursor, RoutingStrategies.Tunnel);
		}

		/// <summary>Gets the layout model of the window.</summary>
		public abstract ILayoutElement Model { get; }

		/// <summary>Gets or sets a value indicating whether the content of the window may change while it floats.</summary>
		public bool IsContentImmutable
		{
			get => GetValue(IsContentImmutableProperty);
			set => SetValue(IsContentImmutableProperty, value);
		}

		/// <summary>Gets a value indicating whether the window is being dragged.</summary>
		public bool IsDragging
		{
			get => _isDragging;
			private set => SetAndRaise(IsDraggingProperty, ref _isDragging, value);
		}

		/// <summary>
		/// Gets or sets a value indicating whether the window is owned by the window that hosts the
		/// <see cref="DockingManager"/>, which keeps it above that window and minimizes it along with it.
		/// </summary>
		public bool OwnedByDockingManagerWindow
		{
			get => GetValue(OwnedByDockingManagerWindowProperty);
			set => SetValue(OwnedByDockingManagerWindowProperty, value);
		}

		/// <summary>Gets or sets a value indicating whether the window can be minimized.</summary>
		public bool AllowMinimize
		{
			get => GetValue(AllowMinimizeProperty);
			set => SetValue(AllowMinimizeProperty, value);
		}

		/// <summary>Gets a value indicating whether the window is maximized.</summary>
		public bool IsMaximized
		{
			get => _isMaximized;
			private set => SetAndRaise(IsMaximizedProperty, ref _isMaximized, value);
		}

		/// <summary><see cref="SingleContent"/> property.</summary>
		public static readonly DirectProperty<LayoutFloatingWindowControl, LayoutContent> SingleContentProperty =
			AvaloniaProperty.RegisterDirect<LayoutFloatingWindowControl, LayoutContent>(nameof(SingleContent), o => o.SingleContent);

		/// <summary><see cref="SingleContentLayoutItem"/> property.</summary>
		public static readonly DirectProperty<LayoutFloatingWindowControl, LayoutItem> SingleContentLayoutItemProperty =
			AvaloniaProperty.RegisterDirect<LayoutFloatingWindowControl, LayoutItem>(nameof(SingleContentLayoutItem), o => o.SingleContentLayoutItem);

		private LayoutContent _singleContent;
		private LayoutItem _singleContentLayoutItem;

		/// <summary>
		/// Gets the selected content when the window holds a single pane, whose title the caption then shows;
		/// otherwise <see langword="null"/>.
		/// </summary>
		public LayoutContent SingleContent
		{
			get => _singleContent;
			private set => SetAndRaise(SingleContentProperty, ref _singleContent, value);
		}

		/// <summary>Gets the layout item of <see cref="SingleContent"/>.</summary>
		public LayoutItem SingleContentLayoutItem
		{
			get => _singleContentLayoutItem;
			private set => SetAndRaise(SingleContentLayoutItemProperty, ref _singleContentLayoutItem, value);
		}

		/// <summary>Recomputes <see cref="SingleContent"/> from the model.</summary>
		protected void UpdateSingleContent()
		{
			LayoutContent content = null;
			switch (Model)
			{
				case LayoutAnchorableFloatingWindow anchorableWindow when anchorableWindow.IsSinglePane:
					content = (anchorableWindow.SinglePane as ILayoutContentSelector)?.SelectedContent;
					break;
				case LayoutDocumentFloatingWindow documentWindow when documentWindow.IsSinglePane:
					content = documentWindow.SinglePane?.SelectedContent;
					break;
			}

			SingleContent = content;
			SingleContentLayoutItem = content == null ? null : Model?.Root?.Manager?.GetLayoutItemFromModel(content);
			Title = content?.Title ?? Model?.Descendents().OfType<LayoutContent>().FirstOrDefault()?.Title ?? string.Empty;
		}

		/// <summary>Gets the command that toggles between the maximized and the normal state.</summary>
		public System.Windows.Input.ICommand ToggleMaximizeCommand => _toggleMaximizeCommand ??= new Commands.RelayCommand<object>(_ => ToggleMaximized());

		private System.Windows.Input.ICommand _toggleMaximizeCommand;

		/// <inheritdoc/>
		protected override Type StyleKeyOverride => GetType();

		/// <summary>Gets a value indicating whether the close was requested by the user (rather than by the docking code).</summary>
		protected bool CloseInitiatedByUser => !_internalCloseFlag;

		/// <summary>Gets or sets a value indicating whether the content stays in the layout when the window closes.</summary>
		internal bool KeepContentVisibleOnClose { get; set; }

		/// <summary>Gets the drag service of the drag in progress, if any.</summary>
		internal DragService CurrentDragService => _dragService;

		/// <summary>Gets the last pointer position of the drag in progress, in screen pixels.</summary>
		internal Point LastDragScreenPoint => _lastDragScreenPoint;

		/// <summary>Applies the position and size stored in the model, in device independent pixels.</summary>
		/// <param name="left">The left edge.</param>
		/// <param name="top">The top edge.</param>
		/// <param name="width">The width.</param>
		/// <param name="height">The height.</param>
		internal void SetFloatingBounds(double left, double top, double width, double height)
		{
			if (width > 0) Width = width;
			if (height > 0) Height = height;
			var scaling = GetDesktopScaling();
			_suppressFloatingPropertiesUpdate = true;
			try
			{
				Position = new PixelPoint((int)Math.Round(left * scaling), (int)Math.Round(top * scaling));
			}
			finally
			{
				_suppressFloatingPropertiesUpdate = false;
			}

			_isPositionInitialized = true;
		}

		/// <summary>Places the window under the pointer of a drag that is about to be handed over to it.</summary>
		/// <param name="dragStart">The drag.</param>
		internal void SetFloatingBoundsForDrag(DragStartInfo dragStart)
		{
			if (dragStart == null) return;
			_suppressFloatingPropertiesUpdate = true;
			try
			{
				Position = new PixelPoint(
					(int)Math.Round(dragStart.ScreenPoint.X - dragStart.GrabOffset.X),
					(int)Math.Round(dragStart.ScreenPoint.Y - dragStart.GrabOffset.Y));
			}
			finally
			{
				_suppressFloatingPropertiesUpdate = false;
			}

			_isPositionInitialized = true;
		}

		/// <summary>Shows the window, owned by the window of the docking manager when that is configured and possible.</summary>
		internal void ShowOwned()
		{
			if (IsVisible) return;
			var owner = GetManagerWindow();
			if (OwnedByDockingManagerWindow && owner != null && owner.IsVisible)
				Show(owner);
			else
				Show();
		}

		/// <summary>Gets the window that hosts the docking manager this window belongs to.</summary>
		/// <returns>The window, or <see langword="null"/>.</returns>
		internal Window GetManagerWindow() => Model?.Root?.Manager is Visual manager ? TopLevel.GetTopLevel(manager) as Window : null;

		/// <summary>Applies the theme of the manager to this top level, which does not inherit resources from it.</summary>
		/// <param name="oldTheme">The previous theme.</param>
		internal virtual void UpdateThemeResources(Theme oldTheme = null)
		{
			if (_appliedThemeStyles != null)
			{
				Styles.Remove(_appliedThemeStyles);
				_appliedThemeStyles = null;
			}

			var manager = _model.Root?.Manager;
			var theme = manager?.DockTheme;
			if (theme == null) return;
			_appliedThemeStyles = theme.CreateStyles();
			if (_appliedThemeStyles != null) Styles.Add(_appliedThemeStyles);
		}

		/// <summary>Starts a drag that was begun on another element, for example a tab that was torn out.</summary>
		/// <param name="dragStart">The drag to take over.</param>
		internal void AttachDrag(DragStartInfo dragStart)
		{
			if (dragStart?.Pointer == null || dragStart.CaptureTarget == null) return;
			BeginDrag(dragStart.Pointer, dragStart.CaptureTarget, dragStart.ScreenPoint, dragStart.GrabOffset);
		}

		/// <summary>Starts dragging the window from a press on its caption.</summary>
		/// <param name="e">The press.</param>
		internal void BeginCaptionDrag(PointerPressedEventArgs e)
		{
			if (Model?.Root?.Manager == null) return;
			ActivateContentForCaptionPress();
			var screenPoint = this.LocalToScreen(e.GetPosition(this));
			var grabOffset = new Vector(screenPoint.X - Position.X, screenPoint.Y - Position.Y);

			// Like the WPF window, whose drag starts when the window moves: a click on the caption only
			// activates the window, and starts no drag (with its drop targets and, without a compositor, the
			// window shrunk to a ghost of itself).
			BeginDrag(e.Pointer, this, screenPoint, grabOffset, waitForMove: true);
			e.Handled = true;
		}

		/// <summary>
		/// Starts a drag that is driven through <see cref="DragTo"/> and <see cref="EndDrag"/> instead of pointer
		/// events, for example by keyboard moves or by tests.
		/// </summary>
		/// <param name="screenPoint">The start position of the virtual pointer in screen pixels.</param>
		internal void BeginProgrammaticDrag(Point screenPoint)
		{
			if (_dragService != null) EndDrag(false);
			if (Model?.Root?.Manager == null) return;
			_dragGrabOffset = new Vector(screenPoint.X - Position.X, screenPoint.Y - Position.Y);
			_dragService = new DragService(this);
			IsDragging = true;
			DragTo(screenPoint);
		}

		/// <summary>Moves a drag that is in progress to <paramref name="screenPoint"/>, as if the pointer had moved there.</summary>
		/// <param name="screenPoint">The pointer position in screen pixels.</param>
		internal void DragTo(Point screenPoint)
		{
			if (_dragService == null) return;
			_lastDragScreenPoint = screenPoint;
			MoveToDragPoint();
			_dragService.UpdateMouseLocation(screenPoint);
		}

		/// <summary>Gets a value indicating whether the window is shrunk to a ghost beside the pointer, see <see cref="SetCompactDrag"/>.</summary>
		internal bool IsCompactDrag => _sizeBeforeCompactDrag.HasValue;

		/// <summary>
		/// Shrinks the dragged window to its caption and moves it beside the pointer, or restores it. Used while
		/// the drop targets are drawn into the window under the pointer (no compositing window manager, so no
		/// transparent overlay window): a full size window following the pointer would hide them.
		/// </summary>
		/// <param name="compact">Whether to shrink the window.</param>
		internal void SetCompactDrag(bool compact)
		{
			if (compact == IsCompactDrag) return;
			var scaling = GetDesktopScaling();
			if (compact)
			{
				_sizeBeforeCompactDrag = new Size(Width, Height);
				_grabOffsetBeforeCompactDrag = _dragGrabOffset;
				Width = Math.Min(Width, CompactDragWidth);
				Height = Math.Min(Height, CompactDragHeight);

				// Below and to the right of the pointer, so the indicator under the pointer stays visible.
				_dragGrabOffset = new Vector(-CompactDragGap * scaling, -CompactDragGap * scaling);
			}
			else
			{
				var size = _sizeBeforeCompactDrag.Value;
				_sizeBeforeCompactDrag = null;
				Width = size.Width;
				Height = size.Height;
				_dragGrabOffset = _grabOffsetBeforeCompactDrag;
			}

			MoveToDragPoint();
		}

		private void MoveToDragPoint()
		{
			Position = new PixelPoint(
				(int)Math.Round(_lastDragScreenPoint.X - _dragGrabOffset.X),
				(int)Math.Round(_lastDragScreenPoint.Y - _dragGrabOffset.Y));
		}

		/// <summary>Ends a drag that is in progress.</summary>
		/// <param name="drop">Whether to drop the window onto the target under the pointer.</param>
		/// <returns><see langword="true"/> when the window was docked.</returns>
		internal bool EndDrag(bool drop)
		{
			var dragService = _dragService;
			if (dragService == null) return false;
			_dragService = null;
			_pendingCaptionDragStart = null;
			DetachDragHandlers();
			SetCompactDrag(false);

			var dropHandled = false;
			if (drop)
				dragService.Drop(_lastDragScreenPoint, out dropHandled);
			else
				dragService.Abort();

			IsDragging = false;
			if (dropHandled)
			{
				// The window is empty now; close it once the drop has settled.
				Dispatcher.UIThread.Post(() => InternalClose(), DispatcherPriority.Background);
			}

			return dropHandled;
		}

		private void BeginDrag(IPointer pointer, Control captureTarget, Point screenPoint, Vector grabOffset, bool waitForMove = false)
		{
			if (_dragService != null) EndDrag(false);
			if (Model?.Root?.Manager == null) return;

			_dragPointer = pointer;
			_dragCaptureTarget = captureTarget;
			_dragGrabOffset = grabOffset;
			_dragService = new DragService(this);
			IsDragging = true;

			captureTarget.AddHandler(PointerMovedEvent, OnDragPointerMoved, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
			captureTarget.AddHandler(PointerReleasedEvent, OnDragPointerReleased, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
			captureTarget.AddHandler(PointerCaptureLostEvent, OnDragPointerCaptureLost, RoutingStrategies.Direct | RoutingStrategies.Bubble, handledEventsToo: true);
			captureTarget.AddHandler(KeyDownEvent, OnDragKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
			pointer.Capture(captureTarget);

			_lastDragScreenPoint = screenPoint;
			if (waitForMove)
				_pendingCaptionDragStart = screenPoint;
			else
				DragTo(screenPoint);
		}

		private void DetachDragHandlers()
		{
			var captureTarget = _dragCaptureTarget;
			var pointer = _dragPointer;
			_dragCaptureTarget = null;
			_dragPointer = null;
			if (captureTarget == null) return;
			captureTarget.RemoveHandler(PointerMovedEvent, OnDragPointerMoved);
			captureTarget.RemoveHandler(PointerReleasedEvent, OnDragPointerReleased);
			captureTarget.RemoveHandler(PointerCaptureLostEvent, OnDragPointerCaptureLost);
			captureTarget.RemoveHandler(KeyDownEvent, OnDragKeyDown);
			if (pointer != null && ReferenceEquals(pointer.Captured, captureTarget)) pointer.Capture(null);
		}

		private void OnDragPointerMoved(object sender, PointerEventArgs e)
		{
			if (_dragService == null || !ReferenceEquals(e.Pointer, _dragPointer)) return;
			var captureTarget = _dragCaptureTarget;
			if (captureTarget == null) return;
			var screenPoint = captureTarget.LocalToScreen(e.GetPosition(captureTarget));
			e.Handled = true;
			if (_pendingCaptionDragStart is { } start)
			{
				var threshold = CaptionDragThreshold * GetDesktopScaling();
				if (Math.Abs(screenPoint.X - start.X) < threshold && Math.Abs(screenPoint.Y - start.Y) < threshold) return;
				_pendingCaptionDragStart = null;
			}

			DragTo(screenPoint);
		}

		private void OnDragPointerReleased(object sender, PointerReleasedEventArgs e)
		{
			if (_dragService == null || !ReferenceEquals(e.Pointer, _dragPointer)) return;
			var captureTarget = _dragCaptureTarget;
			if (captureTarget != null) _lastDragScreenPoint = captureTarget.LocalToScreen(e.GetPosition(captureTarget));
			e.Handled = true;

			// A caption press released before the pointer moved is a click, not a drop.
			EndDrag(drop: _pendingCaptionDragStart == null);
		}

		private void OnDragPointerCaptureLost(object sender, PointerCaptureLostEventArgs e)
		{
			if (_dragService == null || !ReferenceEquals(e.Pointer, _dragPointer)) return;

			// Capture is lost when the pointer leaves the application in the middle of a drag (another app
			// grabbed it, the source window was closed...). Whatever was going on, the drag cannot continue.
			EndDrag(drop: false);
		}

		private void OnDragKeyDown(object sender, KeyEventArgs e)
		{
			if (_dragService == null || e.Key != Key.Escape) return;
			e.Handled = true;
			EndDrag(drop: false);
		}

		/// <summary>Marks the visible content of the window active when its caption is pressed.</summary>
		private void ActivateContentForCaptionPress()
		{
			var content = Model?.Descendents().OfType<LayoutContent>().FirstOrDefault(c => c.IsSelected)
				?? Model?.Descendents().OfType<LayoutContent>().FirstOrDefault();
			if (content != null && !content.IsActive)
				content.IsActive = true;
		}

		/// <summary>Closes the window on behalf of the docking code.</summary>
		/// <param name="closeInitiatedByUser">Whether the close counts as initiated by the user.</param>
		internal void InternalClose(bool closeInitiatedByUser = false)
		{
			_internalCloseFlag = !closeInitiatedByUser;
			if (_isClosing) return;
			_isClosing = true;
			Close();
		}

		/// <summary>Gets a value indicating whether the window is closing or closed.</summary>
		internal bool IsClosingOrClosed => _isClosing;

		/// <inheritdoc/>
		protected override void OnClosing(WindowClosingEventArgs e)
		{
			base.OnClosing(e);
			if (e.Cancel) return;

			// Stop everything that can still touch this window before the platform destroys it.
			EndDrag(drop: false);
			_isClosing = true;
		}

		/// <inheritdoc/>
		protected override void OnClosed(EventArgs e)
		{
			EndDrag(drop: false);
			PositionChanged -= OnPositionChanged;
			base.OnClosed(e);

			// The window that owned this one is brought back to the front, which also keeps it from being
			// minimized along with this window on some platforms.
			GetManagerWindow()?.Activate();
		}

		/// <inheritdoc/>
		protected override void OnOpened(EventArgs e)
		{
			base.OnOpened(e);
			UpdateThemeResources();
			SyncInheritedProperties();

			// Restore maximize state
			var maximized = Model.Descendents().OfType<ILayoutElementForFloatingWindow>().Any(l => l.IsMaximized);
			if (maximized) UpdateMaximizedState(true);

			// The window manager may not put the window where it was asked to: macOS keeps windows below the
			// menu bar, and window managers on Linux may place new windows themselves. Store where it ended up,
			// once it is shown and again once the placement has settled.
			_isPositionInitialized = true;
			if (!IsDragging) UpdatePositionAndSizeOfPanes();
			Dispatcher.UIThread.Post(
				() =>
				{
					UpdateWindowSizeBasedOnMinSize();
					if (IsVisible && !IsDragging) UpdatePositionAndSizeOfPanes();
				},
				DispatcherPriority.Background);
		}

		/// <summary>
		/// Once the window has been laid out the first time, enlarges it so that its contents get their minimum
		/// size and, with <see cref="DockingManager.AutoWindowSizeWhenOpened"/>, the size they ask for - what the
		/// WPF window does on its first activation. A window floated straight after its content was added to the
		/// layout has no measured size to start from otherwise.
		/// </summary>
		private void UpdateWindowSizeBasedOnMinSize()
		{
			if (_isSizedToContent || !IsVisible || Model == null) return;
			_isSizedToContent = true;

			var autoSize = Model.Root?.Manager?.AutoWindowSizeWhenOpened == true;
			double extraWidth = 0, extraHeight = 0;
			foreach (var content in Model.Descendents().OfType<LayoutContent>().Select(c => c.Content).OfType<Control>())
			{
				// The content's host gets the space the window has for it; the content may be larger and clipped.
				if (content.GetVisualParent() is not Visual host) continue;

				var wanted = new Size(
					content.MinWidth + content.Margin.Left + content.Margin.Right,
					content.MinHeight + content.Margin.Top + content.Margin.Bottom);
				if (autoSize)
					wanted = new Size(Math.Max(wanted.Width, content.DesiredSize.Width), Math.Max(wanted.Height, content.DesiredSize.Height));

				extraWidth = Math.Max(extraWidth, wanted.Width - host.Bounds.Width);
				extraHeight = Math.Max(extraHeight, wanted.Height - host.Bounds.Height);
			}

			if (extraWidth > 0) Width = Bounds.Width + extraWidth;
			if (extraHeight > 0) Height = Bounds.Height + extraHeight;
		}

		/// <summary>Copies the inheritable values of the manager onto this top level, which cannot inherit them.</summary>
		internal void SyncInheritedProperties()
		{
			if (Model?.Root?.Manager is not DockingManager manager) return;
			SyncFromManager(manager, DataContextProperty);
			SyncFromManager(manager, TextElement.FontFamilyProperty);
			SyncFromManager(manager, TextElement.FontSizeProperty);
			SyncFromManager(manager, TextElement.ForegroundProperty);
			SyncFromManager(manager, FlowDirectionProperty);
			if (TopLevel.GetTopLevel(manager) is TopLevel managerTopLevel && !IsSet(RequestedThemeVariantProperty))
				RequestedThemeVariant = managerTopLevel.ActualThemeVariant;
		}

		private void SyncFromManager(DockingManager manager, AvaloniaProperty property)
		{
			// A value that was set on the window itself - by a style or by application code - wins over the
			// mirrored one; only values this method set itself are refreshed.
			if (IsSet(property) && !_mirroredProperties.Contains(property)) return;
			SetValue(property, manager.GetValue(property), Avalonia.Data.BindingPriority.Style);
			_mirroredProperties.Add(property);
		}

		/// <inheritdoc/>
		protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
		{
			base.OnPropertyChanged(change);
			if (change.Property == WindowStateProperty)
			{
				IsMaximized = WindowState == WindowState.Maximized;
				foreach (var posElement in Model.Descendents().OfType<ILayoutElementForFloatingWindow>())
					posElement.IsMaximized = IsMaximized;
				if (!AllowMinimize && WindowState == WindowState.Minimized)
					WindowState = WindowState.Normal;
			}
			else if (change.Property == ClientSizeProperty)
			{
				UpdatePositionAndSizeOfPanes();
			}
		}

		private void OnPositionChanged(object sender, PixelPointEventArgs e)
		{
			if (!_isPositionInitialized && !IsVisible) return;
			UpdatePositionAndSizeOfPanes();
		}

		private double GetDesktopScaling()
		{
			try
			{
				return DesktopScaling > 0 ? DesktopScaling : 1.0;
			}
			catch (InvalidOperationException)
			{
				return 1.0;
			}
		}

		private void UpdatePositionAndSizeOfPanes()
		{
			if (_suppressFloatingPropertiesUpdate || WindowState != WindowState.Normal || Model == null) return;
			var scaling = GetDesktopScaling();
			foreach (var posElement in Model.Descendents().OfType<ILayoutElementForFloatingWindow>())
			{
				posElement.FloatingLeft = Position.X / scaling;
				posElement.FloatingTop = Position.Y / scaling;
				if (ClientSize.Width > 0) posElement.FloatingWidth = ClientSize.Width;
				if (ClientSize.Height > 0) posElement.FloatingHeight = ClientSize.Height;
				posElement.RaiseFloatingPropertiesUpdated();
			}
		}

		private void ToggleMaximized() => UpdateMaximizedState(WindowState != WindowState.Maximized);

		private void UpdateMaximizedState(bool isMaximized)
		{
			foreach (var posElement in Model.Descendents().OfType<ILayoutElementForFloatingWindow>())
				posElement.IsMaximized = isMaximized;
			IsMaximized = isMaximized;
			WindowState = isMaximized ? WindowState.Maximized : WindowState.Normal;
		}

		/// <inheritdoc/>
		protected override void OnKeyDown(KeyEventArgs e)
		{
			base.OnKeyDown(e);
			if (e.Handled) return;
			var manager = Model?.Root?.Manager;
			if (manager == null || !manager.AllowMovingFloatingWindowWithKeyboard) return;

			var step = (int)Math.Round(KeyboardMoveStep * GetDesktopScaling());
			switch (e.Key)
			{
				case Key.Left:
					Position = new PixelPoint(Position.X - step, Position.Y);
					e.Handled = true;
					break;
				case Key.Right:
					Position = new PixelPoint(Position.X + step, Position.Y);
					e.Handled = true;
					break;
				case Key.Up:
					Position = new PixelPoint(Position.X, Position.Y - step);
					e.Handled = true;
					break;
				case Key.Down:
					Position = new PixelPoint(Position.X, Position.Y + step);
					e.Handled = true;
					break;
			}
		}

		/// <summary>Gets the edge of the window under <paramref name="point"/>, for resizing.</summary>
		/// <param name="point">A point in window coordinates.</param>
		/// <returns>The edge, or <see langword="null"/> when the point is not on the border.</returns>
		private WindowEdge? GetResizeEdge(Point point)
		{
			if (!CanResize || WindowState != WindowState.Normal) return null;
			var size = Bounds.Size;
			var left = point.X < ResizeBorder;
			var right = point.X > size.Width - ResizeBorder;
			var top = point.Y < ResizeBorder;
			var bottom = point.Y > size.Height - ResizeBorder;
			if (top && left) return WindowEdge.NorthWest;
			if (top && right) return WindowEdge.NorthEast;
			if (bottom && left) return WindowEdge.SouthWest;
			if (bottom && right) return WindowEdge.SouthEast;
			if (left) return WindowEdge.West;
			if (right) return WindowEdge.East;
			if (top) return WindowEdge.North;
			if (bottom) return WindowEdge.South;
			return null;
		}

		private void OnPreviewPointerMovedForCursor(object sender, PointerEventArgs e)
		{
			if (_dragService != null) return;
			var edge = GetResizeEdge(e.GetPosition(this));
			Cursor = edge switch
			{
				WindowEdge.NorthWest or WindowEdge.SouthEast => new Cursor(StandardCursorType.TopLeftCorner),
				WindowEdge.NorthEast or WindowEdge.SouthWest => new Cursor(StandardCursorType.TopRightCorner),
				WindowEdge.West or WindowEdge.East => new Cursor(StandardCursorType.SizeWestEast),
				WindowEdge.North or WindowEdge.South => new Cursor(StandardCursorType.SizeNorthSouth),
				_ => Cursor.Default,
			};
		}

		private void OnPreviewPointerPressed(object sender, PointerPressedEventArgs e)
		{
			var point = e.GetCurrentPoint(this);
			if (!point.Properties.IsLeftButtonPressed) return;

			var edge = GetResizeEdge(point.Position);
			if (edge.HasValue)
			{
				BeginResizeDrag(edge.Value, e);
				e.Handled = true;
				return;
			}

			// The caption strip drags the window; buttons and menus inside it keep their own behaviour.
			if (e.Source is not Visual source) return;
			var caption = source.GetSelfAndVisualAncestors().TakeWhile(v => v != this)
				.OfType<Control>()
				.FirstOrDefault(c => c.Name == "PART_Caption" || c.Classes.Contains("caption"));
			if (caption == null) return;
			if (source.GetSelfAndVisualAncestors().TakeWhile(v => v != caption).Any(v => v is Button || v is ToggleButton)) return;

			if (e.ClickCount == 2)
			{
				ToggleMaximized();
				e.Handled = true;
				return;
			}

			BeginCaptionDrag(e);
		}
	}
}
