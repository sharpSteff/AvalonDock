using Avalonia.Controls.Presenters;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.LogicalTree;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Metadata;
using Avalonia.Styling;
using Avalonia.Threading;
using AvalonDock.Commands;
using AvalonDock.Controls;
using AvalonDock.Layout;
using AvalonDock.Themes;

namespace AvalonDock
{
	/// <summary>
	/// Represents the root control in the visual tree for AvalonDock.
	/// </summary>
	/// <remarks>
	/// This is the Avalonia port of the WPF <c>DockingManager</c>. The layout model, the commands and the
	/// public surface are the same; what differs is how the platform pieces are done:
	/// <list type="bullet">
	/// <item>Floating windows are borderless Avalonia windows moved by a managed drag (see <see cref="LayoutFloatingWindowControl"/>).</item>
	/// <item>The auto-hide flyout lives in the control template instead of a child HWND.</item>
	/// <item>The drop-target overlay is a control shown in a transparent window or in the overlay layer (see <see cref="OverlayWindowMode"/>).</item>
	/// <item>Styles are <see cref="ControlTheme"/>s, template selectors are <see cref="DataTemplateSelector"/>s.</item>
	/// </list>
	/// </remarks>
	[TemplatePart("PART_AutoHideArea", typeof(ContentPresenter))]
	public class DockingManager : TemplatedControl, IOverlayWindowHost, Core.Serialization.ISerializableDockingManager, Core.IDockingManager
	{
		/// <summary>Manages the auto-hide window lifecycle for this docking manager.</summary>
		private AutoHideWindowManager _autoHideWindowManager;

		/// <summary>References the auto-hide area defined by the control template.</summary>
		private Control _autohideArea;

		/// <summary>Stores the currently visible floating window controls.</summary>
		private readonly List<LayoutFloatingWindowControl> _fwList = new List<LayoutFloatingWindowControl>();

		/// <summary>Stores floating window controls that are temporarily hidden while the manager is unloaded.</summary>
		private readonly List<LayoutFloatingWindowControl> _fwHiddenList = new List<LayoutFloatingWindowControl>();

		/// <summary>The order in which the floating windows were last activated, most recent first.</summary>
		private readonly List<LayoutFloatingWindowControl> _fwActivationOrder = new List<LayoutFloatingWindowControl>();

		/// <summary>References the overlay used for drag-and-drop feedback.</summary>
		private OverlayWindow _overlayWindow = null;

		/// <summary>Caches the drop areas available for the active drag operation.</summary>
		private List<IDropArea> _areas = null;

		/// <summary>Indicates whether <see cref="InternalSetActiveContent"/> is currently updating the active content.</summary>
		private bool _insideInternalSetActiveContent = false;

		/// <summary>Stores the layout items attached to the current layout content elements.</summary>
		private readonly List<LayoutItem> _layoutItems = new List<LayoutItem>();

		/// <summary>Indicates whether layout item creation is temporarily suspended.</summary>
		private bool _suspendLayoutItemCreation = false;

		/// <summary>Whether a removal of deleted layout items is pending.</summary>
		private bool _collectLayoutItemsPending = false;

		/// <summary>The styles of the current <see cref="DockTheme"/>.</summary>
		private IStyle _themeStyles;

		/// <summary>The layout engine used for layout tree operations.</summary>
		private readonly ILayoutEngine _layoutEngine = new DefaultLayoutEngine();

		/// <summary>Whether the control is attached to a visual tree that has been shown.</summary>
		private bool _isLoaded;

		/// <summary>The navigator window currently shown, if any.</summary>
		private NavigatorWindow _navigatorWindow;

		private readonly Core.Serialization.ILayoutDtoMapper _dtoMapper = new Serialization.LayoutDtoMapper();

		/// <summary>Synchronizes the MVVM dock layout with the AvalonDock layout.</summary>
		private LayoutSyncBridge _syncBridge;

		/// <summary>Tracks the alignment strategy installed by the base DockingManager so it can be cleaned up.</summary>
		private ILayoutUpdateStrategy _installedAlignmentStrategy;

		private event EventHandler<Core.Events.DocumentCancelEventArgs> _coreDocumentClosing;
		private event EventHandler<Core.Events.DocumentEventArgs> _coreDocumentClosed;
		private event EventHandler<Core.Events.AnchorableCancelEventArgs> _coreAnchorableClosing;
		private event EventHandler<Core.Events.AnchorableEventArgs> _coreAnchorableClosed;
		private event EventHandler<Core.Events.AnchorableCancelEventArgs> _coreAnchorableHiding;
		private event EventHandler<Core.Events.AnchorableEventArgs> _coreAnchorableHidden;
		private event EventHandler<Core.Events.ContentCancelEventArgs> _coreContentFloating;
		private event EventHandler<Core.Events.ContentEventArgs> _coreContentFloated;
		private event EventHandler<Core.Events.ContentCancelEventArgs> _coreContentDocking;
		private event EventHandler<Core.Events.ContentEventArgs> _coreContentDocked;

		/// <summary>Initializes static members of the <see cref="DockingManager"/> class.</summary>
		static DockingManager()
		{
			FocusableProperty.OverrideDefaultValue<DockingManager>(false);
		}

		/// <summary>Initializes a new instance of the <see cref="DockingManager"/> class.</summary>
		public DockingManager()
		{
			IsVirtualizingDocument = true;
			IsVirtualizingAnchorable = true;
			Layout = new LayoutRoot { RootPanel = new LayoutPanel(new LayoutDocumentPaneGroup(new LayoutDocumentPane())) };
			AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
			AddHandler(PointerReleasedEvent, (s, e) => CommandManager.InvalidateRequerySuggested(), RoutingStrategies.Bubble, handledEventsToo: true);
		}

		/// <summary>Gets the <see cref="ILayoutEngine"/> used by this manager for layout tree operations.</summary>
		public virtual ILayoutEngine LayoutEngine => _layoutEngine;

		/// <summary>Gets or sets a value indicating whether document source binding is suspended during deserialization.</summary>
		public bool SuspendDocumentsSourceBinding { get; set; }

		/// <summary>Gets or sets a value indicating whether anchorable source binding is suspended during deserialization.</summary>
		public bool SuspendAnchorablesSourceBinding { get; set; }

		/// <inheritdoc/>
		Core.Serialization.ISerializableLayoutRoot Core.Serialization.ISerializableDockingManager.Layout
		{
			get => Layout;
			set => Layout = (LayoutRoot)value;
		}

		/// <inheritdoc/>
		bool Core.Serialization.ISerializableDockingManager.SuspendDocumentsSourceBinding
		{
			get => SuspendDocumentsSourceBinding;
			set => SuspendDocumentsSourceBinding = value;
		}

		/// <inheritdoc/>
		bool Core.Serialization.ISerializableDockingManager.SuspendAnchorablesSourceBinding
		{
			get => SuspendAnchorablesSourceBinding;
			set => SuspendAnchorablesSourceBinding = value;
		}

		/// <inheritdoc/>
		Core.Serialization.ILayoutDtoMapper Core.Serialization.ISerializableDockingManager.DtoMapper => _dtoMapper;

		/// <inheritdoc/>
		event EventHandler<Core.Events.DocumentCancelEventArgs> Core.IDockingManager.DocumentClosing
		{
			add => _coreDocumentClosing += value;
			remove => _coreDocumentClosing -= value;
		}

		/// <inheritdoc/>
		event EventHandler<Core.Events.DocumentEventArgs> Core.IDockingManager.DocumentClosed
		{
			add => _coreDocumentClosed += value;
			remove => _coreDocumentClosed -= value;
		}

		/// <inheritdoc/>
		event EventHandler<Core.Events.AnchorableCancelEventArgs> Core.IDockingManager.AnchorableClosing
		{
			add => _coreAnchorableClosing += value;
			remove => _coreAnchorableClosing -= value;
		}

		/// <inheritdoc/>
		event EventHandler<Core.Events.AnchorableEventArgs> Core.IDockingManager.AnchorableClosed
		{
			add => _coreAnchorableClosed += value;
			remove => _coreAnchorableClosed -= value;
		}

		/// <inheritdoc/>
		event EventHandler<Core.Events.AnchorableCancelEventArgs> Core.IDockingManager.AnchorableHiding
		{
			add => _coreAnchorableHiding += value;
			remove => _coreAnchorableHiding -= value;
		}

		/// <inheritdoc/>
		event EventHandler<Core.Events.AnchorableEventArgs> Core.IDockingManager.AnchorableHidden
		{
			add => _coreAnchorableHidden += value;
			remove => _coreAnchorableHidden -= value;
		}

		/// <inheritdoc/>
		event EventHandler<Core.Events.ContentCancelEventArgs> Core.IDockingManager.ContentFloating
		{
			add => _coreContentFloating += value;
			remove => _coreContentFloating -= value;
		}

		/// <inheritdoc/>
		event EventHandler<Core.Events.ContentEventArgs> Core.IDockingManager.ContentFloated
		{
			add => _coreContentFloated += value;
			remove => _coreContentFloated -= value;
		}

		/// <inheritdoc/>
		event EventHandler<Core.Events.ContentCancelEventArgs> Core.IDockingManager.ContentDocking
		{
			add => _coreContentDocking += value;
			remove => _coreContentDocking -= value;
		}

		/// <inheritdoc/>
		event EventHandler<Core.Events.ContentEventArgs> Core.IDockingManager.ContentDocked
		{
			add => _coreContentDocked += value;
			remove => _coreContentDocked -= value;
		}

		/// <summary>Event fired when <see cref="Layout"/> changes.</summary>
		public event EventHandler LayoutChanged;

		/// <summary>Event fired when <see cref="Layout"/> is about to be changed.</summary>
		public event EventHandler LayoutChanging;

		/// <summary>Event fired when a document is about to be closed. Subscribers can cancel the operation.</summary>
		public event EventHandler<DocumentClosingEventArgs> DocumentClosing;

		/// <summary>Event fired after a document is closed.</summary>
		public event EventHandler<DocumentClosedEventArgs> DocumentClosed;

		/// <summary>Event fired when an anchorable is about to be closed. Subscribers can cancel the operation.</summary>
		public event EventHandler<AnchorableClosingEventArgs> AnchorableClosing;

		/// <summary>Event fired after an anchorable is closed.</summary>
		public event EventHandler<AnchorableClosedEventArgs> AnchorableClosed;

		/// <summary>Event fired when an anchorable is about to be hidden. Subscribers can cancel the operation.</summary>
		public event EventHandler<AnchorableHidingEventArgs> AnchorableHiding;

		/// <summary>Event fired after an anchorable is hidden.</summary>
		public event EventHandler<AnchorableHiddenEventArgs> AnchorableHidden;

		/// <summary>Event raised when <see cref="ActiveContent"/> changes.</summary>
		public event EventHandler ActiveContentChanged;

		/// <summary>Event raised when a floating window control has been created.</summary>
		public event EventHandler<LayoutFloatingWindowControlCreatedEventArgs> LayoutFloatingWindowControlCreated;

		/// <summary>Event raised when a floating window control has been closed.</summary>
		public event EventHandler<LayoutFloatingWindowControlClosedEventArgs> LayoutFloatingWindowControlClosed;

		/// <summary>Event fired before content is floated. Subscribers can cancel the operation.</summary>
		public event EventHandler<ContentFloatingEventArgs> ContentFloating;

		/// <summary>Event fired after content has been floated.</summary>
		public event EventHandler<ContentFloatedEventArgs> ContentFloated;

		/// <summary>Event fired before content is docked. Subscribers can cancel the operation.</summary>
		public event EventHandler<ContentDockingEventArgs> ContentDocking;

		/// <summary>Event fired after content has been docked.</summary>
		public event EventHandler<ContentDockedEventArgs> ContentDocked;

		#region Properties

		/// <summary><see cref="Layout"/> property.</summary>
		public static readonly StyledProperty<LayoutRoot> LayoutProperty =
			AvaloniaProperty.Register<DockingManager, LayoutRoot>(nameof(Layout), coerce: CoerceLayoutValue);

		/// <summary>Gets or sets the layout root of the layout tree managed by this manager.</summary>
		[Content]
		public LayoutRoot Layout
		{
			get => GetValue(LayoutProperty);
			set => SetValue(LayoutProperty, value);
		}

		private static LayoutRoot CoerceLayoutValue(AvaloniaObject d, LayoutRoot value)
		{
			if (value == null) return new LayoutRoot { RootPanel = new LayoutPanel(new LayoutDocumentPaneGroup(new LayoutDocumentPane())) };
			if (d is DockingManager manager && manager.Layout != value) manager.OnLayoutChanging(value);
			return value;
		}

		/// <summary><see cref="DockLayout"/> property.</summary>
		public static readonly StyledProperty<Core.IRootDock> DockLayoutProperty =
			AvaloniaProperty.Register<DockingManager, Core.IRootDock>(nameof(DockLayout));

		/// <summary>
		/// Gets or sets the MVVM layout model. When set, the docking manager keeps this view model tree in sync
		/// with its layout. When <see langword="null"/>, the manager works in classic (v4) mode.
		/// </summary>
		public Core.IRootDock DockLayout
		{
			get => GetValue(DockLayoutProperty);
			set => SetValue(DockLayoutProperty, value);
		}

		/// <summary><see cref="LayoutUpdateStrategy"/> property.</summary>
		public static readonly StyledProperty<ILayoutUpdateStrategy> LayoutUpdateStrategyProperty =
			AvaloniaProperty.Register<DockingManager, ILayoutUpdateStrategy>(nameof(LayoutUpdateStrategy));

		/// <summary>Gets or sets the strategy asked to position new anchorables and documents in the layout.</summary>
		public ILayoutUpdateStrategy LayoutUpdateStrategy
		{
			get => GetValue(LayoutUpdateStrategyProperty);
			set => SetValue(LayoutUpdateStrategyProperty, value);
		}

		/// <summary><see cref="DocumentPaneTemplate"/> property.</summary>
		public static readonly StyledProperty<IControlTemplate> DocumentPaneTemplateProperty =
			AvaloniaProperty.Register<DockingManager, IControlTemplate>(nameof(DocumentPaneTemplate));

		/// <summary>Gets or sets the template of the <see cref="LayoutDocumentPaneControl"/>s.</summary>
		public IControlTemplate DocumentPaneTemplate
		{
			get => GetValue(DocumentPaneTemplateProperty);
			set => SetValue(DocumentPaneTemplateProperty, value);
		}

		/// <summary><see cref="AnchorablePaneTemplate"/> property.</summary>
		public static readonly StyledProperty<IControlTemplate> AnchorablePaneTemplateProperty =
			AvaloniaProperty.Register<DockingManager, IControlTemplate>(nameof(AnchorablePaneTemplate));

		/// <summary>Gets or sets the template of the <see cref="LayoutAnchorablePaneControl"/>s.</summary>
		public IControlTemplate AnchorablePaneTemplate
		{
			get => GetValue(AnchorablePaneTemplateProperty);
			set => SetValue(AnchorablePaneTemplateProperty, value);
		}

		/// <summary><see cref="AnchorSideTemplate"/> property.</summary>
		public static readonly StyledProperty<IControlTemplate> AnchorSideTemplateProperty =
			AvaloniaProperty.Register<DockingManager, IControlTemplate>(nameof(AnchorSideTemplate));

		/// <summary>Gets or sets the template of the <see cref="LayoutAnchorSideControl"/>s.</summary>
		public IControlTemplate AnchorSideTemplate
		{
			get => GetValue(AnchorSideTemplateProperty);
			set => SetValue(AnchorSideTemplateProperty, value);
		}

