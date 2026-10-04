using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace ToggleTestApp.Views;

/// <summary>
/// Converts IsDirectory to an icon geometry (folder or page). The WPF application uses glyphs of the
/// Segoe MDL2 Assets font, which only exists on Windows.
/// </summary>
public class FileIconConverter : IValueConverter
{
	private static readonly Geometry FolderGeometry = Geometry.Parse("M 0,2 L 4,2 L 5.5,3.5 L 12,3.5 L 12,11 L 0,11 Z");
	private static readonly Geometry PageGeometry = Geometry.Parse("M 1.5,0.5 L 7.5,0.5 L 10.5,3.5 L 10.5,11.5 L 1.5,11.5 Z M 7.5,0.5 L 7.5,3.5 L 10.5,3.5");

	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		return value is true ? FolderGeometry : PageGeometry;
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> throw new NotSupportedException();
}

/// <summary>
/// Converts IsDirectory to icon color (gold for folders, gray for files).
/// </summary>
public class FileColorConverter : IValueConverter
{
	private static readonly SolidColorBrush FolderBrush = new(Color.FromRgb(0xDC, 0xB6, 0x7A));
	private static readonly SolidColorBrush FileBrush = new(Color.FromRgb(0xCC, 0xCC, 0xCC));

	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		return value is true ? FolderBrush : FileBrush;
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> throw new NotSupportedException();
}

/// <summary>
/// Converts a <see cref="ToggleTestApp.ViewModels.FileChangeKind"/> to the colour of its label, as the
/// DataTriggers of the WPF SourceControlView do.
/// </summary>
public class ChangeKindBrushConverter : IValueConverter
{
	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		return value?.ToString() switch
		{
			"Modified" => new SolidColorBrush(Color.Parse("#E2C08D")),
			"Added" => new SolidColorBrush(Color.Parse("#73C991")),
			"Deleted" => new SolidColorBrush(Color.Parse("#C74E39")),
			"Untracked" => new SolidColorBrush(Color.Parse("#73C991")),
			_ => null,
		};
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> throw new NotSupportedException();
}
