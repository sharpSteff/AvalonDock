using NUnit.Framework;

namespace AvalonDock.UITests.TestApp;

/// <summary>Closing a floating tool window the way the operating system does (system menu, Alt+F4).</summary>
public class FloatingWindowCloseTests : UITestBase
{
	public FloatingWindowCloseTests()
		: base(DemoApp.TestApp)
	{
	}

	/// <summary>
	/// Regression for #368: a system close of a floating tool window raises Hiding (TestApp asks a question
	/// for Tool Window 1), hides the tool window without exiting the application, and the tool window can be
	/// shown again.
	/// </summary>
	[Test]
	public async Task FloatingToolWindow_SystemClose_CanBeReshown_Issue368()
	{
		await ClickMenuAsync("Tools", "Float Tool Window1");
		await WaitUntilAsync(async () => (await FindContentAsync("Tool Window 1"))?.IsFloating == true, "Tool Window 1 to float");
		await ArrangeFloatingWindowsAsync();

		Assert.That(await InvokeAsync("avalondock-close-window", "Tool Window 1"), Is.EqualTo("True"));

		// The Hiding question; answering Yes hides the tool window.
		await AnswerDialogAsync("Yes");
		await WaitUntilAsync(async () => (await FindContentAsync("Tool Window 1"))?.IsHidden == true, "Tool Window 1 to be hidden");
		Assert.That(Session.HasExited, Is.False, "Closing a floating tool window must not exit the application.");

		await ClickMenuAsync("Tools", "Tool Window1");
		await WaitUntilAsync(
			async () => (await FindContentAsync("Tool Window 1")) is { IsHidden: false, IsVisible: true },
			"Tool Window 1 to be shown again after the system close hid it");
	}
}
