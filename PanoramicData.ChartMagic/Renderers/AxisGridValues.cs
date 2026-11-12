using static PanoramicData.ChartMagic.Renderers.AxisTicks;

namespace PanoramicData.ChartMagic.Renderers;

internal static class AxisGridValues
{
	/// <summary>
	/// Where the horizontal major gridlines go: at the Y axis labels, unless the gridlines have an
	/// interval of their own.
	/// </summary>
	/// <remarks>
	/// The gridline interval is independent of the label interval in the Microsoft chart control:
	/// gridlines every 10 over labels every 5 draws four lines on a 0 to 35 axis, not eight.
	/// </remarks>
	internal static IReadOnlyList<double> HorizontalMajorGridValues(Chart chart, PlotGeometry geometry)
	{
		var yAxis = chart.ChartArea.YAxis;
		if (yAxis.MajorGridInterval is not > 0 || geometry.YIsLogarithmic)
		{
			return YAxisTickValues(chart, geometry);
		}

		return geometry.IsHorizontalPlot
			? EveryNth(geometry.Categories, yAxis.MajorGridInterval.Value)
			: TickGenerator.Linear(geometry.YDisplayStart, geometry.YDisplayEnd, yAxis.MajorGridInterval, yAxis.TargetTickCount, ValueAxisAnchor(chart, geometry));
	}

	/// <summary>
	/// Where the vertical major gridlines go: at the X axis labels, unless the gridlines have an
	/// interval of their own.
	/// </summary>
	internal static IReadOnlyList<double> VerticalMajorGridValues(Chart chart, PlotGeometry geometry)
	{
		var xAxis = chart.ChartArea.XAxis;
		if (xAxis.MajorGridInterval is not > 0)
		{
			return XAxisTickValues(chart, geometry);
		}

		if (geometry.IsHorizontalPlot)
		{
			return TickGenerator.Linear(geometry.YDisplayStart, geometry.YDisplayEnd, xAxis.MajorGridInterval, xAxis.TargetTickCount, ValueAxisAnchor(chart, geometry));
		}

		return geometry.IsCategorical
			? EveryNth(geometry.Categories, xAxis.MajorGridInterval.Value)
			: TickGenerator.Linear(geometry.XDisplayStart, geometry.XDisplayEnd, xAxis.MajorGridInterval, xAxis.TargetTickCount);
	}

	/// <summary>
	/// Where the vertical minor gridlines go, in pixels across the plot.
	/// </summary>
	/// <remarks>
	/// A category axis subdivides its bands. This used to draw nothing at all, on the reasoning
	/// that there is nothing between one category and the next - but the reference draws them,
	/// roughly four to a band, so a chart asking for X minor gridlines got none and the setting
	/// was neither honoured nor refused.
	/// </remarks>
	internal static IReadOnlyList<double> MinorGridPositions(AxisArea axis, PlotGeometry geometry)
	{
		if (geometry.IsHorizontalPlot)
		{
			return [.. MinorTicks(axis, geometry, isValueAxis: true).Select(v => XAxisPixels(geometry, v))];
		}

		if (!geometry.IsCategorical)
		{
			var span = geometry.XDisplayEnd - geometry.XDisplayStart;
			var interval = axis.MinorGridInterval is > 0
				? axis.MinorGridInterval.Value
				: span / Math.Max(axis.TargetTickCount, 1) / Math.Max(axis.MinorGridSubdivisions, 1);

			return
			[
				.. TickGenerator
					.Linear(geometry.XDisplayStart, geometry.XDisplayEnd, interval, axis.TargetTickCount * axis.MinorGridSubdivisions)
					.Select(geometry.XToPixels)
			];
		}

		return CategoryBandSubdivisions(axis, geometry);
	}

	/// <summary>
	/// Subdivisions of each category band, measured from the band edge rather than its centre, so
	/// the lines fall between the categories as well as within them.
	/// </summary>
	internal static IReadOnlyList<double> CategoryBandSubdivisions(AxisArea axis, PlotGeometry geometry)
	{
		var subdivisions = Math.Max(axis.MinorGridSubdivisions, 1);
		var band = geometry.CategoryBandExtent;
		if (band <= 0)
		{
			return [];
		}

		var step = band / subdivisions;
		var positions = new List<double>();
		for (var x = 0d; x <= geometry.Width + 0.001; x += step)
		{
			positions.Add(Math.Round(x, 2));
			if (positions.Count > 2000)
			{
				break;
			}
		}

		return positions;
	}

	/// <summary>
	/// Minor gridline positions: the interval the caller gave, otherwise a subdivision of the
	/// major interval, or the intermediate steps within each decade on a logarithmic axis.
	/// </summary>
	internal static IReadOnlyList<double> MinorTicks(AxisArea axis, PlotGeometry geometry, bool isValueAxis)
	{
		if (!isValueAxis)
		{
			// Subdivisions of the category band. This used to return nothing, on the reasoning
			// that there is nothing between one category and the next - but the reference draws
			// them, about four to a band, so a chart asking for X minor gridlines got none.
			return [];
		}

		if (geometry.YIsLogarithmic)
		{
			return TickGenerator.Logarithmic(geometry.YLogMinimum, geometry.YLogMaximum, includeMinor: true);
		}

		var interval = axis.MinorGridInterval;
		if (interval is not > 0)
		{
			interval = MajorStep(axis, geometry) / Math.Max(axis.MinorGridSubdivisions, 1);
		}

		return TickGenerator.Linear(
			geometry.YDisplayStart,
			geometry.YDisplayEnd,
			interval,
			axis.TargetTickCount * 5);
	}

	/// <summary>
	/// The step between major ticks on the value axis, which the minor ones subdivide.
	/// </summary>
	private static double MajorStep(AxisArea axis, PlotGeometry geometry)
		=> axis.Interval ?? geometry.ValueAxisInterval
			?? TickGenerator.NiceStep(geometry.YDisplayEnd - geometry.YDisplayStart, axis.TargetTickCount);
}
