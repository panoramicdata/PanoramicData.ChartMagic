using System.Drawing;
using static PanoramicData.ChartMagic.Renderers.AxisTicks;

namespace PanoramicData.ChartMagic.Renderers;

internal sealed partial class RadialRenderer
{
	/// <summary>
	/// The degrees between a polar chart's spokes.
	/// </summary>
	private const double PolarSpokeDegrees = 30;

	/// <summary>
	/// The space between the rim and the label beyond it, in pixels.
	/// </summary>
	private const double LabelGapPixels = 6;

	/// <summary>
	/// The rings at each value tick, the spokes to the rim, and the labels.
	/// </summary>
	private XmlElement PlotWeb(Chart chart, PlotGeometry geometry, RadialScale scale, bool isPolar)
	{
		var webNode = _canvas.Group(isPolar ? "polarWeb" : "radarWeb");
		var yAxis = chart.ChartArea.YAxis;
		var labelStyle = TextStyle.Unstroked(yAxis.FontWeight, yAxis.FontFamily, yAxis.FontSize, yAxis.FontColor);
		List<double> spokeAngles = isPolar
			? [.. Enumerable.Range(0, (int)(360 / PolarSpokeDegrees)).Select(step => step * PolarSpokeDegrees)]
			: [.. Enumerable.Range(0, geometry.Categories.Count).Select(index => AngleOfCategory(index, geometry.Categories.Count))];

		foreach (var value in YAxisTickValues(chart, geometry).Where(value => value > scale.Start))
		{
			webNode.AppendChild(CreateRing(scale, scale.RadiusOf(value), spokeAngles, isPolar, yAxis));
			webNode.AppendChild(CreateTickLabel(chart, geometry, scale, value, labelStyle));
		}

		for (var index = 0; index < spokeAngles.Count; index++)
		{
			var end = scale.PointAt(spokeAngles[index], scale.Radius);
			webNode.AppendChild(_canvas.Line(scale.CentreX, scale.CentreY, end.X, end.Y, yAxis.MajorGridColor, yAxis.GridWidth, yAxis.MajorGridDashStyle));
			webNode.AppendChild(CreateSpokeLabel(geometry, scale, spokeAngles[index], index, isPolar, labelStyle));
		}

		return webNode;
	}

	/// <summary>
	/// A circle for a polar chart, or a polygon with a vertex on every spoke for a radar.
	/// </summary>
	private XmlElement CreateRing(RadialScale scale, double distance, List<double> spokeAngles, bool isPolar, AxisArea yAxis)
	{
		XmlElement ring;
		if (isPolar)
		{
			ring = _canvas.Element("circle");
			ring.SetAttribute("cx", SvgCanvas.N(scale.CentreX));
			ring.SetAttribute("cy", SvgCanvas.N(scale.CentreY));
			ring.SetAttribute("r", SvgCanvas.N(distance));
		}
		else
		{
			ring = _canvas.Element("path");
			ring.SetAttribute("d", PathOf([.. spokeAngles.Select(angle => scale.PointAt(angle, distance))], close: true));
		}

		ring.SetAttribute("fill", "none");
		ring.SetAttribute("stroke", yAxis.MajorGridColor.ToHex());
		ring.SetAttribute("stroke-width", SvgCanvas.N(yAxis.GridWidth));
		return ring;
	}

	/// <summary>
	/// The value of a ring, set just right of the upward spoke.
	/// </summary>
	private XmlElement CreateTickLabel(Chart chart, PlotGeometry geometry, RadialScale scale, double value, TextStyle style)
		=> _canvas.Text(
			FormattableString.Invariant($"radialValue{value}"),
			scale.CentreX + 2,
			scale.CentreY - scale.RadiusOf(value),
			AxisLabelText.YAxisLabel(chart, geometry, value),
			HorizontalAlignment.Left,
			VerticalAlignment.Bottom,
			style);

	/// <summary>
	/// The category name for a radar, or the angle for a polar, beyond the end of a spoke.
	/// </summary>
	private XmlElement CreateSpokeLabel(PlotGeometry geometry, RadialScale scale, double angle, int index, bool isPolar, TextStyle style)
	{
		var anchor = scale.PointAt(angle, scale.Radius + LabelGapPixels);
		var radians = angle * Math.PI / 180;
		var text = isPolar
			? angle.ToString(CultureInfo.InvariantCulture)
			: geometry.CategoryLabel(geometry.Categories[index]) ?? geometry.Categories[index].ToString(CultureInfo.InvariantCulture);
		return _canvas.Text(
			FormattableString.Invariant($"radialLabel{index}"),
			anchor.X,
			anchor.Y,
			text,
			Math.Sin(radians) switch { > 0.1 => HorizontalAlignment.Left, < -0.1 => HorizontalAlignment.Right, _ => HorizontalAlignment.Center },
			Math.Cos(radians) switch { > 0.1 => VerticalAlignment.Bottom, < -0.1 => VerticalAlignment.Top, _ => VerticalAlignment.Middle },
			style);
	}
}
