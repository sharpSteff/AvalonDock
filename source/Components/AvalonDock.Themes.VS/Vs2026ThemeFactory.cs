using System;
using System.IO;
using System.Reflection;
using System.Windows;

namespace AvalonDock.Themes.VS
{
	/// <summary>
	/// Builds the resource dictionary for a bundled VS2026 theme by layering an embedded
	/// VS2026 JSON override file on top of a full VS2022 base palette.
	/// </summary>
	/// <remarks>
	/// VS2026 ships themes as JSON override files that only contain the tokens that differ
	/// from a base theme. Until full VS2026 palette dumps are available, the bundled VS2026
	/// themes reuse the existing VS2022 <c>.vstheme</c> palettes as their base and apply a
	/// small VS2026 override (Fluent accent plus the new independent header/tab tokens).
	/// </remarks>
	internal static class Vs2026ThemeFactory
	{
		public static ResourceDictionary Build(string baseVsThemeResource, string jsonOverrideResource, Uri genericXamlUri)
		{
			VsThemeColorPalette basePalette;
			using (var baseStream = OpenResource(baseVsThemeResource))
			{
				basePalette = VsThemeParser.Parse(baseStream);
			}

			VsThemeColorPalette overrides;
			using (var jsonStream = OpenResource(jsonOverrideResource))
			{
				overrides = VsJsonThemeParser.Parse(jsonStream);
			}

			var merged = basePalette.Merge(overrides);
			return VsThemeResourceBuilder.Build(merged, genericXamlUri);
		}

		private static Stream OpenResource(string name)
		{
			var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
			if (stream == null)
			{
				throw new InvalidOperationException($"Embedded resource '{name}' was not found.");
			}

			return stream;
		}
	}
}
