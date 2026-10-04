using NUnit.Framework;

namespace AvalonDock.UITests.TestApp;

/// <summary>Floating tool windows: creating, activating and sizing them, and the main window next to them.</summary>
public class FloatingWindowTests : UITestBase
{
	private const string FloatingTitle = "Floating window with initial usercontrol size";

	public FloatingWindowTests()
		: base(DemoApp.TestApp)
	{
	}

	/// <summary>Regression for #254.</summary>
	[Test, Order(1)]
	public async Task NewFloatingWindow_CreatesFloatingWindow_Issue254()
	{
		await EnsureFloatingWindowAsync();

		var layout = await GetLayoutAsync();
		Assert.That(layout.FloatingWindows.Count(w => w.IsVisible), Is.GreaterThanOrEqualTo(1),
			"New floating window should create at least one floating window (Issue #254).");
		Assert.That(layout.Contents.Single(c => c.Title == FloatingTitle).IsFloating, Is.True);
	}

	/// <summary>Regression for #408: a floating window can be activated.</summary>
	[Test, Order(2)]
	public async Task FloatingWindow_CanBeActivated_Issue408()
	{
		await EnsureFloatingWindowAsync();
		await ActivateDocumentTabAsync("Document 1");

		await TapToolWindowHeaderAsync(FloatingTitle);

		await WaitUntilAsync(async () => (await FindContentAsync(FloatingTitle))?.IsActive == true,
			"the floating tool window to become active (Issue #408)");
	}

	/// <summary>Regression for #349: the main window stays usable next to a floating window.</summary>
	[Test, Order(3)]
	public async Task MainWindow_AccessibleWithFloatingWindow_Issue349()
	{
		await EnsureFloatingWindowAsync();

		await ActivateDocumentTabAsync("Document 2");
		await ActivateDocumentTabAsync("Document 1");
	}

	/// <summary>Regression for #174: a floating window is not created collapsed.</summary>
	[Test, Order(4)]
	public async Task FloatingWindow_HasReasonableSize_Issue174()
	{
		await EnsureFloatingWindowAsync();

		var window = (await GetLayoutAsync()).FloatingWindows.First(w => w.Contents.Contains(FloatingTitle));
		Assert.That(window.Width, Is.GreaterThan(50), "Floating window should have reasonable width (Issue #174).");
		Assert.That(window.Height, Is.GreaterThan(50), "Floating window should have reasonable height (Issue #174).");
	}

	private async Task EnsureFloatingWindowAsync()
	{
		if ((await GetLayoutAsync()).FloatingWindows.Any(w => w.IsVisible && w.Contents.Contains(FloatingTitle)))
			return;

		await ClickMenuAsync("Tools", "New floating window");
		await WaitUntilAsync(
			async () => (await GetLayoutAsync()).FloatingWindows.Any(w => w.IsVisible && w.Width > 0 && w.Contents.Contains(FloatingTitle)),
			"the new floating window");
		await ArrangeFloatingWindowsAsync();
	}
}
