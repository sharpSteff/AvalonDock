#if AVALONIA
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
#else
using System.Windows;
#endif
#if DEVFLOW_AGENT
using AvalonDock.UITests.Agent;
#endif

namespace TestApp
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            //Dispatcher.Thread.CurrentUICulture = new System.Globalization.CultureInfo("ru");
        }
#if AVALONIA

        public override void Initialize() => AvaloniaXamlLoader.Load(this);

        public override void OnFrameworkInitializationCompleted()
        {
            // What StartupUri="MainWindow.xaml" does in the WPF application.
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                desktop.MainWindow = new MainWindow();

            base.OnFrameworkInitializationCompleted();
#if DEVFLOW_AGENT
            DevFlowAgent.StartIfRequested(this);
#endif
        }
#elif DEVFLOW_AGENT

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            DevFlowAgent.StartIfRequested(this);
        }
#endif
    }
}