		/// <summary><see cref="AnchorGroupTemplate"/> property.</summary>
		public static readonly StyledProperty<IControlTemplate> AnchorGroupTemplateProperty =
			AvaloniaProperty.Register<DockingManager, IControlTemplate>(nameof(AnchorGroupTemplate));

		/// <summary>Gets or sets the template of the <see cref="LayoutAnchorGroupControl"/>s.</summary>
		public IControlTemplate AnchorGroupTemplate
		{
			get => GetValue(AnchorGroupTemplateProperty);
			set => SetValue(AnchorGroupTemplateProperty, value);
		}

		/// <summary><see cref="AnchorTemplate"/> property.</summary>
		public static readonly StyledProperty<IControlTemplate> AnchorTemplateProperty =
			AvaloniaProperty.Register<DockingManager, IControlTemplate>(nameof(AnchorTemplate));

		/// <summary>Gets or sets the template of the <see cref="LayoutAnchorControl"/>s.</summary>
		public IControlTemplate AnchorTemplate
		{
			get => GetValue(AnchorTemplateProperty);
			set => SetValue(AnchorTemplateProperty, value);
		}

		/// <summary><see cref="DocumentPaneControlStyle"/> property.</summary>
		public static readonly StyledProperty<ControlTheme> DocumentPaneControlStyleProperty =
			AvaloniaProperty.Register<DockingManager, ControlTheme>(nameof(DocumentPaneControlStyle));

		/// <summary>Gets or sets the theme of the <see cref="LayoutDocumentPaneControl"/>s.</summary>
		public ControlTheme DocumentPaneControlStyle
		{
			get => GetValue(DocumentPaneControlStyleProperty);
			set => SetValue(DocumentPaneControlStyleProperty, value);
		}

		/// <summary><see cref="AnchorablePaneControlStyle"/> property.</summary>
		public static readonly StyledProperty<ControlTheme> AnchorablePaneControlStyleProperty =
			AvaloniaProperty.Register<DockingManager, ControlTheme>(nameof(AnchorablePaneControlStyle));

		/// <summary>Gets or sets the theme of the <see cref="LayoutAnchorablePaneControl"/>s.</summary>
		public ControlTheme AnchorablePaneControlStyle
		{
			get => GetValue(AnchorablePaneControlStyleProperty);
			set => SetValue(AnchorablePaneControlStyleProperty, value);
		}

		/// <summary><see cref="DocumentHeaderTemplate"/> property.</summary>
		public static readonly StyledProperty<IDataTemplate> DocumentHeaderTemplateProperty =
			AvaloniaProperty.Register<DockingManager, IDataTemplate>(nameof(DocumentHeaderTemplate));

		/// <summary>Gets or sets the template of the header of a document tab (bound to the <see cref="LayoutContent"/>).</summary>
		public IDataTemplate DocumentHeaderTemplate
		{
			get => GetValue(DocumentHeaderTemplateProperty);
			set => SetValue(DocumentHeaderTemplateProperty, value);
		}

		/// <summary><see cref="DocumentHeaderTemplateSelector"/> property.</summary>
		public static readonly StyledProperty<DataTemplateSelector> DocumentHeaderTemplateSelectorProperty =
			AvaloniaProperty.Register<DockingManager, DataTemplateSelector>(nameof(DocumentHeaderTemplateSelector));

		/// <summary>Gets or sets the selector of the template of the header of a document tab.</summary>
		public DataTemplateSelector DocumentHeaderTemplateSelector
		{
			get => GetValue(DocumentHeaderTemplateSelectorProperty);
			set => SetValue(DocumentHeaderTemplateSelectorProperty, value);
		}

		/// <summary><see cref="DocumentTitleTemplate"/> property.</summary>
		public static readonly StyledProperty<IDataTemplate> DocumentTitleTemplateProperty =
			AvaloniaProperty.Register<DockingManager, IDataTemplate>(nameof(DocumentTitleTemplate));

		/// <summary>Gets or sets the template of the title of a floating document window.</summary>
		public IDataTemplate DocumentTitleTemplate
		{
			get => GetValue(DocumentTitleTemplateProperty);
			set => SetValue(DocumentTitleTemplateProperty, value);
		}

		/// <summary><see cref="DocumentTitleTemplateSelector"/> property.</summary>
		public static readonly StyledProperty<DataTemplateSelector> DocumentTitleTemplateSelectorProperty =
			AvaloniaProperty.Register<DockingManager, DataTemplateSelector>(nameof(DocumentTitleTemplateSelector));

		/// <summary>Gets or sets the selector of the template of the title of a floating document window.</summary>
		public DataTemplateSelector DocumentTitleTemplateSelector
		{
			get => GetValue(DocumentTitleTemplateSelectorProperty);
			set => SetValue(DocumentTitleTemplateSelectorProperty, value);
		}

		/// <summary><see cref="AnchorableTitleTemplate"/> property.</summary>
		public static readonly StyledProperty<IDataTemplate> AnchorableTitleTemplateProperty =
			AvaloniaProperty.Register<DockingManager, IDataTemplate>(nameof(AnchorableTitleTemplate));

		/// <summary>Gets or sets the template of the title of an anchorable.</summary>
		public IDataTemplate AnchorableTitleTemplate
		{
			get => GetValue(AnchorableTitleTemplateProperty);
			set => SetValue(AnchorableTitleTemplateProperty, value);
		}

		/// <summary><see cref="AnchorableTitleTemplateSelector"/> property.</summary>
		public static readonly StyledProperty<DataTemplateSelector> AnchorableTitleTemplateSelectorProperty =
			AvaloniaProperty.Register<DockingManager, DataTemplateSelector>(nameof(AnchorableTitleTemplateSelector));

		/// <summary>Gets or sets the selector of the template of the title of an anchorable.</summary>
		public DataTemplateSelector AnchorableTitleTemplateSelector
		{
			get => GetValue(AnchorableTitleTemplateSelectorProperty);
			set => SetValue(AnchorableTitleTemplateSelectorProperty, value);
		}

		/// <summary><see cref="AnchorableHeaderTemplate"/> property.</summary>
		public static readonly StyledProperty<IDataTemplate> AnchorableHeaderTemplateProperty =
			AvaloniaProperty.Register<DockingManager, IDataTemplate>(nameof(AnchorableHeaderTemplate));

		/// <summary>Gets or sets the template of the header of an anchorable tab and of an anchor.</summary>
		public IDataTemplate AnchorableHeaderTemplate
		{
			get => GetValue(AnchorableHeaderTemplateProperty);
			set => SetValue(AnchorableHeaderTemplateProperty, value);
		}

		/// <summary><see cref="AnchorableHeaderTemplateSelector"/> property.</summary>
		public static readonly StyledProperty<DataTemplateSelector> AnchorableHeaderTemplateSelectorProperty =
			AvaloniaProperty.Register<DockingManager, DataTemplateSelector>(nameof(AnchorableHeaderTemplateSelector));

		/// <summary>Gets or sets the selector of the template of the header of an anchorable tab.</summary>
		public DataTemplateSelector AnchorableHeaderTemplateSelector
		{
			get => GetValue(AnchorableHeaderTemplateSelectorProperty);
			set => SetValue(AnchorableHeaderTemplateSelectorProperty, value);
		}

		/// <summary><see cref="DocumentPaneMenuItemHeaderTemplate"/> property.</summary>
		public static readonly StyledProperty<IDataTemplate> DocumentPaneMenuItemHeaderTemplateProperty =
			AvaloniaProperty.Register<DockingManager, IDataTemplate>(nameof(DocumentPaneMenuItemHeaderTemplate));

		/// <summary>Gets or sets the template of the entries of the document list menu of a document pane.</summary>
		public IDataTemplate DocumentPaneMenuItemHeaderTemplate
		{
			get => GetValue(DocumentPaneMenuItemHeaderTemplateProperty);
			set => SetValue(DocumentPaneMenuItemHeaderTemplateProperty, value);
		}

		/// <summary><see cref="DocumentPaneMenuItemHeaderTemplateSelector"/> property.</summary>
		public static readonly StyledProperty<DataTemplateSelector> DocumentPaneMenuItemHeaderTemplateSelectorProperty =
			AvaloniaProperty.Register<DockingManager, DataTemplateSelector>(nameof(DocumentPaneMenuItemHeaderTemplateSelector));

		/// <summary>Gets or sets the selector of the template of the entries of the document list menu.</summary>
		public DataTemplateSelector DocumentPaneMenuItemHeaderTemplateSelector
		{
			get => GetValue(DocumentPaneMenuItemHeaderTemplateSelectorProperty);
			set => SetValue(DocumentPaneMenuItemHeaderTemplateSelectorProperty, value);
		}

		/// <summary><see cref="IconContentTemplate"/> property.</summary>
		public static readonly StyledProperty<IDataTemplate> IconContentTemplateProperty =
			AvaloniaProperty.Register<DockingManager, IDataTemplate>(nameof(IconContentTemplate));

		/// <summary>Gets or sets the template of the icon of a content in menus.</summary>
		public IDataTemplate IconContentTemplate
		{
			get => GetValue(IconContentTemplateProperty);
			set => SetValue(IconContentTemplateProperty, value);
		}

		/// <summary><see cref="IconContentTemplateSelector"/> property.</summary>
		public static readonly StyledProperty<DataTemplateSelector> IconContentTemplateSelectorProperty =
			AvaloniaProperty.Register<DockingManager, DataTemplateSelector>(nameof(IconContentTemplateSelector));

		/// <summary>Gets or sets the selector of the template of the icon of a content.</summary>
		public DataTemplateSelector IconContentTemplateSelector
		{
			get => GetValue(IconContentTemplateSelectorProperty);
			set => SetValue(IconContentTemplateSelectorProperty, value);
		}

		/// <summary><see cref="LayoutItemTemplate"/> property.</summary>
		public static readonly StyledProperty<IDataTemplate> LayoutItemTemplateProperty =
			AvaloniaProperty.Register<DockingManager, IDataTemplate>(nameof(LayoutItemTemplate));

		/// <summary>
		/// Gets or sets the template used for the content of every document and anchorable. When neither this nor
		/// <see cref="LayoutItemTemplateSelector"/> is set, the content picks a template from the data templates
		/// in scope, as any Avalonia content does.
		/// </summary>
		public IDataTemplate LayoutItemTemplate
		{
			get => GetValue(LayoutItemTemplateProperty);
			set => SetValue(LayoutItemTemplateProperty, value);
		}

		/// <summary><see cref="LayoutItemTemplateSelector"/> property.</summary>
		public static readonly StyledProperty<DataTemplateSelector> LayoutItemTemplateSelectorProperty =
			AvaloniaProperty.Register<DockingManager, DataTemplateSelector>(nameof(LayoutItemTemplateSelector));

		/// <summary>Gets or sets the selector of the template used for the content of documents and anchorables.</summary>
		public DataTemplateSelector LayoutItemTemplateSelector
		{
			get => GetValue(LayoutItemTemplateSelectorProperty);
			set => SetValue(LayoutItemTemplateSelectorProperty, value);
		}

		/// <summary><see cref="LayoutRootPanel"/> property.</summary>
		public static readonly StyledProperty<LayoutPanelControl> LayoutRootPanelProperty =
			AvaloniaProperty.Register<DockingManager, LayoutPanelControl>(nameof(LayoutRootPanel));

		/// <summary>Gets or sets the control of the root panel of the layout.</summary>
		public LayoutPanelControl LayoutRootPanel
		{
			get => GetValue(LayoutRootPanelProperty);
			set => SetValue(LayoutRootPanelProperty, value);
		}

		/// <summary><see cref="RightSidePanel"/> property.</summary>
		public static readonly StyledProperty<LayoutAnchorSideControl> RightSidePanelProperty =
			AvaloniaProperty.Register<DockingManager, LayoutAnchorSideControl>(nameof(RightSidePanel));

		/// <summary>Gets or sets the control of the right side bar.</summary>
		public LayoutAnchorSideControl RightSidePanel
		{
			get => GetValue(RightSidePanelProperty);
			set => SetValue(RightSidePanelProperty, value);
		}

		/// <summary><see cref="LeftSidePanel"/> property.</summary>
		public static readonly StyledProperty<LayoutAnchorSideControl> LeftSidePanelProperty =
			AvaloniaProperty.Register<DockingManager, LayoutAnchorSideControl>(nameof(LeftSidePanel));

		/// <summary>Gets or sets the control of the left side bar.</summary>
		public LayoutAnchorSideControl LeftSidePanel
		{
			get => GetValue(LeftSidePanelProperty);
			set => SetValue(LeftSidePanelProperty, value);
		}

		/// <summary><see cref="TopSidePanel"/> property.</summary>
		public static readonly StyledProperty<LayoutAnchorSideControl> TopSidePanelProperty =
			AvaloniaProperty.Register<DockingManager, LayoutAnchorSideControl>(nameof(TopSidePanel));

		/// <summary>Gets or sets the control of the top side bar.</summary>
		public LayoutAnchorSideControl TopSidePanel
		{
			get => GetValue(TopSidePanelProperty);
			set => SetValue(TopSidePanelProperty, value);
		}

		/// <summary><see cref="BottomSidePanel"/> property.</summary>
		public static readonly StyledProperty<LayoutAnchorSideControl> BottomSidePanelProperty =
			AvaloniaProperty.Register<DockingManager, LayoutAnchorSideControl>(nameof(BottomSidePanel));

		/// <summary>Gets or sets the control of the bottom side bar.</summary>
		public LayoutAnchorSideControl BottomSidePanel
		{
			get => GetValue(BottomSidePanelProperty);
			set => SetValue(BottomSidePanelProperty, value);
		}

		/// <summary><see cref="AutoHideWindow"/> property.</summary>
		public static readonly DirectProperty<DockingManager, LayoutAutoHideWindowControl> AutoHideWindowProperty =
			AvaloniaProperty.RegisterDirect<DockingManager, LayoutAutoHideWindowControl>(nameof(AutoHideWindow), o => o.AutoHideWindow);

		private LayoutAutoHideWindowControl _autoHideWindow;

		/// <summary>Gets the flyout that shows auto-hidden anchorables.</summary>
		public LayoutAutoHideWindowControl AutoHideWindow
		{
			get => _autoHideWindow;
			private set => SetAndRaise(AutoHideWindowProperty, ref _autoHideWindow, value);
		}

		/// <summary><see cref="AutoHideDelay"/> property.</summary>
		public static readonly StyledProperty<int> AutoHideDelayProperty =
			AvaloniaProperty.Register<DockingManager, int>(nameof(AutoHideDelay), 500);

		/// <summary>
		/// Gets or sets the time in milliseconds after which the auto-hide flyout closes once it is no longer
		/// active and the pointer has left it.
		/// </summary>
		public int AutoHideDelay
		{
			get => GetValue(AutoHideDelayProperty);
			set => SetValue(AutoHideDelayProperty, value);
		}

		/// <summary>
		/// Gets or sets a value indicating whether auto-hidden anchorables are presented in the classic auto-hide
		/// flyout. Derived managers that present them differently set this to <see langword="false"/>.
		/// </summary>
		public bool SupportsAutoHideFlyout { get; protected set; } = true;

		/// <summary>Gets all <see cref="LayoutFloatingWindowControl"/> instances managed by this manager.</summary>
		public IEnumerable<LayoutFloatingWindowControl> FloatingWindows => _fwList;

		/// <summary><see cref="DocumentsSource"/> property.</summary>
		public static readonly StyledProperty<IEnumerable> DocumentsSourceProperty =
			AvaloniaProperty.Register<DockingManager, IEnumerable>(nameof(DocumentsSource));

		/// <summary>Gets or sets the view models shown as documents.</summary>
		public IEnumerable DocumentsSource
		{
			get => GetValue(DocumentsSourceProperty);
			set => SetValue(DocumentsSourceProperty, value);
		}

