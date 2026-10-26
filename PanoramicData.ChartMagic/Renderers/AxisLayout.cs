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

	/// <summary>The most of the chart area an axis strip may grow to take.</summary>
	private const double MaximumStripFraction = 0.5;

	/// <summary>The space kept between neighbouring labels, and between a label and a title.</summary>
	private const double LabelSpacingPixels = 4;

	private const double EdgePaddingPixels = 2;

	/// <summary>
	/// Rounding headroom when fitting a label to a width worked out from that label: Linux measures
	/// whole pixels, so the room left for the widest label is its own width less rounding error.
	/// </summary>
	private const double FitTolerancePixels = 0.01;

	/// <summary>The sine and cosine of 45 degrees.</summary>
	private static readonly double Diagonal = Math.Sqrt(0.5);

	/// <summary>The most of the plot's own width or height a margin may grow into.</summary>
	private const double MaximumPlotShare = 0.5;

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
		var yLabels = FitYAxis(chart, canvas);
		var (xLabelAngle, xLabels) = FitXAxis(chart, canvas);
		return new(xLabelAngle, xLabels, ThinYAxis(chart, canvas, yLabels), original);
	}

	/// <summary>Puts the chart's margins back as they were before <see cref="Apply"/>.</summary>
	internal void Restore(Chart chart) => _original.ApplyTo(chart);

	private static List<string?> FitYAxis(Chart chart, Canvas canvas)
	{
		var yAxis = chart.ChartArea.YAxis;
		var innerPlot = chart.ChartArea.InnerPlot;
		var geometry = canvas.Geometry();
		var labels = AxisRenderer.YAxisTickValues(chart, geometry)
			.Select(value => (string?)AxisRenderer.YAxisLabel(chart, geometry, value))
			.ToList();

		if (!IsFittable(yAxis, labels.Count, yAxis.XPositionPercent + yAxis.WidthPercent, innerPlot.XPositionPercent))
		{
			return labels;
		}

		var stripWidth = canvas.Width * yAxis.GetCanvasWidthPercent() / 100;
		var labelsReach = yAxis.TickLengthPixels + AxisRenderer.TickLabelGapPixels + Widest(labels, yAxis);
		if (!YAxisNeedsRoom(yAxis, stripWidth, labelsReach))
		{
			return labels;
		}

		var titleRoom = TitleRoom(yAxis, yAxis.FontSize + AxisRenderer.YAxisTitleClearancePixels);
		var chartAreaWidth = canvas.Width * chart.ChartArea.GetCanvasWidthPercent() / 100;
		var target = Target(stripWidth, labelsReach + titleRoom, chartAreaWidth, canvas.Width * innerPlot.GetCanvasWidthPercent() / 100);
		GrowLeftMargin(chart, (target - stripWidth) / chartAreaWidth * 100);

		return labelsReach + titleRoom <= target || !geometry.IsHorizontalPlot
			? labels
			: ShortenYLabels(yAxis, labels, target, titleRoom);
	}

	/// <summary>Whether an axis's labels are this layout's to fit: drawn, level, and in a strip that meets the plot.</summary>
	private static bool IsFittable(AxisArea axis, int labelCount, double stripEdgePercent, double plotEdgePercent)
		=> axis.IsEnabled
			&& axis.LabelsEnabled
			&& axis.LabelAngle == 0
			&& labelCount > 0
			&& Math.Abs(stripEdgePercent - plotEdgePercent) < 0.01;

	/// <summary>Whether a Y label is cut off, or the title would be too small to read.</summary>
	private static bool YAxisNeedsRoom(AxisArea yAxis, double stripWidth, double labelsReach)
		=> labelsReach + EdgePaddingPixels > stripWidth
			|| (HasTitle(yAxis) && stripWidth - labelsReach - AxisRenderer.YAxisTitleClearancePixels < AxisRenderer.MinimumTitleFontSize);

	private static bool HasTitle(AxisArea axis) => axis.Title is { Length: > 0 };

	private static double TitleRoom(AxisArea axis, double roomWithTitle) => HasTitle(axis) ? roomWithTitle : EdgePaddingPixels;

	/// <summary>
	/// The size a strip grows to for what it needs, never past half of the chart area or half of the
	/// plot, and never smaller than it is.
	/// </summary>
	private static double Target(double strip, double needed, double chartArea, double plot)
		=> Math.Min(needed, Math.Max(strip, Math.Min(chartArea * MaximumStripFraction, strip + (plot * MaximumPlotShare))));

	private static void GrowLeftMargin(Chart chart, double growthPercent)
	{
		if (growthPercent <= 0)
		{
			return;
		}

		chart.ChartArea.InnerPlot.XPositionPercent += growthPercent;
		chart.ChartArea.InnerPlot.WidthPercent -= growthPercent;
		chart.ChartArea.YAxis.WidthPercent += growthPercent;
	}

	/// <summary>
	/// Category labels shortened to the capped strip, keeping room for the title only while that
	/// leaves them a useful length.
	/// </summary>
	private static List<string?> ShortenYLabels(AxisArea yAxis, List<string?> labels, double stripWidth, double titleRoom)
	{
		var labelRoom = stripWidth - yAxis.TickLengthPixels - AxisRenderer.TickLabelGapPixels;
		var maximumWidth = labelRoom - titleRoom >= 3 * yAxis.FontSize ? labelRoom - titleRoom : labelRoom - EdgePaddingPixels;
		return [.. labels.Select(label => Fit(label, maximumWidth, yAxis))];
	}

	private static string? Fit(string? label, double maximumWidth, AxisArea axis)
		=> label is null ? null : TextMeasure.Fit(label, maximumWidth + FitTolerancePixels, axis.FontSize, axis.FontWeight);

	private static (int Angle, List<string?> Labels) FitXAxis(Chart chart, Canvas canvas)
	{
		var xAxis = chart.ChartArea.XAxis;
		var innerPlot = chart.ChartArea.InnerPlot;
		var geometry = canvas.Geometry();
		var values = AxisRenderer.XAxisTickValues(chart, geometry);
		var labels = values.Select(value => (string?)AxisRenderer.XAxisLabel(chart, geometry, value)).ToList();

		var aligned = Math.Abs(xAxis.YPositionPercent + xAxis.HeightPercent - innerPlot.YPositionPercent) < 0.01;
		if (!xAxis.IsEnabled || !xAxis.LabelsEnabled || xAxis.LabelAngle != 0 || labels.Count == 0 || !aligned)
		{
			return (xAxis.LabelAngle, labels);
		}

		var fontSize = xAxis.FontSize;
		var positions = values.Select(value => AxisRenderer.XAxisPixels(geometry, value)).ToList();
		var widths = labels.Select(label => TextMeasure.Width(label!, fontSize, xAxis.FontWeight)).ToList();
		var overlaps = Enumerable.Range(1, labels.Count - 1)
			.Any(i => Math.Abs(positions[i] - positions[i - 1]) < ((widths[i] + widths[i - 1]) / 2) + LabelSpacingPixels);
		var angle = overlaps ? RotatedLabelAngle : 0;

		if (overlaps)
		{
			Thin(labels, positions, fontSize + 1, Diagonal);
		}

		var stripHeight = canvas.Height * xAxis.GetCanvasHeightPercent() / 100;
		var labelTop = xAxis.TickLengthPixels + AxisRenderer.TickLabelGapPixels;
		var titleRoom = xAxis.Title is { Length: > 0 } ? fontSize + LabelSpacingPixels : EdgePaddingPixels;
		var labelsDepth = overlaps
			? labels.Select((label, i) => label is null ? 0 : RotatedDepth(widths[i], fontSize)).Max()
			: fontSize;
		var needed = labelTop + labelsDepth + titleRoom;
		var target = stripHeight;
		if (needed > stripHeight)
		{
			var chartAreaHeight = canvas.Height * chart.ChartArea.GetCanvasHeightPercent() / 100;
			var plotHeight = canvas.Height * innerPlot.GetCanvasHeightPercent() / 100;
			var limit = Math.Min(chartAreaHeight * MaximumStripFraction, stripHeight + (plotHeight * MaximumPlotShare));
			target = Math.Min(needed, Math.Max(stripHeight, limit));
			var growth = (target - stripHeight) / chartAreaHeight * 100;
			if (growth > 0)
			{
				innerPlot.YPositionPercent += growth;
				innerPlot.HeightPercent -= growth;
				xAxis.HeightPercent += growth;
			}
		}

		// Numbers are never shortened: "2000000..." reads as another number.
		if (!overlaps || geometry.IsHorizontalPlot || !geometry.IsCategorical)
		{
			return (angle, labels);
		}

		// A rotated label hangs down and to the left of its tick, so it is limited by the depth of
		// the strip and by the space left of its tick.
		var plotLeft = canvas.Width * innerPlot.GetCanvasXLocationPercent() / 100;
		var deepest = (target - labelTop - titleRoom - (fontSize * (0.8 + (0.2 * Diagonal)))) / Diagonal;
		for (var i = 0; i < labels.Count; i++)
		{
			if (labels[i] is { } label)
			{
				var widest = Math.Min(deepest, ((plotLeft + positions[i] - EdgePaddingPixels) / Diagonal) - (0.8 * fontSize));
				labels[i] = TextMeasure.Fit(label, widest + FitTolerancePixels, fontSize, xAxis.FontWeight);
			}
		}

		return (angle, labels);
	}

	/// <summary>
	/// Every Nth Y label, where the labels are closer together than a line of text.
	/// </summary>
	private static List<string?> ThinYAxis(Chart chart, Canvas canvas, List<string?> labels)
	{
		var yAxis = chart.ChartArea.YAxis;
		if (!yAxis.IsEnabled || !yAxis.LabelsEnabled || yAxis.LabelAngle != 0 || labels.Count < 2)
		{
			return labels;
		}

		var geometry = canvas.Geometry();
		var positions = AxisRenderer.YAxisTickValues(chart, geometry)
			.Select(value => geometry.IsHorizontalPlot ? geometry.CategoryToPixels(value) : geometry.YToPixels(value))
			.ToList();
		Thin(labels, positions, yAxis.FontSize + 1, 1);
		return labels;
	}

	/// <summary>
	/// Blanks all but every Nth label, N being the smallest step that puts a line of text between
	/// neighbouring labels.
	/// </summary>
	/// <param name="labels">The labels, thinned in place.</param>
	/// <param name="positions">Where each label sits along its axis.</param>
	/// <param name="lineHeight">The spacing a label needs from the next.</param>
	/// <param name="spacingFactor">The share of the spacing along the axis that is across the labels.</param>
	private static void Thin(List<string?> labels, List<double> positions, double lineHeight, double spacingFactor)
	{
		if (positions.Count < 2 || positions.Count != labels.Count)
		{
			return;
		}

		var spacing = Enumerable.Range(1, positions.Count - 1).Min(i => Math.Abs(positions[i] - positions[i - 1])) * spacingFactor;
		if (spacing >= lineHeight)
		{
			return;
		}

		var step = spacing <= 0 ? labels.Count : (int)Math.Ceiling(lineHeight / spacing);
		for (var i = 0; i < labels.Count; i++)
		{
			if (i % step != 0)
			{
				labels[i] = null;
			}
		}
	}

	/// <summary>How far below its top a label drawn at -45 degrees reaches.</summary>
	private static double RotatedDepth(double width, double fontSize)
		=> (fontSize * (0.8 + (0.2 * Diagonal))) + (width * Diagonal);

	private static double Widest(IEnumerable<string?> labels, AxisArea axis)
		=> labels.Max(label => label is null ? 0 : TextMeasure.Width(label, axis.FontSize, axis.FontWeight));

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
