namespace PanoramicData.ChartMagic.Renderers;

internal static class AxisLabelGeometry
{
	internal const double EdgePaddingPixels = 2;

	/// <summary>
	/// Rounding headroom when fitting a label to a width worked out from that label: Linux measures
	/// whole pixels, so the room left for the widest label is its own width less rounding error.
	/// </summary>
	internal const double FitTolerancePixels = 0.01;

	/// <summary>The sine and cosine of 45 degrees.</summary>
	internal static readonly double Diagonal = Math.Sqrt(0.5);

	/// <summary>Below this a label is upright, and its length no longer limits how far left it reaches.</summary>
	internal const double MinimumCosine = 0.05;

	internal static string? Fit(string? label, double maximumWidth, AxisArea axis)
		=> label is null ? null : TextMeasure.Fit(label, maximumWidth + FitTolerancePixels, axis.FontSize, axis.FontWeight);

	/// <summary>How far below the top of the labels the deepest one reaches.</summary>
	internal static double LabelsDepth(List<string?> labels, List<double> widths, double fontSize, int angle)
		=> angle != 0
			? labels.Select((label, i) => label is null ? 0 : RotatedDepth(widths[i], fontSize, angle)).Max()
			: fontSize;

	/// <summary>The longest a turned label can be and stay right of the image edge.</summary>
	internal static double WidthLeftOfTick(double roomLeft, double cosine, double sine, double fontSize)
		=> cosine < MinimumCosine ? double.MaxValue : (roomLeft - (0.8 * fontSize * sine)) / cosine;

	/// <summary>How far below its top a label drawn at -45 degrees reaches.</summary>
	internal static double RotatedDepth(double width, double fontSize, int angle)
		=> (fontSize * (0.8 + (0.2 * Sine(angle)))) + (width * Sine(angle));

	/// <summary>The sine of a label's angle, taken as positive; exactly the diagonal at 45 degrees.</summary>
	internal static double Sine(int angle) => Math.Abs(angle) == 45 ? Diagonal : Math.Abs(Math.Sin(angle * Math.PI / 180));

	/// <summary>How far left of its anchor a label of this width reaches when drawn at an angle, centred on its line.</summary>
	internal static double HorizontalReach(double width, int angle, double fontSize)
		=> (width * Math.Abs(Math.Cos(angle * Math.PI / 180))) + (0.5 * fontSize * Sine(angle));

	/// <summary>The longest label whose <see cref="HorizontalReach"/> is within a room.</summary>
	internal static double WidthWithinReach(double room, int angle, double fontSize)
	{
		var cosine = Math.Abs(Math.Cos(angle * Math.PI / 180));
		return cosine < MinimumCosine ? double.MaxValue : (room - (0.5 * fontSize * Sine(angle))) / cosine;
	}

	internal static double Widest(IEnumerable<string?> labels, AxisArea axis)
		=> labels.Max(label => label is null ? 0 : TextMeasure.Width(label, axis.FontSize, axis.FontWeight));
}