		/// <summary><see cref="AnchorablesSource"/> property.</summary>
		public static readonly StyledProperty<IEnumerable> AnchorablesSourceProperty =
			AvaloniaProperty.Register<DockingManager, IEnumerable>(nameof(AnchorablesSource));

		/// <summary>Gets or sets the view models shown as anchorables (tool windows).</summary>
		public IEnumerable AnchorablesSource
		{
			get => GetValue(AnchorablesSourceProperty);
			set => SetValue(AnchorablesSourceProperty, value);
		}

		/// <summary><see cref="DocumentContextMenu"/> property.</summary>
		public static readonly StyledProperty<ContextMenu> DocumentContextMenuProperty =
			AvaloniaProperty.Register<DockingManager, ContextMenu>(nameof(DocumentContextMenu));

		/// <summary>Gets or sets the context menu of documents (its data context is the <see cref="LayoutItem"/>).</summary>
		public ContextMenu DocumentContextMenu
		{
			get => GetValue(DocumentContextMenuProperty);
			set => SetValue(DocumentContextMenuProperty, value);
		}

		/// <summary><see cref="AnchorableContextMenu"/> property.</summary>
		public static readonly StyledProperty<ContextMenu> AnchorableContextMenuProperty =
			AvaloniaProperty.Register<DockingManager, ContextMenu>(nameof(AnchorableContextMenu));

		/// <summary>Gets or sets the context menu of anchorables (its data context is the <see cref="LayoutItem"/>).</summary>
		public ContextMenu AnchorableContextMenu
		{
			get => GetValue(AnchorableContextMenuProperty);
			set => SetValue(AnchorableContextMenuProperty, value);
		}

		/// <summary><see cref="ActiveContent"/> property.</summary>
		public static readonly StyledProperty<object> ActiveContentProperty =
			AvaloniaProperty.Register<DockingManager, object>(nameof(ActiveContent), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

		/// <summary>Gets or sets the content of the active document or anchorable.</summary>
		public object ActiveContent
		{
			get => GetValue(ActiveContentProperty);
			set => SetValue(ActiveContentProperty, value);
		}

		/// <summary><see cref="AllowAnchorDoubleClickDock"/> property.</summary>
		public static readonly StyledProperty<bool> AllowAnchorDoubleClickDockProperty =
			AvaloniaProperty.Register<DockingManager, bool>(nameof(AllowAnchorDoubleClickDock), true);

		/// <summary>Gets or sets a value indicating whether double-clicking an anchor docks its anchorable.</summary>
		public bool AllowAnchorDoubleClickDock
		{
			get => GetValue(AllowAnchorDoubleClickDockProperty);
			set => SetValue(AllowAnchorDoubleClickDockProperty, value);
		}

		/// <summary><see cref="AllowAnchorRightClickContextMenu"/> property.</summary>
		public static readonly StyledProperty<bool> AllowAnchorRightClickContextMenuProperty =
			AvaloniaProperty.Register<DockingManager, bool>(nameof(AllowAnchorRightClickContextMenu), true);

		/// <summary>Gets or sets a value indicating whether right-clicking an anchor opens the anchorable context menu.</summary>
		public bool AllowAnchorRightClickContextMenu
		{
			get => GetValue(AllowAnchorRightClickContextMenuProperty);
			set => SetValue(AllowAnchorRightClickContextMenuProperty, value);
		}

		/// <summary><see cref="DockTheme"/> property.</summary>
		public static readonly StyledProperty<Theme> DockThemeProperty =
			AvaloniaProperty.Register<DockingManager, Theme>(nameof(DockTheme));

		/// <summary>
		/// Gets or sets the AvalonDock theme - <c>Theme</c> in the WPF library. It has another name here because
		/// <see cref="StyledElement.Theme"/> is the control theme of the manager itself in Avalonia.
		/// </summary>
		public Theme DockTheme
		{
			get => GetValue(DockThemeProperty);
			set => SetValue(DockThemeProperty, value);
		}

		/// <summary><see cref="GridSplitterWidth"/> property.</summary>
		public static readonly StyledProperty<double> GridSplitterWidthProperty =
			AvaloniaProperty.Register<DockingManager, double>(nameof(GridSplitterWidth), 6.0);

		/// <summary>Gets or sets the width of the splitters between columns.</summary>
		public double GridSplitterWidth
		{
			get => GetValue(GridSplitterWidthProperty);
			set => SetValue(GridSplitterWidthProperty, value);
		}

		/// <summary><see cref="GridSplitterHeight"/> property.</summary>
		public static readonly StyledProperty<double> GridSplitterHeightProperty =
			AvaloniaProperty.Register<DockingManager, double>(nameof(GridSplitterHeight), 6.0);

		/// <summary>Gets or sets the height of the splitters between rows.</summary>
		public double GridSplitterHeight
		{
			get => GetValue(GridSplitterHeightProperty);
			set => SetValue(GridSplitterHeightProperty, value);
		}

		/// <summary><see cref="GridSplitterVerticalStyle"/> property.</summary>
		public static readonly StyledProperty<ControlTheme> GridSplitterVerticalStyleProperty =
			AvaloniaProperty.Register<DockingManager, ControlTheme>(nameof(GridSplitterVerticalStyle));

		/// <summary>Gets or sets the theme of the splitters between columns.</summary>
		public ControlTheme GridSplitterVerticalStyle
		{
			get => GetValue(GridSplitterVerticalStyleProperty);
			set => SetValue(GridSplitterVerticalStyleProperty, value);
		}

		/// <summary><see cref="GridSplitterHorizontalStyle"/> property.</summary>
		public static readonly StyledProperty<ControlTheme> GridSplitterHorizontalStyleProperty =
			AvaloniaProperty.Register<DockingManager, ControlTheme>(nameof(GridSplitterHorizontalStyle));

		/// <summary>Gets or sets the theme of the splitters between rows.</summary>
		public ControlTheme GridSplitterHorizontalStyle
		{
			get => GetValue(GridSplitterHorizontalStyleProperty);
			set => SetValue(GridSplitterHorizontalStyleProperty, value);
		}

		/// <summary><see cref="LayoutItemContainerStyle"/> property.</summary>
		public static readonly StyledProperty<ControlTheme> LayoutItemContainerStyleProperty =
			AvaloniaProperty.Register<DockingManager, ControlTheme>(nameof(LayoutItemContainerStyle));

		/// <summary>
		/// Gets or sets the theme applied to every <see cref="LayoutItem"/>; its setters bind the item to its view
		/// model, e.g. <c>&lt;Setter Property="Title" Value="{Binding Model.Title}"/&gt;</c>.
		/// </summary>
		public ControlTheme LayoutItemContainerStyle
		{
			get => GetValue(LayoutItemContainerStyleProperty);
			set => SetValue(LayoutItemContainerStyleProperty, value);
		}

		/// <summary><see cref="LayoutItemContainerStyleSelector"/> property.</summary>
		public static readonly StyledProperty<StyleSelector> LayoutItemContainerStyleSelectorProperty =
			AvaloniaProperty.Register<DockingManager, StyleSelector>(nameof(LayoutItemContainerStyleSelector));

		/// <summary>Gets or sets the selector of the theme applied to each <see cref="LayoutItem"/>.</summary>
		public StyleSelector LayoutItemContainerStyleSelector
		{
			get => GetValue(LayoutItemContainerStyleSelectorProperty);
			set => SetValue(LayoutItemContainerStyleSelectorProperty, value);
		}

		/// <summary><see cref="ShowSystemMenu"/> property.</summary>
		public static readonly StyledProperty<bool> ShowSystemMenuProperty =
			AvaloniaProperty.Register<DockingManager, bool>(nameof(ShowSystemMenu), true);

		/// <summary>Gets or sets a value indicating whether floating windows show a system menu (kept for API parity).</summary>
		public bool ShowSystemMenu
		{
			get => GetValue(ShowSystemMenuProperty);
			set => SetValue(ShowSystemMenuProperty, value);
		}

		/// <summary><see cref="AllowMixedOrientation"/> property.</summary>
		public static readonly StyledProperty<bool> AllowMixedOrientationProperty =
			AvaloniaProperty.Register<DockingManager, bool>(nameof(AllowMixedOrientation));

		/// <summary>Gets or sets a value indicating whether document pane groups may mix orientations.</summary>
		public bool AllowMixedOrientation
		{
			get => GetValue(AllowMixedOrientationProperty);
			set => SetValue(AllowMixedOrientationProperty, value);
		}

		/// <summary>Gets or sets a value indicating whether document tabs create their content on demand (kept for API parity).</summary>
		public bool IsVirtualizingDocument { get; set; }

		/// <summary>Gets or sets a value indicating whether anchorable tabs create their content on demand (kept for API parity).</summary>
		public bool IsVirtualizingAnchorable { get; set; }

		/// <summary><see cref="IgnoreTabControlKeyBindings"/> property.</summary>
		public static readonly StyledProperty<bool> IgnoreTabControlKeyBindingsProperty =
			AvaloniaProperty.Register<DockingManager, bool>(nameof(IgnoreTabControlKeyBindings));

		/// <summary>Gets or sets a value indicating whether the key handling of the pane tab controls is bypassed.</summary>
		public bool IgnoreTabControlKeyBindings
		{
			get => GetValue(IgnoreTabControlKeyBindingsProperty);
			set => SetValue(IgnoreTabControlKeyBindingsProperty, value);
		}

		/// <summary><see cref="AutoWindowSizeWhenOpened"/> property.</summary>
		public static readonly StyledProperty<bool> AutoWindowSizeWhenOpenedProperty =
			AvaloniaProperty.Register<DockingManager, bool>(nameof(AutoWindowSizeWhenOpened));

		/// <summary>Gets or sets a value indicating whether floating windows size to their content when opened.</summary>
		public bool AutoWindowSizeWhenOpened
		{
			get => GetValue(AutoWindowSizeWhenOpenedProperty);
			set => SetValue(AutoWindowSizeWhenOpenedProperty, value);
		}

		/// <summary><see cref="AllowMovingFloatingWindowWithKeyboard"/> property.</summary>
		public static readonly StyledProperty<bool> AllowMovingFloatingWindowWithKeyboardProperty =
			AvaloniaProperty.Register<DockingManager, bool>(nameof(AllowMovingFloatingWindowWithKeyboard));

		/// <summary>Gets or sets a value indicating whether a floating window can be moved with the arrow keys.</summary>
		public bool AllowMovingFloatingWindowWithKeyboard
		{
			get => GetValue(AllowMovingFloatingWindowWithKeyboardProperty);
			set => SetValue(AllowMovingFloatingWindowWithKeyboardProperty, value);
		}

		/// <summary><see cref="AllowFloatingWindows"/> property.</summary>
		public static readonly StyledProperty<bool> AllowFloatingWindowsProperty =
			AvaloniaProperty.Register<DockingManager, bool>(nameof(AllowFloatingWindows), true);

		/// <summary>
		/// Gets or sets a value indicating whether content may be torn off into floating windows. Turning it off
		/// docks the content of every open floating window back into the layout.
		/// </summary>
		public bool AllowFloatingWindows
		{
			get => GetValue(AllowFloatingWindowsProperty);
			set => SetValue(AllowFloatingWindowsProperty, value);
		}

		/// <summary><see cref="AllowDetachedWindows"/> property.</summary>
		public static readonly StyledProperty<bool> AllowDetachedWindowsProperty =
			AvaloniaProperty.Register<DockingManager, bool>(nameof(AllowDetachedWindows), true);

		/// <summary>
		/// Gets or sets a value indicating whether anchorables may be moved into standalone top-level windows.
		/// Turning it off returns every detached anchorable to the layout.
		/// </summary>
		public bool AllowDetachedWindows
		{
			get => GetValue(AllowDetachedWindowsProperty);
			set => SetValue(AllowDetachedWindowsProperty, value);
		}

		/// <summary><see cref="ShowNavigator"/> property.</summary>
		public static readonly StyledProperty<bool> ShowNavigatorProperty =
			AvaloniaProperty.Register<DockingManager, bool>(nameof(ShowNavigator), true);

		/// <summary>Gets or sets a value indicating whether Ctrl+Tab shows the navigator window.</summary>
		public bool ShowNavigator
		{
			get => GetValue(ShowNavigatorProperty);
			set => SetValue(ShowNavigatorProperty, value);
		}

		/// <summary><see cref="OverlayWindowMode"/> property.</summary>
		public static readonly StyledProperty<OverlayWindowMode> OverlayWindowModeProperty =
			AvaloniaProperty.Register<DockingManager, OverlayWindowMode>(nameof(OverlayWindowMode));

		/// <summary>Gets or sets where the drop targets are shown while a floating window is dragged.</summary>
		public OverlayWindowMode OverlayWindowMode
		{
			get => GetValue(OverlayWindowModeProperty);
			set => SetValue(OverlayWindowModeProperty, value);
		}

		#endregion Properties

		/// <inheritdoc/>
		protected override Type StyleKeyOverride => typeof(DockingManager);

		/// <inheritdoc/>
		protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
		{
			base.OnPropertyChanged(change);

			if (change.Property == LayoutProperty)
				OnLayoutChanged(change.OldValue as LayoutRoot, change.NewValue as LayoutRoot);
			else if (change.Property == DockLayoutProperty)
				OnDockLayoutChanged(change.OldValue as Core.IRootDock, change.NewValue as Core.IRootDock);
			else if (change.Property == DocumentsSourceProperty)
			{
				DetachDocumentsSource(Layout, change.OldValue as IEnumerable);
				AttachDocumentsSource(Layout, change.NewValue as IEnumerable);
			}
			else if (change.Property == AnchorablesSourceProperty)
			{
				DetachAnchorablesSource(Layout, change.OldValue as IEnumerable);
				AttachAnchorablesSource(Layout, change.NewValue as IEnumerable);
			}
			else if (change.Property == ActiveContentProperty)
			{
				if (Layout != null && !_insideInternalSetActiveContent) InternalSetActiveContent(change.NewValue);
				ActiveContentChanged?.Invoke(this, EventArgs.Empty);
			}
			else if (change.Property == DockThemeProperty)
				OnThemeChanged(change.OldValue as Theme, change.NewValue as Theme);
			else if (change.Property == LayoutItemContainerStyleProperty || change.Property == LayoutItemContainerStyleSelectorProperty)
				AttachLayoutItems();
			else if (change.Property == LayoutItemTemplateProperty || change.Property == LayoutItemTemplateSelectorProperty)
				UpdateLayoutItemViewTemplates();
			else if (change.Property == AllowFloatingWindowsProperty)
			{
				if (!(bool)change.NewValue) DockAllFloatingWindows();
				CommandManager.InvalidateRequerySuggested();
			}
			else if (change.Property == AllowDetachedWindowsProperty)
			{
				if (!(bool)change.NewValue) ReattachAllDetachedAnchorables();
				CommandManager.InvalidateRequerySuggested();
			}
			else if (change.Property == LayoutRootPanelProperty || change.Property == LeftSidePanelProperty ||
					 change.Property == TopSidePanelProperty || change.Property == RightSidePanelProperty ||
					 change.Property == BottomSidePanelProperty)
			{
				// The panel controls are logical children of the manager, which is what lets them (and the
				// content they host) inherit DataContext and resources from it.
				if (change.OldValue is Control oldControl) LogicalChildren.Remove(oldControl);
				if (change.NewValue is Control newControl && newControl.Parent == null) LogicalChildren.Add(newControl);
			}
			else if (_fwList.Count > 0 &&
				(change.Property == DataContextProperty || change.Property == FlowDirectionProperty ||
				 change.Property == Avalonia.Controls.Documents.TextElement.FontFamilyProperty ||
				 change.Property == Avalonia.Controls.Documents.TextElement.FontSizeProperty ||
				 change.Property == Avalonia.Controls.Documents.TextElement.ForegroundProperty))
			{
				// Floating windows are top levels of their own and do not inherit from the manager.
				foreach (var floatingWindow in _fwList.Concat(_fwHiddenList).ToArray())
					floatingWindow.SyncInheritedProperties();
			}
		}

		#region Layout

		/// <summary>Called when <see cref="DockLayout"/> changes: creates or removes the <see cref="LayoutSyncBridge"/>.</summary>
		/// <param name="oldValue">The previous root dock.</param>
		/// <param name="newValue">The new root dock.</param>
		protected virtual void OnDockLayoutChanged(Core.IRootDock oldValue, Core.IRootDock newValue)
		{
			_syncBridge?.Detach();
			_syncBridge = null;

			if (newValue != null)
			{
				_syncBridge = new LayoutSyncBridge(this, newValue);
				_syncBridge.Attach();

				if (LayoutUpdateStrategy == null)
				{
					_installedAlignmentStrategy = new DockAlignmentStrategy(_syncBridge.ContentToSideMap);
					LayoutUpdateStrategy = _installedAlignmentStrategy;
				}
			}
			else
			{
				if (_installedAlignmentStrategy != null && LayoutUpdateStrategy == _installedAlignmentStrategy)
					LayoutUpdateStrategy = null;
				_installedAlignmentStrategy = null;
			}
		}

		/// <summary>Called when <see cref="Layout"/> changes.</summary>
		/// <param name="oldLayout">The previous layout.</param>
		/// <param name="newLayout">The new layout.</param>
		protected virtual void OnLayoutChanged(LayoutRoot oldLayout, LayoutRoot newLayout)
		{
			if (oldLayout != null)
			{
				oldLayout.PropertyChanged -= OnLayoutRootPropertyChanged;
				oldLayout.Updated -= OnLayoutRootUpdated;
				DiscardDetachedWindowsOfReplacedLayout();
			}

			foreach (var fwc in _fwList.ToArray())
			{
				fwc.KeepContentVisibleOnClose = true;
				fwc.InternalClose();
			}

			_fwList.Clear();

			foreach (var fwc in _fwHiddenList.ToArray())
				fwc.InternalClose();

			_fwHiddenList.Clear();
			DetachDocumentsSource(oldLayout, DocumentsSource);
			DetachAnchorablesSource(oldLayout, AnchorablesSource);

			if (oldLayout != null && oldLayout.Manager == this)
				oldLayout.Manager = null;

			DetachLayoutItems(oldLayout);

			if (newLayout != null) newLayout.Manager = this;

			AttachLayoutItems();
			AttachDocumentsSource(newLayout, DocumentsSource);
			AttachAnchorablesSource(newLayout, AnchorablesSource);

			// A layout saved while floating was allowed still carries its floating windows. Dock them before any
			// window is created for them, so loading a layout cannot bring the feature back.
			if (!AllowFloatingWindows) DockAllFloatingWindows();

			if (_isLoaded && newLayout != null)
			{
				CreateRootControls();

				foreach (var fw in newLayout.FloatingWindows.Where(x => x.IsValid).ToArray())
					CreateUIElementForModel(fw);
			}

			if (newLayout != null)
			{
				newLayout.PropertyChanged += OnLayoutRootPropertyChanged;
				newLayout.Updated += OnLayoutRootUpdated;
			}

			LayoutChanged?.Invoke(this, EventArgs.Empty);
			CommandManager.InvalidateRequerySuggested();

			RestoreDetachedAnchorables(newLayout);
		}

		private void CreateRootControls()
		{
			LayoutRootPanel = CreateUIElementForModel(Layout.RootPanel) as LayoutPanelControl;
			LeftSidePanel = CreateUIElementForModel(Layout.LeftSide) as LayoutAnchorSideControl;
			TopSidePanel = CreateUIElementForModel(Layout.TopSide) as LayoutAnchorSideControl;
			RightSidePanel = CreateUIElementForModel(Layout.RightSide) as LayoutAnchorSideControl;
			BottomSidePanel = CreateUIElementForModel(Layout.BottomSide) as LayoutAnchorSideControl;
		}

		private void OnLayoutChanging(LayoutRoot newLayout) => LayoutChanging?.Invoke(this, EventArgs.Empty);

		private void OnLayoutRootPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				case nameof(LayoutRoot.RootPanel):
					if (_isLoaded)
						LayoutRootPanel = CreateUIElementForModel(Layout.RootPanel) as LayoutPanelControl;
					break;

				case nameof(LayoutRoot.ActiveContent):
					if (Layout.ActiveContent != null)
						FocusElementManager.SetFocusOnLastElement(Layout.ActiveContent);
					if (!_insideInternalSetActiveContent)
					{
						_insideInternalSetActiveContent = true;
						try
						{
							ActiveContent = Layout.ActiveContent?.Content;
						}
						finally
						{
							_insideInternalSetActiveContent = false;
						}
					}

					break;
			}
		}

