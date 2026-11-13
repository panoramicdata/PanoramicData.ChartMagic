using System.Drawing;

namespace PanoramicData.ChartMagic.Renderers;

internal sealed partial class SeriesRenderer
{
	/// <summary>The fewest Y values a box plot point needs; further ones are unusual points.</summary>
	private const int BoxPlotValueCount = 6;

	// The order of a box plot point's Y values in the Microsoft chart control.
	private const int LowerWhisker = 0;
	private const int UpperWhisker = 1;
	private const int LowerBox = 2;
	private const int UpperBox = 3;
	private const int Average = 4;
	private const int Median = 5;

	/// <summary>The width of a whisker cap, as a fraction of the box.</summary>
	private const double WhiskerCapFraction = 0.5;

	private const double AverageMarkerRadiusPixels = 3;

	private const double UnusualPointRadiusPixels = 2.5;

	/// <summary>
	/// Draws a box plot series: a box, whiskers, a median line and an average marker for each point
	/// carrying six or more Y values, and nothing for a point with fewer.
	/// </summary>
	/// <returns>No data label anchors: a box plot draws no data labels.</returns>
	private List<DataLabelAnchor> PlotBoxPlotSeries(
		PlotGeometry geometry,
		Series series,
		XmlElement seriesNode,
		int slot,
		double groupExtent,
		double slotExtent)
	{
		var pointIndex = 0;
		foreach (var point in series.Points)
		{
			if (point.YValues is { Count: >= BoxPlotValueCount } values)
			{
				var centre = geometry.CategoryToPixels(point.XValue) - (groupExtent / 2) + (slot * slotExtent) + (slotExtent / 2);
				seriesNode.AppendChild(CreateBoxPlotPoint(geometry, series, values, centre, slotExtent, pointIndex));
			}

			pointIndex++;
		}

		return [];
	}

	/// <summary>
	/// One point: its box, then its whiskers and median, then the average and unusual markers.
	/// </summary>
	private XmlElement CreateBoxPlotPoint(PlotGeometry geometry, Series series, IReadOnlyList<double> values, double centre, double width, int pointIndex)
	{
		var pointNode = _canvas.Group(FormattableString.Invariant($"boxPlot{pointIndex}"));
		var top = geometry.YToPixels(values[UpperBox]);
		var box = _canvas.Element("rect");
		box.SetAttribute("x", SvgCanvas.N(centre - (width / 2)));
		box.SetAttribute("y", SvgCanvas.N(top));
		box.SetAttribute("width", SvgCanvas.N(width));
		box.SetAttribute("height", SvgCanvas.N(geometry.YToPixels(values[LowerBox]) - top));
		box.SetStyle(series);
		pointNode.AppendChild(box);

		foreach (var (x1, value1, x2, value2) in BoxPlotSegments(values, centre, width))
		{
			AppendBoxPlotLine(pointNode, series, x1, value1, x2, value2, geometry);
		}

		AppendBoxPlotMarkers(pointNode, geometry, series, values, centre);
		return pointNode;
	}

	/// <summary>
	/// The lines of a box plot point as (x, value) pairs: the two whiskers, their caps, and the median.
	/// </summary>
	private static IEnumerable<(double X1, double Value1, double X2, double Value2)> BoxPlotSegments(IReadOnlyList<double> values, double centre, double width)
	{
		var halfCap = width * WhiskerCapFraction / 2;
		yield return (centre, values[UpperWhisker], centre, values[UpperBox]);
		yield return (centre, values[LowerBox], centre, values[LowerWhisker]);
		yield return (centre - halfCap, values[UpperWhisker], centre + halfCap, values[UpperWhisker]);
		yield return (centre - halfCap, values[LowerWhisker], centre + halfCap, values[LowerWhisker]);
		yield return (centre - (width / 2), values[Median], centre + (width / 2), values[Median]);
	}

	/// <summary>
	/// A line between two values, in the series stroke. A NaN value hides the line, which is how the
	/// Microsoft chart control hides a median or an average.
	/// </summary>
	private void AppendBoxPlotLine(XmlElement parent, Series series, double x1, double value1, double x2, double value2, PlotGeometry geometry)
	{
		if (double.IsNaN(value1) || double.IsNaN(value2))
		{
			return;
		}

		parent.AppendChild(_canvas.Line(
			x1,
			geometry.YToPixels(value1),
			x2,
			geometry.YToPixels(value2),
			BoxPlotLineColor(series),
			series.StrokeWidth,
			series.StrokeStyle));
	}

	/// <summary>
	/// The average as a filled marker at the centre of the box, and each value beyond the sixth as
	/// a hollow one.
	/// </summary>
	private void AppendBoxPlotMarkers(XmlElement parent, PlotGeometry geometry, Series series, IReadOnlyList<double> values, double centre)
	{
		if (!double.IsNaN(values[Average]))
		{
			parent.AppendChild(CreateBoxPlotDot(series, centre, geometry.YToPixels(values[Average]), AverageMarkerRadiusPixels, filled: true));
		}

		foreach (var value in values.Skip(BoxPlotValueCount).Where(value => !double.IsNaN(value)))
		{
			parent.AppendChild(CreateBoxPlotDot(series, centre, geometry.YToPixels(value), UnusualPointRadiusPixels, filled: false));
		}
	}

	private XmlElement CreateBoxPlotDot(Series series, double x, double y, double radius, bool filled)
	{
		var dot = _canvas.Element("circle");
		dot.SetAttribute("cx", SvgCanvas.N(x));
		dot.SetAttribute("cy", SvgCanvas.N(y));
		dot.SetAttribute("r", SvgCanvas.N(radius));
		dot.SetAttribute("fill", filled ? BoxPlotLineColor(series).ToHex() : "none");
		dot.SetAttribute("stroke", BoxPlotLineColor(series).ToHex());
		return dot;
	}

	/// <summary>
	/// The colour of a box plot's lines: the series stroke, or its fill where it has no visible stroke.
	/// </summary>
	private static Color BoxPlotLineColor(Series series)
		=> series.StrokeColor.A > 0 ? series.StrokeColor : series.FillColor;
}
