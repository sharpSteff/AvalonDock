using AvalonDock.Controls;
using AvalonDock.Core;
using Avalonia.Controls;
using Avalonia.Styling;
using ToggleTestApp.ViewModels;

namespace ToggleTestApp.Views;

/// <summary>
/// Selects the appropriate LayoutItem container theme based on whether
/// the content is a toolbox (IToolbox) or a document (EditorTabViewModel).
/// </summary>
public class PanesStyleSelector : StyleSelector
{
	public ControlTheme? ToolboxStyle { get; set; }
	public ControlTheme? DocumentStyle { get; set; }

	public override ControlTheme SelectStyle(object item, Control container)
	{
		if (item is IToolbox)
			return ToolboxStyle!;

		if (item is EditorTabViewModel)
			return DocumentStyle!;

		return null!;
	}
}