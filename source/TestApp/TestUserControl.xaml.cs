#if AVALONIA
using Avalonia.Controls;
using Avalonia.Interactivity;
#else
using System.Windows;
using System.Windows.Controls;
#endif

namespace TestApp
{
	/// <summary>
	/// Interaction logic for TestUserControl.xaml
	/// </summary>
	public partial class TestUserControl : UserControl
	{
		public TestUserControl()
		{
			InitializeComponent();

			this.Loaded += TestUserControl_Loaded;
			this.Unloaded += TestUserControl_Unloaded;
		}

		void TestUserControl_Unloaded(object sender, RoutedEventArgs e)
		{

		}

		void TestUserControl_Loaded(object sender, RoutedEventArgs e)
		{

		}
	}
}