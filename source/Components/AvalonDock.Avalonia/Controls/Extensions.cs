using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

namespace AvalonDock.Controls
{
	/// <summary>
	/// Provides visual and logical tree helpers used by the docking controls.
	/// </summary>
	/// <remarks>
	/// Screen coordinates in this library are <em>device pixels</em>, the unit of
	/// <see cref="Visual.PointToScreen(Point)"/> and <see cref="Window.Position"/>. They are carried as
	/// <see cref="Point"/>/<see cref="Rect"/> so that the drag and drop code can do arithmetic on them, and are
	/// converted back into device independent units of a given visual with <see cref="ScreenToLocal(Visual, Point)"/>.
	/// </remarks>
	public static class Extensions
	{
		/// <summary>Enumerates the visual descendants of <paramref name="visual"/> of type <typeparamref name="T"/>.</summary>
		/// <typeparam name="T">The type of the descendants to find.</typeparam>
		/// <param name="visual">The visual to search.</param>
		/// <returns>The descendants, depth first.</returns>
		public static IEnumerable<T> FindVisualChildren<T>(this Visual visual)
			where T : Visual
		{
			if (visual == null) return Enumerable.Empty<T>();
			return visual.GetVisualDescendants().OfType<T>();
		}

		/// <summary>Enumerates the logical descendants of <paramref name="element"/> of type <typeparamref name="T"/>.</summary>
		/// <typeparam name="T">The type of the descendants to find.</typeparam>
		/// <param name="element">The element to search.</param>
		/// <returns>The descendants, depth first.</returns>
		public static IEnumerable<T> FindLogicalChildren<T>(this ILogical element)
			where T : class
		{
			if (element == null) return Enumerable.Empty<T>();
			return element.GetLogicalDescendants().OfType<T>();
		}

		/// <summary>Finds the nearest visual ancestor of type <typeparamref name="T"/>.</summary>
		/// <typeparam name="T">The type of the ancestor.</typeparam>
		/// <param name="visual">The visual to start from.</param>
		/// <returns>The ancestor, or <see langword="null"/>.</returns>
		public static T FindVisualAncestor<T>(this Visual visual)
			where T : class
		{
			if (visual == null) return null;
			return visual.GetVisualAncestors().OfType<T>().FirstOrDefault();
		}

		/// <summary>Finds the nearest logical ancestor of type <typeparamref name="T"/>.</summary>
		/// <typeparam name="T">The type of the ancestor.</typeparam>
		/// <param name="element">The element to start from.</param>
		/// <returns>The ancestor, or <see langword="null"/>.</returns>
		public static T FindLogicalAncestor<T>(this ILogical element)
			where T : class
		{
			if (element == null) return null;
			return element.GetLogicalAncestors().OfType<T>().FirstOrDefault();
		}

		/// <summary>Gets the area a visual occupies on screen, in device pixels.</summary>
		/// <param name="visual">The visual.</param>
		/// <returns>The screen area, or an empty rect when the visual is not part of a shown window.</returns>
		public static Rect GetScreenArea(this Visual visual)
		{
			if (visual == null || !visual.IsAttachedToVisualTree()) return default;
			var topLeft = visual.PointToScreen(default);
			var bottomRight = visual.PointToScreen(new Point(visual.Bounds.Width, visual.Bounds.Height));
			return new Rect(
				new Point(System.Math.Min(topLeft.X, bottomRight.X), System.Math.Min(topLeft.Y, bottomRight.Y)),
				new Point(System.Math.Max(topLeft.X, bottomRight.X), System.Math.Max(topLeft.Y, bottomRight.Y)));
		}

		/// <summary>
		/// Gets the screen area of a visual that is effectively visible, or an empty rect when it is not, so
		/// that hidden drop indicators can never be hit.
		/// </summary>
		/// <param name="visual">The visual.</param>
		/// <returns>The screen area in device pixels.</returns>
		internal static Rect GetVisibleScreenArea(this Visual visual)
		{
			if (visual == null || !visual.IsEffectivelyVisible) return default;
			return visual.GetScreenArea();
		}

		/// <summary>Converts a screen point (device pixels) into the coordinate space of <paramref name="visual"/>.</summary>
		/// <param name="visual">The visual.</param>
		/// <param name="screenPoint">The screen point.</param>
		/// <returns>The point in the device independent coordinates of <paramref name="visual"/>.</returns>
		public static Point ScreenToLocal(this Visual visual, Point screenPoint)
			=> visual.PointToClient(new PixelPoint((int)System.Math.Round(screenPoint.X), (int)System.Math.Round(screenPoint.Y)));

		/// <summary>Converts a screen rectangle (device pixels) into the coordinate space of <paramref name="visual"/>.</summary>
		/// <param name="visual">The visual.</param>
		/// <param name="screenRect">The screen rectangle.</param>
		/// <returns>The rectangle in the device independent coordinates of <paramref name="visual"/>.</returns>
		public static Rect ScreenToLocal(this Visual visual, Rect screenRect)
		{
			var topLeft = visual.ScreenToLocal(screenRect.TopLeft);
			var bottomRight = visual.ScreenToLocal(screenRect.BottomRight);
			return new Rect(topLeft, bottomRight);
		}

		/// <summary>Gets the screen position (device pixels) of a point given in the coordinates of <paramref name="visual"/>.</summary>
		/// <param name="visual">The visual.</param>
		/// <param name="localPoint">The point.</param>
		/// <returns>The screen point.</returns>
		public static Point LocalToScreen(this Visual visual, Point localPoint)
		{
			var pixel = visual.PointToScreen(localPoint);
			return new Point(pixel.X, pixel.Y);
		}

		/// <summary>Returns the intersection of two rectangles, or an empty rect when they do not intersect.</summary>
		/// <param name="rect">The first rectangle.</param>
		/// <param name="other">The second rectangle.</param>
		/// <returns>The intersection.</returns>
		internal static Rect IntersectOrEmpty(this Rect rect, Rect other)
		{
			var result = rect.Intersect(other);
			return result.Width <= 0 || result.Height <= 0 ? default : result;
		}

		/// <summary>Gets a value indicating whether the rectangle has no area.</summary>
		/// <param name="rect">The rectangle.</param>
		/// <returns><see langword="true"/> when the rect is empty.</returns>
		internal static bool IsEmptyArea(this Rect rect) => rect.Width <= 0 || rect.Height <= 0;
	}
}
