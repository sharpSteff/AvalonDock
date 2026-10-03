using Avalonia;
using AvalonDock.Layout;

namespace AvalonDock.Controls
{
	/// <summary>Represents the <see cref="LayoutItem"/> of a <see cref="LayoutDocument"/>.</summary>
	public class LayoutDocumentItem : LayoutItem
	{
		private LayoutDocument _document;

		/// <summary>Initializes a new instance of the <see cref="LayoutDocumentItem"/> class.</summary>
		internal LayoutDocumentItem()
		{
		}

		/// <summary><see cref="Description"/> property.</summary>
		public static readonly StyledProperty<string> DescriptionProperty =
			AvaloniaProperty.Register<LayoutDocumentItem, string>(nameof(Description));

		/// <summary>Gets or sets the description to display (in the navigator window) for the document.</summary>
		public string Description
		{
			get => GetValue(DescriptionProperty);
			set => SetValue(DescriptionProperty, value);
		}

		/// <inheritdoc/>
		protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
		{
			base.OnPropertyChanged(change);
			if (change.Property == DescriptionProperty && _document != null)
				_document.Description = (string)change.NewValue;
		}

		/// <inheritdoc/>
		protected override void Close()
		{
			if (_document.Root?.Manager == null) return;
			var dockingManager = _document.Root.Manager;
			dockingManager.ExecuteCloseCommand(_document);
		}

		/// <inheritdoc/>
		protected override void OnVisibilityChanged()
		{
			if (_document?.Root != null)
			{
				_document.IsVisible = IsVisible;
				if (_document.Parent is LayoutDocumentPane layoutDocumentPane) layoutDocumentPane.ComputeVisibility();
			}

			base.OnVisibilityChanged();
		}

		/// <inheritdoc/>
		internal override void Attach(LayoutContent model)
		{
			_document = model as LayoutDocument;
			base.Attach(model);
		}

		/// <inheritdoc/>
		internal override void Detach()
		{
			_document = null;
			base.Detach();
		}

		/// <inheritdoc/>
		protected override bool CanExecuteDockAsDocumentCommand()
		{
			return LayoutElement != null && LayoutElement.FindParent<LayoutDocumentPane>() != null && LayoutElement.IsFloating;
		}
	}
}
