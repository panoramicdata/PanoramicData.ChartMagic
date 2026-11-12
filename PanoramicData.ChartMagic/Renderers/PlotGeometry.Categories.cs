namespace PanoramicData.ChartMagic.Renderers;

internal sealed partial class PlotGeometry
{
	private readonly List<double> _categories = [];
	private readonly Dictionary<double, double> _categoryTotals = [];
	private readonly Dictionary<double, string> _categoryLabels = [];

	private void PopulateCategories(Chart chart)
	{
		foreach (var point in chart.Series.SelectMany(series => series.Points))
		{
			if (!_categoryLabels.ContainsKey(point.XValue) && point.XValueString is not null)
			{
				_categoryLabels[point.XValue] = point.XValueString;
			}

			if (!_categories.Contains(point.XValue))
			{
				_categories.Add(point.XValue);
			}
		}

		_categories.Sort();
	}

	private void PopulateCategoryTotals(Chart chart)
	{
		foreach (var point in chart.Series
			.Where(series => IsPercentStacked(series.ChartType))
			.SelectMany(series => series.Points))
		{
			_categoryTotals[point.XValue] =
				_categoryTotals.GetValueOrDefault(point.XValue) + Math.Abs(point.YValue ?? 0);
		}
	}

	internal IReadOnlyList<double> Categories => _categories;

	/// <summary>
	/// A value as its percentage of the total for its category.
	/// </summary>
	/// <remarks>
	/// Returns nought for a category that sums to nothing, rather than dividing by it: an empty
	/// category has no shares to show, and a chart of infinities would be worse than a gap.
	/// </remarks>
	internal double ToPercentOfCategory(double xValue, double value)
	{
		var total = _categoryTotals.GetValueOrDefault(xValue);
		return IsNearlyZero(total) ? 0 : value / total * 100;
	}

	/// <summary>
	/// The number of intervals the category axis is divided into: one more than there are
	/// categories.
	/// </summary>
	/// <remarks>
	/// Not a rounding detail - it is where the categories sit. Dividing the axis by the number
	/// of categories and centring each in its share puts half an interval of padding at each
	/// end; dividing by one more and placing category i at interval i+1 puts a whole interval of
	/// padding at each end, which is what the renderer this matches does.
	///
	/// Measured rather than reasoned about: over an inner plot 488 pixels wide with seven
	/// categories, the other renderer spaced them 61 pixels apart starting 61 pixels in
	/// (488 / 8), where dividing by seven gives 70. The same 61 was measured for a column chart
	/// and for a line chart with markers, so it is a property of the category axis rather than of
	/// either chart type. Column groups were therefore drawn up to 27 pixels away from where they
	/// belonged, and proportionally wider with it.
	/// </remarks>
	/// <remarks>
	/// Without the margin, the first and last categories sit on the ends of the axis, so there are
	/// one fewer intervals than categories. A single category is then centred.
	/// </remarks>
	private int CategoryIntervalCount => IsMarginVisible ? _categories.Count + 1 : _categories.Count > 1 ? _categories.Count - 1 : 2;

	/// <summary>
	/// How many intervals along the axis the category at this index is.
	/// </summary>
	private double CategoryOffset(int index)
		=> IsMarginVisible ? index + 1 : _categories.Count == 1 ? 1 : index;

	/// <summary>
	/// The width of one category interval, or zero when the axis is not categorical.
	/// </summary>
	internal double BandWidth => IsCategorical && _categories.Count > 0 ? Width / CategoryIntervalCount : 0;

	/// <summary>
	/// The extent of one category band along the category axis - horizontal for columns,
	/// vertical for bars.
	/// </summary>
	internal double CategoryBandExtent => IsCategorical && _categories.Count > 0
		? (IsHorizontalPlot ? Height : Width) / CategoryIntervalCount
		: 0;

	/// <summary>
	/// The centre of a category along the category axis.
	/// </summary>
	/// <remarks>
	/// On a horizontal plot the category axis runs upwards: the first category is at the bottom
	/// and the last at the top, which is how the renderer this matches draws a bar chart, and the
	/// opposite of the order the categories are given in. Measured on a seven-day bar chart -
	/// reading the bar lengths off the reference render top to bottom gave Sunday first and Monday
	/// last. Drawing them in the order given put every category against the wrong label.
	/// </remarks>
	internal double CategoryToPixels(double xValue)
	{
		var index = _categories.IndexOf(xValue);
		if (index < 0)
		{
			index = 0;
		}

		var distanceAlongAxis = CategoryOffset(index) * CategoryBandExtent;

		// Distances are measured down the plot, so an axis that runs upwards is that distance
		// taken from the far edge.
		return Math.Round(
			IsHorizontalPlot ? Height - distanceAlongAxis : distanceAlongAxis,
			2);
	}

	/// <summary>
	/// The label for a category, when the data supplied one.
	/// </summary>
	internal string? CategoryLabel(double xValue)
		=> _categoryLabels.TryGetValue(xValue, out var label) ? label : null;
}
