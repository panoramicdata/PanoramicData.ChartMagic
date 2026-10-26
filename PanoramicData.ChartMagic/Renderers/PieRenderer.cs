using System.Drawing;

namespace PanoramicData.ChartMagic.Renderers;

/// <summary>
/// Pies and doughnuts, which are drawn as a ring of wedges rather than against axes.
/// </summary>
/// <param name="canvas">The document this draws into.</param>
internal sealed class PieRenderer(SvgCanvas canvas)
{
	private readonly SvgCanvas _canvas = canvas;

	/// <summary>
	/// The offset that puts a pie start angle of zero at three o clock.
	/// </summary>
	private const double QuarterTurnDegrees = 90;

	/// <summary>
	/// Whether this chart type is drawn as a ring of slices rather than against axes.
	/// </summary>
	internal static bool IsPie(Series series)
		=> series.ChartType is SeriesChartType.Pie or SeriesChartType.Doughnut;

	/// <summary>
	/// Draws a pie or doughnut: one wedge per slice, centred in the chart area, with the slice
	/// labels inside, outside on a leader line, or not at all.
	/// </summary>
	/// <remarks>
	/// Angles run clockwise from twelve o'clock, as they do in the Microsoft chart control, so a
	/// start angle of zero puts the first slice boundary at the top.
	/// </remarks>
	internal void Plot(
		Series series,
		List<PieSlice> slices,
		XmlElement innerPlotNode,
		double plotWidth,
		double plotHeight,
		double imageTop = double.NegativeInfinity,
		double imageBottom = double.PositiveInfinity)
	{
		if (slices.Count == 0)
		{
			return;
		}

		var pieNode = _canvas.Group("pie");
		innerPlotNode.AppendChild(pieNode);

		// Centred in the inner plot, not the chart area. Measured against DocMagic: for a chart
		// area 468x400 with an inner plot inset 10% left and 10% vertically, its pie centre was
		// the inner plot centre and its diameter exactly 0.95 of the shorter inner plot side.
		// Drawing in the chart area instead put the pie 24px left and 20px high, and 8% small.
		var centreX = plotWidth / 2;
		var centreY = plotHeight / 2;

		// The pie is the same size whether its labels are inside or out. Shrinking it to make room
		// for outside labels seems the considerate thing to do, but the renderer this matches does
		// not: measured at 283 pixels across with outside labels and 285 with inside ones, where
		// shrinking gave 225. The labels are allowed to overflow instead, which is what the
		// reference render does - one of them sits outside the chart area entirely.
		var radius = Math.Min(plotWidth, plotHeight) / 2 * 0.95;

		// The percentage is the width of the RING, not the size of the hole, so the hole is what is
		// left over. This was the other way round, which inverted every doughnut: the default of 60
		// drew a 60% hole where the Microsoft chart control draws a 40% one, and asking for a thin
		// 30% ring gave a fat one.
		//
		// Measured on two doughnuts against the reference render, at 285 pixels across: the default
		// left a hole 116 wide (40.7%) and a specified 30 left one 202 wide (70.9%). Both are
		// 100 minus the percentage, and neither is the percentage itself.
		var innerRadius = series.ChartType == SeriesChartType.Doughnut
			? radius * (100 - Math.Clamp(series.DoughnutRadiusPercent ?? 60, 1, 100)) / 100
			: 0;

		foreach (var slice in slices)
		{
			pieNode.AppendChild(CreateWedge(series, slice, centreX, centreY, radius, innerRadius));
		}

		if (series.PieLabelStyle == PieLabelStyle.Disabled)
		{
			return;
		}

		PlotPieLabels(series, slices, pieNode, (centreX, centreY, radius, innerRadius), (imageTop, imageBottom));
	}

