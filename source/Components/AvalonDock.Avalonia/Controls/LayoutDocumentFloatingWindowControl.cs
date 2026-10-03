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
	/// <summary>Represents the floating window that hosts a <see cref="LayoutDocumentFloatingWindow"/>.</summary>
	public class LayoutDocumentFloatingWindowControl : LayoutFloatingWindowControl, IOverlayWindowHost
	{
		private readonly LayoutDocumentFloatingWindow _model;
		private List<IDropArea> _dropAreas = null;
		private OverlayWindow _overlayWindow = null;
		private bool _bindingsEnabled;

		/// <summary>Initializes a new instance of the <see cref="LayoutDocumentFloatingWindowControl"/> class.</summary>
		/// <param name="model">The model.</param>
		/// <param name="isContentImmutable">Whether the content may change while floating.</param>
		internal LayoutDocumentFloatingWindowControl(LayoutDocumentFloatingWindow model, bool isContentImmutable)
			: base(model, isContentImmutable)
		{
			_model = model;
			HideWindowCommand = new RelayCommand<object>(OnExecuteHideWindowCommand, CanExecuteHideWindowCommand);
			CloseWindowCommand = new RelayCommand<object>(OnExecuteCloseWindowCommand, CanExecuteCloseWindowCommand);
			var manager = _model.Root?.Manager;
			if (manager != null) Content = manager.CreateUIElementForModel(_model.RootPanel);
			if (_model.RootPanel != null) _model.RootPanel.ChildrenCollectionChanged += RootPanelOnChildrenCollectionChanged;
			EnableBindings();
			Activated += OnActivated;
			Deactivated += OnDeactivated;
			UpdateSingleContent();
		}

		/// <summary>Initializes a new instance of the <see cref="LayoutDocumentFloatingWindowControl"/> class.</summary>
		/// <param name="model">The model.</param>
		internal LayoutDocumentFloatingWindowControl(LayoutDocumentFloatingWindow model)
			: this(model, false)
		{
		}

		/// <inheritdoc/>
		public override ILayoutElement Model => _model;

		/// <summary>Gets the command that hides (closes) every document of the window.</summary>
		public ICommand HideWindowCommand { get; }

		/// <summary>Gets the command that closes every document of the window.</summary>
		public ICommand CloseWindowCommand { get; }

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
			_model.PropertyChanged -= Model_PropertyChanged;
			if (_model.Root is LayoutRoot layoutRoot) layoutRoot.Updated -= OnRootUpdated;
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
			_overlayWindow?.HideOverlay();
		}

		/// <inheritdoc/>
		IEnumerable<IDropArea> IOverlayWindowHost.GetDropAreas(LayoutFloatingWindowControl draggingWindow)
		{
			if (_dropAreas != null) return _dropAreas;
			_dropAreas = new List<IDropArea>();
			var isDraggingDocuments = draggingWindow.Model is LayoutDocumentFloatingWindow;

			// Determine if floatingWindow is configured to dock as document or not
			var dockAsDocument = true;
			if (!isDraggingDocuments && draggingWindow.Model is LayoutAnchorableFloatingWindow anchorableWindow)
			{
				if (anchorableWindow.Descendents().OfType<LayoutAnchorable>().Any(a => !a.CanDockAsTabbedDocument))
					dockAsDocument = false;
			}

			if (Content is not Visual rootVisual) return _dropAreas;
			foreach (var areaHost in rootVisual.FindVisualChildren<LayoutAnchorablePaneControl>())
				_dropAreas.Add(new DropArea<LayoutAnchorablePaneControl>(areaHost, DropAreaType.AnchorablePane));

			if (dockAsDocument)
			{
				foreach (var areaHost in rootVisual.FindVisualChildren<LayoutDocumentPaneControl>())
					_dropAreas.Add(new DropArea<LayoutDocumentPaneControl>(areaHost, DropAreaType.DocumentPane));
			}

			return _dropAreas;
		}

		/// <inheritdoc/>
		protected override void OnClosing(WindowClosingEventArgs e)
		{
			// A close that does not come from the docking code goes through the standard close logic.
			if (CloseInitiatedByUser && !KeepContentVisibleOnClose)
			{
				e.Cancel = true;
				if (CloseWindowCommand.CanExecute(null)) CloseWindowCommand.Execute(null);
				return;
			}

			base.OnClosing(e);
		}

		/// <inheritdoc/>
		protected override void OnClosed(EventArgs e)
		{
			var root = Model.Root;
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
			if (_model.RootPanel != null) _model.RootPanel.ChildrenCollectionChanged -= RootPanelOnChildrenCollectionChanged;
			Activated -= OnActivated;
			Deactivated -= OnDeactivated;
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

		private void OnRootUpdated(object sender, EventArgs e) => UpdateSingleContent();

		private void Model_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(LayoutDocumentFloatingWindow.RootPanel) && _model.RootPanel == null) InternalClose();
			else if (e.PropertyName == nameof(LayoutDocumentFloatingWindow.IsSinglePane) || e.PropertyName == nameof(LayoutDocumentFloatingWindow.SinglePane)) UpdateSingleContent();
		}

		private void RootPanelOnChildrenCollectionChanged(object sender, EventArgs e)
		{
			if (_model.RootPanel == null || _model.RootPanel.Children.Count == 0) InternalClose();
		}

		private bool CanExecuteHideWindowCommand(object parameter)
		{
			var manager = Model?.Root?.Manager;
			if (manager == null) return false;
			var canExecute = false;
			foreach (var content in Model.Descendents().OfType<LayoutContent>().ToArray())
			{
				if ((content is LayoutAnchorable anchorable && !anchorable.CanHide) || !content.CanClose) return false;
				if (!(manager.GetLayoutItemFromModel(content) is LayoutItem layoutItem) || layoutItem.CloseCommand == null || !layoutItem.CloseCommand.CanExecute(parameter)) return false;
				canExecute = true;
			}

			return canExecute;
		}

		private void OnExecuteHideWindowCommand(object parameter)
		{
			var manager = Model.Root.Manager;
			foreach (var content in Model.Descendents().OfType<LayoutContent>().ToArray())
			{
				if (manager.GetLayoutItemFromModel(content) is LayoutItem layoutItem) layoutItem.CloseCommand.Execute(parameter);
			}
		}

		private bool CanExecuteCloseWindowCommand(object parameter)
		{
			var manager = Model?.Root?.Manager;
			if (manager == null) return false;
			var canExecute = false;
			foreach (var document in Model.Descendents().OfType<LayoutDocument>().ToArray())
			{
				if (!document.CanClose) return false;
				if (!(manager.GetLayoutItemFromModel(document) is LayoutDocumentItem documentLayoutItem) || documentLayoutItem.CloseCommand == null || !documentLayoutItem.CloseCommand.CanExecute(parameter)) return false;
				canExecute = true;
			}

			return canExecute;
		}

		private void OnExecuteCloseWindowCommand(object parameter)
		{
			var manager = Model.Root.Manager;
			foreach (var document in Model.Descendents().OfType<LayoutDocument>().ToArray())
			{
				var documentLayoutItem = manager.GetLayoutItemFromModel(document) as LayoutDocumentItem;
				documentLayoutItem?.CloseCommand.Execute(parameter);
			}
		}
	}
}
