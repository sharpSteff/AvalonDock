using System;
#if !AVALONIA
using System.Windows;
#endif
using AvalonDock;
using AvalonDock.Core;
using AvalonDock.DependencyInjection;
#if AVALONIA
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Templates;
using Avalonia.Markup.Xaml;
#endif
using Microsoft.Extensions.DependencyInjection;
using ToggleTestApp.ViewModels;
#if AVALONIA
using ToggleTestApp.Views;
#endif
#if DEVFLOW_AGENT
using AvalonDock.UITests.Agent;
#endif

namespace ToggleTestApp;

public partial class App : Application
{
	private IServiceProvider? _serviceProvider;

#if AVALONIA
	public override void Initialize() => AvaloniaXamlLoader.Load(this);

	public override void OnFrameworkInitializationCompleted()
	{
		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			var services = new ServiceCollection();
			ConfigureServices(services);
			_serviceProvider = services.BuildServiceProvider();
			RegisterViews();

			desktop.MainWindow = _serviceProvider.GetRequiredService<MainWindow>();
			desktop.Exit += (_, _) => (_serviceProvider as IDisposable)?.Dispose();
		}

		base.OnFrameworkInitializationCompleted();
#if DEVFLOW_AGENT
		DevFlowAgent.StartIfRequested(this);
#endif
	}

	/// <summary>
	/// The implicit DataTemplates of the WPF MainWindow.xaml. They are registered with the application rather
	/// than the main window so that tool windows in floating and standalone windows find them as well.
	/// </summary>
	private void RegisterViews()
	{
		DataTemplates.Add(new FuncDataTemplate<FolderExplorerViewModel>((_, _) => new FolderExplorerView()));
		DataTemplates.Add(new FuncDataTemplate<TerminalViewModel>((_, _) => new TerminalView()));
		DataTemplates.Add(new FuncDataTemplate<SearchViewModel>((_, _) => new SearchView()));
		DataTemplates.Add(new FuncDataTemplate<SourceControlViewModel>((_, _) => new SourceControlView()));
		DataTemplates.Add(new FuncDataTemplate<ProblemsViewModel>((_, _) => new ProblemsView()));
		DataTemplates.Add(new FuncDataTemplate<EditorTabViewModel>((_, _) => new EditorView()));
	}
#else
	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);

		var services = new ServiceCollection();
		ConfigureServices(services);
		_serviceProvider = services.BuildServiceProvider();

		var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
		mainWindow.Show();
#if DEVFLOW_AGENT
		DevFlowAgent.StartIfRequested(this);
#endif
	}
#endif

	private static void ConfigureServices(IServiceCollection services)
	{
		// Dock layout service — configure toggle dock options and toolboxes in one call
		services.AddDockLayoutService(dock =>
		{
			dock.ConfigureToggleDock(opts =>
			{
				opts.ButtonSize = 28;
				opts.DefaultDockWidth = 280;
				opts.DefaultDockHeight = 220;
				opts.LayoutPriority = nameof(DockLayoutPriority.BottomFullWidth);
			});

			// Register toolboxes — order determines sidebar button order
			dock.AddToolbox<FolderExplorerViewModel>(sp =>
				new FolderExplorerViewModel(_ => { }));
			dock.AddToolbox<SearchViewModel>();
			dock.AddToolbox<SourceControlViewModel>();
			dock.AddToolbox<ProblemsViewModel>();
			dock.AddToolbox<TerminalViewModel>();
		});

		// MainViewModel uses only the layout service — all anchorables accessible via GetAnchorable<T>()
		services.AddSingleton<MainViewModel>();

		// Main window
		services.AddSingleton<MainWindow>();
	}

#if !AVALONIA
	protected override void OnExit(ExitEventArgs e)
	{
		if (_serviceProvider is IDisposable disposable)
		{
			disposable.Dispose();
		}

		base.OnExit(e);
	}
#endif
}