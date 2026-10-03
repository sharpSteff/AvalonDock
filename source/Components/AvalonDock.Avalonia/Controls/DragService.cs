using System.Collections.Generic;
using System.Linq;
using Avalonia;
using AvalonDock.Layout;

namespace AvalonDock.Controls
{
	/// <summary>
	/// Tracks a floating window being dragged: finds the host (manager or floating window) under the pointer,
	/// shows its drop targets and drops the window onto the target under the pointer when the drag ends.
	/// All points are screen coordinates in device pixels.
	/// </summary>
	internal class DragService
	{
		private readonly DockingManager _manager;
		private readonly LayoutFloatingWindowControl _floatingWindow;

		// A list of hosts that can display an overlay window and offer a drop target (docking position)
		private List<IOverlayWindowHost> _overlayWindowHosts = new List<IOverlayWindowHost>();

		private IOverlayWindowHost _currentHost;
		private IOverlayWindow _currentWindow;
		private readonly List<IDropArea> _currentWindowAreas = new List<IDropArea>();
		private IDropTarget _currentDropTarget;
		private bool _isDrag;
		private bool _isUpdatingMouseLocation;

		/// <summary>Initializes a new instance of the <see cref="DragService"/> class.</summary>
		/// <param name="floatingWindow">The dragged window.</param>
		public DragService(LayoutFloatingWindowControl floatingWindow)
		{
			_floatingWindow = floatingWindow;
			_manager = floatingWindow.Model.Root.Manager;
		}

		/// <summary>Gets the overlay currently shown.</summary>
		internal IOverlayWindow CurrentOverlayWindow => _currentWindow;

		/// <summary>Gets the drop target currently under the pointer.</summary>
		internal IDropTarget CurrentDropTarget => _currentDropTarget;

		/// <summary>Gets the host currently under the pointer.</summary>
		internal IOverlayWindowHost CurrentHost => _currentHost;

		/// <summary>Updates the drag for a new pointer position.</summary>
		/// <param name="dragPosition">The pointer position in screen pixels.</param>
		internal void UpdateMouseLocation(Point dragPosition)
		{
			if (_isUpdatingMouseLocation)
				return;

			_isUpdatingMouseLocation = true;
			try
			{
				// The floating window's model can be detached from the layout by a concurrent operation (docking,
				// re-floating, a layout reset) while the drag is still being tracked. Nothing meaningful can be
				// done with it then, so treat it as an aborted drag.
				if (_floatingWindow?.Model?.Root == null)
				{
					Abort();
					return;
				}

				if (!_isDrag)
				{
					// A previous drag that never reported its end may still have an overlay on screen.
					_manager?.HideAllOverlayWindows();
					GetOverlayWindowHosts();
					_isDrag = true;
				}

				var newHost = _overlayWindowHosts.FirstOrDefault(oh => oh.HitTestScreen(dragPosition));

				if (_currentHost != null || _currentHost != newHost)
				{
					// is mouse still inside current overlay window host?
					if ((_currentHost != null && !_currentHost.HitTestScreen(dragPosition)) || _currentHost != newHost)
					{
						// exit drop target
						if (_currentDropTarget != null)
							_currentWindow?.DragLeave(_currentDropTarget);
						_currentDropTarget = null;

						// exit area
						_currentWindowAreas.ForEach(a => _currentWindow?.DragLeave(a));
						_currentWindowAreas.Clear();

						// hide current overlay window
						_currentWindow?.DragLeave(_floatingWindow);
						if (_currentHost != null)
						{
							_currentHost.HideOverlayWindow();
							GetOverlayWindowHosts();
						}

						_currentHost = null;
					}

					if (_currentHost != newHost && newHost != null)
					{
						_currentHost = newHost;
						_currentWindow = _currentHost.ShowOverlayWindow(_floatingWindow);
						if (_currentWindow == null)
						{
							_currentHost = null;
							return;
						}

						_currentWindow.DragEnter(_floatingWindow);
						GetOverlayWindowHosts();
					}
				}

				if (_currentHost == null || _currentWindow == null)
					return;

				if (_currentDropTarget != null && !_currentDropTarget.HitTestScreen(dragPosition))
				{
					_currentWindow.DragLeave(_currentDropTarget);
					_currentDropTarget = null;
				}

				var areasToRemove = new List<IDropArea>();
				_currentWindowAreas.ForEach(a =>
				{
					// is mouse still inside this area?
					if (!a.DetectionRect.Contains(dragPosition))
					{
						_currentWindow.DragLeave(a);
						areasToRemove.Add(a);
					}
				});

				areasToRemove.ForEach(a => _currentWindowAreas.Remove(a));

				var areasToAdd = _currentHost.GetDropAreas(_floatingWindow)
					.Where(cw => !_currentWindowAreas.Contains(cw) && cw.DetectionRect.Contains(dragPosition))
					.ToList();

				_currentWindowAreas.AddRange(areasToAdd);
				areasToAdd.ForEach(a => _currentWindow.DragEnter(a));

				if (_currentDropTarget == null)
				{
					foreach (var unused in _currentWindowAreas)
					{
						_currentDropTarget = _currentWindow.GetTargets().FirstOrDefault(dt => dt.HitTestScreen(dragPosition));
						if (_currentDropTarget == null) continue;
						_currentWindow.DragEnter(_currentDropTarget);
						break;
					}
				}
			}
			finally
			{
				_isUpdatingMouseLocation = false;
			}
		}

