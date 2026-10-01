using System.Drawing;

namespace PanoramicData.ChartMagic.Renderers;

/// <summary>
/// The legend, whether it describes series or pie slices.
/// </summary>
/// <param name="canvas">The document this draws into.</param>
internal sealed class LegendRenderer(SvgCanvas canvas)
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
	private sealed record Placement(Entry Entry, double X, double CentreY, IReadOnlyList<string> Lines);

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
	private (double Left, double Top, double Width, double Height) BoundsFor(Legend legend)
		=> (
			_canvas.WidthPixels * legend.GetCanvasXLocationPercent() / 100,
			_canvas.HeightPixels * (100 - legend.GetCanvasYLocationPercent() - legend.GetCanvasHeightPercent()) / 100,
			_canvas.WidthPixels * legend.GetCanvasWidthPercent() / 100,
			_canvas.HeightPixels * legend.GetCanvasHeightPercent() / 100);

	/// <summary>
	/// One column of entries, spread down the legend.
	/// </summary>
	/// <remarks>
	/// <para>
	/// One row per series, spread down the legend rather than packed together, and left-aligned at
	/// an inset proportional to the legend width. Both measured against the renderer this matches,
	/// which gives each entry an equal share of the legend height: on a 400-pixel legend it spaced
	/// three entries 129 apart and two 193 apart, which is the height less one swatch, divided by
	/// the count.
	/// </para>
	/// <para>
	/// A legend too short for its entries keeps them a line apart rather than drawing them over one
	/// another, and moves them only as far as it must to keep them on the image. A label too wide
	/// takes the inset first and is shortened with an ellipsis only when it would leave the image.
	/// </para>
	/// </remarks>
	private List<Placement> LayOutColumn(
		Legend legend,
		LegendMetrics metrics,
		(double Left, double Top, double Width, double Height) bounds,
		List<Entry> entries)
	{
		var lines = entries.ConvertAll(entry => TextMeasure.Wrap(entry.Text, legend.TextWrapThreshold));
		var widestText = lines.SelectMany(l => l).Select(line => TextMeasure.Width(line, metrics.FontSize, legend.FontWeight)).DefaultIfEmpty(0).Max();
		var inset = InsetFor(metrics, Math.Min(metrics.Width, _canvas.WidthPixels - ImageEdgeGapPixels - bounds.Left), widestText);
		var maximumTextWidth = MaximumTextWidth(bounds, inset, metrics);

		var centres = SpreadDown(metrics, bounds, lines);

		return [.. entries.Select((entry, index) => new Placement(
			entry,
			inset,
			centres[index],
			Shorten(lines[index], maximumTextWidth, metrics.FontSize, legend.FontWeight)))];
	}

	/// <summary>
	/// The inset an entry starts at: the measured fraction of the legend, less whatever a long
	/// label needs to stay inside it.
	/// </summary>
	/// <param name="metrics">The legend's measurements.</param>
	/// <param name="usableWidth">How much of the legend's width is on the image.</param>
	/// <param name="widestText">The widest line of entry text.</param>
	private static double InsetFor(LegendMetrics metrics, double usableWidth, double widestText)
	{
		var standard = Math.Round(metrics.Width * LegendInsetFraction, 2);
		var room = usableWidth - metrics.SwatchWidth - (metrics.Padding / 2) - widestText;
		// Floored, so rounding never takes back the room just measured.
		return Math.Floor(Math.Clamp(room, Math.Min(metrics.Padding / 2, standard), standard) * 100) / 100;
	}

	/// <summary>
	/// How wide a label starting at this inset can be before it leaves the image.
	/// </summary>
	private double MaximumTextWidth(
		(double Left, double Top, double Width, double Height) bounds,
		double inset,
		LegendMetrics metrics)
		=> _canvas.WidthPixels - ImageEdgeGapPixels - (bounds.Left + inset + metrics.SwatchWidth + (metrics.Padding / 2));

	/// <summary>
	/// The vertical centre of each entry in one column.
	/// </summary>
	private List<double> SpreadDown(
		LegendMetrics metrics,
		(double Left, double Top, double Width, double Height) bounds,
		List<IReadOnlyList<string>> lines)
	{
		var count = lines.Count;
		var spacing = (metrics.Height - metrics.SwatchHeight) / Math.Max(count, 1);
		var tallest = lines.Select(l => EntryHeight(metrics, l.Count)).DefaultIfEmpty(metrics.SwatchHeight).Max();
		var minimumSpacing = tallest + (metrics.Padding / 2);
		if (spacing < minimumSpacing)
		{
			spacing = minimumSpacing;
		}

		var blockHeight = (spacing * (count - 1)) + tallest;
		var blockTop = (metrics.Height - blockHeight) / 2;

		// Kept on the image, in the legend's own coordinates.
		blockTop = Math.Clamp(blockTop, -bounds.Top, Math.Max(-bounds.Top, _canvas.HeightPixels - bounds.Top - blockHeight));

		return [.. Enumerable.Range(0, count).Select(index => Math.Round(blockTop + (tallest / 2) + (index * spacing), 2))];
	}

	/// <summary>
	/// Rows of entries, packed and centred, wrapping onto another row when the legend is too
	/// narrow for them all.
	/// </summary>
	/// <remarks>
	/// Entries packed one after another and the row centred, rather than each given an equal share
	/// of the width: that is what the reference render does, and a slot sized without reference to
	/// its contents put the next swatch on top of the previous label. Where one row cannot hold
	/// them the entries continue on the next, rather than running off the legend or being dropped:
	/// the Microsoft chart control replaced them with "...", which leaves a reader no way to tell
	/// which series is which.
	/// </remarks>
	private List<Placement> LayOutRows(
		Legend legend,
		LegendMetrics metrics,
		(double Left, double Top, double Width, double Height) bounds,
		List<Entry> entries)
	{
		var gap = metrics.Padding * 2;
		var available = Math.Max(metrics.Width - (2 * metrics.Padding), metrics.SwatchWidth);
		var maximumTextWidth = available - metrics.SwatchWidth - (metrics.Padding / 2);

		var sized = entries.ConvertAll(entry =>
		{
			var lines = Shorten(TextMeasure.Wrap(entry.Text, legend.TextWrapThreshold), maximumTextWidth, metrics.FontSize, legend.FontWeight);
			var textWidth = lines.Select(line => TextMeasure.Width(line, metrics.FontSize, legend.FontWeight)).DefaultIfEmpty(0).Max();
			return (Entry: entry, Lines: lines, Width: metrics.SwatchWidth + (metrics.Padding / 2) + textWidth);
		});

		var rows = new List<List<(Entry Entry, IReadOnlyList<string> Lines, double Width)>> { new() };
		var rowWidth = 0d;
		foreach (var item in sized)
		{
			var needed = rows[^1].Count == 0 ? item.Width : rowWidth + gap + item.Width;
			if (rows[^1].Count > 0 && needed > available)
			{
				rows.Add([]);
				needed = item.Width;
			}

			rows[^1].Add(item);
			rowWidth = needed;
		}

		var rowHeights = rows.ConvertAll(row => row.Max(item => EntryHeight(metrics, item.Lines.Count)));
		var rowGap = metrics.Padding / 2;
		var blockHeight = rowHeights.Sum() + (rowGap * (rows.Count - 1));
		var top = Math.Clamp(
			(metrics.Height - blockHeight) / 2,
			-bounds.Top,
			Math.Max(-bounds.Top, _canvas.HeightPixels - bounds.Top - blockHeight));

		var placements = new List<Placement>();
		for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
		{
			var row = rows[rowIndex];
			var width = row.Sum(item => item.Width) + (gap * (row.Count - 1));
			var x = Math.Max(metrics.Padding, (metrics.Width - width) / 2);
			var centreY = Math.Round(top + (rowHeights[rowIndex] / 2), 2);

			foreach (var item in row)
			{
				placements.Add(new Placement(item.Entry, Math.Round(x, 2), centreY, item.Lines));
				x += item.Width + gap;
			}

			top += rowHeights[rowIndex] + rowGap;
		}

		return placements;
	}

	/// <summary>
	/// Several columns of entries, as many as the legend height needs.
	/// </summary>
	/// <remarks>
	/// The Microsoft chart control's Table style: entries fill a column top to bottom and continue
	/// in the next, so a short wide legend lists them side by side. While one column holds them all
	/// it is laid out exactly as the Column style is.
	/// </remarks>
	private List<Placement> LayOutTable(
		Legend legend,
		LegendMetrics metrics,
		(double Left, double Top, double Width, double Height) bounds,
		List<Entry> entries)
	{
		var lines = entries.ConvertAll(entry => TextMeasure.Wrap(entry.Text, legend.TextWrapThreshold));
		var tallest = lines.Select(l => EntryHeight(metrics, l.Count)).DefaultIfEmpty(metrics.SwatchHeight).Max();
		var perColumn = Math.Max(1, (int)Math.Floor((metrics.Height + (metrics.Padding / 2)) / (tallest + (metrics.Padding / 2))));
		if (perColumn >= entries.Count)
		{
			return LayOutColumn(legend, metrics, bounds, entries);
		}

		var columns = (int)Math.Ceiling(entries.Count / (double)perColumn);
		var columnWidth = metrics.Width / columns;
		var maximumTextWidth = columnWidth - metrics.Padding - metrics.SwatchWidth - (metrics.Padding / 2);

		var placements = new List<Placement>();
		for (var column = 0; column < columns; column++)
		{
			var inColumn = Enumerable.Range(column * perColumn, Math.Min(perColumn, entries.Count - (column * perColumn))).ToList();
			var centres = SpreadDown(metrics, bounds, [.. inColumn.Select(index => lines[index])]);
			for (var row = 0; row < inColumn.Count; row++)
			{
				var index = inColumn[row];
				placements.Add(new Placement(
					entries[index],
					Math.Round((column * columnWidth) + (metrics.Padding / 2), 2),
					centres[row],
					Shorten(lines[index], maximumTextWidth, metrics.FontSize, legend.FontWeight)));
			}
		}

		return placements;
	}

	private static double EntryHeight(LegendMetrics metrics, int lineCount)
		=> Math.Max(metrics.SwatchHeight, lineCount * metrics.FontSize * LineHeightFraction);

	private static List<string> Shorten(IReadOnlyList<string> lines, double maximumWidth, double fontSize, FontWeight fontWeight)
		=> [.. lines.Select(line => TextMeasure.Fit(line, Math.Max(maximumWidth, 0), fontSize, fontWeight))];

	/// <summary>
	/// Draws one entry: its swatch and its text.
	/// </summary>
	/// <remarks>
	/// A line series is represented by a bar rather than a block, so that the legend distinguishes
	/// a line from a filled area at a glance.
	/// </remarks>
	private void AppendEntry(XmlElement legendNode, LegendMetrics metrics, TextStyle labelStyle, Placement placement)
	{
		var swatchHeight = placement.Entry.IsLine ? Math.Max(2, Math.Round(metrics.SwatchHeight / 4, 2)) : metrics.SwatchHeight;
		var swatchNode = CreateSwatch(
			placement.X,
			Math.Round(placement.CentreY - (swatchHeight / 2), 2),
			metrics.SwatchWidth,
			swatchHeight,
			placement.Entry.Color);
		legendNode.AppendChild(swatchNode);

		var lineHeight = metrics.FontSize * LineHeightFraction;
		var textX = placement.X + metrics.SwatchWidth + (metrics.Padding / 2);
		for (var line = 0; line < placement.Lines.Count; line++)
		{
			var y = placement.CentreY + ((line - ((placement.Lines.Count - 1) / 2.0)) * lineHeight);
			legendNode.AppendChild(
				_canvas.Text(
					line == 0 ? placement.Entry.Id : FormattableString.Invariant($"{placement.Entry.Id}{line}"),
					textX,
					y,
					placement.Lines[line],
					HorizontalAlignment.Left,
					VerticalAlignment.Middle,
					labelStyle));
		}
	}

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
