using System.Text.Json;
using Microsoft.Maui.DevFlow.Driver;
using NUnit.Framework;

namespace AvalonDock.UITests;

/// <summary>
/// Base class of the UI tests. Starts a demo application for each fixture and drives it through its
/// DevFlow agent. The helpers only use what the WPF and the Avalonia builds have in common - the
/// AvalonDock control type names, the titles and headers of the demos, and the layout model reported by
/// the avalondock-layout action - so every test runs unchanged against both.
/// </summary>
[TestFixture]
[Category("UI")]
[NonParallelizable]
public abstract class UITestBase
{
	protected static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

	// MaxDepth: the visual trees of the demos nest deeper than the default limit of 64, which makes the
	// driver's own GetTreeAsync return an empty tree.
	private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, MaxDepth = 1024 };

	private HttpClient? _http;

	private readonly DemoApp _app;

	protected UITestBase(DemoApp app)
	{
		_app = app;
	}

	protected DemoAppSession Session { get; private set; } = null!;

	protected AgentClient Client => Session.Client;

	protected UiFramework Framework => Session.Framework;

	[OneTimeSetUp]
	public async Task StartDemoAsync()
	{
		Session = await DemoAppSession.StartAsync(_app, DemoAppSession.SelectedFramework);
		await WaitUntilAsync(async () => (await GetLayoutAsync()).ManagerLoaded, "the DockingManager to load", TimeSpan.FromSeconds(30));
	}

	[OneTimeTearDown]
	public void StopDemo()
	{
		_http?.Dispose();
		Session?.Dispose();
	}

	/// <summary>A plain HTTP client for the agent, for the endpoints the driver does not cover.</summary>
	protected HttpClient Http => _http ??= new HttpClient { BaseAddress = new Uri($"http://localhost:{Session.Port}") };

	[TearDown]
	public async Task CloseTransientUiAsync()
	{
		if (Session == null || Session.HasExited)
			return;

		if (TestContext.CurrentContext.Result.Outcome.Status == NUnit.Framework.Interfaces.TestStatus.Failed)
			await SaveFailureEvidenceAsync();

		// Close menus and popups a failed test may have left open.
		await Client.KeyAsync("Escape");
		await AnswerDialogIfPresentAsync("No");
	}

	/// <summary>
	/// Saves a screenshot of the main window and the layout model of a failed test, as test attachments, to
	/// AVALONDOCK_UI_ARTIFACTS if that is set (CI uploads it) and the test's work directory otherwise.
	/// </summary>
	private async Task SaveFailureEvidenceAsync()
	{
		try
		{
			var directory = Environment.GetEnvironmentVariable("AVALONDOCK_UI_ARTIFACTS");
			if (string.IsNullOrWhiteSpace(directory))
				directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "ui-artifacts");
			Directory.CreateDirectory(directory);

			var name = $"{Framework}-{GetType().Name}-{TestContext.CurrentContext.Test.Name}";
			var screenshot = await Client.ScreenshotAsync();
			if (screenshot != null)
			{
				var path = Path.Combine(directory, name + ".png");
				await File.WriteAllBytesAsync(path, screenshot);
				TestContext.AddTestAttachment(path);
			}

			var layoutPath = Path.Combine(directory, name + ".layout.json");
			await File.WriteAllTextAsync(layoutPath, (await Client.InvokeActionAsync("avalondock-layout"))?.ReturnValue ?? string.Empty);
			TestContext.AddTestAttachment(layoutPath);

			var treePath = Path.Combine(directory, name + ".tree.json");
			await File.WriteAllTextAsync(treePath, await Http.GetStringAsync("/api/v1/ui/tree"));
			TestContext.AddTestAttachment(treePath);
		}
		catch (Exception ex)
		{
			TestContext.Progress.WriteLine($"[UITestBase] Could not save the failure evidence: {ex.Message}");
		}
	}

	// ===== Layout model =====

	/// <summary>The layout model of the main DockingManager.</summary>
	protected async Task<LayoutSnapshot> GetLayoutAsync()
	{
		var result = await Client.InvokeActionAsync("avalondock-layout");
		if (result is not { Success: true } || result.ReturnValue == null)
			throw new InvalidOperationException($"avalondock-layout failed: {result?.Error ?? "no answer"}");

		return JsonSerializer.Deserialize<LayoutSnapshot>(result.ReturnValue, JsonOptions)
			?? throw new InvalidOperationException("avalondock-layout returned no layout.");
	}

	/// <summary>Invokes a DevFlow action of the demo (see DevFlowAgent.cs) and returns its result as text.</summary>
	protected async Task<string?> InvokeAsync(string action, params string[] args)
	{
		var jsonArgs = new System.Text.Json.Nodes.JsonArray(args.Select(a => (System.Text.Json.Nodes.JsonNode?)a).ToArray());
		var result = await Client.InvokeActionAsync(action, jsonArgs);
		if (result is not { Success: true })
			throw new InvalidOperationException($"{action} failed: {result?.Error ?? "no answer"}");
		return result.ReturnValue;
	}

	/// <summary>The application's windows.</summary>
	protected async Task<List<WindowSnapshot>> GetWindowsAsync()
		=> JsonSerializer.Deserialize<List<WindowSnapshot>>(await InvokeAsync("avalondock-windows") ?? "[]", JsonOptions) ?? new List<WindowSnapshot>();

	/// <summary>
	/// Moves the floating windows over the main window's document area, where they cover nothing the tests
	/// click: a new floating window opens at the top left corner of the screen.
	/// </summary>
	protected Task ArrangeFloatingWindowsAsync() => InvokeAsync("avalondock-arrange-floating-windows");

	protected async Task<ContentSnapshot?> FindContentAsync(string title)
		=> (await GetLayoutAsync()).Contents.FirstOrDefault(c => c.Title == title);

	// ===== Element tree =====

	/// <summary>The element trees of all windows.</summary>
	protected async Task<List<ElementInfo>> GetTreeAsync()
	{
		var json = await Http.GetStringAsync("/api/v1/ui/tree");
		return JsonSerializer.Deserialize<List<ElementInfo>>(json, JsonOptions) ?? new List<ElementInfo>();
	}

	/// <summary>Every element of every window, depth first.</summary>
	protected async Task<List<ElementInfo>> GetElementsAsync()
	{
		var result = new List<ElementInfo>();
		foreach (var root in await GetTreeAsync())
			Flatten(root, result);
		return result;
	}

	protected async Task<ElementInfo?> FindElementAsync(Func<ElementInfo, bool> predicate)
		=> (await GetElementsAsync()).FirstOrDefault(predicate);

	/// <summary>
	/// The tab of a document, found as a LayoutDocumentTabItem that shows the title. Null if the document
	/// has no visible tab.
	/// </summary>
	protected async Task<ElementInfo?> FindDocumentTabAsync(string title)
	{
		foreach (var root in await GetTreeAsync())
		{
			var tab = FindContainerShowing(root, title, "LayoutDocumentTabItem");
			if (tab != null)
				return tab;
		}

		return null;
	}

	/// <summary>
	/// The visible header of a tool window: its tab, its pane title or its auto-hide side button.
	/// </summary>
	protected async Task<ElementInfo?> FindToolWindowHeaderAsync(string title)
	{
		var roots = await GetTreeAsync();

		// In order of preference: a tab selects the tool window without touching the pane's buttons. A floating
		// window with a single tool window shows its title in the window's title bar.
		foreach (var type in new[] { "LayoutAnchorableTabItem", "LayoutAnchorControl", "AnchorablePaneTitle", "LayoutAnchorableFloatingWindowControl" })
		{
			foreach (var root in roots)
			{
				var header = FindContainerShowing(root, title, type);
				if (header != null)
					return header;
			}
		}

		return null;
	}

	/// <summary>Clicks the title text of a tool window's header (see <see cref="FindToolWindowHeaderAsync"/>).</summary>
	protected async Task TapToolWindowHeaderAsync(string title)
	{
		var header = await WaitForElementAsync(() => FindToolWindowHeaderAsync(title), $"the header of '{title}'");
		await TapAsync(FindDescendant(header, e => e.IsVisible && e.Text == title) ?? header);
	}

	/// <summary>Selects a tool window by clicking its header, and waits until the layout model has it selected.</summary>
	protected async Task ActivateToolWindowAsync(string title)
	{
		await TapToolWindowHeaderAsync(title);
		await WaitUntilAsync(async () => (await FindContentAsync(title))?.IsSelected == true, $"'{title}' to be selected");
	}

	protected async Task<ElementInfo?> FindByTextAsync(string text)
		=> await FindElementAsync(e => e.IsVisible && e.Text == text);

	protected async Task<ElementInfo> WaitForElementAsync(Func<Task<ElementInfo?>> find, string description, TimeSpan? timeout = null)
	{
		ElementInfo? element = null;
		await WaitUntilAsync(async () => (element = await find()) != null, description, timeout);
		return element!;
	}

	// ===== Input =====

	protected async Task TapAsync(ElementInfo element)
	{
		Assert.That(await Client.TapAsync(element.Id), Is.True, $"Tapping {element.Type} '{element.Text}' failed.");
		await SettleAsync();
	}

	protected async Task PressKeyAsync(string key)
	{
		Assert.That(await Client.KeyAsync(key), Is.True, $"Pressing {key} failed.");
		await SettleAsync();
	}

	/// <summary>Right-clicks an element, e.g. to open its context menu.</summary>
	protected async Task RightTapAsync(ElementInfo element)
	{
		await PostActionAsync("right-tap", new { id = element.Id, elementId = element.Id });
		await SettleAsync();
	}

	/// <summary>Drags with the pointer from one element to another, or by an offset.</summary>
	protected async Task DragAsync(ElementInfo from, ElementInfo? to = null, double dx = 0, double dy = 0, int steps = 20)
	{
		await PostActionAsync("drag", new { fromId = from.Id, toId = to?.Id, dx, dy, steps });
		await SettleAsync();
	}

	/// <summary>Selects a document by clicking its tab, and waits until the layout model has it selected.</summary>
	protected async Task ActivateDocumentTabAsync(string title)
	{
		var tab = await WaitForElementAsync(() => FindDocumentTabAsync(title), $"the tab of '{title}'");
		await TapAsync(FindDescendant(tab, e => e.IsVisible && e.Text == title) ?? tab);
		await WaitUntilAsync(async () => (await FindContentAsync(title))?.IsSelected == true, $"'{title}' to be selected");
	}

	/// <summary>Clicks a document tab's close button, then answers the demo's confirmation question.</summary>
	protected async Task CloseDocumentAsync(string title, bool confirm = true)
	{
		await ActivateDocumentTabAsync(title);
		var tab = await WaitForElementAsync(() => FindDocumentTabAsync(title), $"the tab of '{title}'");
		var closeButton = FindDescendant(tab, e => e.Type == "Button" && e.IsVisible);
		Assert.That(closeButton, Is.Not.Null, $"The tab of '{title}' has no close button.");
		await TapAsync(closeButton!);
		await AnswerDialogAsync(confirm ? "Yes" : "No");
	}

	/// <summary>Saves the layout through the TestApp's Layout > Save menu.</summary>
	protected async Task SaveLayoutAsync(string name)
	{
		await ClickMenuAsync("Layout", "Save", name);
		await SettleAsync();
	}

	/// <summary>Loads a layout through the TestApp's Layout > Load menu.</summary>
	protected async Task LoadLayoutAsync(string name)
	{
		await ClickMenuAsync("Layout", "Load", name);
		await WaitUntilAsync(async () => (await GetLayoutAsync()).ManagerLoaded, "the layout to load");
		await SettleAsync();
	}

	/// <summary>Types text into the focused element, character by character, with native input.</summary>
	protected async Task TypeAsync(string text)
	{
		foreach (var ch in text)
			Assert.That(await Client.KeyAsync(ch.ToString()), Is.True, $"Typing '{ch}' failed.");
		await SettleAsync();
	}

	/// <summary>Moves the pointer over an element.</summary>
	protected Task MovePointerToAsync(ElementInfo element) => PostActionAsync("move", new { elementId = element.Id });

	/// <summary>Clicks through a menu path, e.g. ("Tools", "Tool Window1").</summary>
	protected async Task ClickMenuAsync(params string[] path)
	{
		foreach (var header in path)
		{
			var item = await WaitForElementAsync(
				() => FindElementAsync(e => e.Type == "MenuItem" && e.IsVisible && MenuHeaderIs(e, header)),
				$"menu item '{header}'");
			await TapAsync(item);
		}
	}

	/// <summary>Whether a menu item shows the header, ignoring the underscore that marks an access key.</summary>
	protected static bool MenuHeaderIs(ElementInfo item, string header)
		=> item.Text != null && item.Text.Replace("_", string.Empty, StringComparison.Ordinal) == header.Replace("_", string.Empty, StringComparison.Ordinal);

	// ===== Dialogs =====

	/// <summary>
	/// Answers the demo's Yes/No question - a native message box on WPF, a ConfirmDialog window with
	/// buttons of the same names on Avalonia. Returns false if no question is open.
	/// </summary>
	protected async Task<bool> AnswerDialogIfPresentAsync(string button)
	{
		var dialogButton = await FindElementAsync(e => e.Type == "Button" && e.IsVisible && e.Text == button
			&& e.ParentId != null);
		if (Framework == UiFramework.Avalonia)
		{
			if (dialogButton == null)
				return false;

			await TapAsync(dialogButton);
			return true;
		}

		using var response = await PostMutationAsync("/api/v1/alert/dismiss", new { buttonLabel = button });
		await SettleAsync();
		return response.IsSuccessStatusCode;
	}

	protected async Task AnswerDialogAsync(string button)
	{
		var answered = false;
		await WaitUntilAsync(async () => answered = await AnswerDialogIfPresentAsync(button), $"a dialog with a '{button}' button");
		Assert.That(answered, Is.True);
	}

	/// <summary>
	/// Posts to an action endpoint the driver does not wrap, under the driver's mutation lease.
	/// </summary>
	protected async Task PostActionAsync(string action, object body)
	{
		using var response = await PostMutationAsync($"/api/v1/ui/actions/{action}", body);
		var text = await response.Content.ReadAsStringAsync();
		Assert.That(response.IsSuccessStatusCode, Is.True, $"{action} failed: {text}");
	}

	/// <summary>
	/// Posts a mutation the driver does not wrap. Like the driver's own mutations it claims the mutation
	/// lease first - the lease expires when the tests leave the app alone for a while - and sends it along.
	/// </summary>
	private async Task<HttpResponseMessage> PostMutationAsync(string path, object body)
	{
		var lease = await Client.ControlMutationLeaseAsync("claim");
		Assert.That(lease?.YouHold, Is.Not.False, $"Claiming the mutation lease failed: {lease?.Error}");

		using var request = new HttpRequestMessage(HttpMethod.Post, path)
		{
			// A sized body: the agent's HTTP server does not read chunked request bodies.
			Content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json"),
		};
		request.Headers.Add("X-DevFlow-Lease", Client.MutationLeaseId);
		request.Headers.Add("X-DevFlow-Holder", Client.MutationLeaseHolderKind);
		return await Http.SendAsync(request);
	}

	protected static ElementInfo? FindDescendant(ElementInfo element, Func<ElementInfo, bool> predicate)
	{
		foreach (var child in element.Children ?? Enumerable.Empty<ElementInfo>())
		{
			if (predicate(child))
				return child;
			var found = FindDescendant(child, predicate);
			if (found != null)
				return found;
		}

		return null;
	}

	// ===== Waiting =====

	protected static async Task WaitUntilAsync(Func<Task<bool>> condition, string description, TimeSpan? timeout = null)
	{
		var deadline = DateTime.UtcNow + (timeout ?? DefaultTimeout);
		while (true)
		{
			if (await condition())
				return;
			if (DateTime.UtcNow > deadline)
				Assert.Fail($"Timed out waiting for {description}.");
			await Task.Delay(200);
		}
	}

	/// <summary>Gives the application a moment to process the input before the next query.</summary>
	protected static Task SettleAsync() => Task.Delay(250);

	private static void Flatten(ElementInfo element, List<ElementInfo> into)
	{
		into.Add(element);
		if (element.Children == null)
			return;
		foreach (var child in element.Children)
			Flatten(child, into);
	}

	private static ElementInfo? FindContainerShowing(ElementInfo element, string text, params string[] containerTypes)
	{
		// Visible and laid out: WPF reports the tab of a pane's only tool window, which the pane hides by
		// collapsing its tab strip, as visible but without bounds.
		if (containerTypes.Contains(element.Type) && element.IsVisible && element.Bounds is { Width: > 0, Height: > 0 } && ShowsText(element, text))
			return element;

		if (element.Children == null)
			return null;

		foreach (var child in element.Children)
		{
			var found = FindContainerShowing(child, text, containerTypes);
			if (found != null)
				return found;
		}

		return null;
	}

	private static bool ShowsText(ElementInfo element, string text)
		=> element.Text == text || (element.Children?.Any(c => c.IsVisible && ShowsText(c, text)) ?? false);
}

