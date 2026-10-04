using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Shapes;
using Avalonia.Media;

namespace ToggleTestApp.ViewModels;

/// <summary>
/// Avalonia version of the WPF application's sidebar toggle button icons - the same shapes and geometry.
/// Each icon draws with the inherited TextElement.Foreground, so it follows the active/inactive colour of the
/// sidebar button it sits in.
/// </summary>
public static class ToolboxIcons
{
	public static object Explorer => CreateExplorerIcon();
	public static object Terminal => CreateTerminalIcon();
	public static object Search => CreateSearchIcon();
	public static object Git => CreateGitIcon();
	public static object Problems => CreateProblemsIcon();

	/// <summary>Binds <paramref name="property"/> of <paramref name="shape"/> to the foreground it inherits.</summary>
	private static T WithForeground<T>(T shape, AvaloniaProperty<IBrush?> property)
		where T : Shape
	{
		shape.Bind(property, shape.GetObservable(TextElement.ForegroundProperty));
		return shape;
	}

	private static Line CreateLine(double x1, double y1, double x2, double y2, double thickness)
		=> WithForeground(new Line { StartPoint = new Point(x1, y1), EndPoint = new Point(x2, y2), StrokeThickness = thickness }, Shape.StrokeProperty);

	private static T At<T>(T control, double left, double top)
		where T : Control
	{
		Canvas.SetLeft(control, left);
		Canvas.SetTop(control, top);
		return control;
	}

	private static Viewbox Icon(params Control[] children)
	{
		var canvas = new Canvas { Width = 16, Height = 16 };
		canvas.Children.AddRange(children);
		return new Viewbox { Width = 16, Height = 16, Child = canvas };
	}

	private static Viewbox CreateExplorerIcon() => Icon(
		WithForeground(
			new Path
			{
				Data = Geometry.Parse(
					"M1.5,1 L6,1 L7.5,3 L14.5,3 C15.3,3 15.5,3.5 15.5,4 L15.5,13 C15.5,13.5 15,14 14.5,14 L1.5,14 C1,14 0.5,13.5 0.5,13 L0.5,2 C0.5,1.5 1,1 1.5,1 Z"),
				StrokeThickness = 0.8,
				Fill = Brushes.Transparent,
			},
			Shape.StrokeProperty),
		CreateLine(0.5, 5.5, 15.5, 5.5, 0.6));

	private static Viewbox CreateTerminalIcon() => Icon(
		At(WithForeground(new Rectangle { Width = 15, Height = 13, RadiusX = 1.5, RadiusY = 1.5, StrokeThickness = 0.8, Fill = Brushes.Transparent }, Shape.StrokeProperty), 0.5, 1.5),
		WithForeground(new Path { Data = Geometry.Parse("M4,6 L7,8.5 L4,11"), StrokeThickness = 1.5, Fill = Brushes.Transparent }, Shape.StrokeProperty),
		At(WithForeground(new Rectangle { Width = 4, Height = 1.2 }, Shape.FillProperty), 8.5, 10.5));

	private static Viewbox CreateSearchIcon() => Icon(
		At(WithForeground(new Ellipse { Width = 9, Height = 9, StrokeThickness = 1.2, Fill = Brushes.Transparent }, Shape.StrokeProperty), 2, 2),
		CreateLine(10, 10, 14, 14, 1.5));

	private static Viewbox CreateGitIcon() => Icon(
		At(WithForeground(new Ellipse { Width = 3.5, Height = 3.5, StrokeThickness = 1, Fill = Brushes.Transparent }, Shape.StrokeProperty), 2.5, 2),
		At(WithForeground(new Ellipse { Width = 3.5, Height = 3.5, StrokeThickness = 1, Fill = Brushes.Transparent }, Shape.StrokeProperty), 9, 2),
		At(WithForeground(new Ellipse { Width = 3.5, Height = 3.5, StrokeThickness = 1, Fill = Brushes.Transparent }, Shape.StrokeProperty), 2.5, 10.5),
		CreateLine(4.25, 5.5, 4.25, 10.5, 1),
		WithForeground(new Path { Data = Geometry.Parse("M10.75,5.5 C10.75,8.5 4.25,8.5 4.25,10.5"), StrokeThickness = 1, Fill = Brushes.Transparent }, Shape.StrokeProperty));

	private static Viewbox CreateProblemsIcon() => Icon(
		WithForeground(new Path { Data = Geometry.Parse("M8,1.5 L15,13.5 L1,13.5 Z"), StrokeThickness = 1, Fill = Brushes.Transparent }, Shape.StrokeProperty),
		CreateLine(8, 6, 8, 10, 1.2),
		At(WithForeground(new Ellipse { Width = 1.6, Height = 1.6 }, Shape.FillProperty), 7.2, 11));
}
