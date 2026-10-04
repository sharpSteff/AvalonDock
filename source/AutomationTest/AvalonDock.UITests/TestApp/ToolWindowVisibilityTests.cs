using NUnit.Framework;

namespace AvalonDock.UITests.TestApp;

/// <summary>Showing and hiding tool windows, and their auto-hide flyouts.</summary>
public class ToolWindowVisibilityTests : UITestBase
{
	public ToolWindowVisibilityTests()
		: base(DemoApp.TestApp)
	{
	}

	/// <summary>Regression for #19: Tools > Tool Window1 shows and activates the tool window.</summary>
	[Test, Order(1)]
	public async Task ShowToolWindow_ViaMenu_Issue19()
	{
		await ActivateDocumentTabAsync("Document 1");

		await ClickMenuAsync("Tools", "Tool Window1");

		await WaitUntilAsync(async () => (await FindContentAsync("Tool Window 1"))?.IsActive == true, "Tool Window 1 to be activated (Issue #19)");
	}

	/// <summary>Regression for #62: an auto-hide flyout with text boxes opens.</summary>
	[Test, Order(2)]
	public async Task AutoHideWithTextBoxes_ContentAccessible_Issue62()
	{
		await TapToolWindowHeaderAsync("AutoHide2 Content");

		await WaitUntilAsync(
			async () => (await GetLayoutAsync()).AutoHideWindowContent == "AutoHide2 Content",
			"the flyout of AutoHide2 Content to open (Issue #62)");

		await ActivateDocumentTabAsync("Document 1");
	}

	/// <summary>Regression for #9: the auto-hide tabs are on the side of their anchor group (left).</summary>
	[Test, Order(3)]
	public async Task AutoHideTabs_OnCorrectSide_Issue9()
	{
		var layout = await GetLayoutAsync();
		foreach (var title in new[] { "AutoHide1 Content", "AutoHide2 Content" })
		{
			var header = await FindToolWindowHeaderAsync(title);
			Assert.That(header?.Bounds, Is.Not.Null, $"{title} should have an auto-hide tab.");
			Assert.That(header!.Bounds!.X, Is.LessThan(layout.WindowWidth / 2), $"The {title} tab should be on the left side of the window (Issue #9).");
		}
	}

	/// <summary>Regression for #10: the flyout opens and closes repeatedly.</summary>
	[Test, Order(4)]
	public async Task AutoHideFlyout_IsResponsive_Issue10()
	{
		for (var i = 0; i < 3; i++)
		{
			await TapToolWindowHeaderAsync("AutoHide1 Content");
			await WaitUntilAsync(async () => (await GetLayoutAsync()).AutoHideWindowContent == "AutoHide1 Content", "the flyout to open");

			await ActivateDocumentTabAsync("Document 1");
		}

		Assert.That(Session.HasExited, Is.False, "App should remain responsive during repeated auto-hide open/close (Issue #10).");
	}

	/// <summary>Regression for #382.</summary>
	[Test, Order(5)]
	public async Task AddDynamicContent_DoesNotCrash_Issue382()
	{
		await ActivateDocumentTabAsync("Document 1");
		var addButton = await WaitForElementAsync(
			() => FindElementAsync(e => e.Type == "Button" && e.IsVisible && e.Text == "Click to add 2 documents"),
			"the 'Click to add 2 documents' button");

		await TapAsync(addButton);

		await WaitForElementAsync(() => FindDocumentTabAsync("Test1"), "the Test1 tab (Issue #382)");
		Assert.That((await GetLayoutAsync()).Contents.Any(c => c.Title == "New Anchorable"), Is.True, "The new anchorable should be in the layout.");
	}

	[Test, Order(6)]
	public async Task ShowWinFormsWindowViaMenu_Works()
	{
		await ActivateDocumentTabAsync("Document 1");

		await ClickMenuAsync("Tools", "WinForms Window");

		await WaitUntilAsync(async () => (await FindContentAsync("WinForms Window"))?.IsActive == true, "WinForms Window to be activated");
		Assert.That(await FindToolWindowHeaderAsync("WinForms Window"), Is.Not.Null, "WinForms Window should be visible after menu activation.");
	}
}
