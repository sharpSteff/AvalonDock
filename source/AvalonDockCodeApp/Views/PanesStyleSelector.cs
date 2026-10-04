#if AVALONIA
using AvalonDock.Controls;
using AvalonDock.Core;
using Avalonia.Controls;
using Avalonia.Styling;
using DependencyObject = Avalonia.Controls.Control;
using Style = Avalonia.Styling.ControlTheme;
#else
using System.Windows;
using System.Windows.Controls;
using AvalonDock.Core;
#endif
using ToggleTestApp.ViewModels;

namespace ToggleTestApp.Views;

/// <summary>
/// Selects the appropriate LayoutItem container style based on whether
/// the content is a toolbox (IToolbox) or a document (EditorTabViewModel).
/// </summary>
public class PanesStyleSelector : StyleSelector
{
	public Style? ToolboxStyle { get; set; }
	public Style? DocumentStyle { get; set; }

#if AVALONIA
	public override Style SelectStyle(object item, DependencyObject container)
#else
	public override Style? SelectStyle(object item, DependencyObject container)
#endif
	{
		if (item is IToolbox)
			return ToolboxStyle!;

		if (item is EditorTabViewModel)
			return DocumentStyle!;

#if AVALONIA
		return null!;
#else
		return base.SelectStyle(item, container);
#endif
	}
}