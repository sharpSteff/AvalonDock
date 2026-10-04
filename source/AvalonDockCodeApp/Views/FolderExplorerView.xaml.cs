#if AVALONIA
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
#else
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
#endif
using ToggleTestApp.ViewModels;

namespace ToggleTestApp.Views;

public partial class FolderExplorerView : UserControl
{
	public FolderExplorerView()
	{
		InitializeComponent();
#if AVALONIA
		Tree.AddHandler(TreeViewItem.ExpandedEvent, OnTreeViewItemExpanded);
#endif
	}

#if AVALONIA
	private void OnTreeViewItemExpanded(object? sender, RoutedEventArgs e)
	{
		if (e.Source is TreeViewItem { DataContext: FileTreeItem item })
#else
	private void OnTreeViewItemExpanded(object sender, RoutedEventArgs e)
	{
		if (e.OriginalSource is TreeViewItem { DataContext: FileTreeItem item })
#endif
		{
			item.LoadChildren();
		}
	}

#if AVALONIA
	private void OnTreeViewItemDoubleClick(object? sender, TappedEventArgs e)
#else
	private void OnTreeViewItemDoubleClick(object sender, MouseButtonEventArgs e)
#endif
	{
		// Prevent event bubbling from child TreeViewItems
		if (e.Handled) return;

		if (sender is TreeView tree && tree.SelectedItem is FileTreeItem { IsDirectory: false } item)
		{
			if (DataContext is FolderExplorerViewModel vm)
			{
				vm.OpenFileCommand.Execute(item);
				e.Handled = true;
			}
		}
	}
}