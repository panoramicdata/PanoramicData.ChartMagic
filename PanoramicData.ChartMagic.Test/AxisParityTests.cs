using System.Xml.Linq;
using static PanoramicData.ChartMagic.Test.Support.ChartFixtures;
using static PanoramicData.ChartMagic.Test.Support.RenderedChart;

namespace PanoramicData.ChartMagic.Test;

/// <summary>
/// Axis differences from the Microsoft chart control found by the List.Graph comparison (issue 76).
/// </summary>
public class AxisParityTests
{
	/// <summary>
	/// Windows divides each major interval into five minor ones. The axis here runs 0 to 35 in
	/// steps of 5, so the minor lines are a unit apart and there are 36 of them.
	/// </summary>
	[Fact]
	public void MinorGridlines_DivideEachMajorIntervalIntoFive()
	{
		var chart = ColumnChart(SeriesChartType.Line, 1).ToChart();
		chart.ChartArea.YAxis.MinorGridEnabled = true;

		var ys = MinorLines(Render(chart)).Select(line => Number(line, "y1")).Order().ToList();

		ys.Should().HaveCount(36);
		ys.Zip(ys.Skip(1), (a, b) => b - a).Should().AllSatisfy(gap => gap.Should().BeApproximately(ys[1] - ys[0], 0.05));
	}

	[Fact]
	public void MinorGridlines_FollowAnExplicitMajorInterval()
	{
		var chart = ColumnChart(SeriesChartType.Line, 1).ToChart();
		chart.ChartArea.YAxis.MinorGridEnabled = true;
		chart.ChartArea.YAxis.Interval = 10;

		MinorLines(Render(chart)).Should().HaveCount(18, "minor lines two apart from 0 to 34 on a 0 to 35 axis");
	}

	[Fact]
	public void ShortLabels_ReadTheOriginAsPlainZero()
	{
		var specification = ColumnChart(SeriesChartType.Column, 1);
		specification.UseYAxisShortLabels = true;

		var labels = LabelTexts(Render(specification), "yAxis");

		labels.Should().Contain("0").And.Contain("5.0");
		labels.Should().NotContain("0.0");
	}

	[Fact]
	public void LogarithmicYAxis_StartsAtOneWhereTheDataStartsHigher()
	{
		var specification = SingleSeries(
			SeriesChartType.Line,
			[new(null, 0, 50), new(null, 1, 400), new(null, 2, 900)]);
		specification.YAxisIsLogarithmic = true;

		NumericLabels(Render(specification), "yAxis").Order().Should().Equal(1, 10, 100, 1000);
	}

	[Fact]
	public void LogarithmicYAxis_StillStartsBelowOneWhereTheDataDoes()
	{
		var specification = SingleSeries(
			SeriesChartType.Line,
			[new(null, 0, 0.05), new(null, 1, 4), new(null, 2, 90)]);
		specification.YAxisIsLogarithmic = true;

		NumericLabels(Render(specification), "yAxis").Order().First().Should().Be(0.01);
	}

	private static List<XElement> MinorLines(XDocument document)
		=> [.. Elements(GroupById(document, "gridlines"), "line").Where(line => Number(line, "y1") == Number(line, "y2"))];
}
