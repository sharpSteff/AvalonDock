using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;

namespace AvaloniaDemoProject.Shared;

/// <summary>
/// A Yes/No question in a modal window - what the WPF applications ask with <c>MessageBox.Show</c>. The
/// buttons are named "Yes" and "No" like those of the message box, so UI tests can answer it the same way.
/// </summary>
internal sealed class ConfirmDialog : Window
{
	private ConfirmDialog(string message, string caption)
	{
		Title = caption;
		SizeToContent = SizeToContent.WidthAndHeight;
		CanResize = false;
		ShowInTaskbar = false;
		WindowStartupLocation = WindowStartupLocation.CenterOwner;

		var yes = new Button { Content = "Yes", Name = "Yes", MinWidth = 72, IsDefault = true, HorizontalContentAlignment = HorizontalAlignment.Center };
		var no = new Button { Content = "No", Name = "No", MinWidth = 72, IsCancel = true, HorizontalContentAlignment = HorizontalAlignment.Center };
		yes.Click += (_, _) => Close(true);
		no.Click += (_, _) => Close(false);

		Content = new StackPanel
		{
			Margin = new Thickness(20),
			Spacing = 16,
			Children =
			{
				new TextBlock { Text = message, MaxWidth = 420, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
				new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { yes, no } },
			},
		};
	}

	/// <summary>
	/// Asks <paramref name="message"/> and waits for the answer, like WPF's <c>MessageBox.Show</c>: the caller
	/// can use the answer right away (to cancel a closing document, say) while the UI keeps running in a
	/// nested frame.
	/// </summary>
	/// <param name="owner">The window the question belongs to.</param>
	/// <param name="message">The question.</param>
	/// <param name="caption">The window title.</param>
	/// <returns><see langword="true"/> for "Yes".</returns>
	public static bool Ask(Window owner, string message, string caption)
	{
		var asking = AskAsync(owner, message, caption);
		var frame = new DispatcherFrame();
		asking.ContinueWith(_ => frame.Continue = false, TaskScheduler.FromCurrentSynchronizationContext());
		Dispatcher.UIThread.PushFrame(frame);
		return asking.Result;
	}

	/// <summary>Asks <paramref name="message"/> and returns whether "Yes" was chosen.</summary>
	/// <param name="owner">The window the question belongs to.</param>
	/// <param name="message">The question.</param>
	/// <param name="caption">The window title.</param>
	/// <returns><see langword="true"/> for "Yes".</returns>
	public static Task<bool> AskAsync(Window owner, string message, string caption)
		=> new ConfirmDialog(message, caption).ShowDialog<bool>(owner);
}
