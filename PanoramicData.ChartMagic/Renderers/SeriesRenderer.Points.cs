namespace PanoramicData.ChartMagic.Renderers;

internal sealed partial class SeriesRenderer
{
	/// <summary>
	/// Whether this chart type hangs a filled area beneath the line it traces.
	/// </summary>
	private static bool FillsBeneathItsLine(SeriesChartType chartType)
		=> chartType is SeriesChartType.Area
			or SeriesChartType.SplineArea
			or SeriesChartType.StackedArea
			or SeriesChartType.StackedArea100;

	/// <summary>
	/// The group a series' line belongs in, or null for a chart type that draws no line.
	/// </summary>
	/// <remarks>
	/// A stacked area puts its line in the shared group rather than its own, so that every line
	/// is drawn over every fill rather than being buried by the next series.
	/// </remarks>
	private static XmlElement? LineTarget(SeriesChartType chartType, XmlElement seriesNode, XmlElement stackLines)
		=> chartType switch
		{
			SeriesChartType.Area or SeriesChartType.SplineArea or SeriesChartType.Line or SeriesChartType.FastLine or SeriesChartType.Spline => seriesNode,
			SeriesChartType.StackedArea or SeriesChartType.StackedArea100 => stackLines,
			_ => null
		};

	/// <summary>
	/// Walks a series' points once, building the paths, the stack return path and the markers.
	/// </summary>
	private SeriesTrace TracePoints(
		PlotGeometry geometry,
		Series series,
		Dictionary<string, double>? stackTotals,
		string markerId)
	{
		var points = new List<(double X, double Y)>();
		var returnPathPoints = new List<(double X, double Y)>();
		var markerNodes = new List<XmlElement>();
		var labelAnchors = new List<DataLabelAnchor>();

		var labelLift = LabelLift(series);

		foreach (var chartPoint in series.Points)
		{
			var yValue = StackedValue(geometry, chartPoint, stackTotals, out var previousYValue);

			var xPosition = XPixels(geometry, chartPoint);
			var yPosition = geometry.YToPixels(yValue);
			if (previousYValue is not null)
			{
				returnPathPoints.Add((xPosition, geometry.YToPixels(previousYValue.Value)));
			}

			points.Add((xPosition, yPosition));

			if (series.MarkerStyle != MarkerStyle.None)
			{
				markerNodes.Add(_markers.CreateMarkerReference(markerId, xPosition, yPosition));
			}

			if (chartPoint.YValue is { } pointValue)
			{
				labelAnchors.Add(PointLabelAnchor(chartPoint, pointValue, xPosition, yPosition - labelLift));
			}
		}

		// The outline only. Where the fill starts and finishes is decided once the first and last
		// points are known, because it belongs under them and not at the edges of the plot.
		var (linePath, areaSegments) = SeriesPaths.For(series.ChartType, points);
		return new SeriesTrace(
			linePath,
			areaSegments,
			points.Count > 0 ? points[0].X : null,
			points.Count > 0 ? points[^1].X : 0,
			returnPathPoints,
			markerNodes,
			labelAnchors);
	}

	private static DataLabelAnchor PointLabelAnchor(ChartPoint chartPoint, double value, double x, double y)
		=> new(chartPoint, value, x, y, HorizontalAlignment.Center, VerticalAlignment.Bottom);

	private static double XPixels(PlotGeometry geometry, ChartPoint chartPoint)
		=> geometry.IsCategorical
			? geometry.CategoryToPixels(chartPoint.XValue)
			: geometry.XToPixels(chartPoint.XValue);

	/// <summary>
	/// How far above its point a label sits: clear of the marker where there is one.
	/// </summary>
	private static double LabelLift(Series series)
		=> (series.MarkerStyle != MarkerStyle.None ? (series.MarkerSize ?? 0) / 2 : 0)
			+ (series.FontSize * DataLabelGapFraction);

	/// <summary>
	/// The value a point is drawn at, which for a stacked series is its contribution added to the
	/// running total for its category.
	/// </summary>
	/// <param name="geometry">The plot the point is being drawn into.</param>
	/// <param name="chartPoint">The point.</param>
	/// <param name="stackTotals">
	/// The running totals for the series that stack with this one, or null where it does not stack.
	/// </param>
	/// <param name="previousTotal">
	/// The total this point stacks onto, or null where it is not stacked onto anything. A point
	/// with no value still reports one, because the series below it still has to be closed off.
	/// </param>
	private static double StackedValue(
		PlotGeometry geometry,
		ChartPoint chartPoint,
		Dictionary<string, double>? stackTotals,
		out double? previousTotal)
	{
		previousTotal = null;
		if (stackTotals is null)
		{
			return chartPoint.YValue ?? 0;
		}

		var key = chartPoint.XValue.ToString(CultureInfo.InvariantCulture);
		if (stackTotals.TryGetValue(key, out var runningTotal))
		{
			previousTotal = runningTotal;
		}

		if (chartPoint.YValue is null)
		{
			return 0;
		}

		// A hundred per cent stacked series contributes its share of the category, not its value.
		var contribution = geometry.IsPercentStackedPlot
			? geometry.ToPercentOfCategory(chartPoint.XValue, chartPoint.YValue.Value)
			: chartPoint.YValue.Value;

		var total = contribution + (previousTotal ?? 0);
		stackTotals[key] = total;
		return total;
	}

	/// <summary>
	/// The filled area beneath a traced series.
	/// </summary>
	/// <remarks>
	/// The fill hangs below the line it follows, from the first point to the last. It used to
	/// start at the bottom-left corner of the plot and finish at the bottom-right, which drew a
	/// diagonal ramp up to the first point and another down from the last - inventing data on
	/// either side of the series. With a whole category interval of padding at each end of the
	/// axis, those ramps were a sixth of the chart wide.
	///
	/// And it hangs to the zero line, not to the floor of the plot, so a series with negative
	/// values fills downwards from zero rather than upwards from the bottom.
	/// </remarks>
	private XmlElement CreateAreaNode(PlotGeometry geometry, Series series, SeriesTrace trace)
	{
		var baseline = geometry.ValueAxisOrigin;
		var returnPathPoints = trace.ReturnPathPoints;
		if (returnPathPoints.Count == 0)
		{
			returnPathPoints = [(trace.LastXPosition, baseline)];
		}

		var areaPath = new StringBuilder(
			FormattableString.Invariant($"M{trace.FirstXPosition ?? 0} {baseline}"));
		areaPath.Append(trace.AreaSegments);
		areaPath.Append(string.Join("", returnPathPoints.AsEnumerable().Reverse().Select(p => $"L{p.X} {p.Y}")));
		areaPath.Append('Z');

		var areaNode = _canvas.Element("path");
		areaNode.SetAttribute("d", areaPath.ToString());
		areaNode.SetStyle(series, applyStroke: false);
		return areaNode;
	}

	/// <summary>
	/// Appends a traced series' line, and its markers behind it, to a group.
	/// </summary>
	private void AppendLinePath(XmlElement target, Series series, SeriesTrace trace)
	{
		var pathNode = _canvas.Element("path");
		pathNode.SetAttribute("d", trace.LinePath);
		pathNode.SetStyle(series, applyFill: false);
		target.AppendChild(pathNode);

		foreach (var markerNode in trace.MarkerNodes)
		{
			target.AppendChild(markerNode);
		}
	}
}
