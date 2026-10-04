using Avalonia;
using Avalonia.Media;

namespace ToggleTestApp.ViewModels;

/// <summary>
/// Avalonia version of the WPF application's file extension icons. The WPF version draws a Segoe MDL2 glyph,
/// a font that only Windows has, so this one draws a page whose colour stands for the file type - the same
/// colours as the WPF version.
/// </summary>
public static class FileIconHelper
{
	private static readonly Geometry PageGeometry = Geometry.Parse("M2,0 L8.5,0 L12,3.5 L12,14 L2,14 Z M8.5,0 L8.5,3.5 L12,3.5");

	/// <summary>Gets the icon for a file extension.</summary>
	/// <param name="ext">The extension, with the dot.</param>
	/// <returns>The icon.</returns>
	public static IImage? GetIconForExtension(string ext)
	{
		var brush = new SolidColorBrush(GetColorForExtension(ext));
		return new DrawingImage(new GeometryDrawing
		{
			Geometry = PageGeometry,
			Brush = new SolidColorBrush(GetColorForExtension(ext), 0.35),
			Pen = new Pen(brush, 1.2),
		});
	}

	private static Color GetColorForExtension(string ext)
	{
		return ext.ToLowerInvariant() switch
		{
			".cs" => Color.FromRgb(0x6A, 0x9F, 0x55), // Green for C#
			".xaml" or ".axaml" => Color.FromRgb(0x56, 0x9C, 0xD6), // Blue for XAML
			".xml" or ".config" => Color.FromRgb(0xD4, 0x9C, 0x56), // Orange for XML
			".json" => Color.FromRgb(0xCE, 0x91, 0x78), // Salmon for JSON
			".csproj" or ".sln" or ".props" => Color.FromRgb(0xB8, 0x86, 0xDB), // Purple for project
			".md" => Color.FromRgb(0x56, 0x9C, 0xD6), // Blue for Markdown
			".js" => Color.FromRgb(0xDC, 0xDC, 0x8B), // Yellow for JS
			".css" => Color.FromRgb(0x56, 0x9C, 0xD6), // Blue for CSS
			".html" or ".htm" => Color.FromRgb(0xD4, 0x6B, 0x56), // Red-ish for HTML
			_ => Color.FromRgb(0xCC, 0xCC, 0xCC) // Gray default
		};
	}
}