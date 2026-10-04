using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;

namespace AvalonDock.Themes
{
	/// <summary>
	/// Provides a base class for the themes of the docking controls.
	/// </summary>
	/// <remarks>
	/// A theme is a <see cref="Styles"/> file (an <c>.axaml</c> with a <c>Styles</c> root, usually holding the
	/// control themes in its resources) that the <see cref="DockingManager"/> adds to its own styles and to the
	/// styles of every floating window - top-level windows do not inherit styles from the manager.
	/// </remarks>
	public abstract class Theme : AvaloniaObject, Core.IThemeInfo
	{
		/// <summary>Gets the name of the theme.</summary>
		public virtual string Name => GetType().Name.Replace("Theme", string.Empty);

		/// <inheritdoc/>
		Uri Core.IThemeInfo.ResourceUri => GetResourceUri();

		/// <summary>Gets the <c>avares://</c> URI of the styles file of the theme.</summary>
		/// <returns>The URI, or <see langword="null"/> for a theme that builds its styles in code.</returns>
		public abstract Uri GetResourceUri();

		/// <summary>Creates the styles that apply the theme.</summary>
		/// <returns>The styles, or <see langword="null"/>.</returns>
		public virtual IStyle CreateStyles()
		{
			var uri = GetResourceUri();
			return uri == null ? null : new StyleInclude(uri) { Source = uri };
		}
	}

	/// <summary>The default theme of the docking controls.</summary>
	public class GenericTheme : Theme
	{
		/// <summary>The URI of the default theme.</summary>
		public static readonly Uri ResourceUri = new Uri("avares://AvalonDock.Avalonia/Themes/Generic.axaml");

		/// <inheritdoc/>
		public override Uri GetResourceUri() => ResourceUri;
	}

	/// <summary>A theme given as a resource dictionary, typically one that only overrides brushes of the default theme.</summary>
	public abstract class DictionaryTheme : Theme
	{
		/// <summary>Initializes a new instance of the <see cref="DictionaryTheme"/> class.</summary>
		protected DictionaryTheme()
		{
		}

		/// <summary>Initializes a new instance of the <see cref="DictionaryTheme"/> class.</summary>
		/// <param name="themeResourceDictionary">The resources of the theme.</param>
		protected DictionaryTheme(IResourceDictionary themeResourceDictionary)
		{
			ThemeResourceDictionary = themeResourceDictionary;
		}

		/// <summary>Gets the resources of the theme.</summary>
		public IResourceDictionary ThemeResourceDictionary { get; private set; }

		/// <inheritdoc/>
		public override Uri GetResourceUri() => null;

		/// <inheritdoc/>
		public override IStyle CreateStyles()
		{
			if (ThemeResourceDictionary == null) return null;
			var styles = new Styles();
			styles.Resources.MergedDictionaries.Add(ThemeResourceDictionary);
			return styles;
		}
	}
}
