using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace Microsoft.Win32;

/// <summary>
/// Stand-in for WPF's <c>Microsoft.Win32.OpenFolderDialog</c>, so that the shared MainViewModel compiles and
/// works unchanged: a modal folder picker through Avalonia's storage provider.
/// </summary>
public sealed class OpenFolderDialog
{
	/// <summary>Gets or sets the title of the dialog.</summary>
	public string Title { get; set; } = string.Empty;

	/// <summary>Gets the chosen folder.</summary>
	public string FolderName { get; private set; } = string.Empty;

	/// <summary>Shows the dialog and waits for it, like WPF's modal <c>ShowDialog</c>.</summary>
	/// <returns><see langword="true"/> when a folder was chosen.</returns>
	public bool? ShowDialog()
	{
		var owner = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Windows
			.FirstOrDefault(w => w.IsActive) ?? (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
		if (owner == null) return false;

		var picking = owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = Title, AllowMultiple = false });

		// Modal like the WPF dialog: keep the UI responsive in a nested frame until the picker is closed.
		var frame = new DispatcherFrame();
		picking.ContinueWith(_ => frame.Continue = false, TaskScheduler.FromCurrentSynchronizationContext());
		Dispatcher.UIThread.PushFrame(frame);

		var folder = picking.Result.FirstOrDefault();
		var path = folder?.TryGetLocalPath();
		if (string.IsNullOrEmpty(path)) return false;
		FolderName = path;
		return true;
	}
}
