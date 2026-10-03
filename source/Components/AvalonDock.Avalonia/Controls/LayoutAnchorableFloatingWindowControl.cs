using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using AvalonDock.Commands;
using AvalonDock.Layout;

namespace AvalonDock.Controls
{
	/// <summary>Represents the floating window that hosts a <see cref="LayoutAnchorableFloatingWindow"/>.</summary>
	public class LayoutAnchorableFloatingWindowControl : LayoutFloatingWindowControl, IOverlayWindowHost
	{
		private readonly LayoutAnchorableFloatingWindow _model;
		private OverlayWindow _overlayWindow = null;
		private List<IDropArea> _dropAreas = null;
		private bool _bindingsEnabled;

		/// <summary>Initializes a new instance of the <see cref="LayoutAnchorableFloatingWindowControl"/> class.</summary>
		/// <param name="model">The model.</param>
		/// <param name="isContentImmutable">Whether the content may change while floating.</param>
		internal LayoutAnchorableFloatingWindowControl(LayoutAnchorableFloatingWindow model, bool isContentImmutable)
			: base(model, isContentImmutable)
		{
			_model = model;
			HideWindowCommand = new RelayCommand<object>(OnExecuteHideWindowCommand, CanExecuteHideWindowCommand);
			CloseWindowCommand = new RelayCommand<object>(OnExecuteCloseWindowCommand, CanExecuteCloseWindowCommand);
			UpdateMinSize();

			// The window reaches the screen through a deferred operation, so its model can have left the
			// layout before this runs. There is no manager to build the content from then.
			var manager = _model.Root?.Manager;
			if (manager != null) Content = manager.CreateUIElementForModel(_model.RootPanel);
			EnableBindings();
			_model.IsVisibleChanged += Model_IsVisibleChanged;
			Activated += OnActivated;
			Deactivated += OnDeactivated;
			UpdateSingleContent();
		}

		/// <summary>Initializes a new instance of the <see cref="LayoutAnchorableFloatingWindowControl"/> class.</summary>
		/// <param name="model">The model.</param>
		internal LayoutAnchorableFloatingWindowControl(LayoutAnchorableFloatingWindow model)
			: this(model, false)
		{
		}

		/// <inheritdoc/>
		public override ILayoutElement Model => _model;

		/// <summary>Gets the command that hides every anchorable of the window.</summary>
		public ICommand HideWindowCommand { get; }

		/// <summary>Gets the command that closes every anchorable of the window.</summary>
		public ICommand CloseWindowCommand { get; }

		/// <summary>Gets the command that the close button of the caption executes: close when the content can be closed, hide otherwise.</summary>
		public ICommand CaptionCloseCommand => SingleContent?.CanClose == true ? CloseWindowCommand : HideWindowCommand;

		/// <inheritdoc/>
		DockingManager IOverlayWindowHost.Manager => _model.Root?.Manager;

		/// <summary>Subscribes to the model; undone by <see cref="DisableBindings"/> while the manager is unloaded.</summary>
		public void EnableBindings()
		{
			if (_bindingsEnabled) return;
			_bindingsEnabled = true;
			_model.PropertyChanged += Model_PropertyChanged;
			if (_model.Root is LayoutRoot layoutRoot) layoutRoot.Updated += OnRootUpdated;
		}

		/// <summary>Unsubscribes from the model.</summary>
		public void DisableBindings()
		{
			if (!_bindingsEnabled) return;
			_bindingsEnabled = false;
			if (_model.Root is LayoutRoot layoutRoot) layoutRoot.Updated -= OnRootUpdated;
			_model.PropertyChanged -= Model_PropertyChanged;
		}

		/// <inheritdoc/>
		bool IOverlayWindowHost.HitTestScreen(Point dragPoint) => IsVisible && this.GetScreenArea().Contains(dragPoint);

		/// <inheritdoc/>
		IOverlayWindow IOverlayWindowHost.ShowOverlayWindow(LayoutFloatingWindowControl draggingWindow)
		{
			var manager = _model.Root?.Manager;
			if (manager == null) return null;
			_overlayWindow ??= new OverlayWindow(this);
			_overlayWindow.ShowOver(this, manager.OverlayWindowMode, draggingWindow?.OwnedByDockingManagerWindow == false ? null : GetManagerWindow());
			return _overlayWindow;
		}

		/// <inheritdoc/>
		void IOverlayWindowHost.HideOverlayWindow()
		{
			_dropAreas = null;

			// Hidden and kept for the next drag rather than closed, so that an interrupted drag cannot pile up
			// empty windows (issue #587). It is closed together with this window in OnClosed.
			_overlayWindow?.HideOverlay();
		}

		/// <inheritdoc/>
		IEnumerable<IDropArea> IOverlayWindowHost.GetDropAreas(LayoutFloatingWindowControl draggingWindow)
		{
			if (_dropAreas != null) return _dropAreas;
			_dropAreas = new List<IDropArea>();
			if (draggingWindow.Model is LayoutDocumentFloatingWindow) return _dropAreas;
			if (Content is not Visual rootVisual) return _dropAreas;
			foreach (var areaHost in rootVisual.FindVisualChildren<LayoutAnchorablePaneControl>())
				_dropAreas.Add(new DropArea<LayoutAnchorablePaneControl>(areaHost, DropAreaType.AnchorablePane));
			foreach (var areaHost in rootVisual.FindVisualChildren<LayoutDocumentPaneControl>())
				_dropAreas.Add(new DropArea<LayoutDocumentPaneControl>(areaHost, DropAreaType.DocumentPane));
			return _dropAreas;
		}

