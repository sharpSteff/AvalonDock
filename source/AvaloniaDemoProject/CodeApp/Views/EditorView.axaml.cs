using System;
using Avalonia.Controls;
using AvaloniaEdit.Highlighting;
using ToggleTestApp.ViewModels;

namespace ToggleTestApp.Views;

public partial class EditorView : UserControl
{
	public EditorView()
	{
		InitializeComponent();
	}

	protected override void OnDataContextChanged(EventArgs e)
	{
		base.OnDataContextChanged(e);

		if (DataContext is EditorTabViewModel vm)
		{
			Editor.Text = vm.Content;

			try
			{
				var highlighting = HighlightingManager.Instance.GetDefinition(vm.SyntaxHighlighting);
				Editor.SyntaxHighlighting = highlighting;
			}
			catch
			{
				Editor.SyntaxHighlighting = null;
			}
		}
	}
}