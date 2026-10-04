using NUnit.Framework;

namespace AvalonDock.UITests.CodeApp;

/// <summary>AvalonDockCodeApp starts with its documents and side bar buttons.</summary>
public class ToggleDockSmokeTests : ToggleDockTestBase
{
	[Test, Order(1)]
	public void Application_Starts_MainWindowAppears()
	{
		Assert.That(Session.HasExited, Is.False, "Application should still be running.");
	}

	[Test, Order(2)]
	public async Task MainWindow_HasCorrectTitle()
	{
		Assert.That((await GetLayoutAsync()).WindowTitle, Does.Contain("AvalonDock Code"), "Main window should have the AvalonDock Code title.");
	}

	[Test, Order(3)]
	public async Task Documents_ArePresent()
	{
		Assert.That(await FindDocumentTabAsync("Welcome"), Is.Not.Null, "Welcome document should be present.");
	}

	[Test, Order(4)]
	public async Task ToggleButtons_ArePresent_ForLeftSide()
	{
		Assert.That(await FindToggleButtonAsync("Explorer"), Is.Not.Null, "Explorer toggle button should be present.");
		Assert.That(await FindToggleButtonAsync("Search"), Is.Not.Null, "Search toggle button should be present.");
	}

	[Test, Order(5)]
	public async Task ToggleButtons_ArePresent_ForBottomSide()
	{
		Assert.That(await FindToggleButtonAsync("Problems"), Is.Not.Null, "Problems toggle button should be present.");
		Assert.That(await FindToggleButtonAsync("Terminal"), Is.Not.Null, "Terminal toggle button should be present.");
	}

	/// <summary>All tool windows start auto-hidden except the terminal.</summary>
	[Test, Order(6)]
	public async Task ToggleButtons_InitialState_AllUncheckedButTerminal()
	{
		var buttons = await GetToggleButtonsAsync();
		Assert.That(buttons, Is.Not.Empty);
		foreach (var button in buttons)
		{
			Assert.That(IsChecked(button), Is.EqualTo(button.Text == "Terminal"),
				$"Button '{button.Text}' should be {(button.Text == "Terminal" ? "checked" : "unchecked")} initially.");
		}
	}
}

/// <summary>Docking and hiding tool windows through their side bar buttons.</summary>
public class ToggleDockActivationTests : ToggleDockTestBase
{
	[Test, Order(1)]
	public async Task ClickToggleButton_DocksAnchorable()
	{
		await ClickToggleButtonAsync("Explorer");

		await WaitForToggleStateAsync("Explorer", true, "the Explorer button to be checked after clicking it");
		Assert.That((await FindContentAsync("Explorer"))?.IsAutoHidden, Is.False, "Explorer should be docked.");

		await ClickToggleButtonAsync("Explorer");
		await WaitForToggleStateAsync("Explorer", false);
	}

	[Test, Order(2)]
	public async Task ClickToggleButton_Again_HidesAnchorable()
	{
		await ClickToggleButtonAsync("Explorer");
		await WaitForToggleStateAsync("Explorer", true);

		await ClickToggleButtonAsync("Explorer");

		await WaitForToggleStateAsync("Explorer", false, "the Explorer button to be unchecked after a second click");
	}

	/// <summary>Only one tool window of a side bar section is docked at a time.</summary>
	[Test, Order(3)]
	public async Task ExclusiveToggle_SameSection_OnlyOneActive()
	{
		await ClickToggleButtonAsync("Explorer");
		await WaitForToggleStateAsync("Explorer", true);

		await ClickToggleButtonAsync("Search");

		await WaitForToggleStateAsync("Search", true, "Search to be checked");
		await WaitForToggleStateAsync("Explorer", false, "Explorer to be unchecked (exclusive per section)");

		await ClickToggleButtonAsync("Search");
		await WaitForToggleStateAsync("Search", false);
	}

	[Test, Order(4)]
	public async Task DifferentSections_Toggle_WorkIndependently()
	{
		var explorerBefore = await GetToggleStateAsync("Explorer");
		var terminalBefore = await GetToggleStateAsync("Terminal");

		await ClickToggleButtonAsync("Explorer");
		await ClickToggleButtonAsync("Terminal");

		await WaitForToggleStateAsync("Explorer", explorerBefore != true, "Explorer to change");
		await WaitForToggleStateAsync("Terminal", terminalBefore != true, "Terminal to change, independently of Explorer");

		await ClickToggleButtonAsync("Explorer");
		await ClickToggleButtonAsync("Terminal");

		await WaitForToggleStateAsync("Explorer", explorerBefore == true, "Explorer to be back to its start state");
		await WaitForToggleStateAsync("Terminal", terminalBefore == true, "Terminal to be back to its start state");
	}

	/// <summary>The minimize button of a docked tool window sends it back to the side bar.</summary>
	[Test, Order(5)]
	public async Task MinimizeButton_SendsBackToSidebar()
	{
		await ClickToggleButtonAsync("Explorer");
		await WaitForToggleStateAsync("Explorer", true);

		var minimize = await WaitForElementAsync(
			async () => await FindTitlePartAsync("PART_MinimizeButton") ?? await FindTitlePartAsync("PART_AutoHidePin"),
			"the minimize button of the Explorer pane");
		await TapAsync(minimize);

		await WaitForToggleStateAsync("Explorer", false, "Explorer to be unchecked after clicking the minimize button");
	}
}
