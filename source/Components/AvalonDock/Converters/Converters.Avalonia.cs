using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Controls.Templates;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using AvalonDock.Controls;
using AvalonDock.Layout;

namespace AvalonDock.Converters
{
	/// <summary>
	/// Combines a data template with a <see cref="DataTemplateSelector"/>: the selector is asked first and the
	/// template is used when the selector has nothing for an item. Values: [template, selector].
	/// </summary>
	public sealed class TemplateOrSelectorConverter : IMultiValueConverter
	{
		/// <summary>Gets the shared instance.</summary>
		public static TemplateOrSelectorConverter Instance { get; } = new TemplateOrSelectorConverter();

		/// <inheritdoc/>
		public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
		{
			var template = values?.Count > 0 ? values[0] as IDataTemplate : null;
			var selector = values?.Count > 1 ? values[1] as DataTemplateSelector : null;
			if (selector == null) return template;
			return new SelectorWithFallback(selector, template);
		}

		private sealed class SelectorWithFallback : IDataTemplate
		{
			private readonly DataTemplateSelector _selector;
			private readonly IDataTemplate _fallback;

			public SelectorWithFallback(DataTemplateSelector selector, IDataTemplate fallback)
			{
				_selector = selector;
				_fallback = fallback;
			}

			public Avalonia.Controls.Control Build(object param)
				=> (_selector.SelectTemplate(param, null) ?? _fallback)?.Build(param);

			public bool Match(object data) => _selector.SelectTemplate(data, null) != null || _fallback?.Match(data) == true;
		}
	}

	/// <summary>Converts a number to <see langword="true"/> when it is greater than the parameter (1 by default).</summary>
	public sealed class GreaterThanConverter : IValueConverter
	{
		/// <summary>Gets the shared instance.</summary>
		public static GreaterThanConverter Instance { get; } = new GreaterThanConverter();

		/// <inheritdoc/>
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			var threshold = parameter == null ? 1 : System.Convert.ToInt32(parameter, CultureInfo.InvariantCulture);
			return value is int count && count > threshold;
		}

		/// <inheritdoc/>
		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
	}

	/// <summary>Converts an <see cref="AnchorSide"/> into the orientation in which anchors are stacked on that side.</summary>
	public sealed class AnchorSideToOrientationConverter : IValueConverter
	{
		/// <summary>Gets the shared instance.</summary>
		public static AnchorSideToOrientationConverter Instance { get; } = new AnchorSideToOrientationConverter();

		/// <inheritdoc/>
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			var side = value is AnchorSide anchorSide ? anchorSide : AnchorSide.Left;
			return side == AnchorSide.Left || side == AnchorSide.Right ? Orientation.Vertical : Orientation.Horizontal;
		}

		/// <inheritdoc/>
		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
	}

	/// <summary>Converts an <see cref="AnchorSide"/> into the rotation of the anchors on that side.</summary>
	public sealed class AnchorSideToAngleConverter : IValueConverter
	{
		/// <summary>Gets the shared instance.</summary>
		public static AnchorSideToAngleConverter Instance { get; } = new AnchorSideToAngleConverter();

		/// <inheritdoc/>
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			var side = value is AnchorSide anchorSide ? anchorSide : AnchorSide.Left;
			return side == AnchorSide.Left || side == AnchorSide.Right ? 90.0 : 0.0;
		}

		/// <inheritdoc/>
		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
	}

	/// <summary>Converts a <see cref="LayoutContent"/> into its <see cref="LayoutItem"/>.</summary>
	public sealed class LayoutItemFromLayoutModelConverter : IValueConverter
	{
		/// <summary>Gets the shared instance.</summary>
		public static LayoutItemFromLayoutModelConverter Instance { get; } = new LayoutItemFromLayoutModelConverter();

		/// <inheritdoc/>
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
			=> value is LayoutContent layoutModel ? layoutModel.Root?.Manager?.GetLayoutItemFromModel(layoutModel) : null;

		/// <inheritdoc/>
		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
	}

	/// <summary>Converts a <see cref="LayoutContent"/> into the <see cref="LayoutItem.ActivateCommand"/> of its layout item.</summary>
	public sealed class ActivateCommandLayoutItemFromLayoutModelConverter : IValueConverter
	{
		/// <summary>Gets the shared instance.</summary>
		public static ActivateCommandLayoutItemFromLayoutModelConverter Instance { get; } = new ActivateCommandLayoutItemFromLayoutModelConverter();

		/// <inheritdoc/>
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
			=> value is LayoutContent layoutModel ? layoutModel.Root?.Manager?.GetLayoutItemFromModel(layoutModel)?.ActivateCommand : null;

		/// <inheritdoc/>
		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
	}

	/// <summary>
	/// Picks the command of the close button of a document tab: <c>CloseCommand</c> when the content can be
	/// closed, <c>HideCommand</c> when it can only be hidden (an anchorable docked as a document).
	/// </summary>
	public sealed class CloseOrHideCommandConverter : IMultiValueConverter
	{
		/// <summary>Gets the shared instance.</summary>
		public static CloseOrHideCommandConverter Instance { get; } = new CloseOrHideCommandConverter();

		/// <inheritdoc/>
		public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
		{
			var layoutItem = values?.Count > 0 ? values[0] as LayoutItem : null;
			if (layoutItem == null) return null;
			if (layoutItem.LayoutElement is LayoutAnchorable anchorable && !anchorable.CanClose && anchorable.CanHide && layoutItem is LayoutAnchorableItem anchorableItem)
				return anchorableItem.HideCommand;
			return layoutItem.CloseCommand;
		}
	}
}
