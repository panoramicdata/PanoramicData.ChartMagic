namespace PanoramicData.ChartMagic.Renderers;

/// <summary>
/// Makes room for the axis labels and titles before the axes are drawn.
/// </summary>
/// <remarks>
/// MS-26633. A margin too small for its labels and title grows, moving the plot's edge in, but never
/// past half of the chart area; beyond that a label is shortened with an ellipsis. A chart whose
/// labels already fit is left exactly as it was.
/// </remarks>
internal sealed class AxisLayout
{
	/// <summary>The most of the chart area an axis strip may grow to take.</summary>
	private const double MaximumStripFraction = 0.5;

	private const double EdgePaddingPixels = 2;

	private AxisLayout(int xLabelAngle, IReadOnlyList<string?> xLabels, IReadOnlyList<string?> yLabels)
	{
		XLabelAngle = xLabelAngle;
		XLabels = xLabels;
		YLabels = yLabels;
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
	internal static AxisLayout Apply(Chart chart, AxisHandlerResult axisHandlerResult, int widthPixels, int heightPixels)
	{
		var canvas = new Canvas(chart, axisHandlerResult, widthPixels, heightPixels);
		var yLabels = FitYAxis(chart, canvas);
		var (xLabelAngle, xLabels) = FitXAxis(chart, canvas);
		return new(xLabelAngle, xLabels, yLabels);
	}

	private static List<string?> FitYAxis(Chart chart, Canvas canvas)
	{
		var yAxis = chart.ChartArea.YAxis;
		var innerPlot = chart.ChartArea.InnerPlot;
		var geometry = canvas.Geometry();
		var labels = AxisRenderer.YAxisTickValues(chart, geometry)
			.Select(value => (string?)AxisRenderer.YAxisLabel(chart, geometry, value))
			.ToList();

		var aligned = Math.Abs(yAxis.XPositionPercent + yAxis.WidthPercent - innerPlot.XPositionPercent) < 0.01;
		if (!yAxis.IsEnabled || !yAxis.LabelsEnabled || yAxis.LabelAngle != 0 || labels.Count == 0 || !aligned)
		{
			return labels;
		}

		var stripWidth = canvas.Width * yAxis.GetCanvasWidthPercent() / 100;
		var labelsReach = yAxis.TickLengthPixels + AxisRenderer.TickLabelGapPixels + Widest(labels, yAxis);
		var titleRoom = yAxis.Title is { Length: > 0 } ? yAxis.FontSize + AxisRenderer.YAxisTitleClearancePixels : EdgePaddingPixels;
		var titleDropped = yAxis.Title is { Length: > 0 }
			&& stripWidth - labelsReach - AxisRenderer.YAxisTitleClearancePixels < AxisRenderer.MinimumTitleFontSize;
		if (labelsReach + EdgePaddingPixels <= stripWidth && !titleDropped)
		{
			return labels;
		}

		var chartAreaWidth = canvas.Width * chart.ChartArea.GetCanvasWidthPercent() / 100;
		var target = Math.Min(labelsReach + titleRoom, Math.Max(stripWidth, chartAreaWidth * MaximumStripFraction));
		var growth = (target - stripWidth) / chartAreaWidth * 100;
		if (growth > 0)
		{
			innerPlot.XPositionPercent += growth;
			innerPlot.WidthPercent -= growth;
			yAxis.WidthPercent += growth;
		}

		if (labelsReach + titleRoom <= target)
		{
			return labels;
		}

		// Capped: the labels are shortened, keeping room for the title only while that leaves them
		// a useful length.
		var labelRoom = target - yAxis.TickLengthPixels - AxisRenderer.TickLabelGapPixels;
		var maximumWidth = labelRoom - titleRoom >= 3 * yAxis.FontSize ? labelRoom - titleRoom : labelRoom - EdgePaddingPixels;
		return [.. labels.Select(label => label is null ? null : TextMeasure.Fit(label, maximumWidth, yAxis.FontSize, yAxis.FontWeight))];
	}

	private static (int Angle, List<string?> Labels) FitXAxis(Chart chart, Canvas canvas)
	{
		var geometry = canvas.Geometry();
		return (
			chart.ChartArea.XAxis.LabelAngle,
			[.. AxisRenderer.XAxisTickValues(chart, geometry).Select(value => (string?)AxisRenderer.XAxisLabel(chart, geometry, value))]);
	}

	private static double Widest(IEnumerable<string?> labels, AxisArea axis)
		=> labels.Max(label => label is null ? 0 : TextMeasure.Width(label, axis.FontSize, axis.FontWeight));

	/// <summary>The image size, and the plot as the chart's current margins leave it.</summary>
	private sealed class Canvas(Chart chart, AxisHandlerResult axisHandlerResult, int width, int height)
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
