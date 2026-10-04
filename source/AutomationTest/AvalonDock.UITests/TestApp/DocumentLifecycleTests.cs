using NUnit.Framework;

namespace AvalonDock.UITests.TestApp;

/// <summary>Opening, closing and switching documents.</summary>
public class DocumentLifecycleTests : UITestBase
{
	public DocumentLifecycleTests()
		: base(DemoApp.TestApp)
	{
	}

	/// <summary>Regression for #7.</summary>
	[Test, Order(1)]
	public async Task RapidTabClicking_DoesNotCrash_Issue7()
	{
		for (var i = 0; i < 10; i++)
		{
			await ActivateDocumentTabAsync("Document 1");
			await ActivateDocumentTabAsync("Document 2");
		}

		Assert.That(Session.HasExited, Is.False, "Rapidly clicking between document tabs should not crash (Issue #7).");
	}

	/// <summary>Regression for #204: closing a document leaves the others usable.</summary>
	[Test, Order(2)]
	public async Task CloseDocument_RemainingDocsAccessible_Issue204()
	{
		await AddTwoDocumentsAsync();

		await CloseDocumentAsync("Test1", confirm: true);

		await WaitUntilAsync(async () => await FindContentAsync("Test1") == null, "Test1 to be closed");
		Assert.That(await FindDocumentTabAsync("Document 1"), Is.Not.Null, "Document 1 should still be accessible after closing Test1 (Issue #204).");
		Assert.That(await FindDocumentTabAsync("Document 2"), Is.Not.Null, "Document 2 should still be accessible after closing Test1 (Issue #204).");
		await ActivateDocumentTabAsync("Document 2");
	}

	/// <summary>Regression for #232: documents can be added again after closing one.</summary>
	[Test, Order(3)]
	public async Task CloseAndReAddDocuments_Works_Issue232()
	{
		await AddTwoDocumentsAsync();
		await CloseDocumentAsync("Test1", confirm: true);

		var before = (await GetLayoutAsync()).Documents.Count();
		await AddTwoDocumentsAsync();

		await WaitUntilAsync(async () => (await GetLayoutAsync()).Documents.Count() == before + 2, "two more documents");
	}

	/// <summary>Regression for #196: Ctrl+W does not close a tool window whose CanClose is false.</summary>
	[Test, Order(4)]
	public async Task CanCloseFalse_CannotCloseWindow_Issue196()
	{
		await ActivateToolWindowAsync("WinForms Window");

		await PressKeyAsync("Ctrl+W");
		await AnswerDialogIfPresentAsync("No");

		var winForms = await FindContentAsync("WinForms Window");
		Assert.That(winForms, Is.Not.Null, "WinForms Window with CanClose=False should not be closed (Issue #196).");
		Assert.That(winForms!.IsHidden, Is.False);
	}

	/// <summary>Regression for #184: answering No keeps the document open.</summary>
	[Test, Order(5)]
	public async Task CancelDocumentClose_DocumentRemains_Issue184()
	{
		await CloseDocumentAsync("Document 1", confirm: false);

		Assert.That(await FindContentAsync("Document 1"), Is.Not.Null, "Document 1 should still be open after cancelling close (Issue #184).");
		Assert.That(await FindDocumentTabAsync("Document 1"), Is.Not.Null, "Document 1 should still have its tab (Issue #184).");
	}

	private async Task AddTwoDocumentsAsync()
	{
		await ActivateDocumentTabAsync("Document 1");
		var addButton = await WaitForElementAsync(
			() => FindElementAsync(e => e.Type == "Button" && e.IsVisible && e.Text == "Click to add 2 documents"),
			"the 'Click to add 2 documents' button");
		await TapAsync(addButton);
		await WaitForElementAsync(() => FindDocumentTabAsync("Test1"), "the Test1 tab");
	}
}
