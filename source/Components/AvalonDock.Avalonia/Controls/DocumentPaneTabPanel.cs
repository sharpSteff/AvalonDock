using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using AvalonDock.Layout;

namespace AvalonDock.Controls
{
	/// <summary>
	/// The panel that lays out the tabs of a document pane in one row. Tabs that do not fit are hidden; the
	/// selected tab is moved to the front when it would be one of them.
	/// </summary>
	public class DocumentPaneTabPanel : Panel
	{
		/// <summary>Initializes a new instance of the <see cref="DocumentPaneTabPanel"/> class.</summary>
		public DocumentPaneTabPanel()
		{
			FlowDirection = Avalonia.Media.FlowDirection.LeftToRight;
		}

		/// <inheritdoc/>
		protected override Size MeasureOverride(Size availableSize)
		{
			double width = 0, height = 0;
			foreach (var child in Children)
			{
				child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
				width += child.DesiredSize.Width;
				height = Math.Max(height, child.DesiredSize.Height);
			}

			return new Size(Math.Min(width, availableSize.Width), height);
		}

		/// <inheritdoc/>
		protected override Size ArrangeOverride(Size finalSize)
		{
			var offset = 0.0;
			var skipAllOthers = false;
			foreach (var doc in Children.OfType<TabItem>().Where(IsShown).ToList())
			{
				if (skipAllOthers || offset + doc.DesiredSize.Width > finalSize.Width)
				{
					var layoutContent = doc.DataContext as LayoutContent ?? doc.Content as LayoutContent;
					if (layoutContent != null && layoutContent.IsSelected && !skipAllOthers)
					{
						var parentContainer = layoutContent.Parent as ILayoutContainer;
						var parentSelector = layoutContent.Parent as ILayoutContentSelector;
						var parentPane = layoutContent.Parent as ILayoutPane;
						var contentIndex = parentSelector?.IndexOf(layoutContent) ?? -1;
						if (contentIndex > 0 && parentContainer.ChildrenCount > 1)
						{
							parentPane.MoveChild(contentIndex, 0);
							parentSelector.SelectedContentIndex = 0;
							InvalidateArrange();
							return finalSize;
						}
					}

					doc.Opacity = 0;
					doc.IsHitTestVisible = false;
					doc.Arrange(new Rect(finalSize.Width, 0, 0, 0));
					skipAllOthers = true;
				}
				else
				{
					doc.ClearValue(OpacityProperty);
					doc.ClearValue(IsHitTestVisibleProperty);
					doc.Arrange(new Rect(offset, 0.0, doc.DesiredSize.Width, finalSize.Height));
					offset += doc.DesiredSize.Width;
				}
			}

			return finalSize;
		}

		/// <summary>Gets whether a tab takes part in the layout (hidden documents do not).</summary>
		/// <param name="tab">The tab.</param>
		/// <returns><see langword="true"/> when the tab is shown.</returns>
		private static bool IsShown(TabItem tab) => tab.IsVisible;
	}
}
