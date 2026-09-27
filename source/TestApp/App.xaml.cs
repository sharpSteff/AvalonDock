using System.Windows;
#if LIBREWPF
using LeXtudio.DevFlow.Agent.Core;
using LeXtudio.DevFlow.Agent.Wpf;
using Microsoft.Maui.DevFlow.Agent.Core;
#endif

namespace TestApp
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
#if LIBREWPF
        private WpfAgentService _devFlowService;
#endif

        public App()
        {
            //Dispatcher.Thread.CurrentUICulture = new System.Globalization.CultureInfo("ru");
        }
#if LIBREWPF

        // The DevFlow agent the Linux/macOS integration tests drive; only referenced for LibreWPF builds.
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            _devFlowService = this.AddWpfDevFlowAgent(new AgentOptions
            {
                Port = GetAgentPort()
            });
        }

        private static int GetAgentPort()
        {
            var portValue = System.Environment.GetEnvironmentVariable("DEVFLOW_AGENT_PORT");
            if (int.TryParse(portValue, out var parsedPort) && parsedPort > 0)
            {
                return parsedPort;
            }

            return DevFlowAgentPortResolver.GetPortFromAssemblyMetadata() ?? AgentOptions.DefaultPort;
        }
#endif
    }
}
