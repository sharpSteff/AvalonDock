using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Styling;

namespace AvalonDock.Controls
{
	/// <summary>
	/// Avalonia counterpart of WPF's <c>DataTemplateSelector</c>: picks a data template for an item in code.
	/// </summary>
	/// <remarks>
	/// Avalonia data templates usually select themselves through <see cref="IDataTemplate.Match"/>; this class
	/// exists so the <c>*TemplateSelector</c> properties of <see cref="DockingManager"/> keep their WPF shape.
	/// It is also an <see cref="IDataTemplate"/>, so a selector can be used wherever a template is expected.
	/// </remarks>
	public abstract class DataTemplateSelector : IDataTemplate
	{
		/// <summary>Returns the template to use for <paramref name="item"/>.</summary>
		/// <param name="item">The data item.</param>
		/// <param name="container">The control the item is shown in.</param>
		/// <returns>The template, or <see langword="null"/> to fall back to the default lookup.</returns>
		public abstract IDataTemplate SelectTemplate(object item, Control container);

		/// <inheritdoc/>
		Control ITemplate<object, Control>.Build(object param) => SelectTemplate(param, null)?.Build(param);

		/// <inheritdoc/>
		bool IDataTemplate.Match(object data) => SelectTemplate(data, null) != null;
	}

	/// <summary>
	/// Avalonia counterpart of WPF's <c>StyleSelector</c>: picks a <see cref="ControlTheme"/> for a container in code.
	/// </summary>
	public abstract class StyleSelector
	{
		/// <summary>Returns the theme to apply to <paramref name="container"/>.</summary>
		/// <param name="item">The data item shown by the container.</param>
		/// <param name="container">The container.</param>
		/// <returns>The theme, or <see langword="null"/> for none.</returns>
		public abstract ControlTheme SelectStyle(object item, Control container);
	}
}
