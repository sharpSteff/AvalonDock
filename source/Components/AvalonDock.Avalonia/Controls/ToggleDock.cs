using Avalonia;
using Avalonia.Controls.Templates;

namespace AvalonDock.Controls
{
	/// <summary>
	/// Attached properties that customize the sidebar button of an anchorable in a <see cref="ToggleDockingManager"/>.
	/// </summary>
	public sealed class ToggleDock
	{
		/// <summary>Icon attached property: an <see cref="Avalonia.Media.IImage"/>, a control, or any object rendered through <see cref="IconTemplateProperty"/>.</summary>
		public static readonly AttachedProperty<object> IconProperty =
			AvaloniaProperty.RegisterAttached<ToggleDock, AvaloniaObject, object>("Icon");

		/// <summary>ToolTip attached property: the tool tip of the sidebar button, overriding the title.</summary>
		public static readonly AttachedProperty<object> ToolTipProperty =
			AvaloniaProperty.RegisterAttached<ToggleDock, AvaloniaObject, object>("ToolTip");

		/// <summary>IconTemplate attached property: the template used to render <see cref="IconProperty"/>.</summary>
		public static readonly AttachedProperty<IDataTemplate> IconTemplateProperty =
			AvaloniaProperty.RegisterAttached<ToggleDock, AvaloniaObject, IDataTemplate>("IconTemplate");

		private ToggleDock()
		{
		}

		/// <summary>Gets the icon of the sidebar button of <paramref name="element"/>.</summary>
		/// <param name="element">The anchorable.</param>
		/// <returns>The icon.</returns>
		public static object GetIcon(AvaloniaObject element) => element.GetValue(IconProperty);

		/// <summary>Sets the icon of the sidebar button of <paramref name="element"/>.</summary>
		/// <param name="element">The anchorable.</param>
		/// <param name="value">The icon.</param>
		public static void SetIcon(AvaloniaObject element, object value) => element.SetValue(IconProperty, value);

		/// <summary>Gets the tool tip of the sidebar button of <paramref name="element"/>.</summary>
		/// <param name="element">The anchorable.</param>
		/// <returns>The tool tip.</returns>
		public static object GetToolTip(AvaloniaObject element) => element.GetValue(ToolTipProperty);

		/// <summary>Sets the tool tip of the sidebar button of <paramref name="element"/>.</summary>
		/// <param name="element">The anchorable.</param>
		/// <param name="value">The tool tip.</param>
		public static void SetToolTip(AvaloniaObject element, object value) => element.SetValue(ToolTipProperty, value);

		/// <summary>Gets the icon template of the sidebar button of <paramref name="element"/>.</summary>
		/// <param name="element">The anchorable.</param>
		/// <returns>The template.</returns>
		public static IDataTemplate GetIconTemplate(AvaloniaObject element) => element.GetValue(IconTemplateProperty);

		/// <summary>Sets the icon template of the sidebar button of <paramref name="element"/>.</summary>
		/// <param name="element">The anchorable.</param>
		/// <param name="value">The template.</param>
		public static void SetIconTemplate(AvaloniaObject element, IDataTemplate value) => element.SetValue(IconTemplateProperty, value);
	}
}
