using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace AvalonDock.Controls
{
	/// <summary>
	/// The splitter between two children of a <see cref="LayoutGridControl{T}"/>. Dragging it shows a ghost and
	/// resizes the children when the drag ends. Splitters between columns carry the <c>vertical</c> style class,
	/// splitters between rows the <c>horizontal</c> one.
	/// </summary>
	public class LayoutGridResizerControl : Thumb
	{
		/// <summary><see cref="BackgroundWhileDragging"/> property.</summary>
		public static readonly StyledProperty<IBrush> BackgroundWhileDraggingProperty =
			AvaloniaProperty.Register<LayoutGridResizerControl, IBrush>(nameof(BackgroundWhileDragging), Brushes.Black);

		/// <summary><see cref="OpacityWhileDragging"/> property.</summary>
		public static readonly StyledProperty<double> OpacityWhileDraggingProperty =
			AvaloniaProperty.Register<LayoutGridResizerControl, double>(nameof(OpacityWhileDragging), 0.5);

		/// <summary>Gets or sets the brush of the ghost shown while the splitter is dragged.</summary>
		public IBrush BackgroundWhileDragging
		{
			get => GetValue(BackgroundWhileDraggingProperty);
			set => SetValue(BackgroundWhileDraggingProperty, value);
		}

		/// <summary>Gets or sets the opacity of the ghost shown while the splitter is dragged.</summary>
		public double OpacityWhileDragging
		{
			get => GetValue(OpacityWhileDraggingProperty);
			set => SetValue(OpacityWhileDraggingProperty, value);
		}
	}
}
