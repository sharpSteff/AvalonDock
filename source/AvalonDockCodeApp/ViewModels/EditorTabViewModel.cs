using System;
using System.IO;
#if AVALONIA
using ImageSource = Avalonia.Media.IImage;
#else
using System.Windows.Media;
#endif
using AvalonDock.Mvvm.CommunityToolkit;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ToggleTestApp.ViewModels;

public partial class EditorTabViewModel : ObservableDocument
{
	[ObservableProperty] private string _toolTip = string.Empty;

	[ObservableProperty] private string _filePath = string.Empty;

	[ObservableProperty] private string _content = string.Empty;

	[ObservableProperty] private string _syntaxHighlighting = "Text";

	[ObservableProperty] private ImageSource? _iconSource;

	public string ContentId => Id;

	public void LoadFile(string path)
	{
		FilePath = path;
		Title = Path.GetFileName(path);
		Id = path;
		ToolTip = path;
		IconSource = FileIconHelper.GetIconForExtension(Path.GetExtension(path));
		SyntaxHighlighting = GetHighlightingForExtension(Path.GetExtension(path));

		try
		{
			if (IsBinaryFile(path))
			{
				Content = $"[Binary file — {new FileInfo(path).Length:N0} bytes]";
			}
			else
			{
				Content = File.ReadAllText(path);
			}

			IsModified = false;
		}
		catch (Exception ex)
		{
			Content = $"Error loading file: {ex.Message}";
		}
	}

	private static bool IsBinaryFile(string path)
	{
		try
		{
			var buffer = new byte[8192];
			using var stream = File.OpenRead(path);
			var bytesRead = stream.Read(buffer, 0, buffer.Length);
			for (int i = 0; i < bytesRead; i++)
			{
				if (buffer[i] == 0) return true;
			}

			return false;
		}
		catch
		{
			return false;
		}
	}

	private static string GetHighlightingForExtension(string ext)
	{
		return ext.ToLowerInvariant() switch
		{
			".cs" => "C#",
			".xml" or ".xaml" or ".csproj" or ".sln" or ".config" or ".props" => "XML",
			".js" => "JavaScript",
			".html" or ".htm" => "HTML",
			".css" => "CSS",
			".json" => "JSON",
			".py" => "Python",
			".md" => "MarkDown",
			".sql" => "TSQL",
			".cpp" or ".c" or ".h" => "C++",
			_ => "Text"
		};
	}
}