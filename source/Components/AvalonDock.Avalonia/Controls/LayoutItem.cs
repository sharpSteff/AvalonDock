using System;
using System.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using AvalonDock.Commands;
using AvalonDock.Layout;

namespace AvalonDock.Controls
{
	/// <summary>
	/// Represents the layout item: the view model side of a <see cref="LayoutContent"/> that templates
	/// and <see cref="DockingManager.LayoutItemContainerStyle"/> bind against.
	/// </summary>
	/// <remarks>
	/// In WPF this is a <c>FrameworkElement</c> so that a <c>Style</c> can be applied to it. The Avalonia
	/// counterpart is a <see cref="Control"/> that the <see cref="DockingManager"/> keeps as a logical
	/// child, so a <see cref="Avalonia.Styling.ControlTheme"/> assigned through
	/// <see cref="DockingManager.LayoutItemContainerStyle"/> (or any style targeting it) is applied.
	/// It is never part of the visual tree.
	/// </remarks>
	public abstract class LayoutItem : Control
	{
		private ICommand _defaultCloseCommand;
		private ICommand _defaultFloatCommand;
		private ICommand _defaultDockAsDocumentCommand;
		private ICommand _defaultCloseAllButThisCommand;
		private ICommand _defaultCloseAllCommand;
		private ICommand _defaultActivateCommand;
		private ICommand _defaultNewVerticalTabGroupCommand;
		private ICommand _defaultNewHorizontalTabGroupCommand;
		private ICommand _defaultMoveToNextTabGroupCommand;
		private ICommand _defaultMoveToPreviousTabGroupCommand;
		private ContentControl _view = null;
		private readonly ReentrantFlag _isSelectedReentrantFlag = new ReentrantFlag();
		private readonly ReentrantFlag _isActiveReentrantFlag = new ReentrantFlag();

		/// <summary>Initializes a new instance of the <see cref="LayoutItem"/> class.</summary>
		internal LayoutItem()
		{
		}

		/// <summary>Gets the layout element.</summary>
		public LayoutContent LayoutElement { get; private set; }

		/// <summary>Gets the model (the content of <see cref="LayoutElement"/>).</summary>
		public object Model { get; private set; }

		/// <summary>
		/// Gets the view: the control that presents the content of the layout element. It is created on
		/// first use and moved between hosts when the content moves in the layout.
		/// </summary>
		public ContentControl View
		{
			get
			{
				if (_view != null) return _view;
				_view = new LayoutItemView(this);
				return _view;
			}
		}

		/// <summary><see cref="Title"/> property.</summary>
		public static readonly StyledProperty<string> TitleProperty =
			AvaloniaProperty.Register<LayoutItem, string>(nameof(Title));

		/// <summary>Gets or sets the title.</summary>
		public string Title
		{
			get => GetValue(TitleProperty);
			set => SetValue(TitleProperty, value);
		}

		/// <summary><see cref="IconSource"/> property.</summary>
		public static readonly StyledProperty<IImage> IconSourceProperty =
			AvaloniaProperty.Register<LayoutItem, IImage>(nameof(IconSource));

		/// <summary>Gets or sets the icon source.</summary>
		public IImage IconSource
		{
			get => GetValue(IconSourceProperty);
			set => SetValue(IconSourceProperty, value);
		}

		/// <summary><see cref="ContentId"/> property.</summary>
		public static readonly StyledProperty<string> ContentIdProperty =
			AvaloniaProperty.Register<LayoutItem, string>(nameof(ContentId));

		/// <summary>Gets or sets the content id used to retrieve content when deserializing layouts.</summary>
		public string ContentId
		{
			get => GetValue(ContentIdProperty);
			set => SetValue(ContentIdProperty, value);
		}

		/// <summary><see cref="IsSelected"/> property.</summary>
		public static readonly StyledProperty<bool> IsSelectedProperty =
			AvaloniaProperty.Register<LayoutItem, bool>(nameof(IsSelected));

