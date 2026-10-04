using NUnit.Framework;

namespace AvalonDock.UITests.TestApp;

/// <summary>Layout save/load together with other state: pane sizes, closed documents, auto-hide, active tools.</summary>
public class LayoutSerializationExtendedTests : UITestBase
{
	public LayoutSerializationExtendedTests()
		: base(DemoApp.TestApp)
	{
	}

	/// <summary>Regression for #118: the panes keep their places, so their tool windows stay where they were.</summary>
	[Test, Order(1)]
	public async Task SaveLoadLayout_PreservesPaneRatio_Issue118()
	{
		var tool1Before = (await FindToolWindowHeaderAsync("Tool Window 1"))?.Bounds;
		var winFormsBefore = (await FindToolWindowHeaderAsync("WinForms Window"))?.Bounds;

		await SaveLayoutAsync("Layout_3");
		await LoadLayoutAsync("Layout_3");

		var tool1After = (await WaitForElementAsync(() => FindToolWindowHeaderAsync("Tool Window 1"), "Tool Window 1 after the restore (Issue #118)")).Bounds;
		var winFormsAfter = (await WaitForElementAsync(() => FindToolWindowHeaderAsync("WinForms Window"), "WinForms Window after the restore (Issue #118)")).Bounds;
		Assert.That(tool1After!.X, Is.EqualTo(tool1Before!.X).Within(2), "Tool Window 1 should keep its place (Issue #118).");
		Assert.That(winFormsAfter!.X, Is.EqualTo(winFormsBefore!.X).Within(2), "WinForms Window should keep its place (Issue #118).");
	}

	/// <summary>Regression for #38: loading a layout after closing a document.</summary>
	[Test, Order(2)]
	public async Task LoadLayoutAfterClosingDocument_DoesNotCrash_Issue38()
	{
		await SaveLayoutAsync("Layout_3");

		await ActivateDocumentTabAsync("Document 1");
		await TapAsync(await WaitForElementAsync(
			() => FindElementAsync(e => e.Type == "Button" && e.IsVisible && e.Text == "Click to add 2 documents"),
			"the 'Click to add 2 documents' button"));
		await WaitForElementAsync(() => FindDocumentTabAsync("Test1"), "the Test1 tab");
		await CloseDocumentAsync("Test1", confirm: true);
		await WaitUntilAsync(async () => await FindContentAsync("Test1") == null, "Test1 to close");

		await LoadLayoutAsync("Layout_3");

		Assert.That(Session.HasExited, Is.False, "App should not crash when loading a layout after closing a document (Issue #38).");
		await WaitForElementAsync(() => FindDocumentTabAsync("Document 1"), "Document 1 after the restore");
	}

	/// <summary>Regression for #83.</summary>
	[Test, Order(3)]
	public async Task LayoutRestore_BringsBackDocuments_Issue83()
	{
		await SaveLayoutAsync("Layout_4");
		await LoadLayoutAsync("Layout_4");

		await WaitForElementAsync(() => FindDocumentTabAsync("Document 1"), "Document 1 after the restore (Issue #83)");
		await WaitForElementAsync(() => FindDocumentTabAsync("Document 2"), "Document 2 after the restore (Issue #83)");
	}

	/// <summary>Regression for #111: auto-hide tool windows stay auto-hidden through a save and load.</summary>
	[Test, Order(4)]
	public async Task SaveLoadLayout_WithAutoHidePanels_Issue111()
	{
		await TapToolWindowHeaderAsync("AutoHide1 Content");
		await WaitUntilAsync(async () => (await GetLayoutAsync()).AutoHideWindowContent == "AutoHide1 Content", "the flyout to open");
		await ActivateDocumentTabAsync("Document 1");

		await SaveLayoutAsync("Layout_4");
		await LoadLayoutAsync("Layout_4");

		Assert.That((await FindContentAsync("AutoHide1 Content"))?.IsAutoHidden, Is.True, "AutoHide1 Content should still be auto-hidden (Issue #111).");
		Assert.That(await FindToolWindowHeaderAsync("AutoHide1 Content"), Is.Not.Null, "AutoHide1 Content should have its auto-hide tab (Issue #111).");
	}

	/// <summary>Regression for #59: save and load while a tool window is active.</summary>
	[Test, Order(5)]
	public async Task SaveLoadLayout_WithToolWindowStates_Issue59()
	{
		await ActivateToolWindowAsync("Tool Window 1");

		await SaveLayoutAsync("Layout_4");
		await ActivateDocumentTabAsync("Document 1");
		await LoadLayoutAsync("Layout_4");

		Assert.That(Session.HasExited, Is.False, "App should not crash during layout save/load with tool windows active (Issue #59).");
		Assert.That((await FindContentAsync("Tool Window 1"))?.IsSelected, Is.True, "Tool Window 1 should still be the selected tool window of its pane.");
	}
}
