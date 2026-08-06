using System.IO;
using System.Linq;
using System.Threading;
using AvalonDock;
using AvalonDock.Core;
using AvalonDock.Layout;
using AvalonDock.Serializer.Json;
using AvalonDock.Serializer.Xml;
using NUnit.Framework;

namespace AvalonDockTest
{
	/// <summary>
	/// Covers the tool tip across a layout round trip.
	/// </summary>
	/// <remarks>
	/// The tool tip was written into the layout file by both serializers and read back by neither, so
	/// every restore quietly dropped it while the file carried a value nothing consumed. Only a string
	/// can make the trip: a tool tip built from a control or a binding is not something a layout file
	/// can describe, and it must be left alone rather than overwritten with nothing.
	/// </remarks>
	[TestFixture]
	[Apartment(ApartmentState.STA)]
	public class ToolTipRoundTripTests
	{
		/// <summary>The serializers under test, so every case runs through both formats.</summary>
		/// <returns>The names of the formats.</returns>
		private static string[] Formats() => new[] { "Xml", "Json" };

		/// <summary>Builds a serializer of the named format for a docking manager.</summary>
		/// <param name="format">The format name.</param>
		/// <param name="manager">The docking manager.</param>
		/// <returns>The serializer.</returns>
		private static ILayoutSerializer CreateSerializer(string format, DockingManager manager) =>
			format == "Xml"
				? (ILayoutSerializer)new XmlLayoutSerializer(manager)
				: new JsonLayoutSerializer(manager);

		/// <summary>Builds a layout with one document and one tool window.</summary>
		/// <param name="document">The document of the layout.</param>
		/// <param name="anchorable">The tool window of the layout.</param>
		/// <returns>The layout root.</returns>
		private static LayoutRoot BuildLayout(out LayoutDocument document, out LayoutAnchorable anchorable)
		{
			document = new LayoutDocument { Title = "Readme", ContentId = "readme" };
			anchorable = new LayoutAnchorable { Title = "Explorer", ContentId = "explorer" };

			var documentPane = new LayoutDocumentPane(document);
			var toolPane = new LayoutAnchorablePane(anchorable);
			var panel = new LayoutPanel(new LayoutDocumentPaneGroup(documentPane));
			panel.Children.Add(new LayoutAnchorablePaneGroup(toolPane));

			return new LayoutRoot { RootPanel = panel };
		}

		/// <summary>Stores a layout and restores it into a fresh docking manager.</summary>
		/// <param name="format">The format to round trip through.</param>
		/// <param name="layout">The layout to store.</param>
		/// <returns>The restored layout.</returns>
		private static LayoutRoot RoundTrip(string format, LayoutRoot layout)
		{
			var source = new DockingManager { Layout = layout };
			using var stream = new MemoryStream();
			CreateSerializer(format, source).Serialize(stream);

			var target = new DockingManager();
			var serializer = CreateSerializer(format, target);
			serializer.LayoutSerializationCallback += (s, e) => e.Content = new object();

			using var input = new MemoryStream(stream.ToArray());
			serializer.Deserialize(input);
			return target.Layout;
		}

		/// <summary>A tool tip set as text has to come back as that same text.</summary>
		/// <param name="format">The format to round trip through.</param>
		[Test]
		[TestCaseSource(nameof(Formats))]
		public void RoundTrip_KeepsTheToolTipOfADocumentAndAToolWindow(string format)
		{
			var layout = BuildLayout(out var document, out var anchorable);
			document.ToolTip = "The project readme";
			anchorable.ToolTip = "Files of the solution";

			var restored = RoundTrip(format, layout);

			var restoredDocument = restored.Descendents().OfType<LayoutDocument>()
				.Single(d => d.ContentId == "readme");
			var restoredAnchorable = restored.Descendents().OfType<LayoutAnchorable>()
				.Single(a => a.ContentId == "explorer");

			Assert.Multiple(() =>
			{
				Assert.That(restoredDocument.ToolTip, Is.EqualTo("The project readme"));
				Assert.That(restoredAnchorable.ToolTip, Is.EqualTo("Files of the solution"));
			});
		}

		/// <summary>A layout that carries no tool tip must not invent one.</summary>
		/// <param name="format">The format to round trip through.</param>
		[Test]
		[TestCaseSource(nameof(Formats))]
		public void RoundTrip_LeavesTheToolTipUnsetWhenTheLayoutHasNone(string format)
		{
			var restored = RoundTrip(format, BuildLayout(out _, out _));

			Assert.That(
				restored.Descendents().OfType<LayoutContent>().Select(c => c.ToolTip),
				Is.All.Null);
		}

		/// <summary>
		/// A tool tip that is not text cannot be stored, and the restore must not pretend otherwise by
		/// writing the result of ToString into it.
		/// </summary>
		/// <param name="format">The format to round trip through.</param>
		[Test]
		[TestCaseSource(nameof(Formats))]
		public void RoundTrip_DropsAToolTipThatIsNotText(string format)
		{
			var layout = BuildLayout(out _, out var anchorable);
			anchorable.ToolTip = new object();

			var restored = RoundTrip(format, layout);

			Assert.That(
				restored.Descendents().OfType<LayoutAnchorable>().Single(a => a.ContentId == "explorer").ToolTip,
				Is.Null);
		}

		/// <summary>Both formats have to agree about what the tool tip is after the trip.</summary>
		[Test]
		public void XmlAndJson_RestoreTheSameToolTips()
		{
			string[] ToolTips(string format)
			{
				var layout = BuildLayout(out var document, out var anchorable);
				document.ToolTip = "The project readme";
				anchorable.ToolTip = "Files of the solution";

				return RoundTrip(format, layout).Descendents().OfType<LayoutContent>()
					.OrderBy(c => c.ContentId)
					.Select(c => $"{c.ContentId}={c.ToolTip}")
					.ToArray();
			}

			Assert.That(ToolTips("Json"), Is.EqualTo(ToolTips("Xml")));
		}
	}
}
