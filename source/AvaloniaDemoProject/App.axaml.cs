using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace AvaloniaDemoProject;

public partial class App : Application
{
	private IDisposable? _codeApp;

	public override void Initialize() => AvaloniaXamlLoader.Load(this);

	public override void OnFrameworkInitializationCompleted()
	{
		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			if (Program.IsCodeApp(desktop.Args ?? Array.Empty<string>()))
			{
				var codeApp = new ToggleTestApp.CodeAppHost();
				_codeApp = codeApp;
				desktop.MainWindow = codeApp.CreateMainWindow();
				desktop.Exit += (_, _) => _codeApp?.Dispose();
			}
			else
			{
				desktop.MainWindow = new TestApp.MainWindow();
			}
		}

		base.OnFrameworkInitializationCompleted();
	}
}