	/// <summary>
	/// One wedge of a pie or doughnut, filled in the slice colour and outlined in the series one.
	/// </summary>
	private XmlElement CreateWedge(
		Series series,
		PieSlice slice,
		double centreX,
		double centreY,
		double radius,
		double innerRadius)
	{
		var wedge = _canvas.Element("path");
		wedge.SetAttribute("d", WedgePath(centreX, centreY, radius, innerRadius, slice));
		wedge.SetAttribute("fill", slice.Color.ToHex());
		if (slice.Color.A != 255)
		{
			wedge.SetAttribute(
				"fill-opacity",
				(slice.Color.A / 255f).ToString("F2", CultureInfo.InvariantCulture));
		}

		if (series.StrokeColor != Colors.Transparent && series.StrokeWidth > 0)
		{
			wedge.SetAttribute("stroke", series.StrokeColor.ToHex());
			wedge.SetAttribute("stroke-width", series.StrokeWidth.ToString(CultureInfo.InvariantCulture));
		}

		return wedge;
	}

	/// <summary>
	/// The slice labels, drawn after every wedge so that a label is never covered by the next
	/// slice.
	/// </summary>
	private void PlotPieLabels(
		Series series,
		List<PieSlice> slices,
		XmlElement pieNode,
		(double CentreX, double CentreY, double Radius, double InnerRadius) pie,
		(double Top, double Bottom) image)
	{
		var (centreX, centreY, radius, innerRadius) = pie;
		var labelStyle = TextStyle.Unstroked(series.FontWeight, series.FontFamily, series.FontSize, series.FontColor);
		var labelled = slices.Where(s => s.Label.Length > 0).ToList();

		var placements = series.PieLabelStyle == PieLabelStyle.Outside
			? OutsidePieLabels(series, labelled, pieNode, (centreX, centreY, radius), image)
			: [.. labelled.Select(slice => InsidePieLabelPosition(centreX, centreY, radius, innerRadius, slice))];

		foreach (var (slice, (at, alignment)) in labelled.Zip(placements))
		{
			if (series.LabelBackColor is { A: > 0 } background)
			{
				pieNode.AppendChild(
					_canvas.TextBackground(at.X, at.Y, slice.Label, alignment, VerticalAlignment.Middle, labelStyle, background));
			}

			pieNode.AppendChild(
				_canvas.Text(
					FormattableString.Invariant($"pieLabel{slice.StartAngleDegrees:F2}"),
					at.X,
					at.Y,
					slice.Label,
					alignment,
					VerticalAlignment.Middle,
					labelStyle));
		}
	}

	/// <summary>How far a leader line runs out from the rim before it turns towards its label.</summary>
	private const double LeaderElbowPixels = 12;

	/// <summary>The length of a leader line's level run to its label, at least.</summary>
	private const double LeaderRunPixels = 10;

	/// <summary>The space between the end of a leader line and its label.</summary>
	private const double LeaderLabelGapPixels = 3;

	/// <summary>The space kept between a label and the rim, at the closest.</summary>
	private const double LabelRimClearancePixels = 2;

	/// <summary>
	/// The positions of labels drawn outside the pie, after drawing the leader lines that join
	/// each to its slice.
	/// </summary>
	/// <remarks>
	/// MS-26610: as the Windows renderer draws them. Each line runs out from the middle of the
	/// slice's arc, then level to its label, which reads away from the pie on its own side. Labels
	/// on one side are kept a line apart, and each is held far enough out that no part of it lies
	/// over the pie and inside the image. A transparent line colour draws no lines.
	/// </remarks>
	private List<((double X, double Y) At, HorizontalAlignment Alignment)> OutsidePieLabels(
		Series series,
		List<PieSlice> slices,
		XmlElement pieNode,
		(double CentreX, double CentreY, double Radius) pie,
		(double Top, double Bottom) image)
	{
		var (centreX, centreY, radius) = pie;
		var labels = slices
			.Select(slice => new OutsideLabel(
				PointOnCircle(centreX, centreY, radius, slice.MidAngleDegrees),
				PointOnCircle(centreX, centreY, radius + LeaderElbowPixels, slice.MidAngleDegrees),
				centreX))
			.ToList();

		foreach (var side in labels.GroupBy(label => label.OnTheRight))
		{
			SpaceApart([.. side.OrderBy(label => label.Elbow.Y)], series.FontSize * 1.2, image);
		}

		var placements = new List<((double X, double Y) At, HorizontalAlignment Alignment)>();
		foreach (var label in labels)
		{
			// The closest any part of the label comes to the centre, vertically.
			var nearestDy = Math.Max(0, Math.Abs(label.Y - centreY) - (series.FontSize / 2));
			var clearance = radius + LabelRimClearancePixels;
			var rimHalfWidth = nearestDy >= clearance ? 0 : Math.Sqrt((clearance * clearance) - (nearestDy * nearestDy));

			var sign = label.OnTheRight ? 1 : -1;
			var lineEndX = label.OnTheRight
				? Math.Max(label.Elbow.X + LeaderRunPixels, centreX + rimHalfWidth)
				: Math.Min(label.Elbow.X - LeaderRunPixels, centreX - rimHalfWidth);

			if (series.PieLineColor.A > 0)
			{
				pieNode.AppendChild(LeaderLine(series.PieLineColor, label.Rim, label.Elbow, (lineEndX, label.Y)));
			}

			placements.Add((
				(lineEndX + (sign * LeaderLabelGapPixels), label.Y),
				label.OnTheRight ? HorizontalAlignment.Left : HorizontalAlignment.Right));
		}

		return placements;
	}

