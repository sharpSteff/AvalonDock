using System.ComponentModel;
using Avalonia;
using Avalonia.VisualTree;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using AvalonDock.Layout;

namespace AvalonDock.Controls
{
	/// <summary>
	/// Shared implementation of <see cref="LayoutDocumentControl"/> and <see cref="LayoutAnchorableControl"/>:
	/// the control that hosts the <see cref="LayoutItem.View"/> of a content inside a pane.
	/// </summary>
	public abstract class LayoutContentControlBase : TemplatedControl
	{
		/// <summary><see cref="LayoutItem"/> property.</summary>
		public static readonly DirectProperty<LayoutContentControlBase, LayoutItem> LayoutItemProperty =
			AvaloniaProperty.RegisterDirect<LayoutContentControlBase, LayoutItem>(nameof(LayoutItem), o => o.LayoutItem);

		private LayoutItem _layoutItem;
		private LayoutContent _attachedModel;

		/// <summary>Gets the layout item of the hosted content.</summary>
		public LayoutItem LayoutItem
		{
			get => _layoutItem;
			private set => SetAndRaise(LayoutItemProperty, ref _layoutItem, value);
		}

		/// <summary>Gets the content model as a <see cref="LayoutContent"/>.</summary>
		protected abstract LayoutContent ContentModel { get; }

		/// <summary>Re-attaches to <see cref="ContentModel"/>; called by derived classes when their model changes.</summary>
		protected void OnContentModelChanged()
		{
			if (_attachedModel != null) _attachedModel.PropertyChanged -= Model_PropertyChanged;
			_attachedModel = ContentModel;
			if (_attachedModel != null)
			{
				_attachedModel.PropertyChanged += Model_PropertyChanged;
				LayoutItem = _attachedModel.Root?.Manager?.GetLayoutItemFromModel(_attachedModel);
				IsEnabled = _attachedModel.IsEnabled;
			}
			else
			{
				LayoutItem = null;
			}
		}

		/// <inheritdoc/>
		protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
		{
			base.OnAttachedToVisualTree(e);
			if (_attachedModel == null && ContentModel != null) OnContentModelChanged();
			else if (LayoutItem == null && _attachedModel != null)
				LayoutItem = _attachedModel.Root?.Manager?.GetLayoutItemFromModel(_attachedModel);
		}

		/// <inheritdoc/>
		protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
		{
			// prevent memory leak via event handler
			if (_attachedModel != null) _attachedModel.PropertyChanged -= Model_PropertyChanged;
			_attachedModel = null;
			base.OnDetachedFromVisualTree(e);
		}

		/// <inheritdoc/>
		protected override void OnPointerPressed(PointerPressedEventArgs e)
		{
			SetIsActive();
			base.OnPointerPressed(e);
		}

		/// <inheritdoc/>
		protected override void OnGotFocus(FocusChangedEventArgs e)
		{
			// Not when the focus only comes back to where it was, see FocusElementManager.IsFocusRestore.
			var model = ContentModel;
			if (model == null || !FocusElementManager.IsFocusRestore(model, e))
				SetIsActive();
			base.OnGotFocus(e);
		}

		private void SetIsActive()
		{
			var model = ContentModel;
			if (model != null && !model.IsActive) model.IsActive = true;
		}

		private void Model_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(LayoutContent.Parent) || e.PropertyName == nameof(LayoutContent.Content))
			{
				LayoutItem = _attachedModel?.Root?.Manager?.GetLayoutItemFromModel(_attachedModel);
				return;
			}

