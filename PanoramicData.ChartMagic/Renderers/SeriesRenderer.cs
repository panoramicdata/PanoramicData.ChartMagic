namespace PanoramicData.ChartMagic.Renderers;

/// <summary>
/// The series themselves: lines, filled areas, and columns or bars.
/// </summary>
/// <param name="canvas">The document this draws into.</param>
internal sealed class SeriesRenderer(SvgCanvas canvas)
{
	private readonly SvgCanvas _canvas = canvas;
	private readonly MarkerFactory _markers = new(canvas);

	internal void PlotSeries(Chart chart, PlotGeometry geometry, XmlElement defs, XmlElement innerPlotNode)
	{
		var stackedColumnTotals = new Dictionary<string, double>();
		var stackedAreaTotals = new Dictionary<string, double>();
		var stackLines = _canvas.Group("stackLines");
		var dataLabels = _canvas.Group("dataLabels");
		var bands = BandLayout.For(chart);

		var seriesIndex = -1;
		foreach (var series in chart.Series)
		{
			var seriesNode = _canvas.Group($"series{++seriesIndex}");
			List<DataLabelAnchor> labelAnchors;

			// Add markers to defs if required
			var seriesMarkerId = $"series{seriesIndex}Marker";
			var markerDefinition = _markers.CreateMarkerDefinition(series, seriesMarkerId);
			if (markerDefinition is not null)
			{
				defs.AppendChild(markerDefinition);
			}

			var stackTotals = StackTotalsFor(series.ChartType, stackedColumnTotals, stackedAreaTotals);

			if (PlotGeometry.IsBanded(series.ChartType))
			{
				labelAnchors = PlotBandedSeries(chart, geometry, series, seriesNode, stackTotals, bands.SlotFor(series), bands.SlotCount);
			}
			else
			{
				labelAnchors = PlotPointSeries(geometry, series, seriesNode, stackLines, stackTotals, seriesMarkerId);
			}

			innerPlotNode.AppendChild(seriesNode);
			AppendDataLabels(series, seriesIndex, labelAnchors, dataLabels);
		}

		if (stackLines.ChildNodes.Count != 0)
		{
			innerPlotNode.AppendChild(stackLines);
		}

		// Last, so that no series is drawn over another series' labels.
		if (dataLabels.ChildNodes.Count != 0)
		{
			innerPlotNode.AppendChild(dataLabels);
		}
	}

	/// <summary>
	/// The gap between a data label and the thing it labels, as a fraction of the label's font size.
	/// </summary>
	/// <remarks>
	/// Measured against DocMagic on the [List.Graph:] mixed column and line example: with 16px
	/// labels, the bottom of each column label sat about 8px above the top of its column.
	/// </remarks>
	private const double DataLabelGapFraction = 0.5;

	/// <summary>
	/// Draws one series' data labels, from its label text, beside the points it drew.
	/// </summary>
	/// <remarks>
	/// Label text was carried on every series and drawn only for pies, so labelText=#VAL on a
	/// column, bar or line chart produced a chart with no labels and no error. A series with no
	/// label text draws no labels, as in the Microsoft chart control.
	/// </remarks>
	private void AppendDataLabels(Series series, int seriesIndex, List<DataLabelAnchor> anchors, XmlElement dataLabels)
	{
		if (series.LabelText is not { Length: > 0 } || anchors.Count == 0)
		{
			return;
		}

		var total = anchors.Sum(anchor => Math.Abs(anchor.Value));
		var style = TextStyle.Unstroked(series.FontWeight, series.FontFamily, series.FontSize, series.FontColor);

		var pointIndex = 0;
		foreach (var anchor in anchors)
		{
			var percentage = total == 0 ? 0 : Math.Abs(anchor.Value) / total * 100;
			var text = DataLabelText.Substitute(series.LabelText, anchor.Point, anchor.Value, series.Name, percentage, total);
			if (text is not { Length: > 0 })
			{
				continue;
			}

			dataLabels.AppendChild(
				_canvas.Text(
					FormattableString.Invariant($"series{seriesIndex}Label{pointIndex++}"),
					anchor.X,
					anchor.Y,
					text,
					anchor.HorizontalAlignment,
					anchor.VerticalAlignment,
					style));
		}
	}

