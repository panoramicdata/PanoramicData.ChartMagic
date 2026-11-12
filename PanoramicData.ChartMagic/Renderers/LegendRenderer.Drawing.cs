using System.Drawing;

namespace PanoramicData.ChartMagic.Renderers;

internal sealed partial class LegendRenderer
{
	/// <summary>
	/// Draws one entry: its swatch and its text.
	/// </summary>
	/// <remarks>
	/// A line series is represented by a thin line, with its marker where it has one, rather than
	/// a block, as Windows draws it and so that the legend tells a line from a filled area.
	/// </remarks>
	private void AppendEntry(XmlElement legendNode, LegendMetrics metrics, TextStyle labelStyle, Placement placement)
	{
		var swatchHeight = placement.Item.IsLine ? LineSwatchHeightPixels : metrics.SwatchHeight;
		var swatchNode = CreateSwatch(
			placement.X,
			Math.Round(placement.CentreY - (swatchHeight / 2), 2),
			metrics.SwatchWidth,
			swatchHeight,
			placement.Item.Color);
		legendNode.AppendChild(swatchNode);
		if (placement.Item.MarkerId is { } markerId)
		{
			legendNode.AppendChild(_markers.CreateMarkerReference(markerId, placement.X + (metrics.SwatchWidth / 2), placement.CentreY));
		}

		var lineHeight = metrics.FontSize * LineHeightFraction;
		var textX = placement.X + metrics.SwatchWidth + (metrics.Padding / 2);
		for (var line = 0; line < placement.LabelLines.Count; line++)
		{
			var y = placement.CentreY + ((line - ((placement.LabelLines.Count - 1) / 2.0)) * lineHeight);
			legendNode.AppendChild(
				_canvas.Text(
					line == 0 ? placement.Item.Id : FormattableString.Invariant($"{placement.Item.Id}{line}"),
					textX,
					y,
					placement.LabelLines[line],
					HorizontalAlignment.Left,
					VerticalAlignment.Middle,
					labelStyle));
		}
	}

	/// <summary>The height of the line a line series is represented by.</summary>
	private const double LineSwatchHeightPixels = 2;

	/// <summary>The id of the marker a line series draws on its swatch, or null where it has none.</summary>
	private static string? MarkerIdFor(Series series, int index)
		=> IsLine(series.ChartType) && series.MarkerStyle != MarkerStyle.None
			? FormattableString.Invariant($"series{index}Marker")
			: null;

	/// <summary>
	/// A legend swatch rectangle, filled but not outlined.
	/// </summary>
	private XmlElement CreateSwatch(double x, double y, double width, double height, Color color)
	{
		var swatchNode = _canvas.Element("rect");
		swatchNode.SetAttribute("x", x.ToString(CultureInfo.InvariantCulture));
		swatchNode.SetAttribute("y", y.ToString(CultureInfo.InvariantCulture));
		swatchNode.SetAttribute("width", width.ToString(CultureInfo.InvariantCulture));
		swatchNode.SetAttribute("height", height.ToString(CultureInfo.InvariantCulture));
		swatchNode.SetAttribute("fill", color.ToHex());
		if (color.A != 255)
		{
			swatchNode.SetAttribute(
				"fill-opacity",
				(color.A / 255f).ToString("F2", CultureInfo.InvariantCulture));
		}

		return swatchNode;
	}

	/// <summary>
	/// The colour a series' swatch is drawn in: a line carries its identity in its stroke, a filled
	/// series in its fill.
	/// </summary>
	private static Color SwatchColorFor(Series series)
		=> IsLine(series.ChartType)
			? series.StrokeColor
			: series.FillColor != Colors.Transparent ? series.FillColor : series.StrokeColor;

	private static bool IsLine(SeriesChartType chartType) => chartType
		is SeriesChartType.Line
		or SeriesChartType.FastLine
		or SeriesChartType.Spline
		or SeriesChartType.StepLine;

	/// <summary>
	/// The pixel measurements this legend is laid out in.
	/// </summary>
	private LegendMetrics MetricsFor(Legend legend)
		=> LegendMetrics.For(
			_canvas.WidthPixels * legend.GetCanvasWidthPercent() / 100,
			_canvas.HeightPixels * legend.GetCanvasHeightPercent() / 100,
			legend.FontSize);

	/// <summary>
	/// The style a legend label is drawn in.
	/// </summary>
	private static TextStyle LabelStyleFor(Legend legend)
		=> TextStyle.Unstroked(legend.FontWeight, legend.FontFamily, legend.FontSize, legend.FontColor);

	/// <summary>
	/// A legend entry's text: its legend text where it has one, and its name otherwise.
	/// </summary>
	private static string LegendTextFor(Series series)
		=> series.LegendText is { Length: > 0 } ? series.LegendText : series.Name;
}
