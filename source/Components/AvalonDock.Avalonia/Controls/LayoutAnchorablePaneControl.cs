using System;
using Avalonia.Controls;
using Avalonia.Input;
using AvalonDock.Layout;

namespace AvalonDock.Controls
{
	/// <summary>Represents the control that presents a <see cref="LayoutAnchorablePane"/> as a tab control.</summary>
	public class LayoutAnchorablePaneControl : TabControlEx, ILayoutControl
	{
		private readonly LayoutAnchorablePane _model;

		/// <summary>Initializes a new instance of the <see cref="LayoutAnchorablePaneControl"/> class.</summary>
		/// <param name="model">The pane model.</param>
		/// <param name="isVirtualizing">Kept for API parity with the WPF library; content is always virtualized.</param>
		/// <param name="ignoreTabControlKeyBindings">Whether the key handling of the tab control is bypassed.</param>
		internal LayoutAnchorablePaneControl(LayoutAnchorablePane model, bool isVirtualizing, bool ignoreTabControlKeyBindings = false)
			: base(model, ignoreTabControlKeyBindings)
		{
			_model = model ?? throw new ArgumentNullException(nameof(model));
			AttachItems(_model.Children);
		}

		/// <inheritdoc/>
		public ILayoutElement Model => _model;

		/// <inheritdoc/>
		protected override Type StyleKeyOverride => typeof(LayoutAnchorablePaneControl);

		/// <inheritdoc/>
		protected override void OnGotFocus(FocusChangedEventArgs e)
		{
			if (_model?.SelectedContent != null) _model.SelectedContent.IsActive = true;
			base.OnGotFocus(e);
		}

		/// <inheritdoc/>
		protected override void OnPointerPressed(PointerPressedEventArgs e)
		{
			base.OnPointerPressed(e);
			if (!e.Handled && _model?.SelectedContent != null) _model.SelectedContent.IsActive = true;
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
