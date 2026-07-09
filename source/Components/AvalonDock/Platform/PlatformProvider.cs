/************************************************************************
   AvalonDock

   Copyright (C) 2007-2013 Xceed Software Inc.

   This program is provided to you under the terms of the Microsoft Public
   License (Ms-PL) as published at https://opensource.org/licenses/MS-PL
 ************************************************************************/

using System;
using System.Runtime.InteropServices;

namespace AvalonDock.Platform
{
	/// <summary>
	/// Provides access to the <see cref="IDockingPlatform"/> implementation appropriate for the
	/// current operating system. On Windows this is the native <see cref="Win32DockingPlatform"/>;
	/// on other WPF hosts (for example LibreWPF on Linux) it is the managed
	/// <see cref="ManagedDockingPlatform"/>.
	/// </summary>
	internal static class PlatformProvider
	{
		private static IDockingPlatform _current;

		/// <summary>
		/// Gets or sets the docking platform implementation used by the framework. The default is
		/// selected automatically from the current operating system; a custom implementation can be
		/// assigned (for example from unit tests) before the docking framework is used.
		/// </summary>
		public static IDockingPlatform Current
		{
			get => _current ??= CreateDefault();
			set => _current = value ?? throw new ArgumentNullException(nameof(value));
		}

		private static IDockingPlatform CreateDefault()
		{
			return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
				? new Win32DockingPlatform()
				: (IDockingPlatform)new ManagedDockingPlatform();
		}
	}
}
