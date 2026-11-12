namespace PanoramicData.ChartMagic.Renderers;

internal sealed partial class LegendRenderer
{
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
		LegendBounds bounds,
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
		LegendBounds bounds,
		double inset,
		LegendMetrics metrics)
		=> _canvas.WidthPixels - ImageEdgeGapPixels - (bounds.Left + inset + metrics.SwatchWidth + (metrics.Padding / 2));

	/// <summary>
	/// The vertical centre of each entry in one column.
	/// </summary>
	private List<double> SpreadDown(
		LegendMetrics metrics,
		LegendBounds bounds,
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
		LegendBounds bounds,
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
}
