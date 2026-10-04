using NUnit.Framework;

namespace AvalonDock.UITests.TestApp;

/// <summary>Removing the DockingManager from the window and putting it back (Layout > Unload/Load Manager).</summary>
public class ManagerLifecycleTests : UITestBase
{
	public ManagerLifecycleTests()
		: base(DemoApp.TestApp)
	{
	}

	/// <summary>Regression for #384.</summary>
	[Test, Order(1)]
	public async Task UnloadAndReloadManager_Works_Issue384()
	{
		Assert.That(await FindDocumentTabAsync("Document 1"), Is.Not.Null, "Document 1 should exist before unload.");

		await UnloadManagerAsync();
		Assert.That(await FindDocumentTabAsync("Document 1"), Is.Null, "The documents should be gone with the manager.");

		await LoadManagerAsync();
		await WaitForElementAsync(() => FindDocumentTabAsync("Document 1"), "Document 1 after reloading the manager (Issue #384)");
	}

	/// <summary>Regression for #159.</summary>
	[Test, Order(2)]
	public async Task RepeatedUnloadReload_DoesNotCrash_Issue159()
	{
		for (var i = 0; i < 3; i++)
		{
			await UnloadManagerAsync();
			await LoadManagerAsync();
		}

		Assert.That(Session.HasExited, Is.False, "App should not crash during repeated unload/reload cycles (Issue #159).");
		await WaitForElementAsync(() => FindDocumentTabAsync("Document 1"), "Document 1 after repeated unload/reload");
	}

	[Test, Order(3)]
	public async Task MenusWork_AfterManagerReload()
	{
		await UnloadManagerAsync();
		await LoadManagerAsync();

		await ClickMenuAsync("Tools", "Tool Window1");
		await WaitUntilAsync(async () => (await FindContentAsync("Tool Window 1"))?.IsActive == true, "Tool Window 1 to be activated through the menu");
	}

	/// <summary>Regression for #437: unloading the manager while it has a floating window.</summary>
	[Test, Order(4)]
	public async Task UnloadManagerWithFloatingWindow_DoesNotCrash_Issue437()
	{
		var before = (await GetLayoutAsync()).FloatingWindowCount;
		await ClickMenuAsync("Tools", "New floating window");
		await WaitUntilAsync(async () => (await GetLayoutAsync()).FloatingWindowCount > before, "a new floating window");

		await UnloadManagerAsync();
		await LoadManagerAsync();

		Assert.That(Session.HasExited, Is.False, "App should not crash when unloading manager with floating windows (Issue #437).");
	}

	private async Task UnloadManagerAsync()
	{
		await ClickMenuAsync("Layout", "Unload Manager");
		await WaitUntilAsync(async () => !(await GetLayoutAsync()).ManagerLoaded, "the manager to unload");
	}

	private async Task LoadManagerAsync()
	{
		await ClickMenuAsync("Layout", "Load Manager");
		await WaitUntilAsync(async () => (await GetLayoutAsync()).ManagerLoaded, "the manager to load");
	}
}
