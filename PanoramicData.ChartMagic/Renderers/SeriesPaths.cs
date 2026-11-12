namespace PanoramicData.ChartMagic.Renderers;

/// <summary>
/// The SVG path text for a run of points: straight segments, or a smooth curve through them.
/// </summary>
internal static class SeriesPaths
{
	/// <summary>
	/// The outline of a series and the same run as segments only, as <see cref="RenderModels.SeriesTrace"/> carries them.
	/// </summary>
	internal static (string Line, string Area) For(SeriesChartType chartType, IReadOnlyList<(double X, double Y)> points)
		=> IsSmooth(chartType) ? Smooth(points) : Straight(points);

	/// <summary>
	/// Whether this chart type draws a curve through its points rather than straight segments.
	/// </summary>
	internal static bool IsSmooth(SeriesChartType chartType)
		=> chartType is SeriesChartType.Spline or SeriesChartType.SplineArea;

	private static (string Line, string Area) Straight(IReadOnlyList<(double X, double Y)> points)
	{
		var line = new StringBuilder();
		var area = new StringBuilder();
		for (var index = 0; index < points.Count; index++)
		{
			// Letter - always M to start, afterwards L.
			line.Append($"{(index == 0 ? "M" : " L")}{points[index].X} {points[index].Y}");
			area.Append($" L{points[index].X} {points[index].Y}");
		}

		return (line.ToString(), area.ToString());
	}

	/// <summary>
	/// A cardinal spline of tension one half, which is what the Microsoft chart control draws: each
	/// segment is a cubic Bezier whose tangents are a sixth of the chord between its neighbours.
	/// The ends borrow the end point as their missing neighbour.
	/// </summary>
	private static (string Line, string Area) Smooth(IReadOnlyList<(double X, double Y)> points)
	{
		if (points.Count == 0)
		{
			return (string.Empty, string.Empty);
		}

		var segments = new StringBuilder();
		for (var index = 0; index < points.Count - 1; index++)
		{
			var before = points[Math.Max(index - 1, 0)];
			var from = points[index];
			var to = points[index + 1];
			var after = points[Math.Min(index + 2, points.Count - 1)];
			segments.Append(
				$" C{SvgCanvas.N(from.X + ((to.X - before.X) / 6))} {SvgCanvas.N(from.Y + ((to.Y - before.Y) / 6))}"
				+ $" {SvgCanvas.N(to.X - ((after.X - from.X) / 6))} {SvgCanvas.N(to.Y - ((after.Y - from.Y) / 6))}"
				+ $" {SvgCanvas.N(to.X)} {SvgCanvas.N(to.Y)}");
		}

		var start = $"{SvgCanvas.N(points[0].X)} {SvgCanvas.N(points[0].Y)}";
		return ($"M{start}{segments}", $" L{start}{segments}");
	}
}