	/// <summary>
	/// Labels on one side, top to bottom, pushed a line apart downwards, then back up from the bottom
	/// of the image where that ran off it.
	/// </summary>
	private static void SpaceApart(List<OutsideLabel> ordered, double lineHeight, (double Top, double Bottom) image)
	{
		var previous = image.Top + (lineHeight / 2) - lineHeight;
		foreach (var label in ordered)
		{
			label.Y = Math.Max(label.Elbow.Y, previous + lineHeight);
			previous = label.Y;
		}

		var next = image.Bottom - (lineHeight / 2) + lineHeight;
		for (var i = ordered.Count - 1; i >= 0; i--)
		{
			ordered[i].Y = Math.Min(ordered[i].Y, next - lineHeight);
			next = ordered[i].Y;
		}
	}

	private XmlElement LeaderLine(Color color, params (double X, double Y)[] points)
	{
		var line = _canvas.Element("polyline");
		line.SetAttribute("class", "pieLeader");
		line.SetAttribute("points", string.Join(" ", points.Select(point => $"{SvgCanvas.N(point.X)},{SvgCanvas.N(point.Y)}")));
		line.SetAttribute("fill", "none");
		line.SetAttribute("stroke", color.ToHex());
		line.SetAttribute("stroke-width", "1");
		return line;
	}

	/// <summary>A label drawn outside the pie, while its height is being settled.</summary>
	private sealed class OutsideLabel((double X, double Y) rim, (double X, double Y) elbow, double centreX)
	{
		internal (double X, double Y) Rim { get; } = rim;

		internal (double X, double Y) Elbow { get; } = elbow;

		internal bool OnTheRight { get; } = rim.X >= centreX;

		internal double Y { get; set; } = elbow.Y;
	}

	/// <summary>
	/// Where a label drawn on the pie goes: midway through the ring for a doughnut, and two thirds
	/// out for a pie, which is where the wedge is widest.
	/// </summary>
	private static ((double X, double Y) At, HorizontalAlignment Alignment) InsidePieLabelPosition(
		double centreX,
		double centreY,
		double radius,
		double innerRadius,
		PieSlice slice)
	{
		var labelRadius = innerRadius > 0 ? (radius + innerRadius) / 2 : radius * 0.7;
		return (PointOnCircle(centreX, centreY, labelRadius, slice.MidAngleDegrees), HorizontalAlignment.Center);
	}

