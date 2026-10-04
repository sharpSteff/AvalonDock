using NUnit.Framework;

namespace AvalonDock.UITests.TestApp;

/// <summary>Tool windows: their headers, activation, auto-hide flyouts and new floating tool windows.</summary>
public class ToolWindowTests : UITestBase
{
	public ToolWindowTests()
		: base(DemoApp.TestApp)
	{
	}

	[Test, Order(1)]
	public async Task InitialToolWindows_AreVisible()
	{
		foreach (var title in new[] { "WinForms Window", "Tool Window 1", "Tool Window 2" })
			Assert.That(await FindToolWindowHeaderAsync(title), Is.Not.Null, $"{title} should be visible.");
	}

	/// <summary>Regression for #375: clicking a tool window tab activates it.</summary>
	[Test, Order(2)]
	public async Task ClickToolWindowTab_ActivatesIt_Issue375()
	{
		await ActivateToolWindowAsync("Tool Window 1");
		Assert.That(Session.HasExited, Is.False, "Clicking tool window tab should activate it without crashing (Issue #375).");
	}

	[Test, Order(3)]
	public async Task ToolWindowContent_IsAccessible()
	{
		await ActivateToolWindowAsync("Tool Window 1");

		var timer = await WaitForElementAsync(
			() => FindElementAsync(e => e.Type == "TextBox" && e.IsVisible && e.Text != null && e.Text.StartsWith("Tool Window 1 Attached to Timer", StringComparison.Ordinal)),
			"the content of Tool Window 1");
		Assert.That(timer, Is.Not.Null, "Tool window content should be accessible when activated.");
	}

	[Test, Order(4)]
	public async Task AutoHideTabs_ArePresent_Issue362()
	{
		Assert.That(await FindToolWindowHeaderAsync("AutoHide1 Content"), Is.Not.Null, "AutoHide1 Content tab should be present on the window edge (Issue #362).");
		Assert.That(await FindToolWindowHeaderAsync("AutoHide2 Content"), Is.Not.Null, "AutoHide2 Content tab should be present on the window edge (Issue #362).");

		var layout = await GetLayoutAsync();
		Assert.That(layout.Contents.Single(c => c.Title == "AutoHide1 Content").IsAutoHidden, Is.True);
		Assert.That(layout.Contents.Single(c => c.Title == "AutoHide2 Content").IsAutoHidden, Is.True);
	}

	/// <summary>Regression for #169: clicking an auto-hide tab opens its flyout.</summary>
	[Test, Order(5)]
	public async Task ClickAutoHideTab_OpensFlyout_Issue169()
	{
		await TapToolWindowHeaderAsync("AutoHide1 Content");

		await WaitUntilAsync(
			async () => (await GetLayoutAsync()).AutoHideWindowContent == "AutoHide1 Content",
			"the flyout of AutoHide1 Content to open (Issue #169)");

		// Click the document area to close the flyout.
		await ActivateDocumentTabAsync("Document 1");
	}

	[Test, Order(6)]
	public async Task NewFloatingWindow_CreatesFloatingToolWindow()
	{
		var initialCount = (await GetLayoutAsync()).FloatingWindowCount;

		await ClickMenuAsync("Tools", "New floating window");

		await WaitUntilAsync(async () => (await GetLayoutAsync()).FloatingWindowCount > initialCount, "a new floating window");
		await ArrangeFloatingWindowsAsync();
		var created = (await GetLayoutAsync()).Contents.Single(c => c.Title == "Floating window with initial usercontrol size");
		Assert.That(created.IsFloating, Is.True, "New floating window menu item should create a floating tool window.");
	}
}
