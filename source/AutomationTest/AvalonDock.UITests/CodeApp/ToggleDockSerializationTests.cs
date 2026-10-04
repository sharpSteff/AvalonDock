using NUnit.Framework;

namespace AvalonDock.UITests.CodeApp;

/// <summary>Saving and restoring the ToggleDockingManager's layout (Layout menu of AvalonDockCodeApp).</summary>
public class ToggleDockSerializationTests : ToggleDockTestBase
{
	private const string UnknownToolboxTitle = "Ghost Toolbox";

	[Test, Order(1)]
	public async Task SaveAndLoadLayout_KeepsExactlyTheSameSidebarButtons()
	{
		var before = await GetSidebarTitlesAsync();
		Assert.That(before, Is.Not.Empty, "The sample application should show sidebar buttons.");

		await SaveLayoutAsync();
		await LoadLayoutAsync();

		Assert.That(await GetSidebarTitlesAsync(), Is.EqualTo(before), "Restoring the layout it was saved from has to reproduce the same sidebar.");
	}

	[Test, Order(2)]
	public async Task AfterLoadingLayout_TheSidebarButtonsStillToggleTheirToolWindow()
	{
		await SaveLayoutAsync();
		await LoadLayoutAsync();

		var before = await GetToggleStateAsync("Explorer");
		Assert.That(before, Is.Not.Null, "Explorer should still have a sidebar button after the restore.");

		await ClickToggleButtonAsync("Explorer");

		await WaitForToggleStateAsync("Explorer", before != true,
			"the Explorer button to toggle after a layout restore, which it cannot do while it references the replaced layout");
	}

	[Test, Order(3)]
	public async Task LoadingLayoutWithUnknownToolbox_AddsNoGhostButton()
	{
		await SaveLayoutAsync();
		var expected = await GetSidebarTitlesAsync();

		await ClickMenuAsync("Layout", "Load Layout With Unknown Toolbox");
		await SettleAsync();

		var after = await GetSidebarTitlesAsync();
		Assert.That(after, Does.Not.Contain(UnknownToolboxTitle), "A stored tool window without content must not reach the sidebar.");
		Assert.That(after, Is.EqualTo(expected), "The tool windows the application does offer have to survive that restore unchanged.");
		Assert.That(Session.HasExited, Is.False);
	}

	[Test, Order(4)]
	public async Task LoadingLayoutRepeatedly_DoesNotAccumulateSidebarButtons()
	{
		await SaveLayoutAsync();
		await LoadLayoutAsync();
		var afterFirst = await GetSidebarTitlesAsync();

		await LoadLayoutAsync();
		await LoadLayoutAsync();

		Assert.That(await GetSidebarTitlesAsync(), Is.EqualTo(afterFirst), "Three restores have to leave the same sidebar as one.");
	}

	private async Task SaveLayoutAsync()
	{
		await ClickMenuAsync("Layout", "Save Layout");
		await SettleAsync();
	}

	private async Task LoadLayoutAsync()
	{
		await ClickMenuAsync("Layout", "Load Layout");
		await WaitUntilAsync(async () => (await GetToggleButtonsAsync()).Count > 0, "the side bars after the restore");
		await SettleAsync();
	}
}
