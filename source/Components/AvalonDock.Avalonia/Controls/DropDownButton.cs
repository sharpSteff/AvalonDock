using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

namespace AvalonDock.Controls
{
	/// <summary>A toggle button that opens <see cref="DropDownContextMenu"/> below itself when clicked.</summary>
	public class DropDownButton : ToggleButton
	{
		/// <summary><see cref="DropDownContextMenu"/> property.</summary>
		public static readonly StyledProperty<ContextMenu> DropDownContextMenuProperty =
			AvaloniaProperty.Register<DropDownButton, ContextMenu>(nameof(DropDownContextMenu));

		/// <summary><see cref="DropDownContextMenuDataContext"/> property.</summary>
		public static readonly StyledProperty<object> DropDownContextMenuDataContextProperty =
			AvaloniaProperty.Register<DropDownButton, object>(nameof(DropDownContextMenuDataContext));

		/// <summary>Gets or sets the menu shown when the button is clicked.</summary>
		public ContextMenu DropDownContextMenu
		{
			get => GetValue(DropDownContextMenuProperty);
			set => SetValue(DropDownContextMenuProperty, value);
		}

		/// <summary>Gets or sets the data context given to <see cref="DropDownContextMenu"/> when it opens.</summary>
		public object DropDownContextMenuDataContext
		{
			get => GetValue(DropDownContextMenuDataContextProperty);
			set => SetValue(DropDownContextMenuDataContextProperty, value);
		}

		/// <inheritdoc/>
		protected override Type StyleKeyOverride => typeof(DropDownButton);

		/// <inheritdoc/>
		protected override void OnClick()
		{
			var menu = DropDownContextMenu;
			if (menu != null)
			{
				menu.Placement = PlacementMode.BottomEdgeAlignedLeft;
				menu.DataContext = DropDownContextMenuDataContext;
				menu.Closed += OnContextMenuClosed;
				menu.Open(this);
			}

			base.OnClick();
		}

		private void OnContextMenuClosed(object sender, RoutedEventArgs e)
		{
			if (sender is ContextMenu ctxMenu) ctxMenu.Closed -= OnContextMenuClosed;
			IsChecked = false;
		}
	}

	/// <summary>
	/// A content control that opens <see cref="DropDownContextMenu"/> at the pointer when it is right-clicked.
	/// Used for tab headers and window captions.
	/// </summary>
	public class DropDownControlArea : ContentControl
	{
		/// <summary><see cref="DropDownContextMenu"/> property.</summary>
		public static readonly StyledProperty<ContextMenu> DropDownContextMenuProperty =
			AvaloniaProperty.Register<DropDownControlArea, ContextMenu>(nameof(DropDownContextMenu));

		/// <summary><see cref="DropDownContextMenuDataContext"/> property.</summary>
		public static readonly StyledProperty<object> DropDownContextMenuDataContextProperty =
			AvaloniaProperty.Register<DropDownControlArea, object>(nameof(DropDownContextMenuDataContext));

		/// <summary>Initializes a new instance of the <see cref="DropDownControlArea"/> class.</summary>
		public DropDownControlArea()
		{
			// Keyboard focus must stay with the content of the pane (https://github.com/Dirkster99/AvalonDock/issues/225).
			Focusable = false;
			AddHandler(Avalonia.Input.InputElement.PointerReleasedEvent, OnPreviewPointerReleased, RoutingStrategies.Tunnel);
		}

		/// <summary>Gets or sets the menu shown on right click.</summary>
		public ContextMenu DropDownContextMenu
		{
			get => GetValue(DropDownContextMenuProperty);
			set => SetValue(DropDownContextMenuProperty, value);
		}

		/// <summary>Gets or sets the data context given to <see cref="DropDownContextMenu"/> when it opens.</summary>
		public object DropDownContextMenuDataContext
		{
			get => GetValue(DropDownContextMenuDataContextProperty);
			set => SetValue(DropDownContextMenuDataContextProperty, value);
		}

		/// <inheritdoc/>
		protected override Type StyleKeyOverride => typeof(DropDownControlArea);

		// Handled while tunnelling so that the menu opens before anything inside the area reacts to the
		// release - the WPF library does the same with a class handler on PreviewMouseRightButtonUp.
		private void OnPreviewPointerReleased(object sender, Avalonia.Input.PointerReleasedEventArgs e)
		{
			if (e.Handled || e.InitialPressMouseButton != Avalonia.Input.MouseButton.Right) return;
			var menu = DropDownContextMenu;
			if (menu == null) return;
			menu.Placement = PlacementMode.Pointer;
			menu.DataContext = DropDownContextMenuDataContext;
			menu.Open(e.Source as Control ?? this);
			e.Handled = true;
		}
	}
}
