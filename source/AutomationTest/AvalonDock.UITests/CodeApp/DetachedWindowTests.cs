using NUnit.Framework;

namespace AvalonDock.UITests.CodeApp;

/// <summary>
/// The "Window" view mode, which moves the content of a tool window into a standalone top level window. It is
/// driven through the three dot options menu of a docked tool window title (View Mode > Window).
/// </summary>
public class DetachedWindowTests : ToggleDockTestBase
{
	private const string ToolWindow = "Explorer";

	/// <summary>Leaves every test with the tool window docked again and nothing detached.</summary>
	[TearDown]
	public async Task DockBackAsync()
	{
		if (Session.HasExited)
			return;

		if ((await FindContentAsync(ToolWindow))?.IsDetached == true)
		{
			await InvokeAsync("avalondock-close-window", ToolWindow);
			await WaitUntilAsync(async () => (await FindContentAsync(ToolWindow))?.IsDetached == false, "the standalone window to close");
		}
	}

	[Test, Order(1)]
	public async Task ViewModeMenu_OffersWindowItem()
	{
		await OpenViewModeSubmenuAsync();

		Assert.That(await FindMenuItemAsync("Window"), Is.Not.Null, "The View Mode submenu should offer a 'Window' entry.");
		await PressKeyAsync("Escape");
		await PressKeyAsync("Escape");
	}

	[Test, Order(2)]
	public async Task SelectWindowMode_DetachesIntoTopLevelWindow()
	{
		await DetachAsync();

		var windows = await GetWindowsAsync();
		Assert.That(windows.Any(w => w.Kind == "Detached" && w.Title == ToolWindow && w.IsVisible), Is.True,
			$"A standalone window titled '{ToolWindow}' should appear.");
	}

	/// <summary>Minimizing the standalone window leaves the main window alone: it is a window of its own.</summary>
	[Test, Order(3)]
	public async Task DetachedWindow_IsIndependentTopLevelWindow()
	{
		await DetachAsync();

		Assert.That(await InvokeAsync("avalondock-set-window-state", ToolWindow, "Minimized"), Is.EqualTo("True"));
		await SettleAsync();
		var mainWhileMinimized = (await GetWindowsAsync()).Single(w => w.Kind == "Main");
		await InvokeAsync("avalondock-set-window-state", ToolWindow, "Normal");

		Assert.That(mainWhileMinimized.State, Is.Not.EqualTo("Minimized"), "Minimizing the detached window must leave the main window untouched.");
	}

	/// <summary>Closing the standalone window hands the content back to the docking layout.</summary>
	[Test, Order(4)]
	public async Task ClosingDetachedWindow_DocksContentBack()
	{
		await DetachAsync();

		Assert.That(await InvokeAsync("avalondock-close-window", ToolWindow), Is.EqualTo("True"));

		await WaitUntilAsync(async () => (await FindContentAsync(ToolWindow))?.IsDetached == false, "the content to be docked back");
		Assert.That((await GetWindowsAsync()).Any(w => w.Kind == "Detached" && w.IsVisible), Is.False, "The standalone window should be gone.");
		Assert.That(Session.HasExited, Is.False, "Closing the standalone window must not close the application.");
		Assert.That(await FindToggleButtonAsync(ToolWindow), Is.Not.Null, $"'{ToolWindow}' should be back in the side bar.");
	}

	/// <summary>Selecting "Window" a second time is the way back.</summary>
	[Test, Order(5)]
	public async Task SelectWindowMode_Twice_DocksContentBack()
	{
		await DetachAsync();

		// The title bar is gone with the docked tool window, so the side bar button carries the menu.
		await RightTapAsync(await WaitForElementAsync(() => FindToggleButtonAsync(ToolWindow), $"the '{ToolWindow}' button"));
		await TapAsync(await WaitForElementAsync(() => FindMenuItemAsync("View Mode"), "the 'View Mode' entry"));
		await TapAsync(await WaitForElementAsync(() => FindMenuItemAsync("Window"), "the 'Window' entry"));

		await WaitUntilAsync(async () => (await FindContentAsync(ToolWindow))?.IsDetached == false,
			"selecting 'Window' again to dock the content back");
		Assert.That(Session.HasExited, Is.False);
	}

	/// <summary>While detached, the side bar button brings the window forward instead of docking an empty tool window.</summary>
	[Test, Order(6)]
	public async Task StripeButton_WhileDetached_DoesNotDockAndKeepsWindowAlive()
	{
		await DetachAsync();

		await ClickToggleButtonAsync(ToolWindow);

		Assert.That(Session.HasExited, Is.False);
		Assert.That((await FindContentAsync(ToolWindow))?.IsDetached, Is.True, "The tool window should stay detached.");
		Assert.That((await GetWindowsAsync()).Any(w => w.Kind == "Detached" && w.Title == ToolWindow && w.IsVisible), Is.True,
			"The standalone window should survive a click on the side bar button.");
	}

	/// <summary>The standalone window carries the options menu, so the view mode can be changed from it.</summary>
	[Test, Order(7)]
	public async Task DetachedWindow_CarriesOptionsMenu()
	{
		await DetachAsync();

		var optionsButton = await WaitForElementAsync(
			async () => (await GetTreeAsync())
				.Where(root => root.Type == "DetachedAnchorableWindow")
				.Select(root => FindDescendant(root, e => e.IsVisible && e.NativeProperties != null
					&& e.NativeProperties.TryGetValue("name", out var name) && name == "PART_OptionsButton"))
				.FirstOrDefault(b => b != null),
			"the options button of the standalone window");
		await TapAsync(optionsButton);

		Assert.That(await WaitForElementAsync(() => FindMenuItemAsync("View Mode"), "the 'View Mode' entry"), Is.Not.Null,
			"The menu opened from the standalone window should offer the view modes.");
		await PressKeyAsync("Escape");
	}

	private Task<Microsoft.Maui.DevFlow.Driver.ElementInfo?> FindMenuItemAsync(string header)
		=> FindElementAsync(e => e.Type == "MenuItem" && e.IsVisible && MenuHeaderIs(e, header));

	/// <summary>Docks the tool window (its title bar carries the menu) and opens View Mode of its options menu.</summary>
	private async Task OpenViewModeSubmenuAsync()
	{
		if (await GetToggleStateAsync(ToolWindow) != true)
		{
			await ClickToggleButtonAsync(ToolWindow);
			await WaitForToggleStateAsync(ToolWindow, true);
		}

		await TapAsync(await WaitForElementAsync(() => FindTitlePartAsync("PART_OptionsButton"), "the options button of the tool window"));
		await TapAsync(await WaitForElementAsync(() => FindMenuItemAsync("View Mode"), "the 'View Mode' entry"));
	}

	private async Task DetachAsync()
	{
		await OpenViewModeSubmenuAsync();
		await TapAsync(await WaitForElementAsync(() => FindMenuItemAsync("Window"), "the 'Window' entry"));
		await WaitUntilAsync(async () => (await FindContentAsync(ToolWindow))?.IsDetached == true, $"'{ToolWindow}' to move into a standalone window");
	}
}