		/// <summary>Gets or sets a value indicating whether the item is selected inside its container.</summary>
		public bool IsSelected
		{
			get => GetValue(IsSelectedProperty);
			set => SetValue(IsSelectedProperty, value);
		}

		/// <summary><see cref="IsActive"/> property.</summary>
		public static readonly StyledProperty<bool> IsActiveProperty =
			AvaloniaProperty.Register<LayoutItem, bool>(nameof(IsActive));

		/// <summary>Gets or sets a value indicating whether the item is active in the UI.</summary>
		public bool IsActive
		{
			get => GetValue(IsActiveProperty);
			set => SetValue(IsActiveProperty, value);
		}

		/// <summary><see cref="CanClose"/> property.</summary>
		public static readonly StyledProperty<bool> CanCloseProperty =
			AvaloniaProperty.Register<LayoutItem, bool>(nameof(CanClose), true);

		/// <summary>Gets or sets a value indicating whether the item can be closed.</summary>
		public bool CanClose
		{
			get => GetValue(CanCloseProperty);
			set => SetValue(CanCloseProperty, value);
		}

		/// <summary><see cref="CanFloat"/> property.</summary>
		public static readonly StyledProperty<bool> CanFloatProperty =
			AvaloniaProperty.Register<LayoutItem, bool>(nameof(CanFloat), true);

		/// <summary>Gets or sets a value indicating whether the user can drag the item into a floating window.</summary>
		public bool CanFloat
		{
			get => GetValue(CanFloatProperty);
			set => SetValue(CanFloatProperty, value);
		}

		/// <summary><see cref="CloseCommand"/> property.</summary>
		public static readonly StyledProperty<ICommand> CloseCommandProperty =
			AvaloniaProperty.Register<LayoutItem, ICommand>(nameof(CloseCommand));

		/// <summary>Gets or sets the command executed when the user clicks the close button.</summary>
		public ICommand CloseCommand
		{
			get => GetValue(CloseCommandProperty);
			set => SetValue(CloseCommandProperty, value);
		}

		/// <summary><see cref="FloatCommand"/> property.</summary>
		public static readonly StyledProperty<ICommand> FloatCommandProperty =
			AvaloniaProperty.Register<LayoutItem, ICommand>(nameof(FloatCommand));

		/// <summary>Gets or sets the command executed when the user clicks the float button.</summary>
		public ICommand FloatCommand
		{
			get => GetValue(FloatCommandProperty);
			set => SetValue(FloatCommandProperty, value);
		}

		/// <summary><see cref="DockAsDocumentCommand"/> property.</summary>
		public static readonly StyledProperty<ICommand> DockAsDocumentCommandProperty =
			AvaloniaProperty.Register<LayoutItem, ICommand>(nameof(DockAsDocumentCommand));

		/// <summary>Gets or sets the command executed when the user clicks the dock-as-document button.</summary>
		public ICommand DockAsDocumentCommand
		{
			get => GetValue(DockAsDocumentCommandProperty);
			set => SetValue(DockAsDocumentCommandProperty, value);
		}

		/// <summary><see cref="CloseAllButThisCommand"/> property.</summary>
		public static readonly StyledProperty<ICommand> CloseAllButThisCommandProperty =
			AvaloniaProperty.Register<LayoutItem, ICommand>(nameof(CloseAllButThisCommand));

		/// <summary>Gets or sets the 'Close All But This' command.</summary>
		public ICommand CloseAllButThisCommand
		{
			get => GetValue(CloseAllButThisCommandProperty);
			set => SetValue(CloseAllButThisCommandProperty, value);
		}

		/// <summary><see cref="CloseAllCommand"/> property.</summary>
		public static readonly StyledProperty<ICommand> CloseAllCommandProperty =
			AvaloniaProperty.Register<LayoutItem, ICommand>(nameof(CloseAllCommand));

