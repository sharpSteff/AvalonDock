using System.Collections.Generic;
using Avalonia;
using Avalonia.VisualTree;
using Avalonia.Controls;

namespace AvalonDock.Controls
{
	/// <summary>Provides the <c>IsDraggingOver</c> attached property set on a drop target element while it is hovered.</summary>
	internal abstract class DropTargetBase : AvaloniaObject
	{
		/// <summary>IsDraggingOver attached property.</summary>
		public static readonly AttachedProperty<bool> IsDraggingOverProperty =
			AvaloniaProperty.RegisterAttached<DropTargetBase, Control, bool>("IsDraggingOver");

		/// <summary>Gets a value indicating whether a floating window is dragged over <paramref name="d"/>.</summary>
		/// <param name="d">The element.</param>
		/// <returns>The value.</returns>
		public static bool GetIsDraggingOver(Control d) => d.GetValue(IsDraggingOverProperty);

		/// <summary>Sets a value indicating whether a floating window is dragged over <paramref name="d"/>.</summary>
		/// <param name="d">The element.</param>
		/// <param name="value">The value.</param>
		public static void SetIsDraggingOver(Control d, bool value) => d.SetValue(IsDraggingOverProperty, value);
	}

	/// <summary>A target a floating window can be dropped onto.</summary>
	internal interface IDropTarget
	{
		/// <summary>Gets the kind of drop this target performs.</summary>
		DropTargetType Type { get; }

		/// <summary>Gets the preview of the drop, in the coordinates of <paramref name="overlayWindow"/>.</summary>
		/// <param name="overlayWindow">The overlay showing the preview.</param>
		/// <param name="floatingWindowModel">The model of the dragged window.</param>
		/// <returns>The preview geometry, or <see langword="null"/>.</returns>
		Avalonia.Media.Geometry GetPreviewPath(OverlayWindow overlayWindow, Layout.LayoutFloatingWindow floatingWindowModel);

		/// <summary>Gets whether the screen point (device pixels) is over the target.</summary>
		/// <param name="dragPoint">The screen point.</param>
		/// <returns><see langword="true"/> when the point hits the target.</returns>
		bool HitTestScreen(Point dragPoint);

		/// <summary>Gets the screen bounds (device pixels) of the target.</summary>
		/// <returns>The bounds.</returns>
		Rect GetScreenBounds();

		/// <summary>Drops the floating window onto the target.</summary>
		/// <param name="floatingWindow">The dropped window.</param>
		void Drop(Layout.LayoutFloatingWindow floatingWindow);

		/// <summary>Called when the pointer enters the target.</summary>
		void DragEnter();

		/// <summary>Called when the pointer leaves the target.</summary>
		void DragLeave();
	}

	/// <summary>Something that can show drop targets while a floating window is dragged over it: the manager or a floating window.</summary>
	internal interface IOverlayWindowHost
	{
		/// <summary>Gets the manager the host belongs to.</summary>
		DockingManager Manager { get; }

		/// <summary>Gets whether the screen point (device pixels) is over the host.</summary>
		/// <param name="dragPoint">The screen point.</param>
		/// <returns><see langword="true"/> when the point is over the host.</returns>
		bool HitTestScreen(Point dragPoint);

		/// <summary>Shows the overlay with the drop targets of this host.</summary>
		/// <param name="draggingWindow">The dragged window.</param>
		/// <returns>The overlay.</returns>
		IOverlayWindow ShowOverlayWindow(LayoutFloatingWindowControl draggingWindow);

		/// <summary>Hides the overlay of this host.</summary>
		void HideOverlayWindow();

		/// <summary>Gets the areas of this host that offer drop targets.</summary>
		/// <param name="draggingWindow">The dragged window.</param>
		/// <returns>The drop areas.</returns>
		System.Collections.Generic.IEnumerable<IDropArea> GetDropAreas(LayoutFloatingWindowControl draggingWindow);
	}

	/// <summary>The kind of element a drop area belongs to.</summary>
	public enum DropAreaType
	{
		/// <summary>The docking manager (outer edges).</summary>
		DockingManager,

		/// <summary>A document pane.</summary>
		DocumentPane,

		/// <summary>An empty document pane group.</summary>
		DocumentPaneGroup,

		/// <summary>An anchorable pane.</summary>
		AnchorablePane,
	}

	/// <summary>An area of a host over which drop targets are offered.</summary>
	public interface IDropArea
	{
		/// <summary>Gets the screen area (device pixels) in which the area is detected.</summary>
		Rect DetectionRect { get; }

		/// <summary>Gets the kind of area.</summary>
		DropAreaType Type { get; }
	}

	/// <summary>Helpers for caches of <see cref="IDropArea"/>s.</summary>
	internal static class DropAreaCache
	{
		/// <summary>Gets a value indicating whether a cached list of areas can still be used.</summary>
		/// <param name="areas">The cached areas, may be <see langword="null"/>.</param>
		/// <returns><see langword="true"/> when every area still matches its element.</returns>
		internal static bool IsValid(List<IDropArea> areas)
		{
			if (areas == null) return false;
			foreach (var area in areas)
			{
				if (area is IStaleCheck check && check.IsStale) return false;
			}

			return true;
		}
	}

	/// <summary>Implemented by drop areas that can tell whether they are out of date.</summary>
	internal interface IStaleCheck
	{
		/// <summary>Gets a value indicating whether the area is out of date.</summary>
		bool IsStale { get; }
	}

	/// <summary>An area of a host over which drop targets are offered.</summary>
	/// <typeparam name="T">The type of the element the area belongs to.</typeparam>
	public class DropArea<T> : IDropArea, IStaleCheck
		where T : Control
	{
		/// <summary>Initializes a new instance of the <see cref="DropArea{T}"/> class.</summary>
		/// <param name="areaElement">The element.</param>
		/// <param name="type">The kind of area.</param>
		internal DropArea(T areaElement, DropAreaType type)
		{
			AreaElement = areaElement;
			DetectionRect = areaElement.GetScreenArea();
			Type = type;
		}

		/// <summary>
		/// Gets a value indicating whether the area no longer describes its element: the element left the visual
		/// tree or moved since the area was created - the layout changed while the drag was in progress.
		/// </summary>
		bool IStaleCheck.IsStale => !AreaElement.IsAttachedToVisualTree() || AreaElement.GetScreenArea() != DetectionRect;

		/// <inheritdoc/>
		public Rect DetectionRect { get; }

		/// <inheritdoc/>
		public DropAreaType Type { get; }

		/// <summary>Gets the element that implements a drop target for a drag and drop (dock) operation.</summary>
		public T AreaElement { get; }
	}
}
