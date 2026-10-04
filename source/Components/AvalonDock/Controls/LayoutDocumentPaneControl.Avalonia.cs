using System;
using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Input;
using AvalonDock.Layout;

namespace AvalonDock.Controls
{
	/// <summary>Represents the control that presents a <see cref="LayoutDocumentPane"/> as a tab control.</summary>
	public class LayoutDocumentPaneControl : TabControlEx, ILayoutControl
	{
		private readonly LayoutDocumentPane _model;

		/// <summary>Initializes a new instance of the <see cref="LayoutDocumentPaneControl"/> class.</summary>
		/// <param name="model">The pane model.</param>
		/// <param name="isVirtualizing">Kept for API parity with the WPF library; content is always virtualized.</param>
		/// <param name="ignoreTabControlKeyBindings">Whether the key handling of the tab control is bypassed.</param>
		internal LayoutDocumentPaneControl(LayoutDocumentPane model, bool isVirtualizing, bool ignoreTabControlKeyBindings = false)
			: base(model, ignoreTabControlKeyBindings)
		{
			_model = model ?? throw new ArgumentNullException(nameof(model));
			AttachItems(_model.Children);
		}

		/// <inheritdoc/>
		public ILayoutElement Model => _model;

		/// <inheritdoc/>
		protected override Type StyleKeyOverride => typeof(LayoutDocumentPaneControl);

		/// <inheritdoc/>
		protected override void OnSelectedContentChanged()
		{
			base.OnSelectedContentChanged();
			if (_model.SelectedContent != null)
				_model.SelectedContent.IsActive = true;
		}

		/// <inheritdoc/>
		protected override void OnPointerPressed(PointerPressedEventArgs e)
		{
			base.OnPointerPressed(e);
			if (!e.Handled && _model.SelectedContent != null && !_model.SelectedContent.IsActive)
				_model.SelectedContent.IsActive = true;
		}

		/// <inheritdoc/>
		protected override void OnSizeChanged(SizeChangedEventArgs e)
		{
			base.OnSizeChanged(e);
			var modelWithActualSize = _model as ILayoutPositionableElementWithActualSize;
			modelWithActualSize.ActualWidth = Bounds.Width;
			modelWithActualSize.ActualHeight = Bounds.Height;
		}
	}
}
