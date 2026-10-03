using Avalonia.Controls;

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
		Control Content { get; }
	}
}