using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;

namespace AvalonDock.Controls
{
	/// <summary>
	/// The panel that lays out the tabs of an anchorable pane in one row, shrinking them evenly when they do not fit.
	/// </summary>
	public class AnchorablePaneTabPanel : Panel
	{
		/// <summary>Initializes a new instance of the <see cref="AnchorablePaneTabPanel"/> class.</summary>
		public AnchorablePaneTabPanel()
		{
			FlowDirection = Avalonia.Media.FlowDirection.LeftToRight;
		}

		/// <inheritdoc/>
		protected override Size MeasureOverride(Size availableSize)
		{
			double totWidth = 0;
			double maxHeight = 0;
			var visibleChildren = Children.Where(ch => ch.IsVisible).ToList();
			foreach (var child in visibleChildren)
			{
				child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
				totWidth += child.DesiredSize.Width;
				maxHeight = Math.Max(maxHeight, child.DesiredSize.Height);
			}

			if (totWidth > availableSize.Width && visibleChildren.Count > 0)
			{
				var childFinalDesiredWidth = availableSize.Width / visibleChildren.Count;
				foreach (var child in visibleChildren)
					child.Measure(new Size(childFinalDesiredWidth, availableSize.Height));
			}

			return new Size(Math.Min(availableSize.Width, totWidth), maxHeight);
		}

		/// <inheritdoc/>
		protected override Size ArrangeOverride(Size finalSize)
		{
			var visibleChildren = Children.Where(ch => ch.IsVisible).ToList();
			if (visibleChildren.Count == 0) return finalSize;

			var finalWidth = finalSize.Width;
			var desiredWidth = visibleChildren.Sum(ch => ch.DesiredSize.Width);
			var offsetX = 0.0;

			if (finalWidth > desiredWidth)
			{
				foreach (var child in visibleChildren)
				{
					var childFinalWidth = child.DesiredSize.Width;
					child.Arrange(new Rect(offsetX, 0, childFinalWidth, finalSize.Height));
					offsetX += childFinalWidth;
				}
			}
			else
			{
				var childFinalWidth = finalWidth / visibleChildren.Count;
				foreach (var child in visibleChildren)
				{
					child.Arrange(new Rect(offsetX, 0, childFinalWidth, finalSize.Height));
					offsetX += childFinalWidth;
				}
			}

			return finalSize;
		}
	}
}
