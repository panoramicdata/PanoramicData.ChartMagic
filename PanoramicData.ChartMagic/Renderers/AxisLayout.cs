namespace PanoramicData.ChartMagic.Renderers;

/// <summary>
/// Makes room for the axis labels and titles before the axes are drawn.
/// </summary>
/// <remarks>
/// MS-27510, MS-26633. X labels that would overlap are turned to -45 degrees, and thinned to every
/// Nth when even that is too close. A margin too small for its labels and title grows, moving the
/// plot's edge in, but never past half of the chart area or half of the plot; beyond that a label is
/// shortened with an ellipsis, though a number never is. A chart whose labels already fit is left
/// exactly as it was.
///
/// The margins are moved on the chart itself for the length of one render, and put back by
/// <see cref="Restore"/>, so rendering a chart again lays it out from the same starting point.
/// </remarks>
internal sealed class AxisLayout
{
	internal const int RotatedLabelAngle = -45;

	private readonly Margins _original;

	internal AxisLayout(int xLabelAngle, IReadOnlyList<string?> xLabels, IReadOnlyList<string?> yLabels, Margins original)
	{
		XLabelAngle = xLabelAngle;
		XLabels = xLabels;
		YLabels = yLabels;
		_original = original;
	}

	/// <summary>The angle the X labels are drawn at.</summary>
	internal int XLabelAngle { get; }

	/// <summary>The X label for each X tick, in tick order; null where the tick is left unlabelled.</summary>
	internal IReadOnlyList<string?> XLabels { get; }

	/// <summary>The Y label for each Y tick, in tick order; null where the tick is left unlabelled.</summary>
	internal IReadOnlyList<string?> YLabels { get; }

	/// <summary>
	/// Settles the labels, growing the chart's margins where they need it.
	/// </summary>
	/// <remarks>
	/// The Y margin first, since its width moves the X labels; then the X margin, whose height
	/// spaces the Y labels.
	/// </remarks>
	internal static AxisLayout Apply(Chart chart, AxisHandlerResult axisHandlerResult, int widthPixels, int heightPixels)
	{
		var original = Margins.Of(chart);
		var canvas = new Canvas(chart, axisHandlerResult, widthPixels, heightPixels);
		var yLabels = YAxisFit.Settle(chart, canvas);
		var (xLabelAngle, xLabels) = XAxisFit.Settle(chart, canvas);
		return new(xLabelAngle, xLabels, YAxisFit.ThinToFit(chart, canvas, yLabels), original);
	}

	/// <summary>Puts the chart's margins back as they were before <see cref="Apply"/>.</summary>
	internal void Restore(Chart chart) => _original.ApplyTo(chart);

	/// <summary>The margins a layout may move.</summary>
	internal sealed record Margins(double PlotX, double PlotY, double PlotWidth, double PlotHeight, double YAxisWidth, double XAxisHeight)
	{
		internal static Margins Of(Chart chart)
		{
			var area = chart.ChartArea;
			return new(
				area.InnerPlot.XPositionPercent,
				area.InnerPlot.YPositionPercent,
				area.InnerPlot.WidthPercent,
				area.InnerPlot.HeightPercent,
				area.YAxis.WidthPercent,
				area.XAxis.HeightPercent);
		}

		internal void ApplyTo(Chart chart)
		{
			var area = chart.ChartArea;
			area.InnerPlot.XPositionPercent = PlotX;
			area.InnerPlot.YPositionPercent = PlotY;
			area.InnerPlot.WidthPercent = PlotWidth;
			area.InnerPlot.HeightPercent = PlotHeight;
			area.YAxis.WidthPercent = YAxisWidth;
			area.XAxis.HeightPercent = XAxisHeight;
		}
	}

	/// <summary>The image size, and the plot as the chart's current margins leave it.</summary>
	internal sealed class Canvas(Chart chart, AxisHandlerResult axisHandlerResult, int width, int height)
	{
		internal int Width { get; } = width;

		internal int Height { get; } = height;

		internal PlotGeometry Geometry() => new(
			chart,
			axisHandlerResult,
			Width * chart.ChartArea.InnerPlot.GetCanvasWidthPercent() / 100,
			Height * chart.ChartArea.InnerPlot.GetCanvasHeightPercent() / 100);
	}
}
