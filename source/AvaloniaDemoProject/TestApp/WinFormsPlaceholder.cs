using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace TestApp
{
	/// <summary>
	/// Takes the place of the WPF TestApp's WinForms control (a WindowsFormsHost), which only exists on WPF.
	/// The tool window around it keeps its title, content id and close/hide rules.
	/// </summary>
	public class WinFormsPlaceholder : Border
	{
		public WinFormsPlaceholder()
		{
			Background = Brushes.Transparent;
			Child = new TextBlock
			{
				Text = "WinForms hosting is only available in the WPF application.",
				TextWrapping = TextWrapping.Wrap,
				Margin = new Avalonia.Thickness(6),
				VerticalAlignment = VerticalAlignment.Top,
			};
		}
	}
}
