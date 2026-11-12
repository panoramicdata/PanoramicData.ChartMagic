namespace PanoramicData.ChartMagic.Renderers;

internal static class AxisTicks
{
	internal static IReadOnlyList<double> EveryNth(IReadOnlyList<double> categories, double interval)
	{
		var step = Math.Max(1, (int)Math.Round(interval));
		return [.. categories.Where((_, index) => index % step == 0)];
	}

	/// <summary>
	/// The value the value axis counts its ticks from, or null for multiples of the interval.
	/// </summary>
	/// <remarks>
	/// The value axis is the Y axis area's whichever way the plot runs: a bar chart keeps its range
	/// there too.
	/// </remarks>
	internal static double? ValueAxisAnchor(Chart chart, PlotGeometry geometry)
		=> chart.ChartArea.YAxis is { IntervalStartsAtMinimum: true, Min: not null } ? geometry.YDisplayStart : null;

	internal static double XAxisPixels(PlotGeometry geometry, double value)
		=> geometry.IsHorizontalPlot
			? geometry.ValueToPixels(value)
			: geometry.IsCategorical ? geometry.CategoryToPixels(value) : geometry.XToPixels(value);

	/// <summary>
	/// The values the X axis is labelled at: one per category when the axis is categorical, the
	/// value scale for a bar chart, and readable intervals across the range otherwise.
	/// </summary>
	internal static IReadOnlyList<double> XAxisTickValues(Chart chart, PlotGeometry geometry)
	{
		if (geometry.IsHorizontalPlot)
		{
			return TickGenerator.Linear(
				geometry.YDisplayStart,
				geometry.YDisplayEnd,
				chart.ChartArea.XAxis.Interval,
				chart.ChartArea.XAxis.TargetTickCount,
				ValueAxisAnchor(chart, geometry));
		}

		if (geometry.IsCategorical)
		{
			// An interval on a category axis means every Nth category, which is how a long set of
			// labels is thinned. Ignoring it left every category labelled however many there were.
			var step = (int)Math.Round(chart.ChartArea.XAxis.Interval ?? 1);
			return step <= 1
				? geometry.Categories
				: [.. geometry.Categories.Where((_, index) => index % step == 0)];
		}

		return TickGenerator.Linear(
			geometry.XDisplayStart,
			geometry.XDisplayEnd,
			chart.ChartArea.XAxis.Interval,
			chart.ChartArea.XAxis.TargetTickCount);
	}

	/// <summary>
	/// The values the Y axis is labelled at.
	/// </summary>
	internal static IReadOnlyList<double> YAxisTickValues(Chart chart, PlotGeometry geometry)
	{
		if (geometry.IsHorizontalPlot)
		{
			return geometry.Categories;
		}

		return geometry.YIsLogarithmic
			? TickGenerator.Logarithmic(geometry.YLogMinimum, geometry.YLogMaximum, includeMinor: false)
			: TickGenerator.Linear(
				geometry.YDisplayStart,
				geometry.YDisplayEnd,
				// The interval the bounds were derived from, so the labels land on the bounds
				// rather than being chosen again from the adjusted range.
				chart.ChartArea.YAxis.Interval ?? geometry.ValueAxisInterval,
				chart.ChartArea.YAxis.TargetTickCount,
				ValueAxisAnchor(chart, geometry));
	}
}
