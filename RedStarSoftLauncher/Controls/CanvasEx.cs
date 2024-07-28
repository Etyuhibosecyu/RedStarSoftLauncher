using Avalonia.Controls;

namespace RedStarSoftLauncher.Controls;

public class CanvasEx : Canvas
{
	public static readonly AttachedProperty<double> HorizontalAnchorProperty =
		AvaloniaProperty.RegisterAttached<CanvasEx, Control, double>("HorizontalAnchor", double.NaN);

	public static readonly AttachedProperty<double> VerticalAnchorProperty =
		AvaloniaProperty.RegisterAttached<CanvasEx, Control, double>("VerticalAnchor", double.NaN);

	static CanvasEx() => AffectsParentArrange<CanvasEx>(LeftProperty, TopProperty, RightProperty, BottomProperty,
			HorizontalAnchorProperty, VerticalAnchorProperty);

	public static double GetHorizontalAnchor(AvaloniaObject element) => element.GetValue(HorizontalAnchorProperty);

	public static void SetHorizontalAnchor(AvaloniaObject element, double value) => element.SetValue(HorizontalAnchorProperty, value);

	public static double GetVerticalAnchor(AvaloniaObject element) => element.GetValue(VerticalAnchorProperty);

	public static void SetVerticalAnchor(AvaloniaObject element, double value) => element.SetValue(VerticalAnchorProperty, value);

	protected override void ArrangeChild(Control child, Size finalSize)
	{
		var horizontalAnchor = GetHorizontalAnchor(child);
		var verticalAnchor = GetVerticalAnchor(child);
		var elementRight = GetRight(child);
		double x;
		if (!double.IsNaN(elementRight))
		{
			if (double.IsNaN(horizontalAnchor))
				horizontalAnchor = 1.0;
			x = finalSize.Width - (DesiredSize.Width - child.DesiredSize.Width) * horizontalAnchor - elementRight;
		}
		else
		{
			var elementLeft = GetLeft(child);
			if (double.IsNaN(elementLeft))
				elementLeft = 0;
			if (double.IsNaN(horizontalAnchor))
				horizontalAnchor = 0.0;
			x = elementLeft + (DesiredSize.Width - child.DesiredSize.Width) * horizontalAnchor;
		}
		var elementBottom = GetBottom(child);
		double y;
		if (!double.IsNaN(elementBottom))
		{
			if (double.IsNaN(verticalAnchor))
				verticalAnchor = 1.0;
			y = finalSize.Height - (DesiredSize.Height - child.DesiredSize.Height) * verticalAnchor - elementBottom;
		}
		else
		{
			var elementTop = GetTop(child);
			if (double.IsNaN(elementTop))
				elementTop = 0;
			if (double.IsNaN(verticalAnchor))
				verticalAnchor = 0.0;
			y = elementTop + (DesiredSize.Height - child.DesiredSize.Height) * verticalAnchor;
		}
		child.Arrange(new Rect(new Point(x, y), child.DesiredSize));
	}
}