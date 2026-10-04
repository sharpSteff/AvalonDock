using NUnit.Framework;

namespace AvalonDock.UITests.TestApp;

/// <summary>Rapid and repeated interaction: tab switching, typing, pointer movement, resizing, layouts.</summary>
public class StressTests : UITestBase
{
	public StressTests()
		: base(DemoApp.TestApp)
	{
	}

	/// <summary>Regression for #142.</summary>
	[Test, Order(1)]
	public async Task RapidDocumentSwitching_DoesNotCrash_Issue142()
	{
		var tab1 = await WaitForElementAsync(() => FindDocumentTabAsync("Document 1"), "the tab of Document 1");
		var tab2 = await WaitForElementAsync(() => FindDocumentTabAsync("Document 2"), "the tab of Document 2");
		for (var i = 0; i < 20; i++)
		{
			await Client.TapAsync(tab1.Id);
			await Client.TapAsync(tab2.Id);
		}

		Assert.That(Session.HasExited, Is.False, "App should not crash during rapid document switching (Issue #142).");
		await ActivateDocumentTabAsync("Document 1");
		await ActivateDocumentTabAsync("Document 2");
	}

	/// <summary>Regression for #139: text typed into a document survives switching to another tab and back.</summary>
	[Test, Order(2)]
	public async Task TextPreserved_WhenSwitchingTabs_Issue139()
	{
		await ActivateDocumentTabAsync("Document 1");
		await TapAsync(await WaitForElementAsync(
			() => FindElementAsync(e => e.Type == "TextBox" && e.IsVisible && e.Text == "Document 1 Content"),
			"the text box of Document 1"));

		await PressKeyAsync("Ctrl+A");
		await TypeAsync("TestData139");

		await ActivateDocumentTabAsync("Document 2");
		await ActivateDocumentTabAsync("Document 1");

		await WaitForElementAsync(
			() => FindElementAsync(e => e.Type == "TextBox" && e.IsVisible && e.Text == "TestData139"),
			"the typed text after switching tabs (Issue #139)");
	}

	/// <summary>Regression for #90.</summary>
	[Test, Order(3)]
	public async Task RapidMouseOverToolTabs_DoesNotCrash_Issue90()
	{
		var tool1 = await WaitForElementAsync(() => FindToolWindowHeaderAsync("Tool Window 1"), "the header of Tool Window 1");
		var tool2 = await WaitForElementAsync(() => FindToolWindowHeaderAsync("Tool Window 2"), "the header of Tool Window 2");
		for (var i = 0; i < 10; i++)
		{
			await MovePointerToAsync(tool1);
			await MovePointerToAsync(tool2);
		}

		Assert.That(Session.HasExited, Is.False, "App should not crash during rapid mouse movement over tool tabs (Issue #90).");
	}

	/// <summary>Regression for #162.</summary>
	[Test, Order(4)]
	public async Task RapidKeyboardInput_DoesNotLag_Issue162()
	{
		await ActivateDocumentTabAsync("Document 1");
		var textBox = await WaitForElementAsync(() => FindElementAsync(e => e.Type == "TextBox" && e.IsVisible && e.Text != null && e.Text.StartsWith("TestData139", StringComparison.Ordinal)), "the text box of Document 1");
		await TapAsync(textBox);
		await PressKeyAsync("Ctrl+A");

		await TypeAsync("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789");
		for (var i = 0; i < 10; i++)
		{
			await Client.KeyAsync("Left");
			await Client.KeyAsync("Right");
		}

		await WaitForElementAsync(
			() => FindElementAsync(e => e.Type == "TextBox" && e.IsVisible && e.Text == "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"),
			"all typed characters (Issue #162)");
	}

	/// <summary>Regression for #101.</summary>
	[Test, Order(5)]
	public async Task RapidWindowResize_DoesNotCrash_Issue101()
	{
		var layout = await GetLayoutAsync();
		for (var i = 0; i < 5; i++)
			await InvokeAsync("avalondock-resize-main-window", (layout.WindowWidth - 100 + (i * 40)).ToString(System.Globalization.CultureInfo.InvariantCulture), (layout.WindowHeight - 50 + (i * 20)).ToString(System.Globalization.CultureInfo.InvariantCulture));

		await InvokeAsync("avalondock-resize-main-window", layout.WindowWidth.ToString(System.Globalization.CultureInfo.InvariantCulture), layout.WindowHeight.ToString(System.Globalization.CultureInfo.InvariantCulture));

		Assert.That(Session.HasExited, Is.False, "App should not crash during rapid window resizing (Issue #101).");
		await ActivateDocumentTabAsync("Document 1");
	}

	/// <summary>Regression for #42.</summary>
	[Test, Order(6)]
	public async Task InteractionDuringLayout_DoesNotCrash_Issue42()
	{
		await ClickMenuAsync("Layout", "Save", "Layout_4");
		await ActivateDocumentTabAsync("Document 2");
		await ActivateDocumentTabAsync("Document 1");

		await ClickMenuAsync("Layout", "Load", "Layout_4");
		await ActivateDocumentTabAsync("Document 1");

		Assert.That(Session.HasExited, Is.False, "App should not crash during layout operations with concurrent interaction (Issue #42).");
	}

	/// <summary>Regression for #356.</summary>
	[Test, Order(7)]
	public async Task RapidSaveLoadLayout_Stability_Issue356()
	{
		for (var i = 0; i < 10; i++)
		{
			await ClickMenuAsync("Layout", "Save", "Layout_4");
			await ClickMenuAsync("Layout", "Load", "Layout_4");
		}

		Assert.That(Session.HasExited, Is.False, "App should remain stable after rapid save/load cycles (Issue #356).");
		await WaitForElementAsync(() => FindDocumentTabAsync("Document 1"), "Document 1 after rapid save/load cycles");
	}
}
