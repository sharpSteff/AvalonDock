using System;
using Avalonia.Threading;
using AvalonDock.Layout;

namespace AvalonDock.Controls
{
	/// <summary>Shows the auto-hide flyout and closes it again once it is no longer used.</summary>
	internal class AutoHideWindowManager
	{
		private readonly DockingManager _manager;
		private WeakReference _currentAutohiddenAnchor = null;
		private DispatcherTimer _closeTimer = null;

		/// <summary>Initializes a new instance of the <see cref="AutoHideWindowManager"/> class.</summary>
		/// <param name="manager">The owning manager.</param>
		internal AutoHideWindowManager(DockingManager manager)
		{
			_manager = manager;
			SetupCloseTimer();
		}

		/// <summary>Shows the flyout for <paramref name="anchor"/>.</summary>
		/// <param name="anchor">The anchor.</param>
		public void ShowAutoHideWindow(LayoutAnchorControl anchor)
		{
			if (_currentAutohiddenAnchor.GetValueOrDefault<LayoutAnchorControl>() == anchor) return;
			StopCloseTimer();
			_currentAutohiddenAnchor = new WeakReference(anchor);
			_manager.AutoHideWindow?.Show(anchor);
			StartCloseTimer();
		}

		/// <summary>Hides the flyout, if it belongs to <paramref name="anchor"/> (or unconditionally for <see langword="null"/>).</summary>
		/// <param name="anchor">The anchor, or <see langword="null"/>.</param>
		public void HideAutoWindow(LayoutAnchorControl anchor = null)
		{
			if (anchor == null || anchor == _currentAutohiddenAnchor.GetValueOrDefault<LayoutAnchorControl>())
				StopCloseTimer();
		}

		private void SetupCloseTimer()
		{
			_closeTimer = new DispatcherTimer(DispatcherPriority.Background)
			{
				Interval = TimeSpan.FromMilliseconds(Math.Max(1, _manager.AutoHideDelay)),
			};
			_closeTimer.Tick += (s, e) =>
			{
				var autoHideWindow = _manager?.AutoHideWindow;
				if (autoHideWindow == null)
				{
					StopCloseTimer();
					return;
				}

				if (autoHideWindow.IsPointerOverFlyoutOrAnchor ||
					autoHideWindow.IsResizing ||
					(autoHideWindow.Model is LayoutAnchorable model && model.IsActive))
					return;

				StopCloseTimer();
			};
		}

		private void StartCloseTimer()
		{
			_closeTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(1, _manager.AutoHideDelay));
			_closeTimer.Start();
		}

		private void StopCloseTimer()
		{
			_closeTimer.Stop();
			_manager.AutoHideWindow?.Hide();
			_currentAutohiddenAnchor = null;
		}
	}
}