			if (e.PropertyName != nameof(LayoutContent.IsEnabled)) return;
			var model = _attachedModel;
			if (model == null) return;
			IsEnabled = model.IsEnabled;
			if (IsEnabled || !model.IsActive) return;
			if (model.Parent is LayoutDocumentPane layoutDocumentPane) layoutDocumentPane.SetNextSelectedIndex();
			else if (model.Parent is LayoutAnchorablePane layoutAnchorablePane) layoutAnchorablePane.SetNextSelectedIndex();
		}
	}

	/// <summary>Represents the control that hosts the content of a <see cref="LayoutDocument"/> (or of an anchorable docked as a document).</summary>
	public class LayoutDocumentControl : LayoutContentControlBase
	{
		/// <summary><see cref="Model"/> property.</summary>
		public static readonly StyledProperty<LayoutContent> ModelProperty =
			AvaloniaProperty.Register<LayoutDocumentControl, LayoutContent>(nameof(Model));

		/// <summary>Initializes a new instance of the <see cref="LayoutDocumentControl"/> class.</summary>
		public LayoutDocumentControl()
		{
			Focusable = true;
		}

		/// <summary>Gets or sets the model of the hosted content.</summary>
		public LayoutContent Model
		{
			get => GetValue(ModelProperty);
			set => SetValue(ModelProperty, value);
		}

		/// <inheritdoc/>
		protected override LayoutContent ContentModel => Model;

		/// <inheritdoc/>
		protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
		{
			base.OnPropertyChanged(change);
			if (change.Property == ModelProperty) OnContentModelChanged();
		}
	}

	/// <summary>Represents the control that hosts the content of a <see cref="LayoutAnchorable"/>, below its title.</summary>
	public class LayoutAnchorableControl : LayoutContentControlBase
	{
		/// <summary><see cref="Model"/> property.</summary>
		public static readonly StyledProperty<LayoutAnchorable> ModelProperty =
			AvaloniaProperty.Register<LayoutAnchorableControl, LayoutAnchorable>(nameof(Model));

		/// <summary><see cref="IsTitleVisible"/> property.</summary>
		public static readonly DirectProperty<LayoutAnchorableControl, bool> IsTitleVisibleProperty =
			AvaloniaProperty.RegisterDirect<LayoutAnchorableControl, bool>(nameof(IsTitleVisible), o => o.IsTitleVisible);

		private bool _isTitleVisible = true;
		private LayoutAnchorable _observedModel;
		private LayoutAnchorablePane _observedPane;

		/// <summary>
		/// Gets a value indicating whether the title bar is shown. It is hidden when the anchorable is the only
		/// pane of a floating window, whose own caption shows the title then.
		/// </summary>
		public bool IsTitleVisible
		{
			get => _isTitleVisible;
			private set => SetAndRaise(IsTitleVisibleProperty, ref _isTitleVisible, value);
		}

		private void ObserveModel()
		{
			if (_observedModel != null) _observedModel.PropertyChanged -= OnObservedPropertyChanged;
			_observedModel = Model;
			if (_observedModel != null) _observedModel.PropertyChanged += OnObservedPropertyChanged;
			ObservePane();
		}

		private void ObservePane()
		{
			if (_observedPane != null) _observedPane.PropertyChanged -= OnObservedPropertyChanged;
			_observedPane = Model?.Parent as LayoutAnchorablePane;
			if (_observedPane != null) _observedPane.PropertyChanged += OnObservedPropertyChanged;
			UpdateIsTitleVisible();
		}

		private void OnObservedPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (sender == _observedModel && e.PropertyName == nameof(LayoutAnchorable.Parent)) ObservePane();
			else if (e.PropertyName == nameof(LayoutAnchorablePane.IsDirectlyHostedInFloatingWindow)) UpdateIsTitleVisible();
		}

		private void UpdateIsTitleVisible()
		{
			var model = Model;
			IsTitleVisible = model != null && !(model.IsFloating && (model.Parent as LayoutAnchorablePane)?.IsDirectlyHostedInFloatingWindow == true);
		}

		/// <inheritdoc/>
		protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
		{
			base.OnAttachedToVisualTree(e);
			ObserveModel();
		}

		/// <inheritdoc/>
		protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
		{
			if (_observedModel != null) _observedModel.PropertyChanged -= OnObservedPropertyChanged;
			if (_observedPane != null) _observedPane.PropertyChanged -= OnObservedPropertyChanged;
			_observedModel = null;
			_observedPane = null;
			base.OnDetachedFromVisualTree(e);
		}

		/// <summary>Gets or sets the model of the hosted anchorable.</summary>
		public LayoutAnchorable Model
		{
			get => GetValue(ModelProperty);
			set => SetValue(ModelProperty, value);
		}

		/// <inheritdoc/>
		protected override LayoutContent ContentModel => Model;

		/// <inheritdoc/>
		protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
		{
			base.OnPropertyChanged(change);
			if (change.Property == ModelProperty)
			{
				OnContentModelChanged();
				if (this.IsAttachedToVisualTree()) ObserveModel();
				else UpdateIsTitleVisible();
			}
		}
	}
}
