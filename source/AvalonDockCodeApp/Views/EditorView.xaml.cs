#if AVALONIA
using System;
using Avalonia.Controls;
using AvaloniaEdit.Highlighting;
#else
using System.Windows;
using System.Windows.Controls;
using ICSharpCode.AvalonEdit.Highlighting;
#endif
using ToggleTestApp.ViewModels;

namespace ToggleTestApp.Views;

public partial class EditorView : UserControl
{
	public EditorView()
	{
		InitializeComponent();
#if !AVALONIA
		DataContextChanged += OnDataContextChanged;
#endif
	}

#if AVALONIA
	protected override void OnDataContextChanged(EventArgs e)
	{
		base.OnDataContextChanged(e);

		if (DataContext is EditorTabViewModel vm)
#else
	private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
	{
		if (e.NewValue is EditorTabViewModel vm)
#endif
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