		/// <summary>Ends the drag at <paramref name="dropLocation"/>, dropping onto the target there.</summary>
		/// <param name="dropLocation">The pointer position in screen pixels.</param>
		/// <param name="dropHandled">Set to <see langword="true"/> when the window was dropped onto a target.</param>
		internal void Drop(Point dropLocation, out bool dropHandled)
		{
			dropHandled = false;

			UpdateMouseLocation(dropLocation);

			var floatingWindowModel = _floatingWindow?.Model as LayoutFloatingWindow;
			var root = floatingWindowModel?.Root;

			_currentHost?.HideOverlayWindow();

			if (_currentDropTarget != null && root != null && _currentWindow != null)
			{
				_currentWindow.DragDrop(_currentDropTarget);
				root.CollectGarbage();
				dropHandled = true;
			}

			if (_currentWindow != null)
			{
				_currentWindowAreas.ForEach(a => _currentWindow.DragLeave(a));
				if (_currentDropTarget != null)
					_currentWindow.DragLeave(_currentDropTarget);
				_currentWindow.DragLeave(_floatingWindow);
			}

			_currentWindowAreas.Clear();
			_currentDropTarget = null;
			_currentWindow = null;
			_currentHost = null;
			_isDrag = false;

			// Every host that was asked to show an overlay during the drag is cleared (issue #587).
			_manager?.HideAllOverlayWindows();
		}

		/// <summary>Ends the drag without dropping.</summary>
		internal void Abort()
		{
			if (_currentWindow != null)
			{
				_currentWindowAreas.ForEach(a => _currentWindow.DragLeave(a));
				if (_currentDropTarget != null)
					_currentWindow.DragLeave(_currentDropTarget);
				_currentWindow.DragLeave(_floatingWindow);
			}

			_currentWindowAreas.Clear();
			_currentDropTarget = null;
			_currentWindow = null;
			_currentHost?.HideOverlayWindow();
			_currentHost = null;
			_isDrag = false;
			_manager?.HideAllOverlayWindows();
		}

		private void GetOverlayWindowHosts()
		{
			if (_manager?.Layout?.RootPanel?.CanDock == true)
				_manager.GetOverlayWindowHostsByZOrder(ref _overlayWindowHosts, _floatingWindow);
			else
				_overlayWindowHosts.Clear();
		}
	}
}
