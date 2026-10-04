#if AVALONIA
using Avalonia.Controls;
using Avalonia.Input;
using FrameworkElement = Avalonia.Controls.Control;
using MouseButtonEventArgs = Avalonia.Input.TappedEventArgs;
#else
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
#endif
using ToggleTestApp.ViewModels;

namespace ToggleTestApp.Views;

public partial class SourceControlView : UserControl
{
	public SourceControlView()
	{
		InitializeComponent();
	}

	private void OnChangeItemClick(object? sender, MouseButtonEventArgs e)
	{
		if (sender is FrameworkElement fe && fe.DataContext is ChangeItem item
			&& DataContext is SourceControlViewModel vm)
		{
			vm.OpenFileCommand.Execute(item);
		}
	}
}