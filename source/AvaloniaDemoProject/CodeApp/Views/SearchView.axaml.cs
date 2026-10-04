using Avalonia.Controls;
using Avalonia.Input;
using ToggleTestApp.ViewModels;

namespace ToggleTestApp.Views;

public partial class SearchView : UserControl
{
	public SearchView()
	{
		InitializeComponent();
	}

	private void OnMatchClick(object? sender, TappedEventArgs e)
	{
		if (sender is Control fe && fe.DataContext is SearchMatch match
			&& DataContext is SearchViewModel vm)
		{
			vm.OpenMatchCommand.Execute(match);
		}
	}
}
