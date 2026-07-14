using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;

namespace AvalonDock.Platform
{
	/// <summary>
	/// Linux implementation of IDpiService backed by Xlib. The scale factor comes from
	/// the Xft.dpi resource when set (the mechanism desktop environments use to expose
	/// fractional scaling to X11 clients), falling back to the physical size reported by
	/// the display, and finally to 1.0. The work area honors the window manager's
	/// _NET_WORKAREA hint when published, falling back to the full screen.
	/// </summary>
	internal class LinuxDpiService : IDpiService
	{
		private const double BaselineDpi = 96.0;

		public double GetPrimaryMonitorDpi()
		{
			lock (X11Interop.Lock)
			{
				var display = X11Interop.Display;
				if (display == IntPtr.Zero) return 1.0;

				try
				{
					var xftDpi = ReadXftDpi(display);
					if (xftDpi > 0) return xftDpi / BaselineDpi;

					var screen = X11Interop.XDefaultScreen(display);
					var widthPx = X11Interop.XDisplayWidth(display, screen);
					var widthMM = X11Interop.XDisplayWidthMM(display, screen);
					if (widthPx > 0 && widthMM > 0)
					{
						var dpi = widthPx * 25.4 / widthMM;
						// Physical DPI is noisy (projectors, VMs); only treat clearly
						// hi-dpi outputs as scaled.
						if (dpi >= BaselineDpi) return dpi / BaselineDpi;
					}
				}
				catch
				{
					// Fall through to the default below.
				}

				return 1.0;
			}
		}

		public double GetMonitorDpi(IntPtr windowHandle)
		{
			// Per-monitor DPI would need XRandR correlation; report the primary scale.
			return GetPrimaryMonitorDpi();
		}

		public Rect GetPrimaryMonitorWorkArea()
		{
			lock (X11Interop.Lock)
			{
				var display = X11Interop.Display;
				if (display == IntPtr.Zero) return new Rect(0, 0, 1920, 1080);

				try
				{
					var screen = X11Interop.XDefaultScreen(display);
					var root = X11Interop.XRootWindow(display, screen);
					if (TryReadWorkArea(display, root, out var workArea)) return workArea;

					return new Rect(0, 0, X11Interop.XDisplayWidth(display, screen), X11Interop.XDisplayHeight(display, screen));
				}
				catch
				{
					return new Rect(0, 0, 1920, 1080);
				}
			}
		}

		public Rect GetMonitorWorkArea(IntPtr windowHandle)
		{
			// Per-monitor work areas would need XRandR correlation; report the primary one.
			return GetPrimaryMonitorWorkArea();
		}

		/// <summary>Reads Xft.dpi from the root window's resource manager string, or 0.</summary>
		private static double ReadXftDpi(IntPtr display)
		{
			var resources = X11Interop.XResourceManagerString(display);
			if (resources == IntPtr.Zero) return 0;

			var text = Marshal.PtrToStringAnsi(resources);
			if (string.IsNullOrEmpty(text)) return 0;

			foreach (var line in text.Split('\n'))
			{
				var separator = line.IndexOf(':');
				if (separator <= 0) continue;
				if (!line.Substring(0, separator).Trim().Equals("Xft.dpi", StringComparison.Ordinal)) continue;
				if (double.TryParse(line.Substring(separator + 1).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var dpi))
				{
					return dpi;
				}
			}

			return 0;
		}

		/// <summary>Reads the window manager's _NET_WORKAREA hint for the current desktop, if published.</summary>
		private static bool TryReadWorkArea(IntPtr display, IntPtr root, out Rect workArea)
		{
			workArea = default;

			var netWorkArea = X11Interop.XInternAtom(display, "_NET_WORKAREA", true);
			if (netWorkArea == IntPtr.Zero) return false;

			var status = X11Interop.XGetWindowProperty(display, root, netWorkArea, IntPtr.Zero, new IntPtr(4), false, IntPtr.Zero, out _, out var format, out var nItems, out _, out var prop);
			if (status != 0 || prop == IntPtr.Zero) return false;

			try
			{
				if (format != 32 || nItems.ToInt64() < 4) return false;

				// 32-bit format properties are returned as native longs.
				var values = new long[4];
				for (var i = 0; i < 4; i++)
				{
					values[i] = Marshal.ReadIntPtr(prop, i * IntPtr.Size).ToInt64();
				}

				workArea = new Rect(values[0], values[1], values[2], values[3]);
				return workArea.Width > 0 && workArea.Height > 0;
			}
			catch
			{
				return false;
			}
			finally
			{
				X11Interop.XFree(prop);
			}
		}
	}
}
