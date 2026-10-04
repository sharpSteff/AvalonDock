using System;
using System.Linq;
using Avalonia;

namespace AvaloniaDemoProject;

internal static class Program
{
	/// <summary>
	/// Starts the TestApp port, or the AvalonDockCodeApp port with the arguments <c>--app code</c>.
	/// </summary>
	[STAThread]
	public static void Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

	public static AppBuilder BuildAvaloniaApp()
		=> AppBuilder.Configure<App>()
			.UsePlatformDetect()
			.LogToTrace();

	/// <summary>Gets a value indicating whether the arguments ask for the AvalonDockCodeApp port.</summary>
	internal static bool IsCodeApp(string[] args)
	{
		var index = Array.FindIndex(args, a => a is "--app" or "-app" or "/app");
		return index >= 0 && index + 1 < args.Length && args[index + 1].Equals("code", StringComparison.OrdinalIgnoreCase)
			|| args.Any(a => a.Equals("--code", StringComparison.OrdinalIgnoreCase));
	}
}
