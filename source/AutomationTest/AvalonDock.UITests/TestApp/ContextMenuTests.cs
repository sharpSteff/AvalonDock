using NUnit.Framework;

namespace AvalonDock.UITests.TestApp;

/// <summary>Context menus of document and tool window tabs, and the application menus.</summary>
public class ContextMenuTests : UITestBase
{
	public ContextMenuTests()
		: base(DemoApp.TestApp)
	{
	}

	/// <summary>Regression for #193: right-clicking a document tab opens its context menu.</summary>
	[Test, Order(1)]
	public async Task RightClickDocumentTab_ShowsContextMenu_Issue193()
	{
		var tab = await WaitForElementAsync(() => FindDocumentTabAsync("Document 1"), "the tab of Document 1");

		await RightTapAsync(tab);
		await WaitForElementAsync(() => FindVisibleMenuItemAsync("Close"), "the document context menu");

		await PressKeyAsync("Escape");
		await WaitUntilAsync(async () => await FindVisibleMenuItemAsync("Close") == null, "the context menu to close");
		Assert.That(await FindContentAsync("Document 1"), Is.Not.Null, "Document 1 should still be open.");
	}

	/// <summary>Regression for #200: right-clicking a tool window tab does not crash.</summary>
	[Test, Order(2)]
	public async Task RightClickToolWindowTab_DoesNotCrash_Issue200()
	{
		var header = await WaitForElementAsync(() => FindToolWindowHeaderAsync("Tool Window 1"), "the header of Tool Window 1");

		await RightTapAsync(header);
		await PressKeyAsync("Escape");

		Assert.That(Session.HasExited, Is.False, "App should not crash after right-clicking tool window tab (Issue #200).");
		Assert.That((await FindContentAsync("Tool Window 1"))?.IsHidden, Is.False);
	}

	[Test, Order(3)]
	public async Task ApplicationMenu_ItemsAreAccessible()
	{
		foreach (var header in new[] { "Edit", "Layout", "Tools" })
			Assert.That(await FindVisibleMenuItemAsync(header), Is.Not.Null, $"{header} menu should be accessible.");
	}

	[Test, Order(4)]
	public async Task EditMenu_OpensAndCloses()
	{
		await ClickMenuAsync("Edit");
		await WaitForElementAsync(() => FindVisibleMenuItemAsync("Undo"), "the Undo item of the Edit menu");

		await PressKeyAsync("Escape");
		await WaitUntilAsync(async () => await FindVisibleMenuItemAsync("Undo") == null, "the Edit menu to close");
	}

	[Test, Order(5)]
	public async Task ToolsMenu_ItemsAreAccessible()
	{
		await ClickMenuAsync("Tools");
		foreach (var item in new[] { "WinForms Window", "Tool Window1", "New floating window" })
			await WaitForElementAsync(() => FindVisibleMenuItemAsync(item), $"the '{item}' item of the Tools menu");

		await PressKeyAsync("Escape");
	}

	private Task<Microsoft.Maui.DevFlow.Driver.ElementInfo?> FindVisibleMenuItemAsync(string header)
		=> FindElementAsync(e => e.Type == "MenuItem" && e.IsVisible && e.Text == header);
}