	/// <summary>
	/// The running totals a series stacks onto, or null where it does not stack.
	/// </summary>
	/// <remarks>
	/// Columns and bars share one set of totals because a chart mixes neither with the other, and
	/// areas keep their own so that a stacked area drawn alongside stacked columns does not stack
	/// onto them.
	/// </remarks>
	private static Dictionary<string, double>? StackTotalsFor(
		SeriesChartType chartType,
		Dictionary<string, double> bandTotals,
		Dictionary<string, double> areaTotals)
		=> chartType switch
		{
			SeriesChartType.StackedColumn or SeriesChartType.StackedColumn100 => bandTotals,
			SeriesChartType.StackedBar or SeriesChartType.StackedBar100 => bandTotals,
			SeriesChartType.StackedArea or SeriesChartType.StackedArea100 => areaTotals,
			_ => null
		};

	/// <summary>
	/// Draws a series that is a run of points: its fill where it has one, then its line and any
	/// markers.
	/// </summary>
	/// <remarks>
	/// A stacked area draws its fill in its own group and its line in the shared one, so that
	/// every line is drawn over every fill rather than being buried by the next series.
	/// </remarks>
	/// <returns>Where each point's data label goes.</returns>
	private List<DataLabelAnchor> PlotPointSeries(
		PlotGeometry geometry,
		Series series,
		XmlElement seriesNode,
		XmlElement stackLines,
		Dictionary<string, double>? stackTotals,
		string markerId)
	{
		var trace = TracePoints(geometry, series, stackTotals, markerId);

		if (FillsBeneathItsLine(series.ChartType))
		{
			seriesNode.AppendChild(CreateAreaNode(geometry, series, trace));
		}

		var lineTarget = LineTarget(series.ChartType, seriesNode, stackLines);
		if (lineTarget is not null)
		{
			AppendLinePath(lineTarget, series, trace);
		}

		return trace.LabelAnchors;
	}

	/// <summary>
	/// Whether this chart type hangs a filled area beneath the line it traces.
	/// </summary>
	private static bool FillsBeneathItsLine(SeriesChartType chartType)
		=> chartType is SeriesChartType.Area
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
			SeriesChartType.Area or SeriesChartType.Line or SeriesChartType.FastLine => seriesNode,
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
		var linePath = new StringBuilder();
		// The outline only. Where the fill starts and finishes is decided once the first and last
		// points are known, because it belongs under them and not at the edges of the plot.
		var areaSegments = new StringBuilder();
		double? firstXPosition = null;
		var lastXPosition = 0d;
		var returnPathPoints = new List<(double X, double Y)>();
		var markerNodes = new List<XmlElement>();
		var labelAnchors = new List<DataLabelAnchor>();
		var isFirstPoint = true;

		// A label sits above its point, clear of the marker where there is one.
		var labelLift = (series.MarkerStyle != MarkerStyle.None ? (series.MarkerSize ?? 0) / 2 : 0)
			+ (series.FontSize * DataLabelGapFraction);

		foreach (var chartPoint in series.Points)
		{
			var yValue = StackedValue(geometry, chartPoint, stackTotals, out var previousYValue);

			var xPosition = geometry.IsCategorical
				? geometry.CategoryToPixels(chartPoint.XValue)
				: geometry.XToPixels(chartPoint.XValue);
			var yPosition = geometry.YToPixels(yValue);
			if (previousYValue is not null)
			{
				returnPathPoints.Add((xPosition, geometry.YToPixels(previousYValue.Value)));
			}

			// Letter - always M to start, afterwards L unless the previous value is null
			linePath.Append($"{(isFirstPoint ? "M" : " L")}{xPosition} {yPosition}");
			areaSegments.Append($" L{xPosition} {yPosition}");
			firstXPosition ??= xPosition;
			lastXPosition = xPosition;
			isFirstPoint = false;

			if (series.MarkerStyle != MarkerStyle.None)
			{
				markerNodes.Add(_markers.CreateMarkerReference(markerId, xPosition, yPosition));
			}

			if (chartPoint.YValue is { } pointValue)
			{
				labelAnchors.Add(new DataLabelAnchor(
					chartPoint,
					pointValue,
					xPosition,
					yPosition - labelLift,
					HorizontalAlignment.Center,
					VerticalAlignment.Bottom));
			}
		}