/// <summary>The layout model reported by the avalondock-layout action (see DevFlowAgent.cs).</summary>
public sealed class LayoutSnapshot
{
	public string? WindowTitle { get; set; }

	public double WindowWidth { get; set; }

	public double WindowHeight { get; set; }

	public bool ManagerFound { get; set; }

	public bool ManagerLoaded { get; set; }

	public int FloatingWindowCount { get; set; }

	public string? ActiveContent { get; set; }

	public string? LastFocusedDocument { get; set; }

	/// <summary>The title of the content whose auto-hide flyout is open, if one is.</summary>
	public string? AutoHideWindowContent { get; set; }

	public List<FloatingWindowSnapshot> FloatingWindows { get; set; } = new();

	public List<ContentSnapshot> Contents { get; set; } = new();

	public IEnumerable<ContentSnapshot> Documents => Contents.Where(c => c.Kind == "Document");

	public IEnumerable<ContentSnapshot> Anchorables => Contents.Where(c => c.Kind == "Anchorable");
}

/// <summary>A window of the application, as listed by the avalondock-windows action.</summary>
public sealed class WindowSnapshot
{
	public string Kind { get; set; } = string.Empty;

	public string? Title { get; set; }

	public string? State { get; set; }

	public bool IsVisible { get; set; }
}

/// <summary>A floating window of a <see cref="LayoutSnapshot"/>.</summary>
public sealed class FloatingWindowSnapshot
{
	public List<string> Contents { get; set; } = new();

	public bool IsVisible { get; set; }

	public double Width { get; set; }

	public double Height { get; set; }
}

/// <summary>One document or anchorable of a <see cref="LayoutSnapshot"/>.</summary>
public sealed class ContentSnapshot
{
	public string Kind { get; set; } = string.Empty;

	public string? Title { get; set; }

	public string? ContentId { get; set; }

	public bool IsActive { get; set; }

	public bool IsSelected { get; set; }

	public bool IsFloating { get; set; }

	public bool IsHidden { get; set; }

	public bool IsAutoHidden { get; set; }

	public bool IsDetached { get; set; }

	public bool IsVisible { get; set; }

	public string? Container { get; set; }
}