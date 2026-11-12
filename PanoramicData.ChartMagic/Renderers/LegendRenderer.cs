using System.Drawing;

namespace PanoramicData.ChartMagic.Renderers;

/// <summary>
/// The legend, whether it describes series or pie slices.
/// </summary>
/// <param name="canvas">The document this draws into.</param>
internal sealed partial class LegendRenderer(SvgCanvas canvas)
{
	private readonly SvgCanvas _canvas = canvas;

	/// <summary>
	/// How far in from the left of the legend an entry starts, as a fraction of its width.
	/// </summary>
	/// <remarks>
	/// Measured: 18 pixels into a 144-wide legend and 9 into an 86-wide one, so it scales with the
	/// legend rather than with the font. Scaling matters beyond fidelity - a fixed inset made
	/// LegendWidthPercent change nothing visible, so a chart could ask for a wider legend and get
	/// the same one.
	/// </remarks>
	private const double LegendInsetFraction = 0.12;

	/// <summary>The space kept between legend text and the edge of the image.</summary>
	private const double ImageEdgeGapPixels = 1;

	/// <summary>The height of one line of legend text, as a multiple of the font size.</summary>
	private const double LineHeightFraction = 1.15;

	/// <summary>
	/// One legend entry: a swatch and its text.
	/// </summary>
	/// <param name="Id">The id the entry's text node is given.</param>
	/// <param name="Text">The entry text, before any wrapping or shortening.</param>
	/// <param name="Color">The swatch colour.</param>
	/// <param name="IsLine">Whether the swatch is drawn as a bar, for a line series.</param>
	private sealed record Entry(string Id, string Text, Color Color, bool IsLine);

	/// <summary>
	/// An entry laid out: where its swatch goes, and the lines of text beside it.
	/// </summary>
	private sealed record Placement(Entry Item, double X, double CentreY, IReadOnlyList<string> LabelLines);

	/// <summary>Where the legend sits on the image, in pixels.</summary>
	private readonly record struct LegendBounds(double Left, double Top, double Width, double Height);

	/// <summary>An entry with its wrapped, shortened text and the width it takes in a row.</summary>
	private sealed record SizedEntry(Entry Item, IReadOnlyList<string> Lines, double Width);

	/// <summary>
	/// Draws the legend: one swatch and label per series, inside the legend box.
	/// </summary>
	/// <remarks>
	/// Issue #35: laid out in the legend's own pixel space. The previous version worked in
	/// percentages and then passed them through a helper that scaled by the legend width a
	/// second time, collapsing the spacing to a fraction of what was intended - which is why
	/// three labels landed almost on top of one another. Swatch sizes were percentages of the
	/// whole image rather than of the legend, so they drifted with the output size.
	/// </remarks>
	internal void PlotLegends(Chart chart, XmlElement chartBackgroundAreaNode)
	{
		if (chart.Legends.Count == 0 || chart.Series.Count == 0)
		{
			return;
		}

		var legend = chart.Legends[0];
		var entries = chart.Series
			.Select((series, index) => new Entry(
				$"legendSeries{index}Text",
				LegendTextFor(series),
				SwatchColorFor(series),
				IsLine(series.ChartType)))
			.ToList();

		if (IsReversed(legend.ItemOrder, chart.Series))
		{
			entries.Reverse();
		}

		Plot(chart, legend, entries, chartBackgroundAreaNode);
	}

	/// <summary>
	/// The legend for a pie, which describes slices rather than series.
	/// </summary>
	internal void PlotPieLegend(Chart chart, List<PieSlice> slices, XmlElement chartBackgroundAreaNode)
	{
		if (chart.Legends.Count == 0 || slices.Count == 0)
		{
			return;
		}

		var entries = slices
			.Select((slice, index) => new Entry(
				FormattableString.Invariant($"legendSlice{index}Text"),
				slice.LegendText,
				slice.Color,
				IsLine: false))
			.ToList();

		Plot(chart, chart.Legends[0], entries, chartBackgroundAreaNode);
	}

	/// <summary>
	/// Whether the entries are listed in reverse, which for a stacked chart is the order the stack
	/// reads in from the top.
	/// </summary>
	/// <remarks>
	/// Measured against the Microsoft chart control: a stacked column chart of CPU, Memory and Disk
	/// lists Disk, Memory, CPU, and the same series as plain columns list CPU, Memory, Disk.
	/// </remarks>
	private static bool IsReversed(LegendItemOrder itemOrder, SeriesCollection series) => itemOrder switch
	{
		LegendItemOrder.ReversedSeriesOrder => true,
		LegendItemOrder.SameAsSeriesOrder => false,
		_ => series.Any(s => PlotGeometry.IsStacked(s.ChartType))
	};

	private void Plot(Chart chart, Legend legend, List<Entry> entries, XmlElement chartBackgroundAreaNode)
	{
		var legendNode = _canvas.PositionedGroup(legend, "legend", chart.ChartBackgroundArea);
		chartBackgroundAreaNode.AppendChild(legendNode);

		var metrics = MetricsFor(legend);
		var bounds = BoundsFor(legend);

		var placements = legend.Style switch
		{
			LegendStyle.Row => LayOutRows(legend, metrics, bounds, entries),
			LegendStyle.Table => LayOutTable(legend, metrics, bounds, entries),
			_ => LayOutColumn(legend, metrics, bounds, entries)
		};

		var labelStyle = LabelStyleFor(legend);
		foreach (var placement in placements)
		{
			AppendEntry(legendNode, metrics, labelStyle, placement);
		}
	}

	/// <summary>
	/// Where the legend sits on the image, in pixels.
	/// </summary>
	private LegendBounds BoundsFor(Legend legend)
		=> new(
			_canvas.WidthPixels * legend.GetCanvasXLocationPercent() / 100,
			_canvas.HeightPixels * (100 - legend.GetCanvasYLocationPercent() - legend.GetCanvasHeightPercent()) / 100,
			_canvas.WidthPixels * legend.GetCanvasWidthPercent() / 100,
			_canvas.HeightPixels * legend.GetCanvasHeightPercent() / 100);
}
