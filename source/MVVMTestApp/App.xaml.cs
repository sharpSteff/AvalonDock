using System.Windows;
#if !NETFRAMEWORK
using LeXtudio.DevFlow.Agent.WPF;
#endif

namespace AvalonDock.MVVMTestApp
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
#if !NETFRAMEWORK
		protected override void OnStartup(StartupEventArgs e)
		{
			base.OnStartup(e);
			// Embed the DevFlow HTTP agent (default port 9223) so integration tests can
			// drive the running dock and query its layout. See DockDiagnostics for the
			// [DevFlowAction] verbs.
			this.AddWpfDevFlowAgent();
		}
#endif
	}
}
