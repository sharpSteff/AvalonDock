using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace AvalonDock.Controls
{
	/// <summary>
	/// The title of an anchorable in a <see cref="ToggleDockingManager"/>: an options button (three dots) that
	/// opens the toggle menu and a minimize button that collapses the anchorable onto its sidebar.
	/// </summary>
	[TemplatePart("PART_OptionsButton", typeof(Button))]
	public class ToggleAnchorablePaneTitle : AnchorablePaneTitle
	{
		/// <summary><see cref="ShowMinimizeButton"/> property.</summary>
		public static readonly StyledProperty<bool> ShowMinimizeButtonProperty =
			AvaloniaProperty.Register<ToggleAnchorablePaneTitle, bool>(nameof(ShowMinimizeButton), true);

		/// <summary><see cref="ShowOptionsButton"/> property.</summary>
		public static readonly StyledProperty<bool> ShowOptionsButtonProperty =
			AvaloniaProperty.Register<ToggleAnchorablePaneTitle, bool>(nameof(ShowOptionsButton), true);

		private Button _optionsButton;

		/// <summary>Gets or sets a value indicating whether the minimize button is shown.</summary>
		public bool ShowMinimizeButton
		{
			get => GetValue(ShowMinimizeButtonProperty);
			set => SetValue(ShowMinimizeButtonProperty, value);
		}

		/// <summary>Gets or sets a value indicating whether the options button is shown.</summary>
		public bool ShowOptionsButton
		{
			get => GetValue(ShowOptionsButtonProperty);
			set => SetValue(ShowOptionsButtonProperty, value);
		}

		/// <inheritdoc/>
		protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
		{
			base.OnApplyTemplate(e);
			if (_optionsButton != null) _optionsButton.Click -= OnOptionsButtonClick;
			_optionsButton = e.NameScope.Find<Button>("PART_OptionsButton");
			if (_optionsButton != null) _optionsButton.Click += OnOptionsButtonClick;
		}

		private void OnOptionsButtonClick(object sender, RoutedEventArgs e)
		{
			var manager = FindToggleDockingManager();
			if (manager == null || Model == null) return;

			var menu = manager.BuildToggleContextMenu(Model);
			menu.Placement = PlacementMode.Bottom;
			menu.Open(sender as Control ?? this);
		}

		private ToggleDockingManager FindToggleDockingManager()
		{
			// Inside a detached window this title is the root of its own visual tree, so the manager can only
			// be reached through the model there.
			return this.FindVisualAncestor<ToggleDockingManager>() ?? Model?.Root?.Manager as ToggleDockingManager;
		}
	}
}
