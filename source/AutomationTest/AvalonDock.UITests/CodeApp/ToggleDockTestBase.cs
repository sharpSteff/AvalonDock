using Microsoft.Maui.DevFlow.Driver;
using NUnit.Framework;

namespace AvalonDock.UITests.CodeApp;

/// <summary>
/// Base class of the tests of AvalonDockCodeApp, whose ToggleDockingManager shows its tool windows as toggle
/// buttons in side bars (ToggleDockButton, its title as content, in ToggleDockButtonBar_* bars).
/// </summary>
[Category("ToggleDock")]
public abstract class ToggleDockTestBase : UITestBase
{
	protected ToggleDockTestBase()
		: base(DemoApp.CodeApp)
	{
	}

	/// <summary>The side bar buttons, by title.</summary>
	protected async Task<List<ElementInfo>> GetToggleButtonsAsync()
		=> (await GetElementsAsync()).Where(e => e.Type == "ToggleDockButton" && e.IsVisible && e.Text != null).ToList();

	/// <summary>The titles of the side bar buttons, sorted.</summary>
	protected async Task<string[]> GetSidebarTitlesAsync()
		=> (await GetToggleButtonsAsync()).Select(b => b.Text!).OrderBy(t => t, StringComparer.Ordinal).ToArray();

	protected async Task<ElementInfo?> FindToggleButtonAsync(string title)
		=> (await GetToggleButtonsAsync()).FirstOrDefault(b => b.Text == title);

	protected static bool IsChecked(ElementInfo button)
		=> button.NativeProperties != null && button.NativeProperties.TryGetValue("isChecked", out var value) && value == "true";

	protected async Task<bool?> GetToggleStateAsync(string title)
		=> await FindToggleButtonAsync(title) is { } button ? IsChecked(button) : null;

	protected async Task WaitForToggleStateAsync(string title, bool isChecked, string? because = null)
		=> await WaitUntilAsync(async () => await GetToggleStateAsync(title) == isChecked,
			because ?? $"the '{title}' button to be {(isChecked ? "checked" : "unchecked")}");

	/// <summary>Clicks the side bar button of a tool window.</summary>
	protected async Task ClickToggleButtonAsync(string title)
		=> await TapAsync(await WaitForElementAsync(() => FindToggleButtonAsync(title), $"the '{title}' button"));

	/// <summary>An element of the tool window pane title of a docked tool window, by its template part name.</summary>
	protected async Task<ElementInfo?> FindTitlePartAsync(string partName)
		=> (await GetElementsAsync()).FirstOrDefault(e => e.IsVisible && e.NativeProperties != null
			&& e.NativeProperties.TryGetValue("name", out var name) && name == partName);
}
