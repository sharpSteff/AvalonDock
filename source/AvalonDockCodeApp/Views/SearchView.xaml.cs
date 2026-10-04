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

public partial class SearchView : UserControl
{
	public SearchView()
	{
		InitializeComponent();
	}

	private void OnMatchClick(object? sender, MouseButtonEventArgs e)
	{
		if (sender is FrameworkElement fe && fe.DataContext is SearchMatch match
			&& DataContext is SearchViewModel vm)
		{
			vm.OpenMatchCommand.Execute(match);
		}
	}
}