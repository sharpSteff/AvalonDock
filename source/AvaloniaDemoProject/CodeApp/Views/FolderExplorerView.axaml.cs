using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ToggleTestApp.ViewModels;

namespace ToggleTestApp.Views;

public partial class FolderExplorerView : UserControl
{
	public FolderExplorerView()
	{
		InitializeComponent();
		Tree.AddHandler(TreeViewItem.ExpandedEvent, OnTreeViewItemExpanded);
	}

	private void OnTreeViewItemExpanded(object? sender, RoutedEventArgs e)
	{
		if (e.Source is TreeViewItem { DataContext: FileTreeItem item })
		{
			item.LoadChildren();
		}
	}

	private void OnTreeViewItemDoubleClick(object? sender, TappedEventArgs e)
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
