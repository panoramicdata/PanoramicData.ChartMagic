using System.Drawing;
using static PanoramicData.ChartMagic.Test.Support.ChartFixtures;
using static PanoramicData.ChartMagic.Test.Support.RenderedChart;

namespace PanoramicData.ChartMagic.Test;

/// <summary>
/// Legend differences from the Microsoft chart control found by the List.Graph comparison (issue 76).
/// </summary>
public class LegendParityTests
{
	private static ChartSpecification LineChart(MarkerStyle markerStyle) => SingleSeries(
		SeriesChartType.Line,
		Points(10, 24, 17, 31),
		series =>
		{
			series.MarkerStyle = markerStyle;
			series.MarkerSize = 10;
			series.MarkerFillColor = Color.White;
		});

	[Fact]
	public void LineSeriesLegendSwatch_IsAThinLine()
	{
		var legend = GroupById(Render(LineChart(MarkerStyle.None)), "legend");

		var swatch = Elements(legend, "rect").Skip(1).Single();
		Number(swatch, "height").Should().BeLessThanOrEqualTo(2, "Windows draws a thin line, not a bar");
		Number(swatch, "width").Should().BeGreaterThan(10);
	}

	[Fact]
	public void LineSeriesLegendSwatch_CarriesTheSeriesMarker()
	{
		var legend = GroupById(Render(LineChart(MarkerStyle.Circle)), "legend");

		var swatch = Elements(legend, "rect").Skip(1).Single();
		var marker = Elements(legend, "use").Should().ContainSingle().Subject;
		marker.Attribute("{http://www.w3.org/1999/xlink}href")?.Value.Should().Be("#series0Marker");
		marker.Attribute("transform")!.Value.Should().Contain(
			((Number(swatch, "x") + (Number(swatch, "width") / 2)).ToString(System.Globalization.CultureInfo.InvariantCulture)),
			"the marker sits at the middle of the swatch");
	}

	[Fact]
	public void LineSeriesLegendSwatch_WithNoMarker_HasNone()
		=> Elements(GroupById(Render(LineChart(MarkerStyle.None)), "legend"), "use").Should().BeEmpty();

	[Fact]
	public void FilledSeriesLegendSwatch_IsStillABlock()
	{
		var legend = GroupById(Render(ColumnChart(SeriesChartType.Column, 1)), "legend");

		Number(Elements(legend, "rect").Skip(1).Single(), "height").Should().BeGreaterThan(8);
	}
}