		/// <summary>Gets or sets the 'Close All' command.</summary>
		public ICommand CloseAllCommand
		{
			get => GetValue(CloseAllCommandProperty);
			set => SetValue(CloseAllCommandProperty, value);
		}

		/// <summary><see cref="ActivateCommand"/> property.</summary>
		public static readonly StyledProperty<ICommand> ActivateCommandProperty =
			AvaloniaProperty.Register<LayoutItem, ICommand>(nameof(ActivateCommand));

		/// <summary>Gets or sets the command executed when the content is activated.</summary>
		public ICommand ActivateCommand
		{
			get => GetValue(ActivateCommandProperty);
			set => SetValue(ActivateCommandProperty, value);
		}

		/// <summary><see cref="NewVerticalTabGroupCommand"/> property.</summary>
		public static readonly StyledProperty<ICommand> NewVerticalTabGroupCommandProperty =
			AvaloniaProperty.Register<LayoutItem, ICommand>(nameof(NewVerticalTabGroupCommand));

		/// <summary>Gets or sets the new vertical tab group command.</summary>
		public ICommand NewVerticalTabGroupCommand
		{
			get => GetValue(NewVerticalTabGroupCommandProperty);
			set => SetValue(NewVerticalTabGroupCommandProperty, value);
		}

		/// <summary><see cref="NewHorizontalTabGroupCommand"/> property.</summary>
		public static readonly StyledProperty<ICommand> NewHorizontalTabGroupCommandProperty =
			AvaloniaProperty.Register<LayoutItem, ICommand>(nameof(NewHorizontalTabGroupCommand));

		/// <summary>Gets or sets the new horizontal tab group command.</summary>
		public ICommand NewHorizontalTabGroupCommand
		{
			get => GetValue(NewHorizontalTabGroupCommandProperty);
			set => SetValue(NewHorizontalTabGroupCommandProperty, value);
		}

		/// <summary><see cref="MoveToNextTabGroupCommand"/> property.</summary>
		public static readonly StyledProperty<ICommand> MoveToNextTabGroupCommandProperty =
			AvaloniaProperty.Register<LayoutItem, ICommand>(nameof(MoveToNextTabGroupCommand));

		/// <summary>Gets or sets the move to next tab group command.</summary>
		public ICommand MoveToNextTabGroupCommand
		{
			get => GetValue(MoveToNextTabGroupCommandProperty);
			set => SetValue(MoveToNextTabGroupCommandProperty, value);
		}

		/// <summary><see cref="MoveToPreviousTabGroupCommand"/> property.</summary>
		public static readonly StyledProperty<ICommand> MoveToPreviousTabGroupCommandProperty =
			AvaloniaProperty.Register<LayoutItem, ICommand>(nameof(MoveToPreviousTabGroupCommand));

		/// <summary>Gets or sets the move to previous tab group command.</summary>
		public ICommand MoveToPreviousTabGroupCommand
		{
			get => GetValue(MoveToPreviousTabGroupCommandProperty);
			set => SetValue(MoveToPreviousTabGroupCommandProperty, value);
		}

		/// <inheritdoc/>
		protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
		{
			base.OnPropertyChanged(change);

			if (LayoutElement == null)
			{
				return;
			}

			if (change.Property == TitleProperty)
				LayoutElement.Title = (string)change.NewValue;
			else if (change.Property == IconSourceProperty)
				LayoutElement.IconSource = (IImage)change.NewValue;
			else if (change.Property == ContentIdProperty)
				LayoutElement.ContentId = (string)change.NewValue;
			else if (change.Property == IsSelectedProperty)
				OnIsSelectedChanged((bool)change.NewValue);
			else if (change.Property == IsActiveProperty)
				OnIsActiveChanged((bool)change.NewValue);
			else if (change.Property == CanCloseProperty)
				LayoutElement.CanClose = (bool)change.NewValue;
			else if (change.Property == CanFloatProperty)
				LayoutElement.CanFloat = (bool)change.NewValue;
			else if (change.Property == IsVisibleProperty)
				OnVisibilityChanged();
			else if (change.Property == ToolTip.TipProperty)
				LayoutElement.ToolTip = change.NewValue;
		}

