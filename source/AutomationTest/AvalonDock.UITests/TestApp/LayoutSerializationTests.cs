using NUnit.Framework;

namespace AvalonDock.UITests.TestApp;

/// <summary>Saving and loading layouts through the Layout menu (XmlLayoutSerializer).</summary>
public class LayoutSerializationTests : UITestBase
{
	public LayoutSerializationTests()
		: base(DemoApp.TestApp)
	{
	}

	/// <summary>Regression for #440.</summary>
	[Test, Order(1)]
	public async Task SaveAndLoadLayout_PreservesDocuments_Issue440()
	{
		await SaveLayoutAsync("Layout_1");
		await LoadLayoutAsync("Layout_1");

		Assert.That(await WaitForElementAsync(() => FindDocumentTabAsync("Document 1"), "Document 1 after loading"), Is.Not.Null);
		Assert.That(await WaitForElementAsync(() => FindDocumentTabAsync("Document 2"), "Document 2 after loading"), Is.Not.Null);
	}

	/// <summary>Regression for #392.</summary>
	[Test, Order(2)]
	public async Task SaveAndLoadLayout_PreservesToolWindows_Issue392()
	{
		await SaveLayoutAsync("Layout_1");
		await LoadLayoutAsync("Layout_1");

		foreach (var title in new[] { "Tool Window 1", "Tool Window 2", "AutoHide1 Content", "AutoHide2 Content" })
		{
			var content = await FindContentAsync(title);
			Assert.That(content, Is.Not.Null, $"{title} should be restored (Issue #392).");
			Assert.That(content!.IsHidden, Is.False, $"{title} should not be hidden after the restore.");
		}

		Assert.That(await FindToolWindowHeaderAsync("Tool Window 1"), Is.Not.Null, "Tool Window 1 should be shown after the restore.");
	}

	[Test, Order(3)]
	public async Task LoadLayout_RestoresOriginalState()
	{
		await SaveLayoutAsync("Layout_2");

		await ActivateDocumentTabAsync("Document 1");
		await TapAsync(await WaitForElementAsync(
			() => FindElementAsync(e => e.Type == "Button" && e.IsVisible && e.Text == "Click to add 2 documents"),
			"the 'Click to add 2 documents' button"));
		await WaitForElementAsync(() => FindDocumentTabAsync("Test1"), "the Test1 tab");

		await LoadLayoutAsync("Layout_2");

		await WaitUntilAsync(async () => await FindContentAsync("Test1") == null, "the documents added after the save to go");
		Assert.That(await FindDocumentTabAsync("Document 1"), Is.Not.Null, "Document 1 should be present after loading the saved layout.");
	}

	/// <summary>Regression for #167.</summary>
	[Test, Order(4)]
	public async Task SaveLoadLayout_DoesNotCrash_Issue167()
	{
		for (var i = 0; i < 3; i++)
		{
			await SaveLayoutAsync("Layout_3");
			await LoadLayoutAsync("Layout_3");
		}

		Assert.That(Session.HasExited, Is.False, "Repeated save/load should not crash (Issue #167).");
		Assert.That(await WaitForElementAsync(() => FindDocumentTabAsync("Document 1"), "Document 1"), Is.Not.Null);
	}

	/// <summary>Regression for #443: the restored documents work.</summary>
	[Test, Order(5)]
	public async Task AfterLayoutRestore_DocumentsAccessible_Issue443()
	{
		await SaveLayoutAsync("Layout_4");
		await LoadLayoutAsync("Layout_4");

		await ActivateDocumentTabAsync("Document 2");
		await ActivateDocumentTabAsync("Document 1");
		Assert.That(Session.HasExited, Is.False, "Documents should be usable after the restore (Issue #443).");
	}

	/// <summary>
	/// A tool window moved into a standalone window (anchorable menu > Window) is saved as detached, and the
	/// restore recreates its window.
	/// </summary>
	[Test, Order(6)]
	public async Task SaveAndLoadLayout_RestoresDetachedWindow()
	{
		const string toolWindow = "Tool Window 1";
		await DetachToolWindowAsync(toolWindow);

		await SaveLayoutAsync("Layout_1");

		// Put the tool window back, so that a restore that ignored the detached state would show as a
		// missing window rather than as a leftover from before the save.
		Assert.That(await InvokeAsync("avalondock-close-window", toolWindow), Is.EqualTo("True"));
		await AnswerDialogIfPresentAsync("Yes");
		await WaitUntilAsync(async () => (await FindContentAsync(toolWindow))?.IsDetached == false, "the standalone window to close");

		await LoadLayoutAsync("Layout_1");

		await WaitUntilAsync(async () => (await FindContentAsync(toolWindow))?.IsDetached == true,
			$"'{toolWindow}' to be detached again, as it was when the layout was saved", TimeSpan.FromSeconds(20));
		Assert.That(Session.HasExited, Is.False, "The application should survive restoring a detached tool window.");
	}

	/// <summary>Moves a tool window into a standalone window through the menu of its pane title.</summary>
	private async Task DetachToolWindowAsync(string title)
	{
		// The pane title, and with it its menu, belongs to the selected tool window of the pane.
		await ClickMenuAsync("Tools", "Tool Window1");
		await WaitUntilAsync(async () => (await FindContentAsync(title))?.IsSelected == true, $"'{title}' to be selected");

		var paneTitle = await WaitForElementAsync(
			async () => (await GetElementsAsync()).FirstOrDefault(e => e.Type == "AnchorablePaneTitle" && e.IsVisible && FindDescendant(e, d => d.Text == title) != null),
			$"the pane title of '{title}'");
		var dropDown = FindDescendant(paneTitle, e => e.Type == "DropDownButton" && e.IsVisible);
		if (dropDown != null)
			await TapAsync(dropDown);
		else
			await RightTapAsync(paneTitle);

		await TapAsync(await WaitForElementAsync(
			() => FindElementAsync(e => e.Type == "MenuItem" && e.IsVisible && e.Text == "Window"),
			"the 'Window' item of the anchorable menu"));

		await WaitUntilAsync(async () => (await FindContentAsync(title))?.IsDetached == true, $"'{title}' to move into a standalone window");
	}
}
