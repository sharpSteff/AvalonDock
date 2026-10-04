using System;
using Avalonia;

namespace ToggleTestApp;

/// <summary>
/// Entry point of the Avalonia build; WPF generates its own from App.xaml.
/// </summary>
internal static class Program
{
	[STAThread]
	public static void Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

	public static AppBuilder BuildAvaloniaApp()
		=> AppBuilder.Configure<App>()
			.UsePlatformDetect()
			.LogToTrace();
}