		private void OnIsSelectedChanged(bool newValue)
		{
			if (!_isSelectedReentrantFlag.CanEnter) return;
			using (_isSelectedReentrantFlag.Enter())
			{
				if (LayoutElement != null) LayoutElement.IsSelected = newValue;
			}
		}

		private void OnIsActiveChanged(bool newValue)
		{
			if (!_isActiveReentrantFlag.CanEnter) return;
			using (_isActiveReentrantFlag.Enter())
			{
				if (LayoutElement != null) LayoutElement.IsActive = newValue;
			}
		}

		private bool CanExecuteCloseCommand(object parameter) => LayoutElement != null && LayoutElement.CanClose;

		private void ExecuteCloseCommand(object parameter) => Close();

		/// <summary>Closes the layout element.</summary>
		protected abstract void Close();

		private bool CanExecuteFloatCommand(object anchorable) =>
			LayoutElement != null
			&& LayoutElement.CanFloat
			&& LayoutElement.Root?.Manager?.AllowFloatingWindows != false
			&& LayoutElement.FindParent<LayoutFloatingWindow>() == null;

		private void ExecuteFloatCommand(object parameter) => LayoutElement.Root.Manager.ExecuteFloatCommand(LayoutElement);

		/// <summary>Determines whether the dock as document command can execute.</summary>
		/// <returns><see langword="true"/> if the command can execute.</returns>
		protected virtual bool CanExecuteDockAsDocumentCommand() => LayoutElement != null && LayoutElement.FindParent<LayoutDocumentPane>() == null;

		private bool CanExecuteDockAsDocumentCommand(object parameter) => CanExecuteDockAsDocumentCommand();

		private void ExecuteDockAsDocumentCommand(object parameter) => LayoutElement.Root.Manager.ExecuteDockAsDocumentCommand(LayoutElement);

		private bool CanExecuteCloseAllButThisCommand(object parameter)
		{
			var root = LayoutElement?.Root;
			if (root?.Manager?.Layout == null) return false;
			return root.Manager.Layout.Descendents().OfType<LayoutContent>().Any(d => d != LayoutElement && (d.Parent is LayoutDocumentPane || d.Parent is LayoutDocumentFloatingWindow));
		}

		private void ExecuteCloseAllButThisCommand(object parameter) => LayoutElement.Root.Manager.ExecuteCloseAllButThisCommand(LayoutElement);

		private bool CanExecuteCloseAllCommand(object parameter)
		{
			var root = LayoutElement?.Root;
			if (root?.Manager?.Layout == null) return false;
			return root.Manager.Layout.Descendents().OfType<LayoutContent>().Any(d => d.Parent is LayoutDocumentPane || d.Parent is LayoutDocumentFloatingWindow);
		}

		private void ExecuteCloseAllCommand(object parameter) => LayoutElement.Root.Manager.ExecuteCloseAllCommand(LayoutElement);

		private bool CanExecuteActivateCommand(object parameter) => LayoutElement != null;

		private void ExecuteActivateCommand(object parameter) => LayoutElement.Root.Manager.ExecuteContentActivateCommand(LayoutElement);

		private bool CanExecuteNewVerticalTabGroupCommand(object parameter)
		{
			if (LayoutElement == null) return false;
			if (LayoutElement is LayoutDocument layoutDocument && !layoutDocument.CanMove) return false;
			var parentDocumentGroup = LayoutElement.FindParent<LayoutDocumentPaneGroup>();
			return (parentDocumentGroup == null ||
					  parentDocumentGroup.ChildrenCount == 1 ||
					  parentDocumentGroup.Root.Manager.AllowMixedOrientation ||
					  parentDocumentGroup.Orientation == Orientation.Horizontal) &&
					 LayoutElement.Parent is LayoutDocumentPane parentDocumentPane &&
					 parentDocumentPane.ChildrenCount > 1;
		}

