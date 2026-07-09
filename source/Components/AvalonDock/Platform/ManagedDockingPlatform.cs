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
	/// A managed, platform-neutral implementation of <see cref="IDockingPlatform"/> used on WPF
	/// hosts that do not expose the native user32 window API (for example LibreWPF on Linux).
	///
	/// The native operations are best-effort: window ownership, focus by handle and native
	/// z-order queries are handled by the WPF layer itself on these platforms, so the members
	/// below either no-op or report "not available" and let the caller fall back to its own
	/// managed ordering / logic.
	/// </summary>
	internal sealed class ManagedDockingPlatform : IDockingPlatform
	{
		/// <inheritdoc/>
		public bool SupportsGlobalWindowHooks => false;

		/// <inheritdoc/>
		public void SetOwnerWindow(IntPtr childHandle, IntPtr ownerHandle)
		{
			// Owner relationships are established through Window.Owner by the WPF layer.
		}

		/// <inheritdoc/>
		public IntPtr GetOwnerWindow(IntPtr childHandle) => IntPtr.Zero;

		/// <inheritdoc/>
		public IntPtr GetParentWindow(IntPtr handle) => IntPtr.Zero;

		/// <inheritdoc/>
		public bool SetFocus(IntPtr handle) => false;

		/// <inheritdoc/>
		public bool IsChildWindow(IntPtr parentHandle, IntPtr childHandle) => false;

		/// <inheritdoc/>
		public bool TryGetCursorPosition(out Point screenPosition)
		{
			// There is no backend-agnostic way to read the global cursor position in pure WPF;
			// callers fall back to WPF hit-testing (UIElement.IsMouseOver) when this is unavailable.
			screenPosition = default;
			return false;
		}

		/// <inheritdoc/>
		public void BringWindowToTop(IntPtr handle)
		{
			// z-order of managed windows is controlled through WPF (Window.Activate / Topmost).
		}

		/// <inheritdoc/>
		public IReadOnlyList<IntPtr> GetTopLevelWindowsByZOrder(IntPtr referenceWindow) => Array.Empty<IntPtr>();

		/// <inheritdoc/>
		public bool TryGetWindowZOrder(IntPtr handle, out int zOrder)
		{
			zOrder = 0;
			return false;
		}

		/// <inheritdoc/>
		public bool TryGetWorkAreaToClampInto(Rect windowRect, out Rect workArea)
		{
			// Approximate the native "nearest monitor" behaviour with the primary work area:
			// only clamp when the window does not intersect it at all.
			workArea = SystemParameters.WorkArea;
			return !workArea.IntersectsWith(windowRect);
		}
	}
}
