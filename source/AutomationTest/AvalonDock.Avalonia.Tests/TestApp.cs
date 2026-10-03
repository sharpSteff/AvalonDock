using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;
using AvalonDock.Avalonia.Tests;
using AvalonDock.Themes;

[assembly: AvaloniaTestApplication(typeof(TestApp))]

namespace AvalonDock.Avalonia.Tests
{
	/// <summary>Application used by the headless tests: Fluent plus the AvalonDock default theme.</summary>
	public class TestApp : Application
	{
		public static AppBuilder BuildAvaloniaApp()
			=> AppBuilder.Configure<TestApp>()
				.UseSkia()
				.UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });

		public override void Initialize()
		{
			Styles.Add(new FluentTheme());
			Styles.Add(new AvalonDockTheme());
		}
	}
}