		private void ExecuteNewVerticalTabGroupCommand(object parameter) => NewTabGroup(Orientation.Horizontal);

		private bool CanExecuteNewHorizontalTabGroupCommand(object parameter)
		{
			if (LayoutElement == null) return false;
			if (LayoutElement is LayoutDocument layoutDocument && !layoutDocument.CanMove) return false;
			var parentDocumentGroup = LayoutElement.FindParent<LayoutDocumentPaneGroup>();
			return (parentDocumentGroup == null ||
					  parentDocumentGroup.ChildrenCount == 1 ||
					  parentDocumentGroup.Root.Manager.AllowMixedOrientation ||
					  parentDocumentGroup.Orientation == Orientation.Vertical) &&
					 LayoutElement.Parent is LayoutDocumentPane parentDocumentPane &&
					 parentDocumentPane.ChildrenCount > 1;
		}

		private void ExecuteNewHorizontalTabGroupCommand(object parameter) => NewTabGroup(Orientation.Vertical);

		/// <summary>Moves the layout element into a new document pane next to its current one.</summary>
		/// <param name="orientation">The orientation of the document pane group that holds both panes.</param>
		private void NewTabGroup(Orientation orientation)
		{
			var layoutElement = LayoutElement;
			var parentDocumentGroup = layoutElement.FindParent<LayoutDocumentPaneGroup>();
			var parentDocumentPane = layoutElement.Parent as LayoutDocumentPane;

			if (parentDocumentGroup == null)
			{
				var grandParent = parentDocumentPane.Parent;
				parentDocumentGroup = new LayoutDocumentPaneGroup { Orientation = orientation };
				grandParent.ReplaceChild(parentDocumentPane, parentDocumentGroup);
				parentDocumentGroup.Children.Add(parentDocumentPane);
			}

			parentDocumentGroup.Orientation = orientation;
			var indexOfParentPane = parentDocumentGroup.IndexOfChild(parentDocumentPane);
			parentDocumentGroup.InsertChildAt(indexOfParentPane + 1, new LayoutDocumentPane(layoutElement));
			layoutElement.IsActive = true;
			layoutElement.Root.CollectGarbage();
		}

		private bool CanExecuteMoveToNextTabGroupCommand(object parameter)
		{
			if (LayoutElement == null) return false;
			var parentDocumentGroup = LayoutElement.FindParent<LayoutDocumentPaneGroup>();
			return parentDocumentGroup != null &&
					 LayoutElement.Parent is LayoutDocumentPane parentDocumentPane &&
					 parentDocumentGroup.ChildrenCount > 1 &&
					 parentDocumentGroup.IndexOfChild(parentDocumentPane) < parentDocumentGroup.ChildrenCount - 1 &&
					 parentDocumentGroup.Children[parentDocumentGroup.IndexOfChild(parentDocumentPane) + 1] is LayoutDocumentPane;
		}

		private void ExecuteMoveToNextTabGroupCommand(object parameter) => MoveToTabGroup(+1);

		private bool CanExecuteMoveToPreviousTabGroupCommand(object parameter)
		{
			if (LayoutElement == null) return false;
			var parentDocumentGroup = LayoutElement.FindParent<LayoutDocumentPaneGroup>();
			return parentDocumentGroup != null &&
					 LayoutElement.Parent is LayoutDocumentPane parentDocumentPane &&
					 parentDocumentGroup.ChildrenCount > 1 &&
					 parentDocumentGroup.IndexOfChild(parentDocumentPane) > 0 &&
					 parentDocumentGroup.Children[parentDocumentGroup.IndexOfChild(parentDocumentPane) - 1] is LayoutDocumentPane;
		}

		private void ExecuteMoveToPreviousTabGroupCommand(object parameter) => MoveToTabGroup(-1);

