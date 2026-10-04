using NUnit.Framework;

namespace AvalonDock.UITests.TestApp;

/// <summary>Keyboard input in documents and tool windows, and keyboard navigation between them.</summary>
public class KeyboardNavigationTests : UITestBase
{
	public KeyboardNavigationTests()
		: base(DemoApp.TestApp)
	{
	}

	[Test, Order(1)]
	public async Task KeyboardInput_WorksInDocumentContent()
	{
		await ActivateDocumentTabAsync("Document 1");
		var textBox = await WaitForElementAsync(
			() => FindElementAsync(e => e.Type == "TextBox" && e.IsVisible && e.Text == "Document 1 Content"),
			"the text box of Document 1");

		await TapAsync(textBox);
		await PressKeyAsync("End");
		await PressKeyAsync("x");
		await PressKeyAsync("y");

		await WaitUntilAsync(
			async () => await FindElementAsync(e => e.Type == "TextBox" && e.Text == "Document 1 Contentxy") != null,
			"the typed text to reach the text box of Document 1");
	}

	/// <summary>Regression for #225: arrow keys in a tool window.</summary>
	[Test, Order(2)]
	public async Task ArrowKeys_WorkWithoutCrash_Issue225()
	{
		await ActivateToolWindowAsync("Tool Window 1");

		foreach (var key in new[] { "Up", "Down", "Left", "Right" })
			await PressKeyAsync(key);

		Assert.That(Session.HasExited, Is.False, "App should not crash when using arrow keys in tool window (Issue #225).");
		Assert.That(await FindContentAsync("Tool Window 1"), Is.Not.Null);
	}

	[Test, Order(3)]
	public async Task TabKey_CyclesFocus()
	{
		await ActivateDocumentTabAsync("Document 1");

		for (var i = 0; i < 5; i++)
			await PressKeyAsync("Tab");

		Assert.That(Session.HasExited, Is.False, "App should not crash during Tab key navigation.");
		Assert.That((await GetLayoutAsync()).ManagerLoaded, Is.True);
	}

	[Test, Order(4)]
	public async Task CtrlTab_SwitchesBetweenDocuments()
	{
		await ActivateDocumentTabAsync("Document 1");

		await PressKeyAsync("Ctrl+Tab");

		Assert.That(Session.HasExited, Is.False, "App should not crash during Ctrl+Tab document switching.");
		Assert.That((await GetLayoutAsync()).Documents.Count(), Is.GreaterThanOrEqualTo(2));
	}
}
