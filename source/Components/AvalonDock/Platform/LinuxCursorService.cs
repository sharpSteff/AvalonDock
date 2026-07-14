using System;

namespace AvalonDock.Platform
{
	/// <summary>
	/// Linux implementation of ICursorService backed by Xlib (works on X11 and on
	/// Wayland via XWayland). Positions are reported in X11 root-window (device pixel)
	/// coordinates. When no X display is available every query returns a default.
	/// </summary>
	internal class LinuxCursorService : ICursorService
	{
		public (double X, double Y) GetCursorPosition()
		{
			var (x, y, _) = QueryPointer();
			return (x, y);
		}

		public bool IsLeftButtonDown()
		{
			var (_, _, mask) = QueryPointer();
			return (mask & X11Interop.Button1Mask) != 0;
		}

		public (double X, double Y) GetCursorLocationQuartz()
		{
			// Quartz coordinates are a macOS concept; report root coordinates.
			return GetCursorPosition();
		}

		public (double X, double Y) GetCursorLocationCocoa()
		{
			// Cocoa coordinates are a macOS concept; report root coordinates.
			return GetCursorPosition();
		}

		private static (double X, double Y, uint Mask) QueryPointer()
		{
			lock (X11Interop.Lock)
			{
				var display = X11Interop.Display;
				if (display == IntPtr.Zero) return (0, 0, 0);

				try
				{
					var root = X11Interop.XRootWindow(display, X11Interop.XDefaultScreen(display));
					if (X11Interop.XQueryPointer(display, root, out _, out _, out var rootX, out var rootY, out _, out _, out var mask))
					{
						return (rootX, rootY, mask);
					}
				}
				catch
				{
					// Fall through to the default below.
				}

				return (0, 0, 0);
			}
		}
	}
}