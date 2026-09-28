using System.Drawing;

namespace PanoramicData.ChartMagic.Renderers;

/// <summary>
/// Funnels, which are drawn as a stack of segments narrowing to a neck rather than against axes.
/// </summary>
/// <param name="canvas">The document this draws into.</param>
/// <remarks>
/// <para>
/// Funnel was a member of <see cref="SeriesChartType"/> with nothing that drew it, so a funnel
/// chart rendered as an empty plot with a value axis and no error.
/// </para>
/// <para>
/// This draws the Microsoft chart control's default funnel, FunnelStyle YIsHeight, as measured
/// against DocMagic on the [List.Graph:] funnel example: the points are stacked top to bottom in
/// order, each segment's height is its share of the total, and the outline narrows in a straight
/// line from the full width at the top to a neck, which the last segment runs down into. Each
/// point takes its own colour, and its label sits in a column to the right of the funnel, joined
/// to the segment's edge by a leader line.
/// </para>
/// </remarks>
internal sealed class FunnelRenderer(SvgCanvas canvas)
{
	private readonly SvgCanvas _canvas = canvas;

	/// <summary>
	/// The neck's width as a fraction of the funnel's top width.
	/// </summary>
	/// <remarks>Measured against DocMagic: 70 pixels against a top width of 1041.</remarks>
	private const double NeckWidthFraction = 0.067;

	/// <summary>
	/// The neck's height as a fraction of the funnel's height.
	/// </summary>
	/// <remarks>Measured against DocMagic: 35 pixels against a height of 510.</remarks>
	private const double NeckHeightFraction = 0.069;

	/// <summary>
	/// The space between a leader line's end and its label, in pixels.
	/// </summary>
	private const double LabelPaddingPixels = 6;

	/// <summary>
	/// The average advance of a character as a fraction of the font size, used to reserve the
	/// label column.
	/// </summary>
	/// <remarks>
	/// Deliberately generous. The Microsoft chart control reserves nothing, so a long label runs off
	/// the edge of the image and is cut off; reserving too much only narrows the funnel.
	/// </remarks>
	private const double CharacterWidthFraction = 0.6;

	/// <summary>
	/// The widest the label column may be, as a fraction of the inner plot width.
	/// </summary>
	private const double MaximumLabelColumnFraction = 0.45;

	/// <summary>
	/// The leader line colour.
	/// </summary>
	/// <remarks>
	/// The Microsoft chart control's CalloutLineColor default. A funnel has no setting of its own for
	/// it, and the pie line colour belongs to pies: DocMagic draws funnel leader lines black
	/// whatever that is set to.
	/// </remarks>
	private static readonly Color LeaderLineColor = Color.Black;

	/// <summary>
	/// Whether this series is drawn as a funnel.
	/// </summary>
	internal static bool IsFunnel(Series series) => series.ChartType == SeriesChartType.Funnel;

	/// <summary>
	/// Draws the funnel: one segment per point, then the labels and their leader lines.
	/// </summary>
	/// <param name="series">The funnel series.</param>
	/// <param name="segments">
	/// The points as slices: the pie slice builder already resolves each point's value, share,
	/// colour, label and legend text, which is everything a segment needs. Its angles are unused.
	/// </param>
	/// <param name="innerPlotNode">The group the funnel is drawn into.</param>
	/// <param name="plotWidth">The inner plot width, in pixels.</param>
	/// <param name="plotHeight">The inner plot height, in pixels.</param>
	internal void Plot(Series series, List<PieSlice> segments, XmlElement innerPlotNode, double plotWidth, double plotHeight)
	{
		if (segments.Count == 0)
		{
			return;
		}

		var funnelNode = _canvas.Group("funnel");
		innerPlotNode.AppendChild(funnelNode);

		var labelled = segments.Exists(segment => segment.Label.Length > 0);
		var labelColumnWidth = labelled
			? Math.Min(
				plotWidth * MaximumLabelColumnFraction,
				(segments.Max(segment => segment.Label.Length) * series.FontSize * CharacterWidthFraction) + LabelPaddingPixels)
			: 0;

		var shape = new FunnelShape(plotWidth - labelColumnWidth, plotHeight);
		var total = segments.Sum(segment => segment.Value);
		var top = 0d;
		var labelStyle = TextStyle.Unstroked(series.FontWeight, series.FontFamily, series.FontSize, series.FontColor);

		var segmentIndex = 0;
		foreach (var segment in segments)
		{
			var bottom = top + (segment.Value / total * plotHeight);

			funnelNode.AppendChild(CreateSegment(shape, segment, top, bottom, segmentIndex));

			if (segment.Label.Length > 0)
			{
				var middle = (top + bottom) / 2;
				funnelNode.AppendChild(
					_canvas.Line(shape.CentreX + shape.HalfWidthAt(middle), middle, shape.Width, middle, LeaderLineColor, 1));
				funnelNode.AppendChild(
					_canvas.Text(
						FormattableString.Invariant($"funnelLabel{segmentIndex}"),
						shape.Width + LabelPaddingPixels,
						middle,
						segment.Label,
						HorizontalAlignment.Left,
						VerticalAlignment.Middle,
						labelStyle));
			}

			top = bottom;
			segmentIndex++;
		}
	}

	/// <summary>
	/// One segment: the part of the funnel outline between two heights, filled in the point colour.
	/// </summary>
	private XmlElement CreateSegment(FunnelShape shape, PieSlice segment, double top, double bottom, int segmentIndex)
	{
		var outline = new List<(double X, double Y)>
		{
			(shape.CentreX - shape.HalfWidthAt(top), top),
			(shape.CentreX + shape.HalfWidthAt(top), top)
		};

		// The segment the neck starts in turns a corner there, on each side.
		var turnsIntoTheNeck = top < shape.NeckTop && bottom > shape.NeckTop;
		if (turnsIntoTheNeck)
		{
			outline.Add((shape.CentreX + shape.NeckHalfWidth, shape.NeckTop));
		}

		outline.Add((shape.CentreX + shape.HalfWidthAt(bottom), bottom));
		outline.Add((shape.CentreX - shape.HalfWidthAt(bottom), bottom));

		if (turnsIntoTheNeck)
		{
			outline.Add((shape.CentreX - shape.NeckHalfWidth, shape.NeckTop));
		}

		var path = _canvas.Element("path");
		path.SetAttribute("id", FormattableString.Invariant($"funnelSegment{segmentIndex}"));
		path.SetAttribute(
			"d",
			"M" + string.Join(" L", outline.Select(p => $"{SvgCanvas.N(p.X)} {SvgCanvas.N(p.Y)}")) + " Z");
		path.SetAttribute("fill", segment.Color.ToHex());
		if (segment.Color.A != 255)
		{
			path.SetAttribute(
				"fill-opacity",
				(segment.Color.A / 255f).ToString("F2", CultureInfo.InvariantCulture));
		}

		return path;
	}

	/// <summary>
	/// The outline every segment is cut from: full width at the top, narrowing in a straight line
	/// to the neck, then straight down.
	/// </summary>
	private sealed record FunnelShape(double Width, double Height)
	{
		internal double CentreX => Width / 2;

		internal double NeckHalfWidth => Width * NeckWidthFraction / 2;

		internal double NeckTop => Height * (1 - NeckHeightFraction);

		internal double HalfWidthAt(double y)
			=> y >= NeckTop
				? NeckHalfWidth
				: (Width / 2) - (((Width / 2) - NeckHalfWidth) * y / NeckTop);
	}
}
