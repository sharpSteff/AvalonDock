using Avalonia.Controls;
using Avalonia.Input;
using ToggleTestApp.ViewModels;

namespace ToggleTestApp.Views;

public partial class SourceControlView : UserControl
{
	public SourceControlView()
	{
		InitializeComponent();
	}

	private void OnChangeItemClick(object? sender, TappedEventArgs e)
	{
		if (sender is Control fe && fe.DataContext is ChangeItem item
			&& DataContext is SourceControlViewModel vm)
		{
			vm.OpenFileCommand.Execute(item);
		}
	}
}