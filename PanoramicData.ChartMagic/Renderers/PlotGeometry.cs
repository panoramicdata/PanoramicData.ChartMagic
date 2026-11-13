namespace PanoramicData.ChartMagic.Renderers;

/// <summary>
/// Maps data values onto pixel positions inside the inner plot, and answers the questions the
/// axis and series renderers both need to agree on.
/// </summary>
/// <remarks>
/// Issues #31 and #33: this mapping used to be computed inline in the series loop, so the axes
/// had no way to place a tick under the point it belongs to. Both now go through one object,
/// which is what keeps a category label beneath its column.
/// </remarks>
internal sealed partial class PlotGeometry
{
	private readonly double _xDisplayStart;
	private readonly double _xDisplayRange;
	private readonly double _yDisplayStart;
	private readonly double _yDisplayRange;
	private readonly double _yLogStart;
	private readonly double _yLogRange;

	internal PlotGeometry(Chart chart, AxisHandlerResult axisHandlerResult, double width, double height)
	{
		Width = width;
		Height = height;
		_xDisplayStart = chart.ChartArea.XAxis.Min ?? axisHandlerResult.MinX ?? 0;
		var xDisplayEnd = chart.ChartArea.XAxis.Max ?? axisHandlerResult.MaxX ?? 0;
		_xDisplayRange = xDisplayEnd - _xDisplayStart;
		XDisplayStart = _xDisplayStart;
		XDisplayEnd = xDisplayEnd;

		var bandedSeries = chart.Series.Where(series => IsBanded(series.ChartType)).ToList();
		IsCategorical = bandedSeries.Count > 0
			|| chart.Series.SelectMany(s => s.Points).Any(p => p.XValueString is { Length: > 0 });
		IsHorizontalPlot = bandedSeries.Count > 0
			&& bandedSeries.TrueForAll(series => IsHorizontal(series.ChartType));
		YIsLogarithmic = chart.ChartArea.YAxis.IsLogarithmic;
		IsMarginVisible = chart.ChartArea.XAxis.IsMarginVisible;
		PopulateCategories(chart);

		IsPercentStackedPlot = chart.Series.Any(series => IsPercentStacked(series.ChartType));
		if (IsPercentStackedPlot)
		{
			PopulateCategoryTotals(chart);
		}

		(ValueAxisInterval, _yDisplayStart, _yDisplayRange) = GetLinearAxis(chart, axisHandlerResult);
		(YLogMinimum, YLogMaximum, _yLogStart, _yLogRange) = GetLogarithmicAxis(chart);
	}

	internal double Width { get; }

	internal double Height { get; }

	internal bool IsCategorical { get; }

	/// <summary>
	/// Whether the category axis leaves an interval free at each end. Read from the X axis area for
	/// bar charts too, since it is a property of the category axis wherever that is drawn.
	/// </summary>
	internal bool IsMarginVisible { get; }

	internal bool YIsLogarithmic { get; }

	/// <summary>Whether the category axis runs vertically, as it does for bar charts.</summary>
	internal bool IsHorizontalPlot { get; }

	/// <summary>
	/// The interval the value axis was laid out with, so the tick labels use the same one the
	/// bounds were derived from rather than choosing again from the adjusted range.
	/// </summary>
	internal double? ValueAxisInterval { get; private set; }

	internal double XDisplayStart { get; }

	internal double XDisplayEnd { get; }

	internal double YDisplayStart => YIsLogarithmic ? YLogMinimum : _yDisplayStart;

	internal double YDisplayEnd => YIsLogarithmic ? YLogMaximum : _yDisplayStart + _yDisplayRange;

	internal double YLogMinimum { get; }

	internal double YLogMaximum { get; }

	/// <summary>
	/// Whether the plot shows shares of each category rather than amounts.
	/// </summary>
	internal bool IsPercentStackedPlot { get; private set; }

	/// <summary>
	/// Whether this chart type occupies a band of the axis rather than a single position.
	/// </summary>
	internal static bool IsBanded(SeriesChartType chartType) => chartType
		is SeriesChartType.Column
		or SeriesChartType.StackedColumn
		or SeriesChartType.StackedColumn100
		or SeriesChartType.Bar
		or SeriesChartType.StackedBar
		or SeriesChartType.StackedBar100
		or SeriesChartType.BoxPlot;

	/// <summary>
	/// Whether this chart type stacks onto the running total for its category.
	/// </summary>
	internal static bool IsStacked(SeriesChartType chartType) => chartType
		is SeriesChartType.StackedColumn
		or SeriesChartType.StackedColumn100
		or SeriesChartType.StackedBar
		or SeriesChartType.StackedBar100
		or SeriesChartType.StackedArea
		or SeriesChartType.StackedArea100;

	/// <summary>
	/// Whether this chart type stacks to a full hundred per cent, so the values are shares of
	/// their category rather than amounts.
	/// </summary>
	internal static bool IsPercentStacked(SeriesChartType chartType) => chartType
		is SeriesChartType.StackedColumn100
		or SeriesChartType.StackedBar100
		or SeriesChartType.StackedArea100;

	/// <summary>
	/// Whether this chart type runs along the Y axis rather than the X axis.
	/// </summary>
	internal static bool IsHorizontal(SeriesChartType chartType) => chartType
		is SeriesChartType.Bar
		or SeriesChartType.StackedBar
		or SeriesChartType.StackedBar100;

	private static bool IsNearlyZero(double value) => Math.Abs(value) < 1e-10;
}
