#if AVALONIA
using FrameworkElement = Avalonia.Controls.Control;
#else
using System.Windows;
#endif

namespace AvalonDock.Layout
{
	/// <summary>
	/// Represents a layout element that holds a UI content element
	/// </summary>
	public interface ILayoutContentElement
	{
		/// <summary>
		/// Gets the root control hosted by this layout element
		/// </summary>
		FrameworkElement Content { get; }
	}
}