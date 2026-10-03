using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace AvalonDock.Avalonia.Tests
{
	public partial class XamlLayoutView : UserControl
	{
		public XamlLayoutView() => AvaloniaXamlLoader.Load(this);

		public DockingManager DockingManager => this.FindControl<DockingManager>("Manager");
	}
}
