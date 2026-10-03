namespace AvalonDock.Layout
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Avalonia;

	/// <summary>
	/// Represents a layout element for floating window extension.
	/// </summary>
	/// <remarks>
	/// WPF reads the monitor layout from <c>SystemParameters</c>; Avalonia only exposes screens through a
	/// <see cref="Avalonia.Controls.TopLevel"/>, which the layout model does not know about. The
	/// <see cref="DockingManager"/> therefore publishes the working areas of the screens it can see through
	/// <see cref="ScreenWorkingAreas"/> whenever it is attached to a window.
	/// </remarks>
	public static class ILayoutElementForFloatingWindowExtension
	{
		/// <summary>
		/// Gets or sets a provider for the working areas of the available screens, in screen pixels.
		/// The first entry is treated as the primary screen. When <see langword="null"/>, or when it
		/// returns no screens, floating positions are left untouched.
		/// </summary>
		public static Func<IReadOnlyList<PixelRect>> ScreenWorkingAreas { get; set; }

		/// <summary>
		/// Moves a floating element back onto a screen when its saved position is no longer visible on any.
		/// </summary>
		/// <param name="paneInsideFloatingWindow">The pane inside floating window.</param>
		internal static void KeepInsideNearestMonitor(this ILayoutElementForFloatingWindow paneInsideFloatingWindow)
		{
			IReadOnlyList<PixelRect> screens;
			try
			{
				screens = ScreenWorkingAreas?.Invoke();
			}
			catch (InvalidOperationException)
			{
				return;
			}

			if (screens == null || screens.Count == 0) return;

			var window = new PixelRect(
				(int)paneInsideFloatingWindow.FloatingLeft,
				(int)paneInsideFloatingWindow.FloatingTop,
				Math.Max(1, (int)paneInsideFloatingWindow.FloatingWidth),
				Math.Max(1, (int)paneInsideFloatingWindow.FloatingHeight));

			if (screens.Any(screen => screen.Intersects(window))) return;

			// Nearest screen by distance between centres; with a single monitor this is the primary one.
			var center = window.Center;
			var target = screens.OrderBy(screen => DistanceSquared(screen.Center, center)).First();
			var placed = PlaceOnScreen(target, window);

			paneInsideFloatingWindow.FloatingLeft = placed.X;
			paneInsideFloatingWindow.FloatingTop = placed.Y;
			paneInsideFloatingWindow.FloatingWidth = placed.Width;
			paneInsideFloatingWindow.FloatingHeight = placed.Height;
			paneInsideFloatingWindow.RaiseFloatingPropertiesUpdated();
		}

		private static long DistanceSquared(PixelPoint a, PixelPoint b)
		{
			long dx = a.X - b.X;
			long dy = a.Y - b.Y;
			return (dx * dx) + (dy * dy);
		}

		/// <summary>
		/// Moves <paramref name="windowRect"/> so that it lies on <paramref name="monitorRect"/>, shrinking it
		/// when it is larger than the monitor.
		/// </summary>
		/// <param name="monitorRect">The monitor working area.</param>
		/// <param name="windowRect">The window bounds.</param>
		/// <returns>The adjusted bounds.</returns>
		internal static PixelRect PlaceOnScreen(PixelRect monitorRect, PixelRect windowRect)
		{
			var width = Math.Min(windowRect.Width, monitorRect.Width);
			var height = Math.Min(windowRect.Height, monitorRect.Height);
			var left = windowRect.X;
			var top = windowRect.Y;

			if (windowRect.Right < monitorRect.X) left = monitorRect.X;
			else if (windowRect.X > monitorRect.Right) left = monitorRect.Right - width;

			if (windowRect.Bottom < monitorRect.Y) top = monitorRect.Y;
			else if (windowRect.Y > monitorRect.Bottom) top = monitorRect.Bottom - height;

			return new PixelRect(left, top, width, height);
		}
	}
}
