using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using AvalonDock.Layout;
using AvalonDock.Themes;

namespace AvalonDock.Controls
{
	/// <summary>
	/// An ordinary, independent top-level window that hosts the content of an anchorable detached from the
	/// docking layout (see <see cref="DockingManager.DetachAnchorableToWindow(LayoutAnchorable)"/>).
	/// </summary>
	/// <remarks>
	/// Unlike a floating window it has the native frame and task bar entry of a normal application window,
	/// no owner, and it takes no part in docking: closing it returns the anchorable to the layout.
	/// </remarks>
	public class DetachedAnchorableWindow : Window
	{
		private const double DefaultDetachedWidth = 400d;
		private const double DefaultDetachedHeight = 500d;
		private const double MinimumDetachedSize = 120d;

		private readonly LayoutAnchorable _model;
		private readonly Grid _root;
		private Control _hostedView;
		private IStyle _themeStyles;

		/// <summary>Initializes a new instance of the <see cref="DetachedAnchorableWindow"/> class.</summary>
		/// <param name="model">The detached anchorable.</param>
		/// <param name="hostedView">The view of the anchorable.</param>
		/// <param name="header">The optional header shown above the view.</param>
		public DetachedAnchorableWindow(LayoutAnchorable model, Control hostedView, Control header = null)
		{
			_model = model ?? throw new ArgumentNullException(nameof(model));
			_hostedView = hostedView ?? throw new ArgumentNullException(nameof(hostedView));

			ShowInTaskbar = true;
			CanResize = true;

			_root = new Grid();
			_root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
			_root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
			if (header != null)
			{
				Grid.SetRow(header, 0);
				_root.Children.Add(header);
			}

			Grid.SetRow(_hostedView, 1);
			_root.Children.Add(_hostedView);
			Content = _root;

			ApplyModelBounds();
			UpdateTitle();
			UpdateIcon();
			_model.PropertyChanged += OnModelPropertyChanged;
		}

		/// <summary>Gets the detached anchorable.</summary>
		public LayoutAnchorable Model => _model;

		/// <summary>Gets a value indicating whether the window still hosts the view.</summary>
		public bool HasView => _hostedView != null;

		/// <summary>Gets a value indicating whether the window has been closed.</summary>
		public bool IsClosed { get; private set; }

		/// <inheritdoc/>
		protected override Type StyleKeyOverride => typeof(Window);

		/// <summary>Takes the view out of the window so it can return to the layout.</summary>
		/// <returns>The view.</returns>
		public Control ReleaseView()
		{
			var view = _hostedView;
			_hostedView = null;
			if (view != null && _root.Children.Contains(view)) _root.Children.Remove(view);
			return view;
		}

		/// <summary>Applies <paramref name="newTheme"/>, replacing <paramref name="oldTheme"/>.</summary>
		/// <param name="oldTheme">The previous theme.</param>
		/// <param name="newTheme">The new theme.</param>
		public void UpdateThemeResources(Theme oldTheme, Theme newTheme)
		{
			if (_themeStyles != null)
			{
				Styles.Remove(_themeStyles);
				_themeStyles = null;
			}

			_themeStyles = newTheme?.CreateStyles();
			if (_themeStyles != null) Styles.Add(_themeStyles);
		}

		/// <inheritdoc/>
		protected override void OnClosing(WindowClosingEventArgs e)
		{
			PersistBounds();
			base.OnClosing(e);
		}

		/// <inheritdoc/>
		protected override void OnClosed(EventArgs e)
		{
			IsClosed = true;
			_model.PropertyChanged -= OnModelPropertyChanged;
			base.OnClosed(e);
		}

		private void ApplyModelBounds()
		{
			Width = _model.FloatingWidth > 0d ? _model.FloatingWidth : DefaultDetachedWidth;
			Height = _model.FloatingHeight > 0d ? _model.FloatingHeight : DefaultDetachedHeight;
			MinWidth = MinimumDetachedSize;
			MinHeight = MinimumDetachedSize;

			if (_model.FloatingLeft != 0d || _model.FloatingTop != 0d)
			{
				WindowStartupLocation = WindowStartupLocation.Manual;
				Position = new PixelPoint((int)_model.FloatingLeft, (int)_model.FloatingTop);
			}
			else
			{
				WindowStartupLocation = WindowStartupLocation.CenterScreen;
			}
		}

		private void PersistBounds()
		{
			if (WindowState != WindowState.Normal || ClientSize.Width <= 0d || ClientSize.Height <= 0d) return;
			_model.FloatingLeft = Position.X;
			_model.FloatingTop = Position.Y;
			_model.FloatingWidth = ClientSize.Width;
			_model.FloatingHeight = ClientSize.Height;
		}

		private void OnModelPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(LayoutAnchorable.Title)) UpdateTitle();
			else if (e.PropertyName == nameof(LayoutAnchorable.IconSource)) UpdateIcon();
		}

		private void UpdateTitle() => Title = _model.Title ?? string.Empty;

		private void UpdateIcon() => Icon = _model.IconSource is Bitmap bitmap ? new WindowIcon(bitmap) : null;
	}
}
