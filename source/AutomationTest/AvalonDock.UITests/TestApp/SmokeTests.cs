using NUnit.Framework;

namespace AvalonDock.UITests.TestApp;

/// <summary>
/// Smoke tests for basic AvalonDock UI functionality: the application starts and all major UI elements
/// are present. A baseline before the more specific tests.
/// </summary>
public class SmokeTests : UITestBase
{
	public SmokeTests()
		: base(DemoApp.TestApp)
	{
	}

	[Test, Order(1)]
	public void Application_Starts_MainWindowAppears()
	{
		Assert.That(Session.HasExited, Is.False, "Application should still be running.");
	}

	[Test, Order(2)]
	public async Task MainWindow_HasCorrectTitle()
	{
		var layout = await GetLayoutAsync();
		Assert.That(layout.WindowTitle, Does.Contain("MainWindow"), "Main window should have the expected title.");
	}

	[Test, Order(3)]
	public async Task DockingManager_IsPresent()
	{
		var layout = await GetLayoutAsync();
		Assert.That(layout.ManagerLoaded, Is.True, "The DockingManager should be in the window.");
		Assert.That(await FindDocumentTabAsync("Document 1"), Is.Not.Null, "The DockingManager should show its documents.");
	}

	[Test, Order(4)]
	public async Task MainMenu_IsAccessible()
	{
		var elements = await GetElementsAsync();
		foreach (var header in new[] { "Edit", "Layout", "Tools" })
		{
			Assert.That(elements.Any(e => e.Type == "MenuItem" && e.Text == header), Is.True,
				$"{header} menu should be accessible.");
		}
	}

	[Test, Order(5)]
	public async Task InitialDocuments_AllPresent()
	{
		Assert.That(await FindDocumentTabAsync("Document 1"), Is.Not.Null, "Document 1 should be present.");
		Assert.That(await FindDocumentTabAsync("Document 2"), Is.Not.Null, "Document 2 should be present.");
	}

	[Test, Order(6)]
	public async Task InitialToolWindows_AllPresent()
	{
		foreach (var title in new[] { "WinForms Window", "Tool Window 1", "Tool Window 2" })
			Assert.That(await FindToolWindowHeaderAsync(title), Is.Not.Null, $"{title} should be present.");
	}

	[Test, Order(7)]
	public async Task AutoHidePanels_ArePresent()
	{
		Assert.That(await FindToolWindowHeaderAsync("AutoHide1 Content"), Is.Not.Null, "AutoHide1 Content tab should be present.");
		Assert.That(await FindToolWindowHeaderAsync("AutoHide2 Content"), Is.Not.Null, "AutoHide2 Content tab should be present.");
	}

	[Test, Order(8)]
	public async Task MainWindow_HasReasonableSize()
	{
		var layout = await GetLayoutAsync();
		Assert.That(layout.WindowWidth, Is.GreaterThan(400), "Main window should have a reasonable width.");
		Assert.That(layout.WindowHeight, Is.GreaterThan(300), "Main window should have a reasonable height.");
	}
}