using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.VisualTree;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AvalonDock.Layout;

namespace AvalonDock.Controls
{
	/// <summary>
	/// Remembers which element had the keyboard focus inside each content, and puts the focus back there when
	/// the content is activated again.
	/// </summary>
	/// <remarks>
	/// The WPF version also tracks Win32 focus inside HWND hosts; Avalonia has none, so only the focus of
	/// Avalonia elements is tracked.
	/// </remarks>
	internal static class FocusElementManager
	{
		private static readonly ConditionalWeakTable<ILayoutElement, WeakReference<IInputElement>> _modelFocusedElement =
			new ConditionalWeakTable<ILayoutElement, WeakReference<IInputElement>>();

		private static readonly List<WeakReference<Control>> _trackedRoots = new List<WeakReference<Control>>();

		/// <summary>Starts tracking the focus inside <paramref name="root"/> (a manager or a floating window).</summary>
		/// <param name="root">The element to track.</param>
		internal static void SetupFocusManagement(Control root)
		{
			if (root == null) return;
			root.AddHandler(InputElement.GotFocusEvent, OnGotFocus, RoutingStrategies.Bubble, handledEventsToo: true);
			_trackedRoots.Add(new WeakReference<Control>(root));
		}

		/// <summary>Stops tracking the focus inside <paramref name="root"/>.</summary>
		/// <param name="root">The element.</param>
		internal static void FinalizeFocusManagement(Control root)
		{
			if (root == null) return;
			root.RemoveHandler(InputElement.GotFocusEvent, OnGotFocus);
			_trackedRoots.RemoveAll(w => !w.TryGetTarget(out var target) || ReferenceEquals(target, root));
		}

		/// <summary>Gets the element that last had the focus inside the content of <paramref name="model"/>.</summary>
		/// <param name="model">The content.</param>
		/// <returns>The element, or <see langword="null"/>.</returns>
		internal static IInputElement GetLastFocusedElement(ILayoutElement model)
		{
			if (model != null && _modelFocusedElement.TryGetValue(model, out var reference) && reference.TryGetTarget(out var element))
				return element;
			return null;
		}

		/// <summary>Puts the focus back on the element that last had it inside the content of <paramref name="model"/>.</summary>
		/// <param name="model">The content.</param>
		internal static void SetFocusOnLastElement(ILayoutElement model)
		{
			var element = GetLastFocusedElement(model);
			if (element is not Control control || !control.IsEffectivelyVisible || !control.IsAttachedToVisualTree()) return;
			if (control.IsKeyboardFocusWithin) return;

			// The content may still be on its way into the visual tree; focus it once that is done.
			Dispatcher.UIThread.Post(
				() =>
				{
					if (control.IsAttachedToVisualTree() && !control.IsKeyboardFocusWithin) control.Focus();
				},
				DispatcherPriority.Input);
		}

		/// <summary>
		/// Gets a value indicating whether a focus change gives the focus back to the element of
		/// <paramref name="model"/> that had it last - what a window that is activated again (when a menu or
		/// another window closes, say) does. That is no new choice of content: activating that content again
		/// would undo an activation made in between, e.g. by the menu command that just ran.
		/// </summary>
		/// <param name="model">The content the focused element belongs to.</param>
		/// <param name="e">The focus change.</param>
		/// <returns><see langword="true"/> for a focus restore.</returns>
		internal static bool IsFocusRestore(ILayoutElement model, FocusChangedEventArgs e)
			=> e.NavigationMethod == NavigationMethod.Unspecified
				&& e.Source is IInputElement element
				&& ReferenceEquals(GetLastFocusedElement(model), element);

		private static void OnGotFocus(object sender, FocusChangedEventArgs e)
		{
			if (e.Source is not Visual focusedElement) return;

			// Avoid tracking focus for tab headers
			if (focusedElement is LayoutAnchorableTabItem || focusedElement is LayoutDocumentTabItem) return;

			var parentContent = focusedElement.FindVisualAncestor<LayoutContentControlBase>();
			ILayoutElement model = parentContent switch
			{
				LayoutAnchorableControl anchorableControl => anchorableControl.Model,
				LayoutDocumentControl documentControl => documentControl.Model,
				_ => null,
			};

			if (model == null || focusedElement is not IInputElement inputElement) return;

			var isFocusRestore = IsFocusRestore(model, e);

			_modelFocusedElement.AddOrUpdate(model, new WeakReference<IInputElement>(inputElement));
			if (!isFocusRestore && model is LayoutContent content && !content.IsActive) content.IsActive = true;
		}
	}
}