		private static void OnLayoutRootUpdated(object sender, EventArgs e) => CommandManager.InvalidateRequerySuggested();

		private void InternalSetActiveContent(object contentObject)
		{
			// BugFix for first issue in #59
			var layoutContent = Layout.Descendents().OfType<LayoutContent>().FirstOrDefault(lc => lc == contentObject || lc.Content == contentObject);
			_insideInternalSetActiveContent = true;
			try
			{
				Layout.ActiveContent = layoutContent;
			}
			finally
			{
				_insideInternalSetActiveContent = false;
			}
		}

		#endregion Layout

		#region Lifetime

		/// <inheritdoc/>
		protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
		{
			base.OnApplyTemplate(e);
			_autohideArea = e.NameScope.Find<Control>("PART_AutoHideArea");
		}

		/// <inheritdoc/>
		protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
		{
			base.OnAttachedToVisualTree(e);
			EnsureDefaultTheme();
			EnsureDefaultContextMenus();
			OnManagerLoaded();
		}

		/// <inheritdoc/>
		protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
		{
			OnManagerUnloaded();
			base.OnDetachedFromVisualTree(e);
		}

		/// <summary>
		/// Picks up the context menus of the theme (resources <c>AvalonDock_DocumentContextMenu</c> and
		/// <c>AvalonDock_AnchorableContextMenu</c>) for the menu properties the application left unset.
		/// </summary>
		private void EnsureDefaultContextMenus()
		{
			if (DocumentContextMenu == null && this.TryFindResource("AvalonDock_DocumentContextMenu", out var documentMenu) && documentMenu is ContextMenu dm)
				SetCurrentValue(DocumentContextMenuProperty, dm);
			if (AnchorableContextMenu == null && this.TryFindResource("AvalonDock_AnchorableContextMenu", out var anchorableMenu) && anchorableMenu is ContextMenu am)
				SetCurrentValue(AnchorableContextMenuProperty, am);
		}

		/// <summary>
		/// Makes the default styles available when the application has not added an <see cref="AvalonDockTheme"/>
		/// itself - the counterpart of WPF finding <c>generic.xaml</c> on its own.
		/// </summary>
		private static void EnsureDefaultTheme()
		{
			var application = Application.Current;
			if (application == null) return;
			if (application.Styles.OfType<AvalonDockTheme>().Any()) return;
			application.Styles.Add(new AvalonDockTheme());
		}

		private void OnManagerLoaded()
		{
			if (_isLoaded) return;
			_isLoaded = true;

			// The screens are queried through the most recently attached manager that is still alive, so the
			// provider keeps working when the manager that set it up is gone.
			s_screenSource = new WeakReference<DockingManager>(this);
			ILayoutElementForFloatingWindowExtension.ScreenWorkingAreas ??= GetScreenWorkingAreasOfLastManager;

			if (Layout?.Manager == this)
				CreateRootControls();

			SetupAutoHideWindow();

			foreach (var fwc in _fwHiddenList.ToArray())
			{
				EnableFloatingWindowBindings(fwc);
				if (fwc.KeepContentVisibleOnClose)
				{
					ShowFloatingWindowWhenPossible(fwc);
					fwc.KeepContentVisibleOnClose = false;
				}

				_fwList.Add(fwc);
			}

			_fwHiddenList.Clear();

			// load floating windows not already loaded! (issue #59 & #254 & #426)
			if (Layout != null)
			{
				foreach (var fw in Layout.FloatingWindows.Where(fw => _fwList.All(fwc => fwc.Model != fw)).ToArray())
					CreateUIElementForModel(fw);
			}

			FocusElementManager.SetupFocusManagement(this);
		}

		private void OnManagerUnloaded()
		{
			if (!_isLoaded) return;
			_isLoaded = false;

			// Unloading is not necessarily the end - it also happens when the manager is switched away from,
			// e.g. between tabs. A standalone window left open would keep the view the rebuilt layout needs, so
			// hand the content back and close it. IsDetached is kept so the window is recreated when the manager
			// is loaded again.
			CloseDetachedWindows(returnToLayout: true, keepDetachedFlag: true);
			_autoHideWindowManager?.HideAutoWindow();

			foreach (var fw in _fwList.ToArray())
			{
				// Hide rather than close: the manager may come back (tab switch).
				if (fw.IsVisible)
				{
					fw.KeepContentVisibleOnClose = true;
					fw.Hide();
				}

				DisableFloatingWindowBindings(fw);
				_fwHiddenList.Add(fw);
			}

			_fwList.Clear();
			DestroyOverlayWindow();
			FocusElementManager.FinalizeFocusManagement(this);
		}

		private static void EnableFloatingWindowBindings(LayoutFloatingWindowControl fwc)
		{
			if (fwc is LayoutAnchorableFloatingWindowControl anchorableWindow) anchorableWindow.EnableBindings();
			else if (fwc is LayoutDocumentFloatingWindowControl documentWindow) documentWindow.EnableBindings();
		}

		private static void DisableFloatingWindowBindings(LayoutFloatingWindowControl fwc)
		{
			if (fwc is LayoutAnchorableFloatingWindowControl anchorableWindow) anchorableWindow.DisableBindings();
			else if (fwc is LayoutDocumentFloatingWindowControl documentWindow) documentWindow.DisableBindings();
		}

		private static WeakReference<DockingManager> s_screenSource;

		private static IReadOnlyList<PixelRect> GetScreenWorkingAreasOfLastManager()
			=> s_screenSource != null && s_screenSource.TryGetTarget(out var manager) ? manager.GetScreenWorkingAreas() : Array.Empty<PixelRect>();

		private IReadOnlyList<PixelRect> GetScreenWorkingAreas()
		{
			var screens = TopLevel.GetTopLevel(this)?.Screens;
			if (screens == null) return Array.Empty<PixelRect>();
			var primary = screens.Primary;
			return screens.All
				.OrderByDescending(s => ReferenceEquals(s, primary))
				.Select(s =>
				{
					var scaling = s.Scaling > 0 ? s.Scaling : 1.0;
					var area = s.WorkingArea;
					return new PixelRect((int)(area.X / scaling), (int)(area.Y / scaling), (int)(area.Width / scaling), (int)(area.Height / scaling));
				})
				.ToList();
		}

		/// <inheritdoc/>
		protected override void OnSizeChanged(SizeChangedEventArgs e)
		{
			base.OnSizeChanged(e);
			_areas = null;

			// Panels may be null if the layout has not been loaded yet.
			if (LayoutRootPanel == null || RightSidePanel == null || LeftSidePanel == null || TopSidePanel == null || BottomSidePanel == null)
				return;

			// Lets make sure this always remains non-negative to avoid crash in layout system
			var width = Math.Max(Bounds.Width - GridSplitterWidth - RightSidePanel.Bounds.Width - LeftSidePanel.Bounds.Width, 0);
			var height = Math.Max(Bounds.Height - GridSplitterHeight - TopSidePanel.Bounds.Height - BottomSidePanel.Bounds.Height, 0);
			LayoutRootPanel.AdjustFixedChildrenPanelSizes(new Size(width, height));
		}

