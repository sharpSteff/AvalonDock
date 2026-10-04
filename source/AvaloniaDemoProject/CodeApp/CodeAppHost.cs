using System;
using Avalonia;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using AvalonDock;
using AvalonDock.Core;
using AvalonDock.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using ToggleTestApp.ViewModels;
using ToggleTestApp.Views;

namespace ToggleTestApp;

/// <summary>
/// Startup of the AvalonDockCodeApp port - what the WPF application's App.xaml.cs does. The service
/// configuration is that of the WPF application.
/// </summary>
public sealed class CodeAppHost : IDisposable
{
	private readonly ServiceProvider _serviceProvider;

	public CodeAppHost()
	{
		var services = new ServiceCollection();
		ConfigureServices(services);
		_serviceProvider = services.BuildServiceProvider();

		var application = Application.Current!;
		RegisterViews(application);
		SetAppThemeResources(application, isDark: true);
	}

	/// <summary>Creates the main window.</summary>
	/// <returns>The window.</returns>
	public MainWindow CreateMainWindow() => _serviceProvider.GetRequiredService<MainWindow>();

	/// <summary>
	/// Sets the colours the views use (resources AppPanelBg, AppText, ...) - the same values as in the WPF
	/// application. They are application resources, so tool windows in floating windows find them too.
	/// </summary>
	/// <param name="application">The application.</param>
	/// <param name="isDark">Whether the dark colours are wanted.</param>
	public static void SetAppThemeResources(Application application, bool isDark)
	{
		var resources = application.Resources;
		if (isDark)
		{
			resources["AppPanelBg"] = Brush("#252526");
			resources["AppEditorBg"] = Brush("#1E1E1E");
			resources["AppInputBg"] = Brush("#3C3C3C");
			resources["AppInputBarBg"] = Brush("#2D2D2D");
			resources["AppText"] = Brush("#CCCCCC");
			resources["AppSubText"] = Brush("#808080");
			resources["AppDimText"] = Brush("#555555");
			resources["AppEditorText"] = Brush("#D4D4D4");
			resources["AppLineNumbers"] = Brush("#858585");
			resources["AppScrollbarBg"] = Brush("#2B2B2B");
			resources["AppSelection"] = Brush("#094771");
		}
		else
		{
			resources["AppPanelBg"] = Brush("#F5F5F5");
			resources["AppEditorBg"] = Brush("#FFFFFF");
			resources["AppInputBg"] = Brush("#FFFFFF");
			resources["AppInputBarBg"] = Brush("#E8E8E8");
			resources["AppText"] = Brush("#1E1E1E");
			resources["AppSubText"] = Brush("#616161");
			resources["AppDimText"] = Brush("#999999");
			resources["AppEditorText"] = Brush("#1E1E1E");
			resources["AppLineNumbers"] = Brush("#858585");
			resources["AppScrollbarBg"] = Brush("#E0E0E0");
			resources["AppSelection"] = Brush("#B4D8FD");
		}
	}

	public void Dispose() => _serviceProvider.Dispose();

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

	/// <summary>
	/// The WPF application's implicit DataTemplates. They are registered with the application rather than the
	/// main window so that tool windows in floating and standalone windows find them as well.
	/// </summary>
	private static void RegisterViews(Application application)
	{
		application.DataTemplates.Add(new FuncDataTemplate<FolderExplorerViewModel>((_, _) => new FolderExplorerView()));
		application.DataTemplates.Add(new FuncDataTemplate<TerminalViewModel>((_, _) => new TerminalView()));
		application.DataTemplates.Add(new FuncDataTemplate<SearchViewModel>((_, _) => new SearchView()));
		application.DataTemplates.Add(new FuncDataTemplate<SourceControlViewModel>((_, _) => new SourceControlView()));
		application.DataTemplates.Add(new FuncDataTemplate<ProblemsViewModel>((_, _) => new ProblemsView()));
		application.DataTemplates.Add(new FuncDataTemplate<EditorTabViewModel>((_, _) => new EditorView()));
	}

	private static SolidColorBrush Brush(string hex) => new SolidColorBrush(Color.Parse(hex));
}
