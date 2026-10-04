using NUnit.Framework;

namespace AvalonDock.UITests.CodeApp;

/// <summary>The keyboard shortcuts of the AvalonDockCodeApp toolboxes.</summary>
public class ToggleDockShortcutTests : ToggleDockTestBase
{
	[Test, Order(1)]
	public async Task Shortcut_TogglesExplorer_OnAndOff()
	{
		await WaitForToggleStateAsync("Explorer", false, "Explorer to start unchecked");

		await PressKeyAsync("Ctrl+Shift+E");
		await WaitForToggleStateAsync("Explorer", true, "Explorer to dock after Ctrl+Shift+E");

		await PressKeyAsync("Ctrl+Shift+E");
		await WaitForToggleStateAsync("Explorer", false, "Explorer to hide after a second Ctrl+Shift+E");
	}

	[Test, Order(2)]
	public async Task Shortcut_TogglesOnlyTargetToolbox()
	{
		await WaitForToggleStateAsync("Search", false, "Search to start unchecked");

		await PressKeyAsync("Ctrl+Shift+F");

		await WaitForToggleStateAsync("Search", true, "Search to dock after Ctrl+Shift+F");
		Assert.That(await GetToggleStateAsync("Explorer"), Is.False, "Explorer should remain hidden when only the Search shortcut is pressed.");

		await PressKeyAsync("Ctrl+Shift+F");
		await WaitForToggleStateAsync("Search", false);
	}
}
