using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;

namespace ToggleTestApp.Views;

/// <summary>The WPF application's DataTemplate for the problems toolbox.</summary>
public class ProblemsView : Border
{
	public ProblemsView()
	{
		Bind(BackgroundProperty, this.GetResourceObservable("AppEditorBg"));
		Padding = new Thickness(12);
		var text = new TextBlock { FontSize = 12 };
		text.Bind(TextBlock.TextProperty, new Binding("Status"));
		text.Bind(TextBlock.ForegroundProperty, text.GetResourceObservable("AppSubText"));
		Child = text;
	}
}