using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace AvalonDock.Controls
{
	/// <summary>
	/// Describes a pointer drag that is about to be handed over to a floating window, for example when a
	/// tab is torn out of its pane.
	/// </summary>
	/// <remarks>
	/// The pointer stays captured by <see cref="CaptureTarget"/> - normally the top level that contained the
	/// element the drag started on - for the whole drag. That element does not move while the floating window
	/// follows the pointer, so the pointer positions it reports stay consistent; positions taken relative to
	/// the moving window would drift as its native origin and the managed one fall out of step.
	/// </remarks>
	internal sealed class DragStartInfo
	{
		/// <summary>Initializes a new instance of the <see cref="DragStartInfo"/> class.</summary>
		/// <param name="pointer">The pointer driving the drag.</param>
		/// <param name="captureTarget">The control that keeps the pointer captured.</param>
		/// <param name="screenPoint">The pointer position in screen pixels.</param>
		/// <param name="grabOffset">The offset of the pointer from the top-left corner of the floating window, in screen pixels.</param>
		public DragStartInfo(IPointer pointer, Control captureTarget, Point screenPoint, Vector grabOffset)
		{
			Pointer = pointer;
			CaptureTarget = captureTarget;
			ScreenPoint = screenPoint;
			GrabOffset = grabOffset;
		}

		/// <summary>Gets the pointer driving the drag.</summary>
		public IPointer Pointer { get; }

		/// <summary>Gets the control that keeps the pointer captured during the drag.</summary>
		public Control CaptureTarget { get; }

		/// <summary>Gets the pointer position at the start of the drag, in screen pixels.</summary>
		public Point ScreenPoint { get; }

		/// <summary>Gets the offset of the pointer from the top-left corner of the floating window, in screen pixels.</summary>
		public Vector GrabOffset { get; }

		/// <summary>Creates the drag info for a pointer event raised on <paramref name="source"/>.</summary>
		/// <param name="e">The pointer event.</param>
		/// <param name="source">The element the event was raised on.</param>
		/// <param name="grabOffset">The grab offset in device independent pixels; scaled to screen pixels here.</param>
		/// <returns>The drag info, or <see langword="null"/> when the element is not in a top level.</returns>
		public static DragStartInfo FromPointerEvent(PointerEventArgs e, Visual source, Vector grabOffset)
		{
			var topLevel = TopLevel.GetTopLevel(source);
			if (topLevel == null || e == null) return null;
			var screenPoint = source.LocalToScreen(e.GetPosition(source));
			var scaling = topLevel.RenderScaling;
			return new DragStartInfo(e.Pointer, topLevel, screenPoint, grabOffset * scaling);
		}
	}
}
