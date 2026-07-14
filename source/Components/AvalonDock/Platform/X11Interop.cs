using System;
using System.Runtime.InteropServices;

namespace AvalonDock.Platform
{
	/// <summary>
	/// Shared Xlib interop used by the Linux platform services.
	/// Maintains a single display connection and serializes all Xlib calls behind a lock
	/// (Xlib is not thread-safe without XInitThreads, and AvalonDock may query from
	/// non-UI code paths). All entry points degrade gracefully when no X display is
	/// available (Wayland without XWayland, or a headless host): queries return defaults
	/// and mutations become no-ops.
	/// </summary>
	internal static class X11Interop
	{
		private const string LibX11 = "libX11.so.6";

		internal static readonly object Lock = new object();

		private static IntPtr _display;
		private static bool _displayFailed;
		private static XErrorHandlerDelegate _ignoreErrors;

		internal delegate int XErrorHandlerDelegate(IntPtr display, IntPtr errorEvent);

		[DllImport(LibX11)]
		private static extern IntPtr XOpenDisplay(IntPtr displayName);

		[DllImport(LibX11)]
		internal static extern int XDefaultScreen(IntPtr display);

		[DllImport(LibX11)]
		internal static extern IntPtr XRootWindow(IntPtr display, int screen);

		[DllImport(LibX11)]
		internal static extern int XDisplayWidth(IntPtr display, int screen);

		[DllImport(LibX11)]
		internal static extern int XDisplayHeight(IntPtr display, int screen);

		[DllImport(LibX11)]
		internal static extern int XDisplayWidthMM(IntPtr display, int screen);

		[DllImport(LibX11)]
		internal static extern bool XQueryPointer(IntPtr display, IntPtr window, out IntPtr rootReturn, out IntPtr childReturn, out int rootX, out int rootY, out int winX, out int winY, out uint maskReturn);

		[DllImport(LibX11)]
		internal static extern int XGetGeometry(IntPtr display, IntPtr drawable, out IntPtr rootReturn, out int x, out int y, out uint width, out uint height, out uint borderWidth, out uint depth);

		[DllImport(LibX11)]
		internal static extern int XTranslateCoordinates(IntPtr display, IntPtr srcWindow, IntPtr destWindow, int srcX, int srcY, out int destX, out int destY, out IntPtr childReturn);

		[DllImport(LibX11)]
		internal static extern int XMoveWindow(IntPtr display, IntPtr window, int x, int y);

		[DllImport(LibX11)]
		internal static extern int XRaiseWindow(IntPtr display, IntPtr window);

		[DllImport(LibX11)]
		internal static extern int XFlush(IntPtr display);

		[DllImport(LibX11)]
		internal static extern int XSync(IntPtr display, bool discard);

		[DllImport(LibX11, CharSet = CharSet.Ansi)]
		internal static extern IntPtr XInternAtom(IntPtr display, string atomName, bool onlyIfExists);

		[DllImport(LibX11)]
		internal static extern int XGetWindowProperty(IntPtr display, IntPtr window, IntPtr property, IntPtr longOffset, IntPtr longLength, bool delete, IntPtr reqType, out IntPtr actualType, out int actualFormat, out IntPtr nItems, out IntPtr bytesAfter, out IntPtr propReturn);

		[DllImport(LibX11)]
		internal static extern int XChangeProperty(IntPtr display, IntPtr window, IntPtr property, IntPtr type, int format, int mode, ref uint data, int nElements);

		[DllImport(LibX11)]
		internal static extern int XDeleteProperty(IntPtr display, IntPtr window, IntPtr property);

		[DllImport(LibX11)]
		internal static extern int XSendEvent(IntPtr display, IntPtr window, bool propagate, IntPtr eventMask, ref XClientMessageEvent evt);

		[DllImport(LibX11)]
		internal static extern int XFree(IntPtr data);

		[DllImport(LibX11)]
		internal static extern IntPtr XSetErrorHandler(XErrorHandlerDelegate handler);

		[DllImport(LibX11, EntryPoint = "XSetErrorHandler")]
		internal static extern IntPtr XSetErrorHandlerPtr(IntPtr handler);

		[DllImport(LibX11)]
		internal static extern IntPtr XResourceManagerString(IntPtr display);

		internal const int PropModeReplace = 0;
		internal const uint Button1Mask = 1 << 8;
		internal const long SubstructureRedirectMask = 1L << 20;
		internal const long SubstructureNotifyMask = 1L << 19;

		[StructLayout(LayoutKind.Sequential)]
		internal struct XClientMessageEvent
		{
			public int Type;
			public IntPtr Serial;
			public bool SendEvent;
			public IntPtr Display;
			public IntPtr Window;
			public IntPtr MessageType;
			public int Format;
			public IntPtr Data0;
			public IntPtr Data1;
			public IntPtr Data2;
			public IntPtr Data3;
			public IntPtr Data4;
		}

		internal const int ClientMessage = 33;

		/// <summary>
		/// Gets the shared display connection, or IntPtr.Zero when X11 is unavailable.
		/// Callers must hold <see cref="Lock"/> for the duration of any Xlib call sequence.
		/// </summary>
		internal static IntPtr Display
		{
			get
			{
				if (_display == IntPtr.Zero && !_displayFailed)
				{
					try
					{
						_display = XOpenDisplay(IntPtr.Zero);
					}
					catch (DllNotFoundException)
					{
					}
					catch (EntryPointNotFoundException)
					{
					}

					if (_display == IntPtr.Zero)
					{
						_displayFailed = true;
					}
				}

				return _display;
			}
		}

		/// <summary>
		/// Runs <paramref name="action"/> with a no-op X error handler installed, so a stale or
		/// foreign window id produces a failed call instead of Xlib's default process abort.
		/// Must be called under <see cref="Lock"/>.
		/// </summary>
		/// <param name="display">The X display connection the action operates on.</param>
		/// <param name="action">The Xlib call sequence to run with errors suppressed.</param>
		internal static void WithIgnoredErrors(IntPtr display, Action action)
		{
			_ignoreErrors ??= (d, e) => 0;
			var previous = XSetErrorHandler(_ignoreErrors);
			try
			{
				action();
				// Force queued requests out so their errors hit our handler, not the caller's.
				XSync(display, false);
			}
			finally
			{
				XSetErrorHandlerPtr(previous);
			}
		}
	}
}