		private void OnPreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Tab && e.KeyModifiers.HasFlag(KeyModifiers.Control) && CanShowNavigatorWindow && _navigatorWindow == null)
			{
				ShowNavigatorWindow();
				e.Handled = true;
			}
		}

		private bool CanShowNavigatorWindow => ShowNavigator && _layoutItems.Count > 0;

		private void ShowNavigatorWindow()
		{
			var owner = TopLevel.GetTopLevel(this) as Window;
			if (owner == null) return;
			_navigatorWindow = new NavigatorWindow(this) { WindowStartupLocation = WindowStartupLocation.CenterOwner };
			_navigatorWindow.Closed += (s, e) => _navigatorWindow = null;
			_navigatorWindow.Show(owner);
		}

		private void SetupAutoHideWindow()
		{
			if (_autoHideWindowManager != null)
				_autoHideWindowManager.HideAutoWindow();
			else
				_autoHideWindowManager = new AutoHideWindowManager(this);

			AutoHideWindow ??= new LayoutAutoHideWindowControl();
		}

		/// <summary>Shows the auto-hide flyout for an anchor.</summary>
		/// <param name="anchor">The anchor of the auto-hidden anchorable.</param>
		internal void ShowAutoHideWindow(LayoutAnchorControl anchor) => _autoHideWindowManager?.ShowAutoHideWindow(anchor);

		/// <summary>Hides the auto-hide flyout of an anchor.</summary>
		/// <param name="anchor">The anchor.</param>
		internal void HideAutoHideWindow(LayoutAnchorControl anchor) => _autoHideWindowManager?.HideAutoWindow(anchor);

		/// <summary>Gets the element that hosts the auto-hide flyout.</summary>
		/// <returns>The element.</returns>
		internal Control GetAutoHideAreaElement() => _autohideArea;

		#endregion Lifetime

		#region Theme

		/// <summary>Called when <see cref="DockTheme"/> changes.</summary>
		/// <param name="oldTheme">The previous theme.</param>
		/// <param name="newTheme">The new theme.</param>
		protected virtual void OnThemeChanged(Theme oldTheme, Theme newTheme)
		{
			// Detached windows are roots of their own visual tree and do not pick the theme up implicitly.
			foreach (var entry in _detachedAnchorables.Values)
				entry.Window.UpdateThemeResources(oldTheme, newTheme);

			if (_themeStyles != null)
			{
				Styles.Remove(_themeStyles);
				_themeStyles = null;
			}

			_themeStyles = newTheme?.CreateStyles();
			if (_themeStyles != null) Styles.Add(_themeStyles);

			foreach (var fwc in _fwList.Concat(_fwHiddenList))
				fwc.UpdateThemeResources(oldTheme);
		}

		#endregion Theme

		#region UI creation

		/// <summary>Applies a template property of the manager to <paramref name="control"/> when it is set, keeping the theme's template otherwise.</summary>
		/// <param name="control">The control.</param>
		/// <param name="templateProperty">The template property of the manager.</param>
		internal void ApplyTemplateProperty(TemplatedControl control, StyledProperty<IControlTemplate> templateProperty)
		{
			ObserveWhileInTree(control, templateProperty, () =>
			{
				var template = GetValue(templateProperty);
				if (template != null) control.Template = template;
				else control.ClearValue(TemplatedControl.TemplateProperty);
			});
		}

		/// <summary>Applies a control theme property of the manager to <paramref name="control"/> when it is set.</summary>
		/// <param name="control">The control.</param>
		/// <param name="themeProperty">The theme property of the manager.</param>
		internal void ApplyControlThemeProperty(StyledElement control, StyledProperty<ControlTheme> themeProperty)
		{
			ObserveWhileInTree(control, themeProperty, () =>
			{
				var theme = GetValue(themeProperty);
				if (theme != null) control.Theme = theme;
				else control.ClearValue(StyledElement.ThemeProperty);
			});
		}

		/// <summary>
		/// Runs <paramref name="apply"/> now and whenever <paramref name="property"/> of the manager changes while
		/// <paramref name="control"/> is part of a logical tree. The subscription ends when the control leaves the
		/// tree, so the manager never keeps controls of a discarded layout alive.
		/// </summary>
		private void ObserveWhileInTree<T>(StyledElement control, StyledProperty<T> property, Action apply)
		{
			IDisposable subscription = null;
			void Subscribe()
			{
				subscription?.Dispose();
				subscription = this.GetObservable(property).Subscribe(new ActionObserver<T>(_ => apply()));
			}

			control.AttachedToLogicalTree += (_, _) => Subscribe();
			control.DetachedFromLogicalTree += (_, _) =>
			{
				subscription?.Dispose();
				subscription = null;
			};

			apply();
			if (((ILogical)control).IsAttachedToLogicalTree) Subscribe();
		}

		/// <summary>
		/// Creates the control for a layout model element. Invoked when new UI items are created and when a
		/// deserialized layout is restored to the screen.
		/// </summary>
		/// <param name="model">The layout element.</param>
		/// <returns>The control, or <see langword="null"/> when none can be created.</returns>
		internal Control CreateUIElementForModel(ILayoutElement model)
		{
			switch (model)
			{
				case LayoutPanel layoutPanel:
					return new LayoutPanelControl(layoutPanel);

				case LayoutAnchorablePaneGroup anchorablePaneGroup:
					return new LayoutAnchorablePaneGroupControl(anchorablePaneGroup);

				case LayoutDocumentPaneGroup documentPaneGroup:
					return new LayoutDocumentPaneGroupControl(documentPaneGroup);

				case LayoutAnchorSide anchorSide:
					{
						var templateModelView = new LayoutAnchorSideControl(anchorSide);
						ApplyTemplateProperty(templateModelView, AnchorSideTemplateProperty);
						return templateModelView;
					}

				case LayoutAnchorGroup anchorGroup:
					{
						var templateModelView = new LayoutAnchorGroupControl(anchorGroup);
						ApplyTemplateProperty(templateModelView, AnchorGroupTemplateProperty);
						return templateModelView;
					}

				case LayoutDocumentPane documentPane:
					{
						var templateModelView = new LayoutDocumentPaneControl(documentPane, IsVirtualizingDocument, IgnoreTabControlKeyBindings);
						ApplyControlThemeProperty(templateModelView, DocumentPaneControlStyleProperty);
						ApplyTemplateProperty(templateModelView, DocumentPaneTemplateProperty);
						return templateModelView;
					}

				case LayoutAnchorablePane anchorablePane:
					{
						var templateModelView = new LayoutAnchorablePaneControl(anchorablePane, IsVirtualizingAnchorable, IgnoreTabControlKeyBindings);
						ApplyControlThemeProperty(templateModelView, AnchorablePaneControlStyleProperty);
						ApplyTemplateProperty(templateModelView, AnchorablePaneTemplateProperty);
						return templateModelView;
					}

				case LayoutAnchorableFloatingWindow modelFW:
					{
						if (!AllowFloatingWindows) return null;
						var newFW = new LayoutAnchorableFloatingWindowControl(modelFW);
						RegisterFloatingWindow(newFW);

						// Floating windows can also contain only pane groups at their base (issue #27); make sure
						// the window is positioned back on the current (or nearest) monitor.
						var panegroup = modelFW.RootPanel;
						if (panegroup != null)
						{
							panegroup.KeepInsideNearestMonitor();
							newFW.SetFloatingBounds(panegroup.FloatingLeft, panegroup.FloatingTop, panegroup.FloatingWidth, panegroup.FloatingHeight);
						}

						Dispatcher.UIThread.Post(
							() =>
							{
								if (newFW.IsClosingOrClosed) return;
								if (newFW.Content != null || modelFW.IsVisible)
									ShowFloatingWindowWhenPossible(newFW);
							},
							DispatcherPriority.Send);
						return newFW;
					}

				case LayoutDocumentFloatingWindow modelFW:
					{
						if (!AllowFloatingWindows) return null;
						var newFW = new LayoutDocumentFloatingWindowControl(modelFW);
						RegisterFloatingWindow(newFW);

						var paneForExtensions = modelFW.RootPanel;
						if (paneForExtensions != null)
						{
							paneForExtensions.KeepInsideNearestMonitor();
							newFW.SetFloatingBounds(paneForExtensions.FloatingLeft, paneForExtensions.FloatingTop, paneForExtensions.FloatingWidth, paneForExtensions.FloatingHeight);
						}

						ShowFloatingWindowWhenPossible(newFW);
						return newFW;
					}

				case LayoutDocument layoutDocument:
					return new LayoutDocumentControl { Model = layoutDocument };
			}

			return null;
		}

		/// <summary>Adds a floating window to the windows of this manager.</summary>
		/// <param name="fwc">The window.</param>
		private void RegisterFloatingWindow(LayoutFloatingWindowControl fwc)
		{
			// Fill list before calling Show (issue #254)
			_fwList.Add(fwc);
			fwc.Activated += OnFloatingWindowActivated;
			FocusElementManager.SetupFocusManagement(fwc);
		}

		private void OnFloatingWindowActivated(object sender, EventArgs e)
		{
			if (sender is not LayoutFloatingWindowControl fwc) return;
			_fwActivationOrder.Remove(fwc);
			_fwActivationOrder.Insert(0, fwc);
		}

		/// <summary>
		/// Shows a floating window, or - when the window hosting this manager has not been shown yet - shows it
		/// as soon as that window is (issue #618).
		/// </summary>
		/// <param name="fwc">The floating window.</param>
		private void ShowFloatingWindowWhenPossible(LayoutFloatingWindowControl fwc)
		{
			if (fwc.IsClosingOrClosed) return;
			var hostWindow = TopLevel.GetTopLevel(this) as Window;
			if (hostWindow == null || hostWindow.IsVisible)
			{
				fwc.ShowOwned();
				return;
			}

			void OnHostOpened(object sender, EventArgs e)
			{
				hostWindow.Opened -= OnHostOpened;
				if (!fwc.IsClosingOrClosed) fwc.ShowOwned();
			}

			hostWindow.Opened += OnHostOpened;
		}

		/// <summary>Return the layout item of the given content.</summary>
		/// <param name="content">The content to search.</param>
		/// <returns>The <see cref="LayoutAnchorableItem"/> or <see cref="LayoutDocumentItem"/> of the content.</returns>
		public LayoutItem GetLayoutItemFromModel(LayoutContent content) => _layoutItems.FirstOrDefault(item => item.LayoutElement == content);

		/// <summary>Creates a floating window control for the specified layout content.</summary>
		/// <param name="contentModel">The layout content to host in the floating window.</param>
		/// <param name="isContentImmutable">if set to <c>true</c>, the content cannot be changed while floating.</param>
		/// <returns>The floating window control, or <see langword="null"/> while <see cref="AllowFloatingWindows"/> is off.</returns>
		public LayoutFloatingWindowControl CreateFloatingWindow(LayoutContent contentModel, bool isContentImmutable)
		{
			if (!AllowFloatingWindows) return null;

			if (contentModel is LayoutAnchorable anchorable && !(contentModel.Parent is ILayoutPane))
			{
				var pane = new LayoutAnchorablePane(anchorable)
				{
					FloatingTop = contentModel.FloatingTop,
					FloatingLeft = contentModel.FloatingLeft,
					FloatingWidth = contentModel.FloatingWidth,
					FloatingHeight = contentModel.FloatingHeight
				};
				return CreateFloatingWindowForLayoutAnchorableWithoutParent(pane, isContentImmutable);
			}

			return CreateFloatingWindowCore(contentModel, isContentImmutable);
		}

		#endregion UI creation

		#region Floating windows

		/// <summary>
		/// Docks the content of every floating window back into the layout and closes the windows.
		/// </summary>
		/// <remarks>
		/// Each piece of content goes back to the pane it was floated from; content that no longer knows that
		/// pane is placed the way newly added content is. This runs automatically when
		/// <see cref="AllowFloatingWindows"/> is turned off.
		/// </remarks>
		public void DockAllFloatingWindows()
		{
			var layout = Layout;
			if (layout == null) return;

			// Materialised before anything moves: docking mutates both the floating window collection and the
			// trees hanging off it.
			var floatingContents = layout.FloatingWindows.SelectMany(fw => fw.Descendents().OfType<LayoutContent>()).ToArray();

			foreach (var content in floatingContents)
			{
				if (content.Root != layout) continue;
				if (content.FindParent<LayoutFloatingWindow>() == null) continue;

				if (HasDockablePreviousContainer(content))
					content.Dock();
				else
					DockOutsideFloatingWindows(content);
			}

			layout.CollectGarbage();
			CloseEmptiedFloatingWindows(layout);
			layout.CollectGarbage();
		}

		private static bool HasDockablePreviousContainer(LayoutContent content) =>
			((ILayoutPreviousContainer)content).PreviousContainer is ILayoutElement previous
			&& previous.Root == content.Root
			&& previous.FindParent<LayoutFloatingWindow>() == null;

		private void DockOutsideFloatingWindows(LayoutContent content)
		{
			var layout = Layout;
			if (layout == null) return;

			var wasFloating = content.IsFloating;

			if (content is LayoutAnchorable anchorable)
			{
				var anchorablePane = layout.Descendents().OfType<LayoutAnchorablePane>()
					.FirstOrDefault(pane => pane.FindParent<LayoutFloatingWindow>() == null);

				if (anchorablePane == null)
				{
					anchorablePane = new LayoutAnchorablePane { DockWidth = new GridLength(200.0, GridUnitType.Pixel) };
					EnsureRootPanel(layout).Children.Add(anchorablePane);
				}

				content.Parent?.RemoveChild(content);
				anchorablePane.Children.Add(anchorable);
			}
			else
			{
				var documentPane = layout.Descendents().OfType<LayoutDocumentPane>()
					.FirstOrDefault(pane => pane.FindParent<LayoutFloatingWindow>() == null);

				if (documentPane == null)
				{
					documentPane = new LayoutDocumentPane();
					EnsureRootPanel(layout).Children.Add(new LayoutDocumentPaneGroup(documentPane));
				}

				content.Parent?.RemoveChild(content);
				documentPane.Children.Add(content);
			}

			((ILayoutPreviousContainer)content).PreviousContainer = null;
			content.PreviousContainerIndex = -1;
			content.IsSelected = true;

			if (wasFloating && !content.IsFloating) RaiseContentDocked(content);
		}

		private static LayoutPanel EnsureRootPanel(LayoutRoot layout)
		{
			if (layout.RootPanel != null) return layout.RootPanel;
			var panel = new LayoutPanel { Orientation = Orientation.Horizontal };
			layout.RootPanel = panel;
			return panel;
		}

		private void CloseEmptiedFloatingWindows(LayoutRoot layout)
		{
			foreach (var fwc in _fwList.ToArray())
			{
				if (fwc.Model == null || fwc.Model.Descendents().OfType<LayoutContent>().Any()) continue;
				fwc.InternalClose();
			}

			foreach (var fw in layout.FloatingWindows.ToArray())
			{
				if (fw.Descendents().OfType<LayoutContent>().Any()) continue;
				layout.FloatingWindows.Remove(fw);
			}
		}

		/// <summary>Floats the given content and, when <paramref name="startDrag"/> is set, lets the pointer drag the new window.</summary>
		/// <param name="contentModel">The content to float.</param>
		/// <param name="startDrag">Kept for API parity; a drag needs pointer information, see the overload taking a <see cref="DragStartInfo"/>.</param>
		internal virtual void StartDraggingFloatingWindowForContent(LayoutContent contentModel, bool startDrag = true)
			=> StartDraggingFloatingWindowForContent(contentModel, (DragStartInfo)null);

		/// <summary>
		/// Executes when the user starts to drag a <see cref="LayoutDocument"/> or <see cref="LayoutAnchorable"/>
		/// by its tab: floats the content and hands the drag over to the new window.
		/// </summary>
		/// <param name="contentModel">The layout content to float.</param>
		/// <param name="dragStart">The drag to continue in the floating window, or <see langword="null"/> to only float.</param>
		internal virtual void StartDraggingFloatingWindowForContent(LayoutContent contentModel, DragStartInfo dragStart)
		{
			// Ensure window can float only if corresponding property is set accordingly
			if (contentModel == null) return;
			if (!AllowFloatingWindows) return;
			if (!contentModel.CanFloat) return;

			var floatingArgs = new ContentFloatingEventArgs(contentModel);
			ContentFloating?.Invoke(this, floatingArgs);
			if (floatingArgs.Cancel) return;

			var coreFloatArgs = new Core.Events.ContentCancelEventArgs(contentModel);
			_coreContentFloating?.Invoke(this, coreFloatArgs);
			if (coreFloatArgs.Cancel) return;

			LayoutFloatingWindowControl fwc = null;

			// For last document re-use floating window
			if (contentModel.Parent?.ChildrenCount == 1)
			{
				foreach (var fw in _fwList)
				{
					var found = fw.Model.Descendents().OfType<LayoutDocument>().Any(doc => doc == contentModel);
					if (!found) continue;
					if (fw.Model.Descendents().OfType<LayoutDocument>().Count() + fw.Model.Descendents().OfType<LayoutAnchorable>().Count() == 1)
						fwc = fw;
					break;
				}
			}

			if (fwc == null)
			{
				fwc = CreateFloatingWindow(contentModel, false);
				if (fwc != null) LayoutFloatingWindowControlCreated?.Invoke(this, new LayoutFloatingWindowControlCreatedEventArgs(fwc));
			}

			if (fwc == null) return;

			if (dragStart != null)
				fwc.SetFloatingBoundsForDrag(dragStart);

			ShowFloatingWindowWhenPossible(fwc);
			if (dragStart != null) fwc.AttachDrag(dragStart);

			ContentFloated?.Invoke(this, new ContentFloatedEventArgs(contentModel));
			_coreContentFloated?.Invoke(this, new Core.Events.ContentEventArgs(contentModel));
		}

		/// <summary>
		/// Executes when the user starts to drag a docked anchorable pane by its title bar: floats the whole pane
		/// and hands the drag over to the new window.
		/// </summary>
		/// <param name="paneModel">The pane to float.</param>
		/// <param name="dragStart">The drag to continue in the floating window, or <see langword="null"/>.</param>
		internal virtual void StartDraggingFloatingWindowForPane(LayoutAnchorablePane paneModel, DragStartInfo dragStart = null)
		{
			if (!AllowFloatingWindows || paneModel == null) return;

			var firstContent = paneModel.Children.FirstOrDefault();
			if (firstContent != null)
			{
				var floatingArgs = new ContentFloatingEventArgs(firstContent);
				ContentFloating?.Invoke(this, floatingArgs);
				if (floatingArgs.Cancel) return;

				var coreFloatArgs = new Core.Events.ContentCancelEventArgs(firstContent);
				_coreContentFloating?.Invoke(this, coreFloatArgs);
				if (coreFloatArgs.Cancel) return;
			}

			var fwc = CreateFloatingWindowForLayoutAnchorableWithoutParent(paneModel, false);
			if (fwc == null) return;

			LayoutFloatingWindowControlCreated?.Invoke(this, new LayoutFloatingWindowControlCreatedEventArgs(fwc));

			if (dragStart != null) fwc.SetFloatingBoundsForDrag(dragStart);
			ShowFloatingWindowWhenPossible(fwc);
			if (dragStart != null) fwc.AttachDrag(dragStart);

			if (firstContent != null)
			{
				ContentFloated?.Invoke(this, new ContentFloatedEventArgs(firstContent));
				_coreContentFloated?.Invoke(this, new Core.Events.ContentEventArgs(firstContent));
			}
		}

		/// <summary>Enumerates the floating windows of this manager, the most recently activated first.</summary>
		/// <returns>The floating windows.</returns>
		internal IEnumerable<LayoutFloatingWindowControl> GetFloatingWindowsByZOrder()
		{
			var windows = _fwList.Where(fw => fw.IsVisible && fw.Model?.Root?.Manager == this).ToArray();
			try
			{
				// Avalonia can sort top levels by their real z-order on most platforms.
				var span = windows.Cast<Window>().ToArray();
				Window.SortWindowsByZOrder(span);
				return Enumerable.Reverse(span).OfType<LayoutFloatingWindowControl>().ToList();
			}
			catch (Exception)
			{
				return windows.OrderBy(fw => { var i = _fwActivationOrder.IndexOf(fw); return i < 0 ? int.MaxValue : i; }).ToList();
			}
		}

		/// <summary>Builds the ordered list of drop target hosts for a drag: floating windows above the manager first.</summary>
		/// <param name="overlayWindowHosts">The collection to fill.</param>
		/// <param name="dragFloatingWindow">The window being dragged, which is never a host.</param>
		internal void GetOverlayWindowHostsByZOrder(ref List<IOverlayWindowHost> overlayWindowHosts, LayoutFloatingWindowControl dragFloatingWindow)
		{
			overlayWindowHosts.Clear();

			// Floating windows are owned by the manager's window, so they are always above it.
			foreach (var fw in GetFloatingWindowsByZOrder())
			{
				if (fw != dragFloatingWindow && fw is IOverlayWindowHost host)
					overlayWindowHosts.Add(host);
			}

			overlayWindowHosts.Add(this);
		}

		/// <summary>Removes a closed floating window from this manager.</summary>
		/// <param name="floatingWindow">The window.</param>
		internal void RemoveFloatingWindow(LayoutFloatingWindowControl floatingWindow)
		{
			_fwList.Remove(floatingWindow);
			_fwHiddenList.Remove(floatingWindow);
			_fwActivationOrder.Remove(floatingWindow);
			floatingWindow.Activated -= OnFloatingWindowActivated;
			FocusElementManager.FinalizeFocusManagement(floatingWindow);
			LayoutFloatingWindowControlClosed?.Invoke(this, new LayoutFloatingWindowControlClosedEventArgs(floatingWindow));
		}

		/// <summary>Takes every overlay of this manager and of its floating windows off the screen.</summary>
		/// <remarks>
		/// A drag does not always report its end, so every start and end of a drag clears the overlays of all
		/// hosts, not only the one the drag knows about (issue #587).
		/// </remarks>
		internal void HideAllOverlayWindows()
		{
			((IOverlayWindowHost)this).HideOverlayWindow();
			foreach (var host in _fwList.OfType<IOverlayWindowHost>().ToArray())
				host.HideOverlayWindow();
		}

		private LayoutFloatingWindowControl CreateFloatingWindowForLayoutAnchorableWithoutParent(LayoutAnchorablePane paneModel, bool isContentImmutable)
		{
			if (!AllowFloatingWindows) return null;
			if (paneModel.Children.Any(c => !c.CanFloat)) return null;

			var paneAsPositionableElement = paneModel as ILayoutPositionableElement;
			var paneAsWithActualSize = paneModel as ILayoutPositionableElementWithActualSize;

			var fwWidth = paneAsPositionableElement.FloatingWidth;
			var fwHeight = paneAsPositionableElement.FloatingHeight;
			var fwLeft = paneAsPositionableElement.FloatingLeft;
			var fwTop = paneAsPositionableElement.FloatingTop;

			if (fwWidth == 0.0) fwWidth = paneAsWithActualSize.ActualWidth + 10;
			if (fwHeight == 0.0) fwHeight = paneAsWithActualSize.ActualHeight + 10;

			var destPane = new LayoutAnchorablePane
			{
				DockWidth = paneAsPositionableElement.DockWidth,
				DockHeight = paneAsPositionableElement.DockHeight,
				DockMinHeight = paneAsPositionableElement.DockMinHeight,
				DockMinWidth = paneAsPositionableElement.DockMinWidth,
				FloatingLeft = paneAsPositionableElement.FloatingLeft,
				FloatingTop = paneAsPositionableElement.FloatingTop,
				FloatingWidth = paneAsPositionableElement.FloatingWidth,
				FloatingHeight = paneAsPositionableElement.FloatingHeight,
			};

			var savePreviousContainer = paneModel.FindParent<LayoutFloatingWindow>() == null;
			var currentSelectedContentIndex = paneModel.SelectedContentIndex;
			while (paneModel.Children.Count > 0)
			{
				var contentModel = paneModel.Children[paneModel.Children.Count - 1];

				if (savePreviousContainer)
				{
					((ILayoutPreviousContainer)contentModel).PreviousContainer = paneModel;
					contentModel.PreviousContainerIndex = paneModel.Children.Count - 1;
				}

				paneModel.RemoveChildAt(paneModel.Children.Count - 1);
				destPane.Children.Insert(0, contentModel);
			}

			if (destPane.Children.Count > 0) destPane.SelectedContentIndex = currentSelectedContentIndex;

			var fw = new LayoutAnchorableFloatingWindow
			{
				RootPanel = new LayoutAnchorablePaneGroup(destPane)
				{
					DockHeight = destPane.DockHeight,
					DockWidth = destPane.DockWidth,
					DockMinHeight = destPane.DockMinHeight,
					DockMinWidth = destPane.DockMinWidth,
				}
			};

			Layout.FloatingWindows.Add(fw);

			var fwc = new LayoutAnchorableFloatingWindowControl(fw, isContentImmutable);
			fwc.SetFloatingBounds(fwLeft, fwTop, fwWidth, fwHeight);
			RegisterFloatingWindow(fwc);
			Layout.CollectGarbage();
			InvalidateArrange();
			return fwc;
		}

		private LayoutFloatingWindowControl CreateFloatingWindowCore(LayoutContent contentModel, bool isContentImmutable)
		{
			if (!AllowFloatingWindows) return null;
			if (!contentModel.CanFloat) return null;
			if (contentModel is LayoutAnchorable contentModelAsAnchorable && contentModelAsAnchorable.IsAutoHidden)
				contentModelAsAnchorable.ToggleAutoHide();

			var parentPane = contentModel.Parent as ILayoutPane;
			var parentPaneAsPositionableElement = contentModel.Parent as ILayoutPositionableElement;
			var parentPaneAsWithActualSize = contentModel.Parent as ILayoutPositionableElementWithActualSize;
			if (parentPane == null) return null;
			var contentModelParentChildrenIndex = parentPane.Children.ToList().IndexOf(contentModel);

			if (contentModel.FindParent<LayoutFloatingWindow>() == null)
			{
				((ILayoutPreviousContainer)contentModel).PreviousContainer = parentPane;
				contentModel.PreviousContainerIndex = contentModelParentChildrenIndex;
			}

			parentPane.RemoveChildAt(contentModelParentChildrenIndex);

			var fwWidth = contentModel.FloatingWidth;
			var fwHeight = contentModel.FloatingHeight;

			if (fwWidth == 0.0) fwWidth = parentPaneAsPositionableElement.FloatingWidth;
			if (fwHeight == 0.0) fwHeight = parentPaneAsPositionableElement.FloatingHeight;

			if (fwWidth == 0.0) fwWidth = parentPaneAsWithActualSize.ActualWidth + 10;
			if (fwHeight == 0.0) fwHeight = parentPaneAsWithActualSize.ActualHeight + 10;

			LayoutFloatingWindowControl fwc;
			if (contentModel is LayoutAnchorable anchorableContent)
			{
				var fw = new LayoutAnchorableFloatingWindow
				{
					RootPanel = new LayoutAnchorablePaneGroup(new LayoutAnchorablePane(anchorableContent)
					{
						DockWidth = parentPaneAsPositionableElement.DockWidth,
						DockHeight = parentPaneAsPositionableElement.DockHeight,
						DockMinHeight = parentPaneAsPositionableElement.DockMinHeight,
						DockMinWidth = parentPaneAsPositionableElement.DockMinWidth,
						FloatingLeft = parentPaneAsPositionableElement.FloatingLeft,
						FloatingTop = parentPaneAsPositionableElement.FloatingTop,
						FloatingWidth = parentPaneAsPositionableElement.FloatingWidth,
						FloatingHeight = parentPaneAsPositionableElement.FloatingHeight,
					})
				};

				Layout.FloatingWindows.Add(fw);
				fwc = new LayoutAnchorableFloatingWindowControl(fw, isContentImmutable);
			}
			else
			{
				var documentContent = contentModel as LayoutDocument;
				var fw = new LayoutDocumentFloatingWindow
				{
					RootPanel = new LayoutDocumentPaneGroup(new LayoutDocumentPane(documentContent)
					{
						DockWidth = parentPaneAsPositionableElement.DockWidth,
						DockHeight = parentPaneAsPositionableElement.DockHeight,
						DockMinHeight = parentPaneAsPositionableElement.DockMinHeight,
						DockMinWidth = parentPaneAsPositionableElement.DockMinWidth,
						FloatingLeft = parentPaneAsPositionableElement.FloatingLeft,
						FloatingTop = parentPaneAsPositionableElement.FloatingTop,
						FloatingWidth = parentPaneAsPositionableElement.FloatingWidth,
						FloatingHeight = parentPaneAsPositionableElement.FloatingHeight,
					})
				};

				Layout.FloatingWindows.Add(fw);
				fwc = new LayoutDocumentFloatingWindowControl(fw, isContentImmutable);
			}

			fwc.SetFloatingBounds(contentModel.FloatingLeft, contentModel.FloatingTop, fwWidth, fwHeight);
			RegisterFloatingWindow(fwc);
			Layout.CollectGarbage();
			return fwc;
		}

		#endregion Floating windows

		#region IOverlayWindowHost

		/// <inheritdoc/>
		DockingManager IOverlayWindowHost.Manager => this;

		/// <inheritdoc/>
		bool IOverlayWindowHost.HitTestScreen(Point dragPoint) => IsEffectivelyVisible && this.GetScreenArea().Contains(dragPoint);

		/// <inheritdoc/>
		IOverlayWindow IOverlayWindowHost.ShowOverlayWindow(LayoutFloatingWindowControl draggingWindow)
		{
			_overlayWindow ??= new OverlayWindow(this);
			var owner = draggingWindow?.OwnedByDockingManagerWindow == false ? null : TopLevel.GetTopLevel(this) as Window;
			_overlayWindow.ShowOver(this, OverlayWindowMode, owner);
			return _overlayWindow;
		}

		/// <inheritdoc/>
		void IOverlayWindowHost.HideOverlayWindow()
		{
			_areas = null;

			// The overlay is hidden and kept for the next drag instead of being closed, so that a drag which
			// never reports its end cannot leave a growing number of empty windows behind (issue #587).
			_overlayWindow?.HideOverlay();
		}

		/// <inheritdoc/>
		IEnumerable<IDropArea> IOverlayWindowHost.GetDropAreas(LayoutFloatingWindowControl draggingWindow)
		{
			if (DropAreaCache.IsValid(_areas)) return _areas;
			_areas = new List<IDropArea>();
			var isDraggingDocuments = draggingWindow.Model is LayoutDocumentFloatingWindow;
			if (!isDraggingDocuments)
			{
				_areas.Add(new DropArea<DockingManager>(this, DropAreaType.DockingManager));
				foreach (var areaHost in this.FindVisualChildren<LayoutAnchorablePaneControl>())
				{
					if (areaHost.IsEffectivelyVisible && areaHost.Model.Descendents().Any())
						_areas.Add(new DropArea<LayoutAnchorablePaneControl>(areaHost, DropAreaType.AnchorablePane));
				}
			}

			// Determine if floatingWindow is configured to dock as document or not
			var dockAsDocument = true;
			if (!isDraggingDocuments && draggingWindow.Model is LayoutAnchorableFloatingWindow anchorableWindow)
			{
				foreach (var item in GetAnchorableInFloatingWindow(anchorableWindow))
				{
					if (item.CanDockAsTabbedDocument) continue;
					dockAsDocument = false;
					break;
				}
			}

			// Dock only documents and tools in DocumentPane if configuration does allow that
			if (dockAsDocument)
			{
				foreach (var areaHost in this.FindVisualChildren<LayoutDocumentPaneControl>().Where(c => c.IsEffectivelyVisible))
					_areas.Add(new DropArea<LayoutDocumentPaneControl>(areaHost, DropAreaType.DocumentPane));

				foreach (var areaHost in this.FindVisualChildren<LayoutDocumentPaneGroupControl>().Where(c => c.IsEffectivelyVisible))
				{
					var documentGroupModel = areaHost.Model as LayoutDocumentPaneGroup;
					if (!documentGroupModel.Children.Any(c => c.IsVisible))
						_areas.Add(new DropArea<LayoutDocumentPaneGroupControl>(areaHost, DropAreaType.DocumentPaneGroup));
				}
			}

			return _areas;
		}

		private static IEnumerable<LayoutAnchorable> GetAnchorableInFloatingWindow(LayoutAnchorableFloatingWindow window)
		{
			if (window.SinglePane is LayoutAnchorablePane singlePane && window.IsSinglePane && singlePane.SelectedContent != null)
			{
				yield return singlePane.SelectedContent as LayoutAnchorable;
				yield break;
			}

			foreach (var anchorable in window.Descendents().OfType<LayoutAnchorable>())
				yield return anchorable;
		}

		private void DestroyOverlayWindow()
		{
			if (_overlayWindow == null) return;
			_overlayWindow.Close();
			_overlayWindow = null;
		}

		#endregion IOverlayWindowHost

		#region Commands

		/// <summary>Closes all documents except the selected content.</summary>
		/// <param name="contentSelected">The document to keep open.</param>
		internal void ExecuteCloseAllButThisCommand(LayoutContent contentSelected)
		{
			foreach (var contentToClose in Layout.Descendents().OfType<LayoutContent>().Where(d => d != contentSelected && (d.Parent is LayoutDocumentPane || d.Parent is LayoutDocumentFloatingWindow)).ToArray())
				Close(contentToClose);
		}

		/// <summary>Closes all docked and floating documents.</summary>
		/// <param name="contentSelected">The content that initiated the command.</param>
		internal void ExecuteCloseAllCommand(LayoutContent contentSelected)
		{
			foreach (var contentToClose in Layout.Descendents().OfType<LayoutContent>().Where(d => d.Parent is LayoutDocumentPane || d.Parent is LayoutDocumentFloatingWindow).ToArray())
				Close(contentToClose);
		}

		/// <summary>Closes the specified anchorable.</summary>
		/// <param name="anchorable">The anchorable to close.</param>
		internal virtual void ExecuteCloseCommand(LayoutAnchorable anchorable)
		{
			// Closing removes the anchorable from the layout; its content has to be back in the layout first,
			// otherwise a standalone window would keep the only reference to it.
			ReattachAnchorable(anchorable);

			if (!(anchorable is LayoutAnchorable model)) return;

			var closingArgs = new AnchorableClosingEventArgs(model);
			AnchorableClosing?.Invoke(this, closingArgs);
			if (closingArgs.Cancel) return;

			var coreClosingArgs = new Core.Events.AnchorableCancelEventArgs(model);
			_coreAnchorableClosing?.Invoke(this, coreClosingArgs);
			if (coreClosingArgs.Cancel) return;

			if (model.CloseAnchorable())
			{
				AnchorableClosed?.Invoke(this, new AnchorableClosedEventArgs(model));
				_coreAnchorableClosed?.Invoke(this, new Core.Events.AnchorableEventArgs(model));
			}
		}

		/// <summary>Closes the specified document.</summary>
		/// <param name="document">The document to close.</param>
		internal void ExecuteCloseCommand(LayoutDocument document)
		{
			if (DocumentClosing != null)
			{
				var argsClosing = new DocumentClosingEventArgs(document);
				DocumentClosing(this, argsClosing);
				if (argsClosing.Cancel) return;
			}

			var coreClosingArgs = new Core.Events.DocumentCancelEventArgs(document);
			_coreDocumentClosing?.Invoke(this, coreClosingArgs);
			if (coreClosingArgs.Cancel) return;

			// Get the document to activate after the close.
			var documentToActivate = GetDocumentToActivate(document);

			if (!document.CloseDocument()) return;

			DocumentClosed?.Invoke(this, new DocumentClosedEventArgs(document));
			_coreDocumentClosed?.Invoke(this, new Core.Events.DocumentEventArgs(document));

			// get rid of the closed document content
			document.Content = null;

			// Activate the document determined to be the next active document.
			if (documentToActivate != null) documentToActivate.IsActive = true;
		}

		private LayoutDocument GetDocumentToActivate(LayoutDocument previousDocument)
		{
			var siblingDocuments = previousDocument.Parent?.Children.OfType<LayoutDocument>().ToList() ?? new List<LayoutDocument>();
			for (var i = 1; i < siblingDocuments.Count; i++)
			{
				if (siblingDocuments[i] == previousDocument) return siblingDocuments[i - 1];
			}

			return Layout.Descendents().OfType<LayoutDocument>().FirstOrDefault(d => d.IsSelected && d != previousDocument);
		}

		/// <summary>Hides the specified anchorable.</summary>
		/// <param name="anchorable">The anchorable to hide.</param>
		internal virtual void ExecuteHideCommand(LayoutAnchorable anchorable)
		{
			ReattachAnchorable(anchorable);

			if (!(anchorable is LayoutAnchorable model)) return;

			var hidingArgs = new AnchorableHidingEventArgs(model);
			AnchorableHiding?.Invoke(this, hidingArgs);
			if (hidingArgs.CloseInsteadOfHide)
			{
				ExecuteCloseCommand(model);
				return;
			}

			if (hidingArgs.Cancel) return;

			var coreHidingArgs = new Core.Events.AnchorableCancelEventArgs(model);
			_coreAnchorableHiding?.Invoke(this, coreHidingArgs);
			if (coreHidingArgs.CloseInsteadOfHide)
			{
				ExecuteCloseCommand(model);
				return;
			}

			if (coreHidingArgs.Cancel) return;

			if (model.HideAnchorable(true))
			{
				AnchorableHidden?.Invoke(this, new AnchorableHiddenEventArgs(model));
				_coreAnchorableHidden?.Invoke(this, new Core.Events.AnchorableEventArgs(model));
			}
		}

		/// <summary>Toggles auto-hide for the specified anchorable.</summary>
		/// <param name="anchorable">The anchorable whose auto-hide state should be toggled.</param>
		internal virtual void ExecuteAutoHideCommand(LayoutAnchorable anchorable)
		{
			ReattachAnchorable(anchorable);
			anchorable.ToggleAutoHide();
		}

		/// <summary>Floats the given content (the Float command).</summary>
		/// <param name="contentToFloat">The content to float.</param>
		internal void ExecuteFloatCommand(LayoutContent contentToFloat)
		{
			ReattachAnchorable(contentToFloat as LayoutAnchorable);

			var floatingArgs = new ContentFloatingEventArgs(contentToFloat);
			ContentFloating?.Invoke(this, floatingArgs);
			if (floatingArgs.Cancel) return;

			var coreFloatingArgs = new Core.Events.ContentCancelEventArgs(contentToFloat);
			_coreContentFloating?.Invoke(this, coreFloatingArgs);
			if (coreFloatingArgs.Cancel) return;

			contentToFloat.Float();
			ContentFloated?.Invoke(this, new ContentFloatedEventArgs(contentToFloat));
			_coreContentFloated?.Invoke(this, new Core.Events.ContentEventArgs(contentToFloat));
		}

		/// <summary>Docks the specified anchorable.</summary>
		/// <param name="anchorable">The anchorable to dock.</param>
		internal void ExecuteDockCommand(LayoutAnchorable anchorable)
		{
			ReattachAnchorable(anchorable);
			if (!RaiseContentDocking(anchorable)) return;
			anchorable.Dock();
		}

		/// <summary>Docks the specified content as a document.</summary>
		/// <param name="content">The content to dock as a document.</param>
		internal void ExecuteDockAsDocumentCommand(LayoutContent content)
		{
			ReattachAnchorable(content as LayoutAnchorable);
			if (!RaiseContentDocking(content)) return;
			content.DockAsDocument();
		}

		/// <summary>
		/// Raises <see cref="ContentDocking"/> (and its core counterpart). Must run before any layout mutation,
		/// the only point where the operation can still be cancelled atomically.
		/// </summary>
		/// <param name="content">The content that is about to be docked.</param>
		/// <returns><c>false</c> if a handler cancelled the operation.</returns>
		internal bool RaiseContentDocking(LayoutContent content)
		{
			var dockingArgs = new ContentDockingEventArgs(content);
			ContentDocking?.Invoke(this, dockingArgs);
			if (dockingArgs.Cancel) return false;

			var coreDockingArgs = new Core.Events.ContentCancelEventArgs(content);
			_coreContentDocking?.Invoke(this, coreDockingArgs);
			return !coreDockingArgs.Cancel;
		}

		/// <summary>Raises <see cref="ContentDocked"/> (and its core counterpart).</summary>
		/// <param name="content">The content that has just been docked.</param>
		internal void RaiseContentDocked(LayoutContent content)
		{
			ContentDocked?.Invoke(this, new ContentDockedEventArgs(content));
			_coreContentDocked?.Invoke(this, new Core.Events.ContentEventArgs(content));
		}

		/// <summary>Activates the specified layout content.</summary>
		/// <param name="content">The content to activate.</param>
		internal void ExecuteContentActivateCommand(LayoutContent content) => content.IsActive = true;

		private void Close(LayoutContent contentToClose)
		{
			if (!contentToClose.CanClose) return;

			var layoutItem = GetLayoutItemFromModel(contentToClose);
			if (layoutItem?.CloseCommand != null)
			{
				if (layoutItem.CloseCommand.CanExecute(null))
					layoutItem.CloseCommand.Execute(null);
			}
			else if (contentToClose is LayoutDocument document)
			{
				ExecuteCloseCommand(document);
			}
			else if (contentToClose is LayoutAnchorable anchorable)
			{
				ExecuteCloseCommand(anchorable);
			}
		}

		#endregion Commands

		#region Detached windows

		/// <summary>The anchorables currently detached into a standalone window, mapped to the state needed to return them.</summary>
		private readonly Dictionary<LayoutAnchorable, DetachedEntry> _detachedAnchorables = new Dictionary<LayoutAnchorable, DetachedEntry>();

		/// <summary>Set once the host window has been hooked, so detached windows are cleaned up on shutdown.</summary>
		private bool _hostWindowHooked;

		/// <summary>Gets the anchorables that are currently detached into a standalone window.</summary>
		public IEnumerable<LayoutAnchorable> DetachedAnchorables => _detachedAnchorables.Keys.ToList();

		/// <summary>Gets the standalone window an anchorable is detached to, if any.</summary>
		/// <param name="anchorable">The anchorable.</param>
		/// <returns>The window, or <see langword="null"/>.</returns>
		internal DetachedAnchorableWindow GetDetachedWindow(LayoutAnchorable anchorable)
			=> anchorable != null && _detachedAnchorables.TryGetValue(anchorable, out var entry) ? entry.Window : null;

		/// <summary>Gets a value indicating whether the given anchorable is currently hosted by a standalone window.</summary>
		/// <param name="anchorable">The anchorable to test.</param>
		/// <returns><see langword="true"/> when the anchorable is detached.</returns>
		public bool IsDetached(LayoutAnchorable anchorable) => anchorable != null && _detachedAnchorables.ContainsKey(anchorable);

		/// <summary>Moves the content of the given anchorable out of the layout and into a standalone top-level window.</summary>
		/// <param name="anchorable">The anchorable to detach.</param>
		public void DetachAnchorableToWindow(LayoutAnchorable anchorable)
		{
			if (!AllowDetachedWindows) return;
			if (anchorable == null || IsDetached(anchorable)) return;
			if (!(GetLayoutItemFromModel(anchorable) is LayoutAnchorableItem layoutItem)) return;

			// Resolved before the anchorable leaves the layout: without a view there is nothing to hand to a
			// window, and taking it out of the layout first would leave it hidden with no window to bring it back.
			var view = layoutItem.View;
			if (view == null) return;

			var restoreState = DetachFromLayout(anchorable);

			// The view may still be held by the host of the pane it was shown in.
			if (view is LayoutItemView itemView) itemView.Host?.ReleaseView();

			var window = new DetachedAnchorableWindow(anchorable, view, CreateDetachedWindowHeader(anchorable));
			window.UpdateThemeResources(null, DockTheme);
			window.Closed += OnDetachedWindowClosed;

			_detachedAnchorables[anchorable] = new DetachedEntry(window, restoreState);
			anchorable.IsDetached = true;

			HookHostWindow();
			window.Show();

			OnDetachedAnchorablesChanged(anchorable);
		}

		/// <summary>Closes the standalone window of the given anchorable and returns its content to the layout.</summary>
		/// <param name="anchorable">The anchorable to return.</param>
		public void ReattachAnchorable(LayoutAnchorable anchorable) =>
			ReattachAnchorableCore(anchorable, returnToLayout: true, keepDetachedFlag: false);

		private void ReattachAnchorableCore(LayoutAnchorable anchorable, bool returnToLayout, bool keepDetachedFlag)
		{
			if (anchorable == null || !_detachedAnchorables.TryGetValue(anchorable, out var entry)) return;

			// Remove the bookkeeping first: ReturnToLayout below must not take the detached branch, and closing
			// the window must not re-enter through OnDetachedWindowClosed.
			_detachedAnchorables.Remove(anchorable);
			if (!keepDetachedFlag) anchorable.IsDetached = false;
			entry.Window.Closed -= OnDetachedWindowClosed;

			entry.Window.ReleaseView();
			if (!entry.Window.IsClosed) entry.Window.Close();

			// Returning the anchorable rebuilds the control that hosts the view, which claims it again.
			if (returnToLayout) ReturnToLayout(anchorable, entry.RestoreState);

			OnDetachedAnchorablesChanged(anchorable);
		}

		/// <summary>Returns every detached anchorable to the layout.</summary>
		public void ReattachAllDetachedAnchorables()
		{
			foreach (var anchorable in _detachedAnchorables.Keys.ToList())
				ReattachAnchorable(anchorable);
		}

		/// <summary>Takes the given anchorable out of the layout in preparation for hosting its content in a standalone window.</summary>
		/// <param name="anchorable">The anchorable being detached.</param>
		/// <returns>State that <see cref="ReturnToLayout"/> needs to put the anchorable back, or <see langword="null"/>.</returns>
		protected virtual object DetachFromLayout(LayoutAnchorable anchorable)
		{
			anchorable?.HideAnchorable(false);
			return null;
		}

		/// <summary>Puts a previously detached anchorable back into the layout.</summary>
		/// <param name="anchorable">The anchorable being returned.</param>
		/// <param name="restoreState">The state produced by <see cref="DetachFromLayout"/>.</param>
		protected virtual void ReturnToLayout(LayoutAnchorable anchorable, object restoreState)
		{
			if (anchorable != null && anchorable.IsHidden) anchorable.Show();
		}

		/// <summary>Creates the header shown at the top of a detached window.</summary>
		/// <param name="anchorable">The anchorable the window hosts.</param>
		/// <returns>The header element, or <see langword="null"/> for a window without a header.</returns>
		protected virtual Control CreateDetachedWindowHeader(LayoutAnchorable anchorable) => new AnchorablePaneTitle { Model = anchorable };

		/// <summary>Called after an anchorable was detached or returned, for derived managers to react.</summary>
		/// <param name="anchorable">The anchorable whose detached state just changed.</param>
		protected virtual void OnDetachedAnchorablesChanged(LayoutAnchorable anchorable)
		{
		}

		/// <summary>Brings the standalone window of the given anchorable to the front.</summary>
		/// <param name="anchorable">The detached anchorable.</param>
		protected void ActivateDetachedWindow(LayoutAnchorable anchorable)
		{
			if (anchorable == null || !_detachedAnchorables.TryGetValue(anchorable, out var entry)) return;
			if (entry.Window.WindowState == WindowState.Minimized) entry.Window.WindowState = WindowState.Normal;
			entry.Window.Activate();
		}

		private void OnDetachedWindowClosed(object sender, EventArgs e)
		{
			if (sender is DetachedAnchorableWindow window) ReattachAnchorable(window.Model);
		}

		private void HookHostWindow()
		{
			if (_hostWindowHooked) return;
			if (TopLevel.GetTopLevel(this) is not Window hostWindow) return;
			hostWindow.Closed += OnHostWindowClosed;
			_hostWindowHooked = true;
		}

		private void OnHostWindowClosed(object sender, EventArgs e)
		{
			if (sender is Window hostWindow) hostWindow.Closed -= OnHostWindowClosed;
			_hostWindowHooked = false;

			// The layout is being destroyed; what matters is that no ownerless window survives.
			CloseDetachedWindows(returnToLayout: false, keepDetachedFlag: true);
		}

		private void CloseDetachedWindows(bool returnToLayout, bool keepDetachedFlag)
		{
			foreach (var anchorable in _detachedAnchorables.Keys.ToList())
				ReattachAnchorableCore(anchorable, returnToLayout, keepDetachedFlag);
		}

		private void DiscardDetachedWindowsOfReplacedLayout() => CloseDetachedWindows(returnToLayout: true, keepDetachedFlag: true);

		private void RestoreDetachedAnchorables(LayoutRoot layout)
		{
			if (layout == null) return;

			var toDetach = layout.Descendents().OfType<LayoutAnchorable>().Where(a => a.IsDetached).ToList();
			if (toDetach.Count == 0) return;

			// The flag is re-applied by DetachAnchorableToWindow; clear it so a failed restore cannot leave the
			// model claiming to be detached without a window.
			foreach (var anchorable in toDetach)
				anchorable.IsDetached = false;

			if (!AllowDetachedWindows) return;

			Dispatcher.UIThread.Post(
				() =>
				{
					foreach (var anchorable in toDetach)
						if (anchorable.Root == layout) DetachAnchorableToWindow(anchorable);
				},
				DispatcherPriority.Loaded);
		}

		/// <summary>Tracks one anchorable that is currently hosted by a standalone window.</summary>
		private sealed class DetachedEntry
		{
			public DetachedEntry(DetachedAnchorableWindow window, object restoreState)
			{
				Window = window;
				RestoreState = restoreState;
			}

			public DetachedAnchorableWindow Window { get; }

			public object RestoreState { get; }
		}

		#endregion Detached windows

		#region Documents and anchorables sources

		private void AttachDocumentsSource(LayoutRoot layout, IEnumerable documentsSource)
		{
			if (documentsSource == null || layout == null) return;

			var documentsImported = layout.Descendents().OfType<LayoutDocument>().Select(d => d.Content).ToArray();
			var listOfDocumentsToImport = documentsSource.OfType<object>().Where(d => !documentsImported.Contains(d)).ToList();

			LayoutDocumentPane documentPane = null;
			if (layout.LastFocusedDocument != null)
				documentPane = layout.LastFocusedDocument.Parent as LayoutDocumentPane;

			if (documentPane == null)
				documentPane = layout.Descendents().OfType<LayoutDocumentPane>().FirstOrDefault();

			_suspendLayoutItemCreation = true;
			try
			{
				foreach (var documentContentToImport in listOfDocumentsToImport)
				{
					var documentToImport = new LayoutDocument { Content = documentContentToImport };

					var added = false;
					if (LayoutUpdateStrategy != null)
						added = LayoutUpdateStrategy.BeforeInsertDocument(layout, documentToImport, documentPane);

					if (!added)
					{
						if (documentPane == null)
							throw new InvalidOperationException("Layout must contains at least one LayoutDocumentPane in order to host documents");

						documentPane.Children.Add(documentToImport);
					}

					LayoutUpdateStrategy?.AfterInsertDocument(layout, documentToImport);
					CreateDocumentLayoutItem(documentToImport);
				}
			}
			finally
			{
				_suspendLayoutItemCreation = false;
			}

			if (documentsSource is INotifyCollectionChanged documentsSourceAsNotifier)
				documentsSourceAsNotifier.CollectionChanged += DocumentsSourceElementsChanged;
		}

		private void DocumentsSourceElementsChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			if (Layout == null) return;

			// When deserializing documents are created automatically by the deserializer
			if (SuspendDocumentsSourceBinding) return;

			// handle remove
			if ((e.Action == NotifyCollectionChangedAction.Remove || e.Action == NotifyCollectionChangedAction.Replace) && e.OldItems != null)
			{
				var documentsToRemove = Layout.Descendents().OfType<LayoutDocument>().Where(d => e.OldItems.Contains(d.Content)).ToArray();
				foreach (var documentToRemove in documentsToRemove)
				{
					documentToRemove.Content = null;
					documentToRemove.Parent?.RemoveChild(documentToRemove);
				}
			}

			// handle add
			if (e.NewItems != null && (e.Action == NotifyCollectionChangedAction.Add || e.Action == NotifyCollectionChangedAction.Replace))
			{
				LayoutDocumentPane documentPane = null;
				if (Layout.LastFocusedDocument != null)
					documentPane = Layout.LastFocusedDocument.Parent as LayoutDocumentPane;

				if (documentPane == null)
					documentPane = Layout.Descendents().OfType<LayoutDocumentPane>().FirstOrDefault();

				_suspendLayoutItemCreation = true;
				try
				{
					foreach (var documentContentToImport in e.NewItems)
					{
						var documentToImport = new LayoutDocument { Content = documentContentToImport };

						var added = false;
						if (LayoutUpdateStrategy != null)
							added = LayoutUpdateStrategy.BeforeInsertDocument(Layout, documentToImport, documentPane);

						if (!added)
						{
							if (documentPane == null)
								throw new InvalidOperationException("Layout must contains at least one LayoutDocumentPane in order to host documents");

							documentPane.Children.Add(documentToImport);
						}

						LayoutUpdateStrategy?.AfterInsertDocument(Layout, documentToImport);
						var root = documentToImport.Root;
						if (root != null && root.Manager == this)
							CreateDocumentLayoutItem(documentToImport);
					}
				}
				finally
				{
					_suspendLayoutItemCreation = false;
				}
			}

			if (e.Action == NotifyCollectionChangedAction.Reset)
			{
				// Remove documents that are no longer in the DocumentSource.
				foreach (var documentToRemove in GetItemsToRemoveAfterReset<LayoutDocument>(DocumentsSource))
					documentToRemove.Parent?.RemoveChild(documentToRemove);
			}

			Layout?.CollectGarbage();
		}

		private TLayoutType[] GetItemsToRemoveAfterReset<TLayoutType>(IEnumerable source)
			where TLayoutType : LayoutContent
		{
			var itemsThatRemain = new HashSet<object>(source?.Cast<object>() ?? Enumerable.Empty<object>(), ReferenceEqualityComparer.Default);
			return Layout.Descendents().OfType<TLayoutType>().Where(x => !itemsThatRemain.Contains(x.Content)).ToArray();
		}

		private void DetachDocumentsSource(LayoutRoot layout, IEnumerable documentsSource)
		{
			if (documentsSource == null || layout == null) return;

			var documentsToRemove = layout.Descendents().OfType<LayoutDocument>().Where(d => documentsSource.Contains(d.Content)).ToArray();
			foreach (var documentToRemove in documentsToRemove)
				documentToRemove.Parent?.RemoveChild(documentToRemove);

			if (documentsSource is INotifyCollectionChanged documentsSourceAsNotifier)
				documentsSourceAsNotifier.CollectionChanged -= DocumentsSourceElementsChanged;
		}

		private void AttachAnchorablesSource(LayoutRoot layout, IEnumerable anchorablesSource)
		{
			if (anchorablesSource == null || layout == null) return;

			var anchorablesImported = layout.Descendents().OfType<LayoutAnchorable>().Select(d => d.Content).ToArray();
			var listOfAnchorablesToImport = anchorablesSource.OfType<object>().Where(a => !anchorablesImported.Contains(a)).ToList();

			var anchorablePane = FindDefaultAnchorablePane(layout);

			_suspendLayoutItemCreation = true;
			try
			{
				foreach (var anchorableContentToImport in listOfAnchorablesToImport)
				{
					var anchorableToImport = new LayoutAnchorable { Content = anchorableContentToImport };
					var added = false;
					if (LayoutUpdateStrategy != null)
						added = LayoutUpdateStrategy.BeforeInsertAnchorable(layout, anchorableToImport, anchorablePane);

					if (!added)
					{
						anchorablePane ??= CreateDefaultAnchorablePane(layout);
						anchorablePane.Children.Add(anchorableToImport);
					}

					LayoutUpdateStrategy?.AfterInsertAnchorable(layout, anchorableToImport);
					CreateAnchorableLayoutItem(anchorableToImport);
				}
			}
			finally
			{
				_suspendLayoutItemCreation = false;
			}

			if (anchorablesSource is INotifyCollectionChanged anchorablesSourceAsNotifier)
				anchorablesSourceAsNotifier.CollectionChanged += AnchorablesSourceElementsChanged;
		}

		private static LayoutAnchorablePane FindDefaultAnchorablePane(LayoutRoot layout)
		{
			// look for active content parent pane, then for a pane on the right side, then for any pane
			return layout.ActiveContent?.Parent as LayoutAnchorablePane
				?? layout.Descendents().OfType<LayoutAnchorablePane>().FirstOrDefault(pane => !pane.IsHostedInFloatingWindow && pane.GetSide() == AnchorSide.Right)
				?? layout.Descendents().OfType<LayoutAnchorablePane>().FirstOrDefault();
		}

		private static LayoutAnchorablePane CreateDefaultAnchorablePane(LayoutRoot layout)
		{
			var mainLayoutPanel = new LayoutPanel { Orientation = Orientation.Horizontal };
			if (layout.RootPanel != null) mainLayoutPanel.Children.Add(layout.RootPanel);
			layout.RootPanel = mainLayoutPanel;
			var anchorablePane = new LayoutAnchorablePane { DockWidth = new GridLength(200.0, GridUnitType.Pixel) };
			mainLayoutPanel.Children.Add(anchorablePane);
			return anchorablePane;
		}

		private void AnchorablesSourceElementsChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			if (Layout == null) return;

			// When deserializing documents are created automatically by the deserializer
			if (SuspendAnchorablesSourceBinding) return;

			// handle remove
			if ((e.Action == NotifyCollectionChangedAction.Remove || e.Action == NotifyCollectionChangedAction.Replace) && e.OldItems != null)
			{
				var anchorablesToRemove = Layout.Descendents().OfType<LayoutAnchorable>().Where(d => e.OldItems.Contains(d.Content)).ToArray();
				foreach (var anchorableToRemove in anchorablesToRemove)
				{
					anchorableToRemove.Content = null;
					anchorableToRemove.Parent?.RemoveChild(anchorableToRemove);
				}
			}

			// handle add
			if (e.NewItems != null && (e.Action == NotifyCollectionChangedAction.Add || e.Action == NotifyCollectionChangedAction.Replace))
			{
				var anchorablePane = FindDefaultAnchorablePane(Layout);

				_suspendLayoutItemCreation = true;
				try
				{
					foreach (var anchorableContentToImport in e.NewItems)
					{
						var anchorableToImport = new LayoutAnchorable { Content = anchorableContentToImport };
						var added = false;
						if (LayoutUpdateStrategy != null)
							added = LayoutUpdateStrategy.BeforeInsertAnchorable(Layout, anchorableToImport, anchorablePane);

						if (!added)
						{
							anchorablePane ??= CreateDefaultAnchorablePane(Layout);
							anchorablePane.Children.Add(anchorableToImport);
						}

						LayoutUpdateStrategy?.AfterInsertAnchorable(Layout, anchorableToImport);
						var root = anchorableToImport.Root;
						if (root != null && root.Manager == this)
							CreateAnchorableLayoutItem(anchorableToImport);
					}
				}
				finally
				{
					_suspendLayoutItemCreation = false;
				}
			}

			if (e.Action == NotifyCollectionChangedAction.Reset)
			{
				// Remove anchorables that are no longer in the AnchorablesSource.
				foreach (var anchorableToRemove in GetItemsToRemoveAfterReset<LayoutAnchorable>(AnchorablesSource))
					anchorableToRemove.Parent?.RemoveChild(anchorableToRemove);
			}

			Layout?.CollectGarbage();
		}

		private void DetachAnchorablesSource(LayoutRoot layout, IEnumerable anchorablesSource)
		{
			if (anchorablesSource == null || layout == null) return;

			var anchorablesToRemove = layout.Descendents().OfType<LayoutAnchorable>().Where(d => anchorablesSource.Contains(d.Content)).ToArray();
			foreach (var anchorableToRemove in anchorablesToRemove)
				anchorableToRemove.Parent?.RemoveChild(anchorableToRemove);

			if (anchorablesSource is INotifyCollectionChanged anchorablesSourceAsNotifier)
				anchorablesSourceAsNotifier.CollectionChanged -= AnchorablesSourceElementsChanged;
		}

		#endregion Documents and anchorables sources

		#region Layout items

		private void Layout_ElementRemoved(object sender, LayoutElementEventArgs e)
		{
			if (_suspendLayoutItemCreation) return;
			CollectLayoutItemsDeleted();
		}

		private void Layout_ElementAdded(object sender, LayoutElementEventArgs e)
		{
			if (_suspendLayoutItemCreation) return;
			foreach (var content in Layout.Descendents().OfType<LayoutContent>().ToList())
			{
				if (content is LayoutDocument document) CreateDocumentLayoutItem(document);
				else if (content is LayoutAnchorable anchorable) CreateAnchorableLayoutItem(anchorable);
			}

			CollectLayoutItemsDeleted();
		}

		/// <summary>Detaches and removes the layout items whose content is no longer part of the layout.</summary>
		private void CollectLayoutItemsDeleted()
		{
			if (_collectLayoutItemsPending) return;
			_collectLayoutItemsPending = true;
			Dispatcher.UIThread.Post(
				() =>
				{
					_collectLayoutItemsPending = false;
					foreach (var itemToRemove in _layoutItems.Where(item => item.LayoutElement?.Root != Layout).ToArray())
					{
						RemoveLayoutItem(itemToRemove);
					}
				},
				DispatcherPriority.Normal);
		}

		private void RemoveLayoutItem(LayoutItem item)
		{
			// The content of a detached anchorable leaves the layout but keeps its item.
			if (item.LayoutElement is LayoutAnchorable anchorable && IsDetached(anchorable)) return;
			item.Detach();
			_layoutItems.Remove(item);
			LogicalChildren.Remove(item);
		}

		private void DetachLayoutItems(LayoutRoot layout)
		{
			foreach (var item in _layoutItems.ToArray())
			{
				item.Detach();
				LogicalChildren.Remove(item);
			}

			_layoutItems.Clear();
			if (layout == null) return;
			layout.ElementAdded -= Layout_ElementAdded;
			layout.ElementRemoved -= Layout_ElementRemoved;
		}

		private void AttachLayoutItems()
		{
			var layout = Layout;
			if (layout == null) return;
			foreach (var document in layout.Descendents().OfType<LayoutDocument>().ToArray())
				CreateDocumentLayoutItem(document);

			foreach (var anchorable in layout.Descendents().OfType<LayoutAnchorable>().ToArray())
				CreateAnchorableLayoutItem(anchorable);

			layout.ElementAdded -= Layout_ElementAdded;
			layout.ElementRemoved -= Layout_ElementRemoved;
			layout.ElementAdded += Layout_ElementAdded;
			layout.ElementRemoved += Layout_ElementRemoved;

			foreach (var item in _layoutItems) ApplyStyleToLayoutItem(item);
		}

		private void UpdateLayoutItemViewTemplates()
		{
			foreach (var item in _layoutItems.Where(i => i.IsViewExists()))
				(item.View as LayoutItemView)?.UpdateTemplate();
		}

		/// <summary>
		/// Applies the theme to a <see cref="LayoutItem"/> from <see cref="LayoutItemContainerStyle"/> or
		/// <see cref="LayoutItemContainerStyleSelector"/>.
		/// </summary>
		/// <param name="layoutItem">The layout item to style.</param>
		private void ApplyStyleToLayoutItem(LayoutItem layoutItem)
		{
			layoutItem._ClearDefaultBindings();
			if (LayoutItemContainerStyle != null)
				layoutItem.Theme = LayoutItemContainerStyle;
			else if (LayoutItemContainerStyleSelector != null)
				layoutItem.Theme = LayoutItemContainerStyleSelector.SelectStyle(layoutItem.Model, layoutItem);
			else
				layoutItem.ClearValue(StyledElement.ThemeProperty);

			// A theme only applies once the item is part of a logical tree that is attached; until then the
			// setters are applied explicitly so that the item reflects them right away.
			layoutItem.ApplyStyling();
			layoutItem._SetDefaultBindings();
		}

		private void CreateAnchorableLayoutItem(LayoutAnchorable contentToAttach)
		{
			if (_layoutItems.Any(item => item.LayoutElement == contentToAttach)) return;

			var layoutItem = new LayoutAnchorableItem();
			layoutItem.Attach(contentToAttach);
			_layoutItems.Add(layoutItem);
			LogicalChildren.Add(layoutItem);
			ApplyStyleToLayoutItem(layoutItem);
		}

		private void CreateDocumentLayoutItem(LayoutDocument contentToAttach)
		{
			if (_layoutItems.Any(item => item.LayoutElement == contentToAttach)) return;

			var layoutItem = new LayoutDocumentItem();
			layoutItem.Attach(contentToAttach);
			_layoutItems.Add(layoutItem);
			LogicalChildren.Add(layoutItem);
			ApplyStyleToLayoutItem(layoutItem);
		}

		/// <summary>Gets the layout items of the current layout.</summary>
		internal IReadOnlyList<LayoutItem> LayoutItems => _layoutItems;

		#endregion Layout items

		/// <summary>Minimal observer that forwards notifications to a delegate.</summary>
		/// <typeparam name="T">The type of the notifications.</typeparam>
		private sealed class ActionObserver<T> : IObserver<T>
		{
			private readonly Action<T> _onNext;

			public ActionObserver(Action<T> onNext) => _onNext = onNext;

			public void OnCompleted()
			{
			}

			public void OnError(Exception error)
			{
			}

			public void OnNext(T value) => _onNext(value);
		}
	}
}
