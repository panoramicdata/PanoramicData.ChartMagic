using static PanoramicData.ChartMagic.Renderers.AxisTicks;

namespace PanoramicData.ChartMagic.Renderers;

/// <summary>
/// The axis strips and the gridlines that belong to them.
/// </summary>
/// <param name="canvas">The document the axes are drawn into.</param>
internal sealed partial class AxisRenderer(SvgCanvas canvas)
{
	private readonly SvgCanvas _canvas = canvas;

	/// <summary>
	/// Gap between a tick mark and the label that belongs to it, in pixels.
	/// </summary>
	internal const double TickLabelGapPixels = 4;

	/// <summary>
	/// A positioned group for an axis, aligned to the plot it annotates.
	/// </summary>
	/// <remarks>
	/// An axis frame is not an independent rectangle: it has to line up with the inner plot along
	/// the dimension they share, or the ticks and labels it draws point at the wrong values. The
	/// axis areas carry their own defaults - 10% in and 90% long - which do not track the plot, so
	/// a caller that moved the plot got axes that stayed put. Measured on a chart with the report
	/// defaults: the value axis line was drawn from y 59 to 360 where the reference render drew it
	/// from 40 to 339, exactly the 5% of the height by which the plot had moved.
	///
	/// The other dimension - how wide the value-axis strip is, how tall the category-axis strip -
	/// stays with the axis area, since that is a real setting.
	/// </remarks>
	private XmlElement GetAxisGroup(Chart chart, AxisArea axis, string id)
	{
		var innerPlot = chart.ChartArea.InnerPlot;
		var isVertical = ReferenceEquals(axis, chart.ChartArea.YAxis)
			|| ReferenceEquals(axis, chart.ChartArea.YAxis2Area);

		if (isVertical)
		{
			axis.YPositionPercent = innerPlot.YPositionPercent;
			axis.HeightPercent = innerPlot.HeightPercent;
		}
		else
		{
			axis.XPositionPercent = innerPlot.XPositionPercent;
			axis.WidthPercent = innerPlot.WidthPercent;
		}

		return _canvas.PositionedGroup(axis, id, chart.ChartArea);
	}

	/// <summary>
	/// Draws the axis strips: their backgrounds, then the axis line, ticks, labels and title.
	/// </summary>
	internal void PlotAxes(Chart chart, PlotGeometry geometry, AxisLayout layout, XmlElement chartAreaNode)
	{
		// X Axis
		var xAxis = chart.ChartArea.XAxis;
		var xAxisNode = GetAxisGroup(chart, xAxis, "xAxis");
		chartAreaNode.AppendChild(xAxisNode);
		if (xAxis.IsEnabled && xAxis.LabelsEnabled)
		{
			DrawXAxis(chart, geometry, layout, xAxis, xAxisNode);
		}

		// Y Axis
		var yAxis = chart.ChartArea.YAxis;
		var yAxisNode = GetAxisGroup(chart, yAxis, "yAxis");
		chartAreaNode.AppendChild(yAxisNode);
		if (yAxis.IsEnabled && yAxis.LabelsEnabled)
		{
			DrawYAxis(chart, geometry, layout, yAxis, yAxisNode);
		}
	}

	/// <summary>
	/// The X axis strip sits directly beneath the plot and shares its width and horizontal
	/// origin, so a local X coordinate in one is the same local X coordinate in the other, and
	/// the strip top edge is the plot bottom edge.
	/// </summary>
	private void DrawXAxis(Chart chart, PlotGeometry geometry, AxisLayout layout, AxisArea xAxis, XmlElement xAxisNode)
	{
		var axisHeight = _canvas.HeightPixels * xAxis.GetCanvasHeightPercent() / 100;

		xAxisNode.AppendChild(_canvas.Line(0, 0, geometry.Width, 0, xAxis.LineColor, xAxis.LineWidth, xAxis.LineDashStyle));

		var tickLength = xAxis.TickLengthPixels;
		var labelY = tickLength + TickLabelGapPixels;
		var isRotated = layout.XLabelAngle != 0;
		var labelStyle = TextStyle.Unstroked(xAxis.FontWeight, xAxis.FontFamily, xAxis.FontSize, xAxis.FontColor);

		foreach (var (value, label) in XAxisTickValues(chart, geometry).Zip(layout.XLabels))
		{
			var x = XAxisPixels(geometry, value);
			xAxisNode.AppendChild(_canvas.Line(x, 0, x, tickLength, xAxis.LineColor, xAxis.LineWidth));

			if (label is null)
			{
				continue;
			}

			xAxisNode.AppendChild(
				_canvas.Text(
					FormattableString.Invariant($"xAxisLabel{x}"),
					x,
					labelY,
					label,
					// A rotated label reads better anchored at its end, so that it runs away
					// from its tick rather than across it.
					isRotated ? HorizontalAlignment.Right : HorizontalAlignment.Center,
					VerticalAlignment.Top,
					labelStyle,
					layout.XLabelAngle));
		}

		if (xAxis.Title is { Length: > 0 })
		{
			xAxisNode.AppendChild(
				_canvas.Text(
					"xAxisTitle",
					geometry.Width / 2,
					// Held clear of the bottom edge: on the baseline exactly, the descenders fall
					// outside the viewport and are clipped.
					axisHeight - (xAxis.FontSize * 0.2),
					xAxis.Title,
					HorizontalAlignment.Center,
					VerticalAlignment.Bottom,
					// MS-27539 The axis weight, as the Windows renderer draws it: regular unless asked for.
					labelStyle));
		}
	}