		return new SeriesTrace(
			linePath.ToString(),
			areaSegments.ToString(),
			firstXPosition,
			lastXPosition,
			returnPathPoints,
			markerNodes,
			labelAnchors);
	}

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

	/// <summary>
	/// Draws one column or bar series: a rectangle per point, running from the value axis origin
	/// to the value of the point, occupying its slot within the category band.
	/// </summary>
	/// <remarks>
	/// Issue #33: the renderer had no case for any of these chart types, so a
	/// column chart rendered its legend and nothing else - no exception, no empty-plot warning,
	/// just a blank plot area beside a correct-looking legend.
	/// </remarks>
	/// <returns>Where each point's data label goes.</returns>
	private List<DataLabelAnchor> PlotBandedSeries(
		Chart chart,
		PlotGeometry geometry,
		Series series,
		XmlElement seriesNode,
		Dictionary<string, double>? stackTotals,
		int slot,
		int slotCount)
	{
		var bandExtent = geometry.CategoryBandExtent;
		var groupExtent = bandExtent * chart.ChartArea.ColumnBandFillFraction;
		var slotExtent = groupExtent / slotCount;
		var origin = geometry.ValueAxisOrigin;
		var isHorizontal = PlotGeometry.IsHorizontal(series.ChartType);
		var labelAnchors = new List<DataLabelAnchor>();
		var gap = series.FontSize * DataLabelGapFraction;

		foreach (var chartPoint in series.Points)
		{
			if (chartPoint.YValue is not { } pointValue)
			{
				continue;
			}

			var (from, to) = BandSpan(geometry, chartPoint, stackTotals, origin);
			var slotStart = geometry.CategoryToPixels(chartPoint.XValue) - (groupExtent / 2) + (slot * slotExtent);

			var rectNode = CreateBandRect(isHorizontal, from, to, slotStart, slotExtent);
			rectNode.SetStyle(series);
			seriesNode.AppendChild(rectNode);

			labelAnchors.Add(BandLabelAnchor(
				chartPoint,
				pointValue,
				isHorizontal,
				stackTotals is not null,
				from,
				to,
				slotStart + (slotExtent / 2),
				gap));
		}

		return labelAnchors;
	}

	/// <summary>
	/// Where a column or bar's data label goes.
	/// </summary>
	/// <remarks>
	/// As measured against DocMagic on the [List.Graph:] examples: a column's label is centred just
	/// above its top, and a bar's is just beyond its end, reading outwards. A negative value's label
	/// goes beyond its end on the other side. A stacked segment's label is centred in the segment,
	/// because outside it would sit on the next segment up.
	/// </remarks>
	private static DataLabelAnchor BandLabelAnchor(
		ChartPoint point,
		double value,
		bool isHorizontal,
		bool isStacked,
		double from,
		double to,
		double across,
		double gap)
	{
		if (isStacked)
		{
			var middle = (from + to) / 2;
			return isHorizontal
				? new DataLabelAnchor(point, value, middle, across, HorizontalAlignment.Center, VerticalAlignment.Middle)
				: new DataLabelAnchor(point, value, across, middle, HorizontalAlignment.Center, VerticalAlignment.Middle);
		}

		// Pixel positions grow rightwards and downwards, so the far end of a positive bar is the
		// larger X, and the far end of a positive column is the smaller Y.
		var isNegative = value < 0;
		if (isHorizontal)
		{
			return isNegative
				? new DataLabelAnchor(point, value, Math.Min(from, to) - gap, across, HorizontalAlignment.Right, VerticalAlignment.Middle)
				: new DataLabelAnchor(point, value, Math.Max(from, to) + gap, across, HorizontalAlignment.Left, VerticalAlignment.Middle);
		}

		return isNegative
			? new DataLabelAnchor(point, value, across, Math.Max(from, to) + gap, HorizontalAlignment.Center, VerticalAlignment.Top)
			: new DataLabelAnchor(point, value, across, Math.Min(from, to) - gap, HorizontalAlignment.Center, VerticalAlignment.Bottom);
	}

	/// <summary>
	/// Where one column or bar starts and finishes along the value axis, in pixels.
	/// </summary>
	private static (double From, double To) BandSpan(
		PlotGeometry geometry,
		ChartPoint chartPoint,
		Dictionary<string, double>? stackTotals,
		double origin)
	{
		if (stackTotals is null)
		{
			return (origin, geometry.ValueToPixels(chartPoint.YValue!.Value));
		}

		var key = chartPoint.XValue.ToString(CultureInfo.InvariantCulture);
		var previousTotal = stackTotals.TryGetValue(key, out var runningTotal) ? runningTotal : 0;

		// A hundred per cent stacked series contributes its share of the category, not its value.
		var contribution = geometry.IsPercentStackedPlot
			? geometry.ToPercentOfCategory(chartPoint.XValue, chartPoint.YValue!.Value)
			: chartPoint.YValue!.Value;

		var newTotal = previousTotal + contribution;
		stackTotals[key] = newTotal;
		return (geometry.ValueToPixels(previousTotal), geometry.ValueToPixels(newTotal));
	}

	/// <summary>
	/// The rectangle for one column or bar: its span lies along the value axis and its thickness
	/// across the category axis, whichever way round those two are.
	/// </summary>
	private XmlElement CreateBandRect(bool isHorizontal, double from, double to, double slotStart, double slotExtent)
	{
		var rectNode = _canvas.Element("rect");
		var near = Math.Round(Math.Min(from, to), 2).ToString(CultureInfo.InvariantCulture);
		var extent = Math.Round(Math.Abs(to - from), 2).ToString(CultureInfo.InvariantCulture);
		var across = Math.Round(slotStart, 2).ToString(CultureInfo.InvariantCulture);
		var thickness = Math.Round(slotExtent, 2).ToString(CultureInfo.InvariantCulture);

		if (isHorizontal)
		{
			rectNode.SetAttribute("x", near);
			rectNode.SetAttribute("y", across);
			rectNode.SetAttribute("width", extent);
			rectNode.SetAttribute("height", thickness);
		}
		else
		{
			rectNode.SetAttribute("x", across);
			rectNode.SetAttribute("y", near);
			rectNode.SetAttribute("width", thickness);
			rectNode.SetAttribute("height", extent);
		}

		return rectNode;
	}

	/// <summary>
	/// How the column and bar series of a chart divide up a category band.
	/// </summary>
	/// <param name="GroupedSeries">The banded series that stand side by side.</param>
	/// <param name="SlotCount">How many slots a band is divided into.</param>
	/// <remarks>
	/// Issue #33: a column or bar occupies a slot within its category band. Grouped series take
	/// one slot each; all stacked series share a single slot, because they stack on top of one
	/// another rather than standing side by side.
	/// </remarks>
	private sealed record BandLayout(List<Series> GroupedSeries, int SlotCount)
	{
		internal static BandLayout For(Chart chart)
		{
			var bandedSeries = chart.Series.Where(s => PlotGeometry.IsBanded(s.ChartType)).ToList();
			var groupedSeries = bandedSeries.Where(s => !PlotGeometry.IsStacked(s.ChartType)).ToList();
			var hasStackedBanded = bandedSeries.Exists(s => PlotGeometry.IsStacked(s.ChartType));
			return new BandLayout(groupedSeries, Math.Max(1, groupedSeries.Count + (hasStackedBanded ? 1 : 0)));
		}

		/// <summary>
		/// The slot a series occupies: its own, or the shared last one if it is stacked.
		/// </summary>
		internal int SlotFor(Series series)
			=> PlotGeometry.IsStacked(series.ChartType)
				? GroupedSeries.Count
				: GroupedSeries.IndexOf(series);
	}
}
