/************************************************************************
   AvalonDock

   Copyright (C) 2007-2013 Xceed Software Inc.

   This program is provided to you under the terms of the Microsoft Public
   License (Ms-PL) as published at https://opensource.org/licenses/MS-PL
 ************************************************************************/

using System;
using System.Collections.Generic;
using System.Windows;

namespace AvalonDock.Platform
{
	/// <summary>
	/// Abstracts the operating-system specific windowing services that the docking
	/// framework relies on (window ownership, focus, cursor, z-order and monitor
	/// queries).
	///
	/// On Windows these map onto the <see cref="Win32Helper"/> P/Invoke calls. On
	/// cross-platform WPF hosts (for example LibreWPF on Linux) the native user32
	/// entry points are not available, so a managed best-effort implementation is
	/// used instead. Keeping the surface behind this interface allows the shared
	/// docking logic to run unchanged on every platform.
	/// </summary>
	internal interface IDockingPlatform
	{
		/// <summary>
		/// Gets a value indicating whether the platform supports installing a global
		/// window message hook (used for cross-window focus tracking). Only the native
		/// Windows implementation supports this.
		/// </summary>
		bool SupportsGlobalWindowHooks { get; }

		/// <summary>
		/// Gets a value indicating whether floating windows can be dragged through the native
		/// window move loop (started with <c>WM_NCLBUTTONDOWN</c> / <c>HT_CAPTION</c> and driven by
		/// <c>WM_MOVING</c>/<c>WM_EXITSIZEMOVE</c>). When <c>false</c> the docking framework uses a
		/// managed, WPF mouse-capture based drag instead.
		/// </summary>
		bool SupportsNativeWindowMoveLoop { get; }

		/// <summary>Sets the owner window of a top level window.</summary>
		/// <param name="childHandle">The handle of the owned window.</param>
		/// <param name="ownerHandle">The handle of the owner window (or <see cref="IntPtr.Zero"/> to clear it).</param>
		void SetOwnerWindow(IntPtr childHandle, IntPtr ownerHandle);

		/// <summary>Gets the owner window of a top level window.</summary>
		/// <param name="childHandle">The handle of the owned window.</param>
		/// <returns>The owner window handle, or <see cref="IntPtr.Zero"/> when there is none.</returns>
		IntPtr GetOwnerWindow(IntPtr childHandle);

		/// <summary>Gets the parent window of a window.</summary>
		/// <param name="handle">The window handle.</param>
		/// <returns>The parent window handle, or <see cref="IntPtr.Zero"/> when there is none.</returns>
		IntPtr GetParentWindow(IntPtr handle);

		/// <summary>Sets the keyboard focus to the window with the given handle.</summary>
		/// <param name="handle">The window handle to focus.</param>
		/// <returns><c>true</c> when a window previously had the focus (i.e. the call succeeded); otherwise <c>false</c>.</returns>
		bool SetFocus(IntPtr handle);

		/// <summary>Determines whether a window is a child (or descendant) of another window.</summary>
		/// <param name="parentHandle">The candidate parent window handle.</param>
		/// <param name="childHandle">The candidate child window handle.</param>
		/// <returns><c>true</c> when <paramref name="childHandle"/> is a descendant of <paramref name="parentHandle"/>.</returns>
		bool IsChildWindow(IntPtr parentHandle, IntPtr childHandle);

		/// <summary>Gets the current cursor position in screen (device) coordinates.</summary>
		/// <param name="screenPosition">The current cursor position, when available.</param>
		/// <returns><c>true</c> when the position could be determined; otherwise <c>false</c>.</returns>
		bool TryGetCursorPosition(out Point screenPosition);

		/// <summary>Raises a window to the top of the z-order without activating it.</summary>
		/// <param name="handle">The window handle.</param>
		void BringWindowToTop(IntPtr handle);

		/// <summary>
		/// Enumerates the top level windows related to <paramref name="referenceWindow"/> ordered
		/// from top-most to bottom-most in the z-order.
		/// </summary>
		/// <param name="referenceWindow">A window used as the starting point of the enumeration.</param>
		/// <returns>
		/// The ordered window handles, or an empty list when the platform cannot provide a
		/// native z-order (in which case callers should fall back to their own ordering).
		/// </returns>
		IReadOnlyList<IntPtr> GetTopLevelWindowsByZOrder(IntPtr referenceWindow);

		/// <summary>Gets the z-order index of a window (0 == top-most).</summary>
		/// <param name="handle">The window handle.</param>
		/// <param name="zOrder">The z-order index, when available.</param>
		/// <returns><c>true</c> when the z-order could be determined; otherwise <c>false</c>.</returns>
		bool TryGetWindowZOrder(IntPtr handle, out int zOrder);

		/// <summary>
		/// Determines whether a floating window rectangle needs to be clamped back onto a
		/// monitor and, if so, returns the working area to clamp it into.
		/// </summary>
		/// <param name="windowRect">The floating window rectangle in screen coordinates.</param>
		/// <param name="workArea">The working area to clamp into, when clamping is required.</param>
		/// <returns>
		/// <c>true</c> when <paramref name="windowRect"/> lies (partly) off screen and should be
		/// clamped into <paramref name="workArea"/>; <c>false</c> when it is already visible on a monitor.
		/// </returns>
		bool TryGetWorkAreaToClampInto(Rect windowRect, out Rect workArea);
	}
}
