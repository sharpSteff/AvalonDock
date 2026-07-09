/************************************************************************
   AvalonDock

   Copyright (C) 2007-2013 Xceed Software Inc.

   This program is provided to you under the terms of the Microsoft Public
   License (Ms-PL) as published at https://opensource.org/licenses/MS-PL
 ************************************************************************/

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;

namespace AvalonDock.Platform
{
	/// <summary>
	/// The native Windows implementation of <see cref="IDockingPlatform"/>. Every member simply
	/// delegates to the existing <see cref="Win32Helper"/> P/Invoke calls so the behaviour on
	/// Windows is identical to previous versions.
	/// </summary>
	internal sealed class Win32DockingPlatform : IDockingPlatform
	{
		private const uint MONITOR_DEFAULTTONULL = 0x00000000;
		private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

		/// <inheritdoc/>
		public bool SupportsGlobalWindowHooks => true;

		/// <inheritdoc/>
		public bool SupportsNativeWindowMoveLoop => true;

		/// <inheritdoc/>
		public void SetOwnerWindow(IntPtr childHandle, IntPtr ownerHandle) => Win32Helper.SetOwner(childHandle, ownerHandle);

		/// <inheritdoc/>
		public IntPtr GetOwnerWindow(IntPtr childHandle) => Win32Helper.GetOwner(childHandle);

		/// <inheritdoc/>
		public IntPtr GetParentWindow(IntPtr handle) => Win32Helper.GetParent(handle);

		/// <inheritdoc/>
		public bool SetFocus(IntPtr handle) => Win32Helper.SetFocus(handle) != IntPtr.Zero;

		/// <inheritdoc/>
		public bool IsChildWindow(IntPtr parentHandle, IntPtr childHandle) => Win32Helper.IsChild(parentHandle, childHandle);

		/// <inheritdoc/>
		public bool TryGetCursorPosition(out Point screenPosition)
		{
			var pt = new Win32Helper.Win32Point();
			if (Win32Helper.GetCursorPos(ref pt))
			{
				screenPosition = new Point(pt.X, pt.Y);
				return true;
			}

			screenPosition = default;
			return false;
		}

		/// <inheritdoc/>
		public void BringWindowToTop(IntPtr handle) => Win32Helper.BringWindowToTop(handle);

		/// <inheritdoc/>
		public IReadOnlyList<IntPtr> GetTopLevelWindowsByZOrder(IntPtr referenceWindow)
		{
			var result = new List<IntPtr>();
			var current = Win32Helper.GetWindow(referenceWindow, (uint)Win32Helper.GetWindow_Cmd.GW_HWNDFIRST);
			while (current != IntPtr.Zero)
			{
				result.Add(current);
				current = Win32Helper.GetWindow(current, (uint)Win32Helper.GetWindow_Cmd.GW_HWNDNEXT);
			}

			return result;
		}

		/// <inheritdoc/>
		public bool TryGetWindowZOrder(IntPtr handle, out int zOrder) => Win32Helper.GetWindowZOrder(handle, out zOrder);

		/// <inheritdoc/>
		public bool TryGetWorkAreaToClampInto(Rect windowRect, out Rect workArea)
		{
			workArea = Rect.Empty;

			var r = new Win32Helper.RECT
			{
				Left = (int)windowRect.Left,
				Top = (int)windowRect.Top,
				Right = (int)windowRect.Right,
				Bottom = (int)windowRect.Bottom
			};

			// When the rectangle already intersects a monitor there is nothing to clamp.
			if (Win32Helper.MonitorFromRect(ref r, MONITOR_DEFAULTTONULL) != IntPtr.Zero)
				return false;

			var nearestMonitor = Win32Helper.MonitorFromRect(ref r, MONITOR_DEFAULTTONEAREST);
			if (nearestMonitor == IntPtr.Zero)
				return false;

			var monitorInfo = new Win32Helper.MonitorInfo();
			monitorInfo.Size = Marshal.SizeOf(monitorInfo);
			Win32Helper.GetMonitorInfo(nearestMonitor, monitorInfo);

			workArea = new Rect(
				new Point(monitorInfo.Work.Left, monitorInfo.Work.Top),
				new Point(monitorInfo.Work.Right, monitorInfo.Work.Bottom));
			return true;
		}
	}
}
