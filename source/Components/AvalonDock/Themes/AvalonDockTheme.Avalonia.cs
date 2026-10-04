using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;

namespace AvalonDock.Themes
{
	/// <summary>
	/// The default styles of the docking controls, to be added to the application styles:
	/// <code>&lt;avalonDock:AvalonDockTheme /&gt;</code>
	/// </summary>
	/// <remarks>
	/// A <see cref="DockingManager"/> adds these styles to the application itself when it finds none, which is
	/// what the WPF library gets from <c>generic.xaml</c>. Adding them explicitly controls their order relative
	/// to the application's own styles. The styles only cover the docking controls; standard controls such as
	/// context menus come from the application's base theme (Fluent, Simple, ...).
	/// </remarks>
	public class AvalonDockTheme : Styles
	{
		/// <summary>Initializes a new instance of the <see cref="AvalonDockTheme"/> class.</summary>
		public AvalonDockTheme()
		{
			Add(new StyleInclude(GenericTheme.ResourceUri) { Source = GenericTheme.ResourceUri });
		}
	}
}
