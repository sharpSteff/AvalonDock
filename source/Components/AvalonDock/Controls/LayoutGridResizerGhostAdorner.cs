using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace AvalonDock.Controls
{
	/// <summary>
	/// Renders the splitter-resize preview ("ghost") as an adorner inside the grid instead of a
	/// transient top-level overlay window, which disturbs a captured Thumb drag on the LibreWPF
	/// portable backend (see docs/librewpf.md).
	/// </summary>
	internal sealed class LayoutGridResizerGhostAdorner : Adorner
	{
		private readonly Brush _brush;
		private readonly double _opacity;
		private readonly Size _ghostSize;

		/// <summary>
		/// Initializes a new instance of the <see cref="LayoutGridResizerGhostAdorner"/> class.
		/// </summary>
		/// <param name="adornedElement">The grid element the ghost is drawn over.</param>
		/// <param name="brush">The fill brush of the ghost rectangle.</param>
		/// <param name="opacity">The opacity of the ghost rectangle.</param>
		/// <param name="ghostSize">The size of the ghost rectangle.</param>
		/// <param name="position">The initial position, relative to the adorned element.</param>
		internal LayoutGridResizerGhostAdorner(UIElement adornedElement, Brush brush, double opacity, Size ghostSize, Point position)
			: base(adornedElement)
		{
			_brush = brush;
			_opacity = opacity;
			_ghostSize = ghostSize;
			Position = position;
			IsHitTestVisible = false;
		}

		/// <summary>Gets or sets the ghost position, relative to the adorned element.</summary>
		internal Point Position { get; set; }

		/// <inheritdoc/>
		protected override void OnRender(DrawingContext drawingContext)
		{
			drawingContext.PushOpacity(_opacity);
			drawingContext.DrawRectangle(_brush, null, new Rect(Position, _ghostSize));
			drawingContext.Pop();
		}
	}
}