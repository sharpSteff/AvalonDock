using System.Windows.Media;

namespace ToggleTestApp.ViewModels;

/// <summary>
/// Provides file extension icons as DrawingImage for MVVM binding to ImageSource.
/// </summary>
public static class FileIconHelper
{
	public static ImageSource? GetIconForExtension(string ext)
	{
		var color = GetColorForExtension(ext);
		var glyph = GetGlyphForExtension(ext);
		return CreateTextIcon(glyph, color);
	}

	private static ImageSource CreateTextIcon(string text, Color color)
	{
		var formatted = new FormattedText(
			text,
			System.Globalization.CultureInfo.InvariantCulture,
			System.Windows.FlowDirection.LeftToRight,
			new Typeface("Segoe MDL2 Assets"),
			12,
			new SolidColorBrush(color),
			1.0);

		var drawing = new GeometryDrawing(
			new SolidColorBrush(color),
			null,
			formatted.BuildGeometry(new System.Windows.Point(0, 0)));

		var image = new DrawingImage(drawing);
		image.Freeze();
		return image;
	}

	private static string GetGlyphForExtension(string ext)
	{
		return ext.ToLowerInvariant() switch
		{
			".cs" => "\uE943", // Code
			".xaml" or ".xml" => "\uE9D5", // FileExplorer
			".json" => "\uE9D5",
			".csproj" or ".sln" or ".props" => "\uE90F", // Settings
			".md" => "\uE8A5", // Document
			".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".ico" => "\uEB9F", // Photo
			_ => "\uE8A5" // Document
		};
	}

	private static Color GetColorForExtension(string ext)
	{
		return ext.ToLowerInvariant() switch
		{
			".cs" => Color.FromRgb(0x6A, 0x9F, 0x55), // Green for C#
			".xaml" => Color.FromRgb(0x56, 0x9C, 0xD6), // Blue for XAML
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