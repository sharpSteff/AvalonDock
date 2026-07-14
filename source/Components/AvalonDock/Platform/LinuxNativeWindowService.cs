using System;

namespace AvalonDock.Platform
{
	/// <summary>
	/// Linux implementation of INativeWindowService backed by Xlib (works on X11 and on
	/// Wayland via XWayland). The window handle is interpreted as an X11 window id;
	/// positions are X11 root-window (device pixel) coordinates. Every operation is
	/// wrapped in an ignore-errors scope so a stale or foreign handle degrades to a
	/// no-op/default instead of Xlib's default process abort, and everything degrades
	/// gracefully when no X display is available.
	/// </summary>
	internal class LinuxNativeWindowService : INativeWindowService
	{
		public (double X, double Y) GetWindowPosition(IntPtr windowHandle)
		{
			var (x, y, _, _) = GetFrame(windowHandle);
			return (x, y);
		}

		public void SetWindowPosition(IntPtr windowHandle, double x, double y)
		{
			WithWindow(windowHandle, (display, window) =>
			{
				X11Interop.XMoveWindow(display, window, (int)x, (int)y);
				X11Interop.XFlush(display);
			});
		}

		public (double X, double Y, double Width, double Height) GetWindowFrame(IntPtr windowHandle)
		{
			return GetFrame(windowHandle);
		}

		public void BringToFront(IntPtr windowHandle)
		{
			WithWindow(windowHandle, (display, window) =>
			{
				// _NET_ACTIVE_WINDOW is the EWMH-correct way to raise+focus; ask the WM
				// first and raise directly as a fallback for non-EWMH window managers.
				var netActiveWindow = X11Interop.XInternAtom(display, "_NET_ACTIVE_WINDOW", true);
				if (netActiveWindow != IntPtr.Zero)
				{
					SendRootClientMessage(display, window, netActiveWindow, 1);
				}

				X11Interop.XRaiseWindow(display, window);
				X11Interop.XFlush(display);
			});
		}

		public void CloseWindow(IntPtr windowHandle)
		{
			WithWindow(windowHandle, (display, window) =>
			{
				// Ask the window manager to close the window (EWMH _NET_CLOSE_WINDOW);
				// destroying the window behind the toolkit's back is not safe.
				var netCloseWindow = X11Interop.XInternAtom(display, "_NET_CLOSE_WINDOW", true);
				if (netCloseWindow != IntPtr.Zero)
				{
					SendRootClientMessage(display, window, netCloseWindow, 1);
					X11Interop.XFlush(display);
				}
			});
		}

		public void DisableWindowTabbing(IntPtr windowHandle)
		{
			// Window tabbing is a macOS concept; nothing to do on Linux.
		}

		public void SetWindowAlpha(IntPtr windowHandle, double alpha)
		{
			WithWindow(windowHandle, (display, window) =>
			{
				var netWmWindowOpacity = X11Interop.XInternAtom(display, "_NET_WM_WINDOW_OPACITY", false);
				var cardinal = X11Interop.XInternAtom(display, "CARDINAL", false);
				if (netWmWindowOpacity == IntPtr.Zero || cardinal == IntPtr.Zero) return;

				if (alpha >= 1.0)
				{
					X11Interop.XDeleteProperty(display, window, netWmWindowOpacity);
				}
				else
				{
					var opacity = (uint)(Math.Max(0.0, alpha) * uint.MaxValue);
					X11Interop.XChangeProperty(display, window, netWmWindowOpacity, cardinal, 32, X11Interop.PropModeReplace, ref opacity, 1);
				}

				X11Interop.XFlush(display);
			});
		}

		public void SetWindowLevel(IntPtr windowHandle, int level)
		{
			WithWindow(windowHandle, (display, window) =>
			{
				// Map positive levels to always-on-top, negative to below, zero to normal,
				// approximating the NSWindow level semantics the interface was modeled on.
				var netWmState = X11Interop.XInternAtom(display, "_NET_WM_STATE", true);
				var stateAbove = X11Interop.XInternAtom(display, "_NET_WM_STATE_ABOVE", true);
				var stateBelow = X11Interop.XInternAtom(display, "_NET_WM_STATE_BELOW", true);
				if (netWmState == IntPtr.Zero || stateAbove == IntPtr.Zero || stateBelow == IntPtr.Zero) return;

				const long RemoveState = 0;
				const long AddState = 1;

				SendNetWmState(display, window, netWmState, level > 0 ? AddState : RemoveState, stateAbove);
				SendNetWmState(display, window, netWmState, level < 0 ? AddState : RemoveState, stateBelow);
				X11Interop.XFlush(display);
			});
		}

		public (double X, double Y, double Width, double Height) GetContentViewFrame(IntPtr windowHandle)
		{
			// X11 windows have no separate content view; report the window frame.
			return GetFrame(windowHandle);
		}

		public (double X, double Y) GetWindowContentOrigin(IntPtr windowHandle)
		{
			var (x, y, _, _) = GetFrame(windowHandle);
			return (x, y);
		}

		private static (double X, double Y, double Width, double Height) GetFrame(IntPtr windowHandle)
		{
			double x = 0, y = 0, width = 0, height = 0;
			WithWindow(windowHandle, (display, window) =>
			{
				if (X11Interop.XGetGeometry(display, window, out var root, out _, out _, out var w, out var h, out _, out _) == 0)
				{
					return;
				}

				// Geometry is relative to the parent (the WM frame); translate the
				// window origin into root coordinates.
				if (X11Interop.XTranslateCoordinates(display, window, root, 0, 0, out var rootX, out var rootY, out _) != 0)
				{
					x = rootX;
					y = rootY;
					width = w;
					height = h;
				}
			});

			return (x, y, width, height);
		}

		/// <summary>Runs an Xlib action against a window handle with the shared lock and error guard.</summary>
		private static void WithWindow(IntPtr windowHandle, Action<IntPtr, IntPtr> action)
		{
			if (windowHandle == IntPtr.Zero) return;

			lock (X11Interop.Lock)
			{
				var display = X11Interop.Display;
				if (display == IntPtr.Zero) return;

				try
				{
					X11Interop.WithIgnoredErrors(display, () => action(display, windowHandle));
				}
				catch
				{
					// Degrade to a no-op on any interop failure.
				}
			}
		}

		private static void SendRootClientMessage(IntPtr display, IntPtr window, IntPtr messageType, long data0)
		{
			var root = X11Interop.XRootWindow(display, X11Interop.XDefaultScreen(display));
			var evt = new X11Interop.XClientMessageEvent
			{
				Type = X11Interop.ClientMessage,
				Window = window,
				MessageType = messageType,
				Format = 32,
				Data0 = new IntPtr(data0),
			};
			X11Interop.XSendEvent(display, root, false, new IntPtr(X11Interop.SubstructureRedirectMask | X11Interop.SubstructureNotifyMask), ref evt);
		}

		private static void SendNetWmState(IntPtr display, IntPtr window, IntPtr netWmState, long action, IntPtr state)
		{
			var root = X11Interop.XRootWindow(display, X11Interop.XDefaultScreen(display));
			var evt = new X11Interop.XClientMessageEvent
			{
				Type = X11Interop.ClientMessage,
				Window = window,
				MessageType = netWmState,
				Format = 32,
				Data0 = new IntPtr(action),
				Data1 = state,
			};
			X11Interop.XSendEvent(display, root, false, new IntPtr(X11Interop.SubstructureRedirectMask | X11Interop.SubstructureNotifyMask), ref evt);
		}
	}
}