	/// <summary>
	/// The Y axis strip sits immediately left of the plot and shares its height, so the axis
	/// line is drawn along the strip right-hand edge, which is the plot left edge.
	/// </summary>
	private void DrawYAxis(Chart chart, PlotGeometry geometry, AxisLayout layout, AxisArea yAxis, XmlElement yAxisNode)
	{
		var axisWidth = _canvas.WidthPixels * yAxis.GetCanvasWidthPercent() / 100;

		yAxisNode.AppendChild(_canvas.Line(axisWidth, 0, axisWidth, geometry.Height, yAxis.LineColor, yAxis.LineWidth, yAxis.LineDashStyle));

		var tickLength = yAxis.TickLengthPixels;
		var labelX = axisWidth - tickLength - TickLabelGapPixels;
		var labelStyle = TextStyle.Unstroked(yAxis.FontWeight, yAxis.FontFamily, yAxis.FontSize, yAxis.FontColor);
		var widestLabel = 0d;

		foreach (var (value, label) in YAxisTickValues(chart, geometry).Zip(layout.YLabels))
		{
			var y = geometry.IsHorizontalPlot ? geometry.CategoryToPixels(value) : geometry.YToPixels(value);
			yAxisNode.AppendChild(
				_canvas.Line(axisWidth - tickLength, y, axisWidth, y, yAxis.LineColor, yAxis.LineWidth));

			if (label is null)
			{
				continue;
			}

			widestLabel = Math.Max(widestLabel, TextMeasure.Width(label, yAxis.FontSize, yAxis.FontWeight));

			yAxisNode.AppendChild(
				_canvas.Text(
					FormattableString.Invariant($"yAxisLabel{y}"),
					labelX,
					y,
					label,
					HorizontalAlignment.Right,
					VerticalAlignment.Middle,
					labelStyle,
					yAxis.LabelAngle));
		}

		if (yAxis.Title is { Length: > 0 }
			&& YAxisTitleFontSize(labelX - widestLabel, yAxis.FontSize) is { } titleFontSize)
		{
			// Rotated a quarter turn anticlockwise and centred on the axis, as a Y axis title
			// conventionally reads. Rotated about its baseline, so the glyphs lie to its left.
			yAxisNode.AppendChild(
				_canvas.Text(
					"yAxisTitle",
					TitleEdgeGapPixels + (titleFontSize * TitleAscentFraction),
					geometry.Height / 2,
					yAxis.Title,
					HorizontalAlignment.Center,
					VerticalAlignment.Bottom,
					labelStyle with { FontSize = titleFontSize },
					-90));
		}
	}

	private const double TitleEdgeGapPixels = 1;

	/// <summary>The space a Y axis title keeps from the image edge and from the labels, together.</summary>
	internal const double YAxisTitleClearancePixels = (2 * TitleEdgeGapPixels) + TickLabelGapPixels;

	private const double TitleAscentFraction = 0.8;

	/// <summary>The smallest a Y axis title is shrunk to before it is left out.</summary>
	internal const double MinimumTitleFontSize = 8;

	/// <summary>
	/// The size the Y axis title can be drawn at in the space left of the labels, or null if none.
	/// </summary>
	/// <remarks>
	/// Drawn at full size, a title in a narrow strip lay over the tick labels. The Microsoft chart
	/// control leaves it out; a smaller title still says what the axis measures, so it is shrunk to
	/// fit first and only left out when it would be too small to read.
	/// </remarks>
	private static double? YAxisTitleFontSize(double labelsLeftEdge, double fontSize)
	{
		var available = labelsLeftEdge - YAxisTitleClearancePixels;
		var fitted = Math.Min(fontSize, available);
		return fitted >= MinimumTitleFontSize ? fitted : null;
	}
}
