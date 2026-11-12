namespace PanoramicData.ChartMagic.Renderers;

/// <summary>
/// Radar and polar charts, which are drawn on a circular web rather than against axes.
/// </summary>
/// <param name="canvas">The document this draws into.</param>
/// <remarks>
/// <para>
/// Both put the origin at the centre and run angles clockwise from twelve o'clock. A radar spaces
/// its categories evenly round the circle and joins the series into a closed polygon; a polar
/// takes each point's X value as its angle in degrees and leaves the line open.
/// </para>
/// <para>
/// The geometry is the conventional one and has not been measured against the Microsoft chart
/// control, unlike the pie and funnel renderers.
/// </para>
/// </remarks>
internal sealed partial class RadialRenderer(SvgCanvas canvas)
{
	private readonly SvgCanvas _canvas = canvas;

	/// <summary>
	/// The share of the shorter side of the plot the rings span, leaving the rest for the labels.
	/// </summary>
	private const double RingFraction = 0.75;

	/// <summary>
	/// Whether this chart type is drawn on a circular web rather than against axes.
	/// </summary>
	internal static bool IsRadial(Series series)
		=> series.ChartType is SeriesChartType.Radar or SeriesChartType.Polar;

	/// <summary>
	/// Draws the web, then each radial series over it.
	/// </summary>
	internal void Plot(Chart chart, PlotGeometry geometry, XmlElement innerPlotNode)
	{
		var radialSeries = chart.Series.Where(IsRadial).ToList();
		var isPolar = radialSeries[0].ChartType == SeriesChartType.Polar;
		var scale = new RadialScale(
			geometry.Width / 2,
			geometry.Height / 2,
			Math.Min(geometry.Width, geometry.Height) / 2 * RingFraction,
			geometry.YDisplayStart,
			geometry.YDisplayEnd - geometry.YDisplayStart);

		var node = _canvas.Group(isPolar ? "polar" : "radar");
		innerPlotNode.AppendChild(node);
		node.AppendChild(PlotWeb(chart, geometry, scale, isPolar));

		for (var index = 0; index < radialSeries.Count; index++)
		{
			node.AppendChild(PlotSeries(radialSeries[index], index, geometry, scale, isPolar));
		}
	}

	/// <summary>
	/// One series: a closed polygon over the categories for a radar, an open line over its points
	/// for a polar.
	/// </summary>
	private XmlElement PlotSeries(Series series, int index, PlotGeometry geometry, RadialScale scale, bool isPolar)
	{
		var vertices = isPolar
			? PolarVertices(series, scale)
			: RadarVertices(series, geometry, scale);

		var path = _canvas.Element("path");
		path.SetAttribute("d", PathOf(vertices, close: !isPolar));
		path.SetStyle(series, applyFill: !isPolar);

		var seriesNode = _canvas.Group($"series{index}");
		seriesNode.AppendChild(path);
		return seriesNode;
	}

	private static List<(double X, double Y)> PolarVertices(Series series, RadialScale scale)
		=> [.. series.Points
			.Where(point => point.YValue is not null)
			.Select(point => scale.PointAt(point.XValue, scale.RadiusOf(point.YValue!.Value)))];

	/// <summary>
	/// One vertex per category, at the series' value there, or at the centre where it has none.
	/// </summary>
	private static List<(double X, double Y)> RadarVertices(Series series, PlotGeometry geometry, RadialScale scale)
		=> [.. geometry.Categories.Select((category, index) => scale.PointAt(
			AngleOfCategory(index, geometry.Categories.Count),
			scale.RadiusOf(series.Points.FirstOrDefault(point => point.XValue == category)?.YValue ?? scale.Start)))];

	private static double AngleOfCategory(int index, int count) => 360d * index / count;

	private static string PathOf(List<(double X, double Y)> vertices, bool close)
		=> "M" + string.Join(" L", vertices.Select(v => $"{SvgCanvas.N(v.X)} {SvgCanvas.N(v.Y)}")) + (close ? " Z" : string.Empty);
}
