using System;

namespace ToggleTestApp.ViewModels;

/// <summary>
/// The part of WPF's <c>Dispatcher</c> the shared view models use, on top of Avalonia's UI thread dispatcher.
/// The view models refer to it as <c>Dispatcher</c> through a using alias when compiled with AVALONIA.
/// </summary>
internal sealed class UiDispatcher
{
	private UiDispatcher()
	{
	}

	/// <summary>Gets the dispatcher of the UI thread.</summary>
	public static UiDispatcher CurrentDispatcher { get; } = new UiDispatcher();

	/// <summary>Runs <paramref name="action"/> on the UI thread and waits for it.</summary>
	/// <param name="action">The action.</param>
	public void Invoke(Action action) => Avalonia.Threading.Dispatcher.UIThread.Invoke(action);

	/// <summary>Queues <paramref name="action"/> on the UI thread.</summary>
	/// <param name="action">The action.</param>
	public void BeginInvoke(Action action) => Avalonia.Threading.Dispatcher.UIThread.Post(action);
}