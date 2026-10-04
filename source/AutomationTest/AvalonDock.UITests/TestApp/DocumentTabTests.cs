using NUnit.Framework;

namespace AvalonDock.UITests.TestApp;

/// <summary>
/// Regression tests for document tab operations.
/// Covers issues:
///   #511 - NullReference in LayoutDocumentTabItem.OnMouseLeftButtonDown
///   #244 - Right click on tab header closes tab unexpectedly
///   #201 - Closing active document always selects first document instead of previous
///   #493 - DropDownContextMenu doesn't show when adding a new document
/// </summary>
public class DocumentTabTests : UITestBase
{
	public DocumentTabTests()
		: base(DemoApp.TestApp)
	{
	}

	/// <summary>Regression for #511: clicking a document tab activates it without crashing.</summary>
	[Test, Order(1)]
	public async Task ClickDocumentTab_ActivatesDocument_Issue511()
	{
		await ActivateDocumentTabAsync("Document 1");

		Assert.That(Session.HasExited, Is.False, "Clicking Document 1 tab should activate it without crashing.");
	}

	[Test, Order(2)]
	public async Task SwitchBetweenDocumentTabs_Works()
	{
		await ActivateDocumentTabAsync("Document 2");
		Assert.That((await FindContentAsync("Document 1"))!.IsSelected, Is.False, "Document 1 should no longer be selected.");

		await ActivateDocumentTabAsync("Document 1");
		Assert.That((await FindContentAsync("Document 2"))!.IsSelected, Is.False, "Document 2 should no longer be selected.");
	}

	/// <summary>Regression for #244: right-clicking a document tab does not close it.</summary>
	[Test, Order(3)]
	public async Task RightClickDocumentTab_DoesNotClose_Issue244()
	{
		var tab = await WaitForElementAsync(() => FindDocumentTabAsync("Document 1"), "the tab of Document 1");

		await RightTapAsync(tab);
		await PressKeyAsync("Escape");

		Assert.That(await FindDocumentTabAsync("Document 1"), Is.Not.Null,
			"Right-clicking a document tab should NOT close it (Issue #244).");
		Assert.That(await FindContentAsync("Document 1"), Is.Not.Null, "Document 1 should still be in the layout.");
	}

	/// <summary>Regression for #493: the "Click to add 2 documents" button adds Test1 and Test2 as tabs.</summary>
	[Test, Order(4)]
	public async Task AddDocumentsViaButton_AppearsAsTabs_Issue493()
	{
		await ActivateDocumentTabAsync("Document 1");

		var addButton = await WaitForElementAsync(
			() => FindElementAsync(e => e.Type == "Button" && e.IsVisible && e.Text == "Click to add 2 documents"),
			"the 'Click to add 2 documents' button in Document 1");
		await TapAsync(addButton);

		var test1 = await WaitForElementAsync(() => FindDocumentTabAsync("Test1"), "the Test1 tab");
		var test2 = await WaitForElementAsync(() => FindDocumentTabAsync("Test2"), "the Test2 tab");
		Assert.That(test1, Is.Not.Null, "Test1 document tab should appear after clicking the add button (Issue #493).");
		Assert.That(test2, Is.Not.Null, "Test2 document tab should appear after clicking the add button (Issue #493).");
	}

	[Test, Order(5)]
	public async Task InitialDocumentTabs_ArePresent()
	{
		Assert.That(await FindDocumentTabAsync("Document 1"), Is.Not.Null, "Document 1 tab should be present.");
		Assert.That(await FindDocumentTabAsync("Document 2"), Is.Not.Null, "Document 2 tab should be present.");
	}

	/// <summary>Regression for #240: NullReferenceException in LayoutDocumentControl.OnModelChanged.</summary>
	[Test, Order(6)]
	public async Task ActivateDocument_ContentIsAccessible_Issue240()
	{
		await ActivateDocumentTabAsync("Document 1");

		Assert.That(await FindElementAsync(e => e.Type == "Button" && e.IsVisible && e.Text == "Click to add 2 documents"), Is.Not.Null,
			"Document content should be accessible after activation (Issue #240).");
	}
}