		/// <inheritdoc/>
		protected override void OnClosing(WindowClosingEventArgs e)
		{
			// A close that does not come from the docking code (the platform's close button, Alt+F4, the task
			// bar) goes through the same hide/close logic as the close button of the caption.
			if (CloseInitiatedByUser && !KeepContentVisibleOnClose)
			{
				e.Cancel = true;
				if (HideWindowCommand.CanExecute(null)) HideWindowCommand.Execute(null);
				else if (CloseWindowCommand.CanExecute(null)) CloseWindowCommand.Execute(null);
				return;
			}

			base.OnClosing(e);
		}

		/// <inheritdoc/>
		protected override void OnClosed(EventArgs e)
		{
			var root = Model?.Root;
			if (root != null)
			{
				if (root is LayoutRoot layoutRoot) layoutRoot.Updated -= OnRootUpdated;
				root.Manager?.RemoveFloatingWindow(this);
				root.CollectGarbage();
			}

			_overlayWindow?.Close();
			_overlayWindow = null;

			base.OnClosed(e);
			if (!CloseInitiatedByUser) root?.FloatingWindows.Remove(_model);
			_model.PropertyChanged -= Model_PropertyChanged;
			_model.IsVisibleChanged -= Model_IsVisibleChanged;
			Activated -= OnActivated;
			Deactivated -= OnDeactivated;
		}

		/// <inheritdoc/>
		internal override void UpdateThemeResources(Themes.Theme oldTheme = null)
		{
			base.UpdateThemeResources(oldTheme);
		}

		private void OnActivated(object sender, EventArgs e)
		{
			if (_model.IsSinglePane) LayoutFloatingWindowControlHelper.ActiveTheContentOfSinglePane(this, true);
			else LayoutFloatingWindowControlHelper.ActiveTheContentOfMultiPane(this, true);
		}

		private void OnDeactivated(object sender, EventArgs e)
		{
			if (_model.IsSinglePane) LayoutFloatingWindowControlHelper.ActiveTheContentOfSinglePane(this, false);
		}

		private void UpdateMinSize()
		{
			if (_model?.RootPanel == null) return;
			MinWidth = _model.RootPanel.CalculatedDockMinWidth();
			MinHeight = _model.RootPanel.CalculatedDockMinHeight();
		}

		private void OnRootUpdated(object sender, EventArgs e)
		{
			UpdateMinSize();
			UpdateSingleContent();
		}

		private void Model_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				case nameof(LayoutAnchorableFloatingWindow.RootPanel):
					if (_model.RootPanel == null) InternalClose();
					break;

				case nameof(LayoutAnchorableFloatingWindow.IsVisible):
					if (_model.IsVisible != IsVisible)
					{
						if (_model.IsVisible) ShowOwned();
						else Hide();
					}

					break;

				case nameof(LayoutAnchorableFloatingWindow.IsSinglePane):
				case nameof(LayoutAnchorableFloatingWindow.SinglePane):
					UpdateSingleContent();
					break;
			}
		}

		private void Model_IsVisibleChanged(object sender, EventArgs e)
		{
			if (!IsVisible && _model.IsVisible && !IsClosingOrClosed) ShowOwned();
		}

		private bool CanExecuteHideWindowCommand(object parameter)
		{
			var manager = Model?.Root?.Manager;
			if (manager == null) return false;

			var canExecute = false;
			foreach (var anchorable in Model.Descendents().OfType<LayoutAnchorable>().ToArray())
			{
				if (!anchorable.CanHide) return false;
				var anchorableLayoutItem = manager.GetLayoutItemFromModel(anchorable) as LayoutAnchorableItem;
				if (anchorableLayoutItem?.HideCommand == null || !anchorableLayoutItem.HideCommand.CanExecute(parameter)) return false;
				canExecute = true;
			}

			return canExecute;
		}

		private void OnExecuteHideWindowCommand(object parameter)
		{
			var manager = Model.Root.Manager;
			foreach (var anchorable in Model.Descendents().OfType<LayoutAnchorable>().ToArray())
			{
				var anchorableLayoutItem = manager.GetLayoutItemFromModel(anchorable) as LayoutAnchorableItem;
				anchorableLayoutItem?.HideCommand.Execute(parameter);
			}

			Hide();
		}

		private bool CanExecuteCloseWindowCommand(object parameter)
		{
			var manager = Model?.Root?.Manager;
			if (manager == null) return false;

			var canExecute = false;
			foreach (var anchorable in Model.Descendents().OfType<LayoutAnchorable>().ToArray())
			{
				if (!anchorable.CanClose) return false;
				var anchorableLayoutItem = manager.GetLayoutItemFromModel(anchorable) as LayoutAnchorableItem;
				if (anchorableLayoutItem?.CloseCommand == null || !anchorableLayoutItem.CloseCommand.CanExecute(parameter)) return false;
				canExecute = true;
			}

			return canExecute;
		}

		private void OnExecuteCloseWindowCommand(object parameter)
		{
			var manager = Model.Root.Manager;
			foreach (var anchorable in Model.Descendents().OfType<LayoutAnchorable>().ToArray())
			{
				var anchorableLayoutItem = manager.GetLayoutItemFromModel(anchorable) as LayoutAnchorableItem;
				anchorableLayoutItem?.CloseCommand.Execute(parameter);
			}
		}
	}
}
