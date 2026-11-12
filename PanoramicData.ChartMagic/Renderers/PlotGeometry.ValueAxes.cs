namespace PanoramicData.ChartMagic.Renderers;

internal sealed partial class PlotGeometry
{
	private (double? Interval, double Start, double Range) GetLinearAxis(
		Chart chart,
		AxisHandlerResult axisHandlerResult)
	{
		var yAxis = chart.ChartArea.YAxis;
		if (IsPercentStackedPlot)
		{
			return PercentStackedValueAxis(yAxis);
		}

		return YIsLogarithmic
			? LogarithmicValueAxis(yAxis, axisHandlerResult)
			: GeneratedValueAxis(yAxis, axisHandlerResult);
	}

	/// <summary>
	/// The value axis of a hundred per cent stacked plot, which is a percentage scale in fifths.
	/// </summary>
	private static (double? Interval, double Start, double Range) PercentStackedValueAxis(AxisArea yAxis)
	{
		var start = yAxis.Min ?? 0;
		return (20, start, (yAxis.Max ?? 100) - start);
	}

	/// <summary>
	/// The linear span of a logarithmic value axis, which is the data range as it stands: the
	/// decades themselves come from the logarithmic mapping rather than from an interval.
	/// </summary>
	private static (double? Interval, double Start, double Range) LogarithmicValueAxis(
		AxisArea yAxis,
		AxisHandlerResult axisHandlerResult)
	{
		var start = yAxis.Min ?? axisHandlerResult.MinY ?? 0;
		return (null, start, (yAxis.Max ?? axisHandlerResult.MaxY ?? 0) - start);
	}

	/// <summary>
	/// The value axis with bounds chosen to land on readable ticks, except at whichever end the
	/// chart pins for itself.
	/// </summary>
	private static (double? Interval, double Start, double Range) GeneratedValueAxis(
		AxisArea yAxis,
		AxisHandlerResult axisHandlerResult)
	{
		var (step, generatedStart, generatedEnd) = TickGenerator.LinearBounds(
			axisHandlerResult.MinY ?? 0,
			axisHandlerResult.MaxY ?? 0);
		var start = yAxis.Min ?? generatedStart;
		return (step, start, (yAxis.Max ?? generatedEnd) - start);
	}

	private static (double Minimum, double Maximum, double Start, double Range) GetLogarithmicAxis(Chart chart)
	{
		if (!chart.ChartArea.YAxis.IsLogarithmic)
		{
			return (0, 0, 0, 0);
		}

		var positiveValues = chart.Series.SelectMany(series => series.Points)
			.Where(point => point.YValue is > 0)
			.Select(point => point.YValue!.Value)
			.ToList();
		var smallest = chart.ChartArea.YAxis.Min is > 0
			? chart.ChartArea.YAxis.Min.Value
			: positiveValues.Count > 0 ? positiveValues.Min() : 1;
		var largest = chart.ChartArea.YAxis.Max is > 0
			? chart.ChartArea.YAxis.Max.Value
			: positiveValues.Count > 0 ? positiveValues.Max() : 10;
		var minimum = Math.Pow(10, Math.Floor(Math.Log10(smallest)));
		var maximum = Math.Pow(10, Math.Ceiling(Math.Log10(largest <= smallest ? smallest * 10 : largest)));
		var start = Math.Log10(minimum);
		return (minimum, maximum, start, Math.Log10(maximum) - start);
	}
}
