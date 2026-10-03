using System;
using System.Windows.Input;
using Avalonia;
using AvalonDock.Commands;
using AvalonDock.Layout;

namespace AvalonDock.Controls
{
	/// <summary>Represents the <see cref="LayoutItem"/> of a <see cref="LayoutAnchorable"/>.</summary>
	public class LayoutAnchorableItem : LayoutItem
	{
		private LayoutAnchorable _anchorable;
		private ICommand _defaultHideCommand;
		private ICommand _defaultAutoHideCommand;
		private ICommand _defaultDockCommand;
		private ICommand _defaultDetachToWindowCommand;
		private readonly ReentrantFlag _visibilityReentrantFlag = new ReentrantFlag();
		private readonly ReentrantFlag _anchorableVisibilityReentrantFlag = new ReentrantFlag();

		static LayoutAnchorableItem()
		{
			// #182: LayoutAnchorable initializes with CanClose == false, so the item does too.
			CanCloseProperty.OverrideDefaultValue<LayoutAnchorableItem>(false);
		}

		/// <summary>Initializes a new instance of the <see cref="LayoutAnchorableItem"/> class.</summary>
		internal LayoutAnchorableItem()
		{
		}

		/// <summary><see cref="HideCommand"/> property.</summary>
		public static readonly StyledProperty<ICommand> HideCommandProperty =
			AvaloniaProperty.Register<LayoutAnchorableItem, ICommand>(nameof(HideCommand));

		/// <summary>Gets or sets the command executed when the anchorable is hidden.</summary>
		public ICommand HideCommand
		{
			get => GetValue(HideCommandProperty);
			set => SetValue(HideCommandProperty, value);
		}

		/// <summary><see cref="DetachToWindowCommand"/> property.</summary>
		public static readonly StyledProperty<ICommand> DetachToWindowCommandProperty =
			AvaloniaProperty.Register<LayoutAnchorableItem, ICommand>(nameof(DetachToWindowCommand));

		/// <summary>Gets or sets the command executed when the anchorable is moved into a standalone window.</summary>
		public ICommand DetachToWindowCommand
		{
			get => GetValue(DetachToWindowCommandProperty);
			set => SetValue(DetachToWindowCommandProperty, value);
		}

		/// <summary><see cref="AutoHideCommand"/> property.</summary>
		public static readonly StyledProperty<ICommand> AutoHideCommandProperty =
			AvaloniaProperty.Register<LayoutAnchorableItem, ICommand>(nameof(AutoHideCommand));

		/// <summary>Gets or sets the command executed when the user clicks the auto hide button.</summary>
		public ICommand AutoHideCommand
		{
			get => GetValue(AutoHideCommandProperty);
			set => SetValue(AutoHideCommandProperty, value);
		}

		/// <summary><see cref="DockCommand"/> property.</summary>
		public static readonly StyledProperty<ICommand> DockCommandProperty =
			AvaloniaProperty.Register<LayoutAnchorableItem, ICommand>(nameof(DockCommand));

		/// <summary>Gets or sets the command executed when the user clicks the Dock button.</summary>
		public ICommand DockCommand
		{
			get => GetValue(DockCommandProperty);
			set => SetValue(DockCommandProperty, value);
		}

		/// <summary><see cref="CanHide"/> property.</summary>
		public static readonly StyledProperty<bool> CanHideProperty =
			AvaloniaProperty.Register<LayoutAnchorableItem, bool>(nameof(CanHide), true);

		/// <summary>Gets or sets a value indicating whether the user can hide the anchorable.</summary>
		public bool CanHide
		{
			get => GetValue(CanHideProperty);
			set => SetValue(CanHideProperty, value);
		}

		/// <summary><see cref="CanMove"/> property.</summary>
		public static readonly StyledProperty<bool> CanMoveProperty =
			AvaloniaProperty.Register<LayoutAnchorableItem, bool>(nameof(CanMove), true);

		/// <summary>Gets or sets a value indicating whether the user can move the anchorable.</summary>
		public bool CanMove
		{
			get => GetValue(CanMoveProperty);
			set => SetValue(CanMoveProperty, value);
		}

		/// <inheritdoc/>
		protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
		{
			base.OnPropertyChanged(change);
			if (_anchorable == null) return;
			if (change.Property == CanHideProperty) _anchorable.CanHide = (bool)change.NewValue;
			else if (change.Property == CanMoveProperty) _anchorable.CanMove = (bool)change.NewValue;
		}

		private bool CanExecuteHideCommand(object parameter) => LayoutElement != null && _anchorable.CanHide;

		private void ExecuteHideCommand(object parameter) => _anchorable?.Root?.Manager?.ExecuteHideCommand(_anchorable);

