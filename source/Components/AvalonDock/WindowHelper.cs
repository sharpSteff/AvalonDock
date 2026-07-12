using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using AvalonDock.Platform;

namespace AvalonDock
{
	/// <summary>
	/// Provides helper members for window Helper.
	/// </summary>
	internal static class WindowHelper
	{
		/// <summary>
		/// Executes the is Attached To Presentation Source operation.
		/// </summary>
		/// <param name="element">The element.</param>
		/// <returns>true if the operation succeeds; otherwise, false.</returns>
		public static bool IsAttachedToPresentationSource(this Visual element)
		{
			return PresentationSource.FromVisual(element) != null;
		}

		/// <summary>
		/// Sets the set Parent To Main Window Of.
		/// </summary>
		/// <param name="window">The window.</param>
		/// <param name="element">The element.</param>
		public static void SetParentToMainWindowOf(this Window window, Visual element)
		{
			var wndParent = Window.GetWindow(element);
			if (wndParent != null)
			{
				window.Owner = wndParent;
			}
			else
			{
				if (GetParentWindowHandle(element, out IntPtr parentHwnd))
					PlatformProvider.Current.SetOwnerWindow(new WindowInteropHelper(window).Handle, parentHwnd);
			}
		}

		/// <summary>
		/// Gets the get Parent Window Handle.
		/// </summary>
		/// <param name="window">The window.</param>
		/// <returns>The requested value.</returns>
		public static IntPtr GetParentWindowHandle(this Window window)
		{
			if (window.Owner != null)
				return new WindowInteropHelper(window.Owner).Handle;
			else
				return PlatformProvider.Current.GetOwnerWindow(new WindowInteropHelper(window).Handle);
		}

		/// <summary>
		/// Gets the get Parent Window Handle.
		/// </summary>
		/// <param name="element">The element.</param>
		/// <param name="hwnd">The hwnd.</param>
		/// <returns>true if the operation succeeds; otherwise, false.</returns>
		public static bool GetParentWindowHandle(this Visual element, out IntPtr hwnd)
		{
			hwnd = IntPtr.Zero;

			if (!(PresentationSource.FromVisual(element) is HwndSource wpfHandle))
				return false;

			hwnd = PlatformProvider.Current.GetParentWindow(wpfHandle.Handle);
			if (hwnd == IntPtr.Zero)
				hwnd = wpfHandle.Handle;
			return true;
		}

		/// <summary>
		/// Sets the set Parent Window To Null.
		/// </summary>
		/// <param name="window">The window.</param>
		public static void SetParentWindowToNull(this Window window)
		{
			if (window.Owner != null)
			{
				window.Owner = null;
			}
			else
			{
				PlatformProvider.Current.SetOwnerWindow(new WindowInteropHelper(window).Handle, IntPtr.Zero);
			}
		}
	}
}