		private void MoveToTabGroup(int offset)
		{
			var layoutElement = LayoutElement;
			var parentDocumentGroup = layoutElement.FindParent<LayoutDocumentPaneGroup>();
			var parentDocumentPane = layoutElement.Parent as LayoutDocumentPane;
			var indexOfParentPane = parentDocumentGroup.IndexOfChild(parentDocumentPane);
			var targetDocumentPane = parentDocumentGroup.Children[indexOfParentPane + offset] as LayoutDocumentPane;
			targetDocumentPane.InsertChildAt(0, layoutElement);
			layoutElement.IsActive = true;
			layoutElement.Root.CollectGarbage();
		}

		/// <summary>Creates the default commands.</summary>
		protected virtual void InitDefaultCommands()
		{
			_defaultCloseCommand = new RelayCommand<object>(ExecuteCloseCommand, CanExecuteCloseCommand);
			_defaultFloatCommand = new RelayCommand<object>(ExecuteFloatCommand, CanExecuteFloatCommand);
			_defaultDockAsDocumentCommand = new RelayCommand<object>(ExecuteDockAsDocumentCommand, CanExecuteDockAsDocumentCommand);
			_defaultCloseAllButThisCommand = new RelayCommand<object>(ExecuteCloseAllButThisCommand, CanExecuteCloseAllButThisCommand);
			_defaultCloseAllCommand = new RelayCommand<object>(ExecuteCloseAllCommand, CanExecuteCloseAllCommand);
			_defaultActivateCommand = new RelayCommand<object>(ExecuteActivateCommand, CanExecuteActivateCommand);
			_defaultNewVerticalTabGroupCommand = new RelayCommand<object>(ExecuteNewVerticalTabGroupCommand, CanExecuteNewVerticalTabGroupCommand);
			_defaultNewHorizontalTabGroupCommand = new RelayCommand<object>(ExecuteNewHorizontalTabGroupCommand, CanExecuteNewHorizontalTabGroupCommand);
			_defaultMoveToNextTabGroupCommand = new RelayCommand<object>(ExecuteMoveToNextTabGroupCommand, CanExecuteMoveToNextTabGroupCommand);
			_defaultMoveToPreviousTabGroupCommand = new RelayCommand<object>(ExecuteMoveToPreviousTabGroupCommand, CanExecuteMoveToPreviousTabGroupCommand);
		}

		/// <summary>Clears the default commands so that a container style can supply its own.</summary>
		protected virtual void ClearDefaultBindings()
		{
			ClearIfDefault(CloseCommandProperty, _defaultCloseCommand);
			ClearIfDefault(FloatCommandProperty, _defaultFloatCommand);
			ClearIfDefault(DockAsDocumentCommandProperty, _defaultDockAsDocumentCommand);
			ClearIfDefault(CloseAllButThisCommandProperty, _defaultCloseAllButThisCommand);
			ClearIfDefault(CloseAllCommandProperty, _defaultCloseAllCommand);
			ClearIfDefault(ActivateCommandProperty, _defaultActivateCommand);
			ClearIfDefault(NewVerticalTabGroupCommandProperty, _defaultNewVerticalTabGroupCommand);
			ClearIfDefault(NewHorizontalTabGroupCommandProperty, _defaultNewHorizontalTabGroupCommand);
			ClearIfDefault(MoveToNextTabGroupCommandProperty, _defaultMoveToNextTabGroupCommand);
			ClearIfDefault(MoveToPreviousTabGroupCommandProperty, _defaultMoveToPreviousTabGroupCommand);
		}

		/// <summary>Clears <paramref name="property"/> when it still holds the default command.</summary>
		/// <param name="property">The command property.</param>
		/// <param name="defaultCommand">The default command for that property.</param>
		protected void ClearIfDefault(StyledProperty<ICommand> property, ICommand defaultCommand)
		{
			if (ReferenceEquals(GetValue(property), defaultCommand)) ClearValue(property);
		}

