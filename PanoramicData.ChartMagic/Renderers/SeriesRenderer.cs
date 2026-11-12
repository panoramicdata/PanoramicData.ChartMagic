namespace PanoramicData.ChartMagic.Renderers;

/// <summary>
/// The series themselves: lines, filled areas, and columns or bars.
/// </summary>
/// <param name="canvas">The document this draws into.</param>
internal sealed partial class SeriesRenderer(SvgCanvas canvas)
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
		var clip = PlotClip(geometry, defs);
		stackLines.SetAttribute("clip-path", clip);

		var seriesIndex = -1;
		foreach (var series in chart.Series)
		{
			var seriesNode = _canvas.Group($"series{++seriesIndex}");
			seriesNode.SetAttribute("clip-path", clip);
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
			AppendDataLabels(series, seriesIndex, labelAnchors, dataLabels, chart.Culture);
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
	/// A clip path covering the inner plot, as a clip-path reference.
	/// </summary>
	/// <remarks>
	/// Data beyond an explicit axis minimum or maximum was drawn straight through the axis and
	/// over the chart around it. The Microsoft chart control clips series to the plot. A pixel of
	/// slack either side keeps a line lying exactly on the plot edge whole.
	/// </remarks>
	private string PlotClip(PlotGeometry geometry, XmlElement defs)
	{
		const string id = "innerPlotClip";
		const double slack = 1;

		var rect = _canvas.Element("rect");
		rect.SetAttribute("x", SvgCanvas.N(-slack));
		rect.SetAttribute("y", SvgCanvas.N(-slack));
		rect.SetAttribute("width", SvgCanvas.N(geometry.Width + (2 * slack)));
		rect.SetAttribute("height", SvgCanvas.N(geometry.Height + (2 * slack)));

		var clipPath = _canvas.Element("clipPath");
		clipPath.SetAttribute("id", id);
		clipPath.AppendChild(rect);
		defs.AppendChild(clipPath);

		return $"url(#{id})";
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
	private void AppendDataLabels(Series series, int seriesIndex, List<DataLabelAnchor> anchors, XmlElement dataLabels, CultureInfo culture)
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
			var text = DataLabelText.Substitute(series.LabelText, anchor.Point, anchor.Value, series.Name, percentage, total, culture);
			if (text is not { Length: > 0 })
			{
				continue;
			}

			if (series.LabelBackColor is { A: > 0 } background)
			{
				dataLabels.AppendChild(
					_canvas.TextBackground(anchor.X, anchor.Y, text, anchor.HorizontalAlignment, anchor.VerticalAlignment, style, background));
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
}
