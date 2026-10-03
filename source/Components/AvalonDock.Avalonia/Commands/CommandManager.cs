using System;
using System.Collections.Generic;
using Avalonia.Threading;

namespace AvalonDock.Commands
{
	/// <summary>
	/// Minimal stand-in for WPF's <c>System.Windows.Input.CommandManager</c>, which Avalonia does not have.
	/// </summary>
	/// <remarks>
	/// WPF re-queries every command after each input event. Avalonia leaves that to the command, so the
	/// commands of this library raise <see cref="System.Windows.Input.ICommand.CanExecuteChanged"/> through
	/// this class whenever the layout changes (see <see cref="InvalidateRequerySuggested"/>). Avalonia's
	/// command sources unsubscribe when they leave the logical tree, which is why handlers are held strongly.
	/// </remarks>
	public static class CommandManager
	{
		private static readonly List<EventHandler> _handlers = new List<EventHandler>();
		private static bool _requeryPosted;

		/// <summary>Occurs when the state of the commands of this library may have changed.</summary>
		public static event EventHandler RequerySuggested
		{
			add
			{
				if (value == null) return;
				lock (_handlers) _handlers.Add(value);
			}

			remove
			{
				if (value == null) return;
				lock (_handlers) _handlers.Remove(value);
			}
		}

		/// <summary>
		/// Asks every command to re-evaluate whether it can execute. Calls are coalesced into one
		/// notification raised on the UI thread.
		/// </summary>
		public static void InvalidateRequerySuggested()
		{
			if (_requeryPosted) return;
			_requeryPosted = true;
			Dispatcher.UIThread.Post(RaiseRequerySuggested, DispatcherPriority.Background);
		}

		private static void RaiseRequerySuggested()
		{
			_requeryPosted = false;
			EventHandler[] handlers;
			lock (_handlers) handlers = _handlers.ToArray();
			foreach (var handler in handlers) handler(null, EventArgs.Empty);
		}
	}
}