		private bool CanExecuteDetachToWindowCommand(object parameter) =>
			LayoutElement != null
			&& _anchorable?.Root?.Manager != null
			&& _anchorable.Root.Manager.AllowDetachedWindows
			&& _anchorable.CanFloat;

		private void ExecuteDetachToWindowCommand(object parameter)
		{
			var manager = _anchorable?.Root?.Manager;
			if (manager == null) return;

			if (manager.IsDetached(_anchorable))
				manager.ReattachAnchorable(_anchorable);
			else
				manager.DetachAnchorableToWindow(_anchorable);
		}

		private bool CanExecuteAutoHideCommand(object parameter)
		{
			if (LayoutElement == null) return false;
			if (LayoutElement.FindParent<LayoutAnchorableFloatingWindow>() != null) return false; // is floating
			return _anchorable.CanAutoHide;
		}

		private void ExecuteAutoHideCommand(object parameter) => _anchorable?.Root?.Manager?.ExecuteAutoHideCommand(_anchorable);

		private bool CanExecuteDockCommand(object parameter) => LayoutElement?.FindParent<LayoutAnchorableFloatingWindow>() != null;

		private void ExecuteDockCommand(object parameter) => LayoutElement.Root.Manager.ExecuteDockCommand(_anchorable);

		/// <inheritdoc/>
		internal override void Attach(LayoutContent model)
		{
			_anchorable = model as LayoutAnchorable;
			_anchorable.IsVisibleChanged += Anchorable_IsVisibleChanged;
			base.Attach(model);
		}

		/// <inheritdoc/>
		internal override void Detach()
		{
			_anchorable.IsVisibleChanged -= Anchorable_IsVisibleChanged;
			_anchorable = null;
			base.Detach();
		}

		/// <inheritdoc/>
		protected override bool CanExecuteDockAsDocumentCommand()
		{
			var canExecute = base.CanExecuteDockAsDocumentCommand();
			if (canExecute && _anchorable != null) return _anchorable.CanDockAsTabbedDocument;
			return canExecute;
		}

		/// <inheritdoc/>
		protected override void Close()
		{
			if (_anchorable.Root?.Manager == null) return;
			var dockingManager = _anchorable.Root.Manager;
			dockingManager.ExecuteCloseCommand(_anchorable);
		}

		/// <inheritdoc/>
		protected override void InitDefaultCommands()
		{
			_defaultHideCommand = new RelayCommand<object>(ExecuteHideCommand, CanExecuteHideCommand);
			_defaultAutoHideCommand = new RelayCommand<object>(ExecuteAutoHideCommand, CanExecuteAutoHideCommand);
			_defaultDockCommand = new RelayCommand<object>(ExecuteDockCommand, CanExecuteDockCommand);
			_defaultDetachToWindowCommand = new RelayCommand<object>(ExecuteDetachToWindowCommand, CanExecuteDetachToWindowCommand);
			base.InitDefaultCommands();
		}

		/// <inheritdoc/>
		protected override void ClearDefaultBindings()
		{
			ClearIfDefault(HideCommandProperty, _defaultHideCommand);
			ClearIfDefault(AutoHideCommandProperty, _defaultAutoHideCommand);
			ClearIfDefault(DockCommandProperty, _defaultDockCommand);
			ClearIfDefault(DetachToWindowCommandProperty, _defaultDetachToWindowCommand);
			base.ClearDefaultBindings();
		}

		/// <inheritdoc/>
		protected override void SetDefaultBindings()
		{
			SetIfUnset(HideCommandProperty, _defaultHideCommand);
			SetIfUnset(AutoHideCommandProperty, _defaultAutoHideCommand);
			SetIfUnset(DockCommandProperty, _defaultDockCommand);
			SetIfUnset(DetachToWindowCommandProperty, _defaultDetachToWindowCommand);
			using (_visibilityReentrantFlag.Enter())
			{
				SetCurrentValue(IsVisibleProperty, _anchorable.IsVisible);
			}

			base.SetDefaultBindings();
		}

		/// <inheritdoc/>
		protected override void OnVisibilityChanged()
		{
			if (_anchorable?.Root != null && _visibilityReentrantFlag.CanEnter)
			{
				using (_visibilityReentrantFlag.Enter())
				{
					if (IsVisible) _anchorable.Show();
					else _anchorable.HideAnchorable(false);
				}
			}

			base.OnVisibilityChanged();
		}

		private void Anchorable_IsVisibleChanged(object sender, EventArgs e)
		{
			if (_anchorable?.Root == null || !_anchorableVisibilityReentrantFlag.CanEnter) return;
			using (_anchorableVisibilityReentrantFlag.Enter())
			{
				SetCurrentValue(IsVisibleProperty, _anchorable.IsVisible);
			}
		}
	}
}
