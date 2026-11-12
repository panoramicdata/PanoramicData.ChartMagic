namespace PanoramicData.ChartMagic.Renderers;

internal sealed partial class SeriesRenderer
{
	/// <summary>
	/// Draws one column or bar series: a rectangle per point, running from the value axis origin
	/// to the value of the point, occupying its slot within the category band.
	/// </summary>
	/// <remarks>
	/// Issue #33: the renderer had no case for any of these chart types, so a
	/// column chart rendered its legend and nothing else - no exception, no empty-plot warning,
	/// just a blank plot area beside a correct-looking legend.
	/// </remarks>
	/// <returns>Where each point's data label goes.</returns>
	private List<DataLabelAnchor> PlotBandedSeries(
		Chart chart,
		PlotGeometry geometry,
		Series series,
		XmlElement seriesNode,
		Dictionary<string, double>? stackTotals,
		int slot,
		int slotCount)
	{
		var bandExtent = geometry.CategoryBandExtent;
		var groupExtent = bandExtent * chart.ChartArea.ColumnBandFillFraction;
		var slotExtent = groupExtent / slotCount;
		var origin = geometry.ValueAxisOrigin;
		var isHorizontal = PlotGeometry.IsHorizontal(series.ChartType);
		var labelAnchors = new List<DataLabelAnchor>();
		var gap = series.FontSize * DataLabelGapFraction;

		foreach (var chartPoint in series.Points)
		{
			if (chartPoint.YValue is not { } pointValue)
			{
				continue;
			}

			var (from, to) = BandSpan(geometry, chartPoint, stackTotals, origin);
			var slotStart = geometry.CategoryToPixels(chartPoint.XValue) - (groupExtent / 2) + (slot * slotExtent);

			var rectNode = CreateBandRect(isHorizontal, from, to, slotStart, slotExtent);
			rectNode.SetStyle(series);
			seriesNode.AppendChild(rectNode);

			labelAnchors.Add(BandLabelAnchor(
				chartPoint,
				pointValue,
				isHorizontal,
				stackTotals is not null,
				from,
				to,
				slotStart + (slotExtent / 2),
				gap));
		}

		return labelAnchors;
	}

	/// <summary>
	/// Where a column or bar's data label goes.
	/// </summary>
	/// <remarks>
	/// As measured against DocMagic on the [List.Graph:] examples: a column's label is centred just
	/// above its top, and a bar's is just beyond its end, reading outwards. A negative value's label
	/// goes beyond its end on the other side. A stacked segment's label is centred in the segment,
	/// because outside it would sit on the next segment up.
	/// </remarks>
	private static DataLabelAnchor BandLabelAnchor(
		ChartPoint point,
		double value,
		bool isHorizontal,
		bool isStacked,
		double from,
		double to,
		double across,
		double gap)
	{
		if (isStacked)
		{
			var middle = (from + to) / 2;
			return isHorizontal
				? new DataLabelAnchor(point, value, middle, across, HorizontalAlignment.Center, VerticalAlignment.Middle)
				: new DataLabelAnchor(point, value, across, middle, HorizontalAlignment.Center, VerticalAlignment.Middle);
		}

		// Pixel positions grow rightwards and downwards, so the far end of a positive bar is the
		// larger X, and the far end of a positive column is the smaller Y.
		var isNegative = value < 0;
		if (isHorizontal)
		{
			return isNegative
				? new DataLabelAnchor(point, value, Math.Min(from, to) - gap, across, HorizontalAlignment.Right, VerticalAlignment.Middle)
				: new DataLabelAnchor(point, value, Math.Max(from, to) + gap, across, HorizontalAlignment.Left, VerticalAlignment.Middle);
		}

		return isNegative
			? new DataLabelAnchor(point, value, across, Math.Max(from, to) + gap, HorizontalAlignment.Center, VerticalAlignment.Top)
			: new DataLabelAnchor(point, value, across, Math.Min(from, to) - gap, HorizontalAlignment.Center, VerticalAlignment.Bottom);
	}

	/// <summary>
	/// Where one column or bar starts and finishes along the value axis, in pixels.
	/// </summary>
	private static (double From, double To) BandSpan(
		PlotGeometry geometry,
		ChartPoint chartPoint,
		Dictionary<string, double>? stackTotals,
		double origin)
	{
		if (stackTotals is null)
		{
			return (origin, geometry.ValueToPixels(chartPoint.YValue!.Value));
		}

		var key = chartPoint.XValue.ToString(CultureInfo.InvariantCulture);
		var previousTotal = stackTotals.TryGetValue(key, out var runningTotal) ? runningTotal : 0;

		// A hundred per cent stacked series contributes its share of the category, not its value.
		var contribution = geometry.IsPercentStackedPlot
			? geometry.ToPercentOfCategory(chartPoint.XValue, chartPoint.YValue!.Value)
			: chartPoint.YValue!.Value;

		var newTotal = previousTotal + contribution;
		stackTotals[key] = newTotal;
		return (geometry.ValueToPixels(previousTotal), geometry.ValueToPixels(newTotal));
	}

	/// <summary>
	/// The rectangle for one column or bar: its span lies along the value axis and its thickness
	/// across the category axis, whichever way round those two are.
	/// </summary>
	private XmlElement CreateBandRect(bool isHorizontal, double from, double to, double slotStart, double slotExtent)
	{
		var rectNode = _canvas.Element("rect");
		var near = Math.Round(Math.Min(from, to), 2).ToString(CultureInfo.InvariantCulture);
		var extent = Math.Round(Math.Abs(to - from), 2).ToString(CultureInfo.InvariantCulture);
		var across = Math.Round(slotStart, 2).ToString(CultureInfo.InvariantCulture);
		var thickness = Math.Round(slotExtent, 2).ToString(CultureInfo.InvariantCulture);

		if (isHorizontal)
		{
			rectNode.SetAttribute("x", near);
			rectNode.SetAttribute("y", across);
			rectNode.SetAttribute("width", extent);
			rectNode.SetAttribute("height", thickness);
		}
		else
		{
			rectNode.SetAttribute("x", across);
			rectNode.SetAttribute("y", near);
			rectNode.SetAttribute("width", thickness);
			rectNode.SetAttribute("height", extent);
		}

		return rectNode;
	}

	/// <summary>
	/// How the column and bar series of a chart divide up a category band.
	/// </summary>
	/// <param name="GroupedSeries">The banded series that stand side by side.</param>
	/// <param name="SlotCount">How many slots a band is divided into.</param>
	/// <remarks>
	/// Issue #33: a column or bar occupies a slot within its category band. Grouped series take
	/// one slot each; all stacked series share a single slot, because they stack on top of one
	/// another rather than standing side by side.
	/// </remarks>
	private sealed record BandLayout(List<Series> GroupedSeries, int SlotCount)
	{
		internal static BandLayout For(Chart chart)
		{
			var bandedSeries = chart.Series.Where(s => PlotGeometry.IsBanded(s.ChartType)).ToList();
			var groupedSeries = bandedSeries.Where(s => !PlotGeometry.IsStacked(s.ChartType)).ToList();
			var hasStackedBanded = bandedSeries.Exists(s => PlotGeometry.IsStacked(s.ChartType));
			return new BandLayout(groupedSeries, Math.Max(1, groupedSeries.Count + (hasStackedBanded ? 1 : 0)));
		}

		/// <summary>
		/// The slot a series occupies: its own, or the shared last one if it is stacked.
		/// </summary>
		internal int SlotFor(Series series)
			=> PlotGeometry.IsStacked(series.ChartType)
				? GroupedSeries.Count
				: GroupedSeries.IndexOf(series);
	}
}