	/// <summary>
	/// The path for one wedge: a filled sector for a pie, or a band between two radii for a
	/// doughnut.
	/// </summary>
	private static string WedgePath(double centreX, double centreY, double radius, double innerRadius, PieSlice slice)
	{
		// A single slice covering the whole circle cannot be drawn as one arc, because its start
		// and end points coincide and the arc becomes a no-op. Two half arcs draw it correctly.
		var sweep = Math.Min(slice.SweepAngleDegrees, 360);
		if (sweep >= 359.999)
		{
			return FullRingPath(centreX, centreY, radius, innerRadius);
		}

		var start = slice.StartAngleDegrees;
		var end = start + sweep;
		var largeArc = sweep > 180 ? 1 : 0;

		var outerStart = PointOnCircle(centreX, centreY, radius, start);
		var outerEnd = PointOnCircle(centreX, centreY, radius, end);

		if (innerRadius <= 0)
		{
			return $"M{SvgCanvas.N(centreX)} {SvgCanvas.N(centreY)} L{SvgCanvas.N(outerStart.X)} {SvgCanvas.N(outerStart.Y)} "
				+ $"A{SvgCanvas.N(radius)} {SvgCanvas.N(radius)} 0 {largeArc} 1 {SvgCanvas.N(outerEnd.X)} {SvgCanvas.N(outerEnd.Y)} Z";
		}

		var innerEnd = PointOnCircle(centreX, centreY, innerRadius, end);
		var innerStart = PointOnCircle(centreX, centreY, innerRadius, start);

		return $"M{SvgCanvas.N(outerStart.X)} {SvgCanvas.N(outerStart.Y)} "
			+ $"A{SvgCanvas.N(radius)} {SvgCanvas.N(radius)} 0 {largeArc} 1 {SvgCanvas.N(outerEnd.X)} {SvgCanvas.N(outerEnd.Y)} "
			+ $"L{SvgCanvas.N(innerEnd.X)} {SvgCanvas.N(innerEnd.Y)} "
			+ $"A{SvgCanvas.N(innerRadius)} {SvgCanvas.N(innerRadius)} 0 {largeArc} 0 {SvgCanvas.N(innerStart.X)} {SvgCanvas.N(innerStart.Y)} Z";
	}

	private static string FullRingPath(double centreX, double centreY, double radius, double innerRadius)
	{
		var top = PointOnCircle(centreX, centreY, radius, 0);
		var bottom = PointOnCircle(centreX, centreY, radius, 180);
		var outer = $"M{SvgCanvas.N(top.X)} {SvgCanvas.N(top.Y)} "
			+ $"A{SvgCanvas.N(radius)} {SvgCanvas.N(radius)} 0 1 1 {SvgCanvas.N(bottom.X)} {SvgCanvas.N(bottom.Y)} "
			+ $"A{SvgCanvas.N(radius)} {SvgCanvas.N(radius)} 0 1 1 {SvgCanvas.N(top.X)} {SvgCanvas.N(top.Y)} Z";

		if (innerRadius <= 0)
		{
			return outer;
		}

		// The hole is drawn the other way round, so that the default fill rule leaves it empty.
		var innerTop = PointOnCircle(centreX, centreY, innerRadius, 0);
		var innerBottom = PointOnCircle(centreX, centreY, innerRadius, 180);
		return outer
			+ $" M{SvgCanvas.N(innerTop.X)} {SvgCanvas.N(innerTop.Y)} "
			+ $"A{SvgCanvas.N(innerRadius)} {SvgCanvas.N(innerRadius)} 0 1 0 {SvgCanvas.N(innerBottom.X)} {SvgCanvas.N(innerBottom.Y)} "
			+ $"A{SvgCanvas.N(innerRadius)} {SvgCanvas.N(innerRadius)} 0 1 0 {SvgCanvas.N(innerTop.X)} {SvgCanvas.N(innerTop.Y)} Z";
	}

	/// <summary>
	/// A point on a circle, at an angle measured clockwise from twelve o'clock.
	/// </summary>
	private static (double X, double Y) PointOnCircle(double centreX, double centreY, double radius, double angleDegrees)
	{
		// A start angle of zero puts the first slice boundary at three o clock, not twelve.
		// Established by rendering four equal quarters through both renderers: the Microsoft
		// chart control put the first quarter between three and six o clock.
		var radians = ToRadians(angleDegrees + QuarterTurnDegrees);
		return (centreX + (radius * Math.Sin(radians)), centreY - (radius * Math.Cos(radians)));
	}

	private static double ToRadians(double degrees) => degrees * Math.PI / 180;
}
