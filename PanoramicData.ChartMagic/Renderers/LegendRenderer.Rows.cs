namespace PanoramicData.ChartMagic.Renderers;

internal sealed partial class LegendRenderer
{
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
		LegendBounds bounds,
		List<Entry> entries)
	{
		var gap = metrics.Padding * 2;
		var available = Math.Max(metrics.Width - (2 * metrics.Padding), metrics.SwatchWidth);
		var maximumTextWidth = available - metrics.SwatchWidth - (metrics.Padding / 2);

		var sized = entries.ConvertAll(entry =>
		{
			var lines = Shorten(TextMeasure.Wrap(entry.Text, legend.TextWrapThreshold), maximumTextWidth, metrics.FontSize, legend.FontWeight);
			var textWidth = lines.Select(line => TextMeasure.Width(line, metrics.FontSize, legend.FontWeight)).DefaultIfEmpty(0).Max();
			return new SizedEntry(entry, lines, metrics.SwatchWidth + (metrics.Padding / 2) + textWidth);
		});

		var rows = PackRows(sized, gap, available);
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
				placements.Add(new Placement(item.Item, Math.Round(x, 2), centreY, item.Lines));
				x += item.Width + gap;
			}

			top += rowHeights[rowIndex] + rowGap;
		}

		return placements;
	}

	/// <summary>
	/// Fills rows in order, starting another when the next entry would not fit.
	/// </summary>
	private static List<List<SizedEntry>> PackRows(List<SizedEntry> sized, double gap, double available)
	{
		var rows = new List<List<SizedEntry>> { new() };
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

		return rows;
	}
}