		/// <summary>Sets the default commands for every command a container style left unset.</summary>
		protected virtual void SetDefaultBindings()
		{
			SetIfUnset(CloseCommandProperty, _defaultCloseCommand);
			SetIfUnset(FloatCommandProperty, _defaultFloatCommand);
			SetIfUnset(DockAsDocumentCommandProperty, _defaultDockAsDocumentCommand);
			SetIfUnset(CloseAllButThisCommandProperty, _defaultCloseAllButThisCommand);
			SetIfUnset(CloseAllCommandProperty, _defaultCloseAllCommand);
			SetIfUnset(ActivateCommandProperty, _defaultActivateCommand);
			SetIfUnset(NewVerticalTabGroupCommandProperty, _defaultNewVerticalTabGroupCommand);
			SetIfUnset(NewHorizontalTabGroupCommandProperty, _defaultNewHorizontalTabGroupCommand);
			SetIfUnset(MoveToNextTabGroupCommandProperty, _defaultMoveToNextTabGroupCommand);
			SetIfUnset(MoveToPreviousTabGroupCommandProperty, _defaultMoveToPreviousTabGroupCommand);

			IsSelected = LayoutElement.IsSelected;
			IsActive = LayoutElement.IsActive;

			// Seeded with SetCurrentValue rather than a local value so a style setter for CanClose still wins.
			SetCurrentValue(CanCloseProperty, LayoutElement.CanClose);
		}

		/// <summary>Sets <paramref name="property"/> to <paramref name="defaultCommand"/> when it holds no command.</summary>
		/// <param name="property">The command property.</param>
		/// <param name="defaultCommand">The default command for that property.</param>
		protected void SetIfUnset(StyledProperty<ICommand> property, ICommand defaultCommand)
		{
			if (GetValue(property) == null) SetCurrentValue(property, defaultCommand);
		}

		/// <summary>Called when <see cref="Visual.IsVisible"/> changes.</summary>
		protected virtual void OnVisibilityChanged()
		{
		}

		/// <summary>Attaches the item to the given content.</summary>
		/// <param name="model">The layout model.</param>
		internal virtual void Attach(LayoutContent model)
		{
			LayoutElement = model;
			Model = model.Content;
			InitDefaultCommands();
			LayoutElement.IsSelectedChanged += LayoutElement_IsSelectedChanged;
			LayoutElement.IsActiveChanged += LayoutElement_IsActiveChanged;
			DataContext = this;
		}

		/// <summary>Detaches the item from its content.</summary>
		internal virtual void Detach()
		{
			LayoutElement.IsSelectedChanged -= LayoutElement_IsSelectedChanged;
			LayoutElement.IsActiveChanged -= LayoutElement_IsActiveChanged;
			LayoutElement = null;
			Model = null;
			if (_view is LayoutItemView view) view.Release();
		}

		/// <summary>Clears the default commands.</summary>
		internal void _ClearDefaultBindings() => ClearDefaultBindings();

		/// <summary>Sets the default commands.</summary>
		internal void _SetDefaultBindings() => SetDefaultBindings();

		/// <summary>Gets a value indicating whether the view has been created.</summary>
		/// <returns><see langword="true"/> when <see cref="View"/> has been created.</returns>
		internal bool IsViewExists() => _view != null;

		private void LayoutElement_IsActiveChanged(object sender, EventArgs e)
		{
			if (!_isActiveReentrantFlag.CanEnter) return;
			using (_isActiveReentrantFlag.Enter())
			{
				IsActive = LayoutElement.IsActive;
			}
		}

		private void LayoutElement_IsSelectedChanged(object sender, EventArgs e)
		{
			if (!_isSelectedReentrantFlag.CanEnter) return;
			using (_isSelectedReentrantFlag.Enter())
			{
				IsSelected = LayoutElement.IsSelected;
			}
		}
	}
}
