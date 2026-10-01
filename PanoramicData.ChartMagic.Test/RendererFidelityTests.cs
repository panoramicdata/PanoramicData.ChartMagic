using PanoramicData.ChartMagic.Renderers;
using System.Drawing;
using System.Globalization;
using System.Xml.Linq;
using static PanoramicData.ChartMagic.Test.Support.ChartFixtures;
using static PanoramicData.ChartMagic.Test.Support.RenderedChart;

namespace PanoramicData.ChartMagic.Test;

/// <summary>
/// Settings the Magic Suite renderer comparison (MS-26667) found ignored, refused or drawn wrongly.
/// </summary>
public class RendererFidelityTests
{
	private static readonly string[] Cities = ["London", "Manchester", "Leeds"];

	[Fact]
	public void CurrencyFormat_UsesTheChartCulturesSymbol()
	{
		// MS-26683: with the invariant culture C2 shows the generic currency sign.
		var chart = ColumnChart(SeriesChartType.Column, 1).ToChart();
		chart.ChartArea.YAxis.LabelFormat = "C2";
		chart.Culture = CultureInfo.GetCultureInfo("en-GB");

		LabelTexts(Render(chart), "yAxis").Should().Contain("£30.00").And.NotContain(label => label.Contains('¤'));
	}

	[Fact]
	public void PercentFormat_UsesTheChartCulturesPattern()
	{
		// MS-26633: the invariant culture writes "3,500 %"; en-GB writes "3,500%".
		var chart = ColumnChart(SeriesChartType.Column, 1).ToChart();
		chart.ChartArea.YAxis.LabelFormat = "P0";
		chart.Culture = CultureInfo.GetCultureInfo("en-GB");

		LabelTexts(Render(chart), "yAxis").Should().Contain("3,000%");
	}

	[Fact]
	public void DataLabels_UseTheChartCulture()
	{
		var specification = ColumnChart(SeriesChartType.Column, 1);
		specification.SeriesList[0].LabelText = "#VAL";
		specification.SeriesList[0].Points = Points(1.5, 2.5, 3.5, 4.5);
		var chart = specification.ToChart();
		chart.Culture = CultureInfo.GetCultureInfo("de-DE");

		LabelTexts(Render(chart), "dataLabels").Should().Contain("1,5");
	}

	[Fact]
	public void MajorGridInterval_IsIndependentOfTheLabelInterval()
	{
		// MS-26664: gridlines every 10 over labels every 5 drew a line at every label.
		var chart = ColumnChart(SeriesChartType.Column, 1).ToChart();
		chart.ChartArea.YAxis.MajorGridEnabled = true;
		chart.ChartArea.YAxis.MajorGridInterval = 10;

		var document = Render(chart);

		NumericLabels(document, "yAxis").Should().Contain(5, "the labels keep their own interval");
		HorizontalGridlineCount(document).Should().Be(4, "gridlines at 0, 10, 20 and 30 on a 0 to 35 axis");
	}

	[Fact]
	public void MajorGridDashStyle_IsDrawn()
	{
		// MS-26606: a dashed gridline was refused, so the chart was not drawn at all.
		var chart = ColumnChart(SeriesChartType.StackedColumn, 2).ToChart();
		chart.ChartArea.YAxis.MajorGridEnabled = true;
		chart.ChartArea.YAxis.MajorGridDashStyle = ChartDashStyle.Dash;

		Gridlines(Render(chart)).Should().NotBeEmpty().And.AllSatisfy(line =>
			line.Attribute("stroke-dasharray").Should().NotBeNull());
	}

	[Fact]
	public void MinorGridWidth_CanDifferFromTheMajor()
	{
		var chart = ColumnChart(SeriesChartType.Line, 1).ToChart();
		chart.ChartArea.YAxis.MajorGridEnabled = true;
		chart.ChartArea.YAxis.MinorGridEnabled = true;
		chart.ChartArea.YAxis.GridWidth = 3;
		chart.ChartArea.YAxis.MinorGridWidth = 1;

		Gridlines(Render(chart)).Select(line => Number(line, "stroke-width")).Distinct().Should().BeEquivalentTo([1d, 3d]);
	}

	[Fact]
	public void Series_AreClippedToThePlot()
	{
		// MS-26641: data beyond an explicit maximum was drawn over the chart above the plot.
		var chart = SingleSeries(SeriesChartType.Line, Points(10, 24, 17, 31)).ToChart();
		chart.ChartArea.YAxis.Min = 5;
		chart.ChartArea.YAxis.Max = 25;

		var document = Render(chart);
		var clip = Defs(document).Elements().Single(e => e.Name.LocalName == "clipPath");
		var plot = Elements(GroupById(document, "innerPlot"), "rect").First();

		Number(clip.Elements().Single(), "height").Should().BeApproximately(Number(plot, "height"), 2.5);
		GroupById(document, "series0").Attribute("clip-path")!.Value.Should().Be($"url(#{clip.Attribute("id")!.Value})");
	}

	[Fact]
	public void IntervalStartsAtMinimum_CountsTicksFromTheMinimum()
	{
		// The Microsoft chart control labels a 5 to 25 axis at an interval of 4 as 5, 9 ... 25.
		var chart = SingleSeries(SeriesChartType.Line, Points(10, 24, 17, 31)).ToChart();
		chart.ChartArea.YAxis.Min = 5;
		chart.ChartArea.YAxis.Max = 25;
		chart.ChartArea.YAxis.Interval = 4;
		chart.ChartArea.YAxis.IntervalStartsAtMinimum = true;

		NumericLabels(Render(chart), "yAxis").Order().Should().Equal(5, 9, 13, 17, 21, 25);
	}

	[Fact]
	public void IntervalStartsAtMinimum_KeepsAFractionalMinimum()
	{
		var chart = SingleSeries(SeriesChartType.Line, Points(12, -8, 14, 26)).ToChart();
		chart.ChartArea.YAxis.Min = -15.5;
		chart.ChartArea.YAxis.Max = 30.5;
		chart.ChartArea.YAxis.Interval = 9;
		chart.ChartArea.YAxis.IntervalStartsAtMinimum = true;

		NumericLabels(Render(chart), "yAxis").Order().Should().Equal(-15.5, -6.5, 2.5, 11.5, 20.5, 29.5);
	}

	[Fact]
	public void WithoutAMargin_TheCategoriesRunFromEndToEnd()
	{
		// MS-26628: the axis margin setting was refused.
		var withMargin = SingleSeries(SeriesChartType.Line, Points(10, 24, 17, 31)).ToChart();
		var withoutMargin = SingleSeries(SeriesChartType.Line, Points(10, 24, 17, 31)).ToChart();
		withoutMargin.ChartArea.XAxis.IsMarginVisible = false;

		var marginVertices = PathVertexXValues(Elements(GroupById(Render(withMargin), "series0"), "path").First());
		var noMarginVertices = PathVertexXValues(Elements(GroupById(Render(withoutMargin), "series0"), "path").First());

		marginVertices[0].Should().BeGreaterThan(0);
		noMarginVertices[0].Should().Be(0, "the first category sits on the start of the axis");
		noMarginVertices[^1].Should().BeGreaterThan(marginVertices[^1], "and the last on its end");
	}

	[Fact]
	public void YAxisTitle_InANarrowStrip_IsShrunkClearOfTheLabels()
	{
		// MS-26597: drawn at full size, the title lay over the tick labels.
		var chart = ColumnChart(SeriesChartType.Line, 1).ToChart();
		chart.ChartArea.YAxis.Title = "Percent";
		chart.ChartArea.YAxis.LabelFormat = "0.0";
		chart.ChartArea.YAxis.FontSize = 16;
		chart.ChartArea.YAxis.WidthPercent = 12;

		var document = Render(chart, 720, 400);
		var title = Elements(GroupById(document, "yAxis"), "text").SingleOrDefault(t => t.Attribute("id")?.Value == "yAxisTitle");
		var labels = Elements(GroupById(document, "yAxis"), "text").Where(t => t.Attribute("id")?.Value != "yAxisTitle").ToList();

		title.Should().NotBeNull();
		var titleRight = Number(title!, "x") + (Number(title!, "font-size") * 0.2);
		var labelsLeft = labels.Min(label => Number(label, "x") - TextMeasure.Width(label.Value, Number(label, "font-size")));
		titleRight.Should().BeLessThan(labelsLeft, "the title is clear of the labels");
		Number(title!, "font-size").Should().BeLessThan(16, "it was shrunk to fit");
	}

	[Fact]
	public void YAxisTitle_WithRoom_KeepsItsSize()
	{
		var chart = ColumnChart(SeriesChartType.Line, 1).ToChart();
		chart.ChartArea.YAxis.Title = "Percent";
		chart.ChartArea.YAxis.FontSize = 16;
		chart.ChartArea.YAxis.WidthPercent = 20;

		var title = Elements(GroupById(Render(chart, 720, 400), "yAxis"), "text").Single(t => t.Attribute("id")?.Value == "yAxisTitle");

		Number(title, "font-size").Should().Be(16);
	}

	[Fact]
	public void LabelBackColor_IsDrawnBehindEachDataLabel()
	{
		// MS-26614: a label background colour was refused.
		var specification = ColumnChart(SeriesChartType.Column, 1);
		specification.SeriesList[0].LabelText = "#VAL";
		var chart = specification.ToChart();
		chart.Series[0].LabelBackColor = Color.Yellow;

		var labels = GroupById(Render(chart), "dataLabels");

		Elements(labels, "rect").Should().HaveCount(4).And.AllSatisfy(rect => rect.Attribute("fill")!.Value.Should().BeEquivalentTo("#FFFF00"));
		Elements(labels, "text").Should().HaveCount(4);
	}

	[Fact]
	public void ChartBorder_IsDrawnLast_InsideTheImage_WithDashesScaledToItsWidth()
	{
		// MS-26605: a 4 pixel dashed border was covered by the legend box, lost half its width off
		// the image edge, and read as solid because the dashes did not scale with the width.
		var specification = ColumnChart(SeriesChartType.Column, 2);
		specification.ChartBorderColor = Color.Red;
		specification.ChartBorderWidth = 4;
		specification.ChartBorderLineDashStyle = ChartDashStyle.Dash;

		var document = Render(specification, 720, 400);
		var svg = document.Root!;
		var border = svg.Elements().Last();

		border.Attribute("id")!.Value.Should().Be("chartBorder", "it is drawn after everything else");
		Number(border, "x").Should().Be(2);
		Number(border, "width").Should().Be(716);
		var style = border.Attribute("style")!.Value.Split(';');
		style.Should().Contain("stroke-dasharray:12.00,4.00").And.Contain("stroke-linecap:butt");
	}

	[Fact]
	public void LegendLabel_UsesTheInsetBeforeBeingShortened()
	{
		// MS-26593: "Manchester" fits a 144 pixel legend on the right of a 720 pixel image once
		// the inset is given up, so it is not shortened.
		var specification = SingleSeries(SeriesChartType.Pie, [.. Cities.Select((city, index) => new ChartPoint(city, index, 30 - (index * 5)))]);
		specification.LegendStyle = LegendStyle.Column;
		specification.LegendXPositionPercent = 80;
		specification.LegendWidthPercent = 20;
		specification.LegendFontSize = 16;

		LabelTexts(Render(specification, 720, 400), "legend").Should().Contain("Manchester");
	}
	[Theory]
	[InlineData(0, 30, 10, new double[] { 0, 10, 20, 30 })]
	[InlineData(5, 25, 4, new double[] { 5, 9, 13, 17, 21, 25 })]
	public void Linear_FromAnAnchor_CountsFromIt(double min, double max, double interval, double[] expected)
		=> TickGenerator.Linear(min, max, interval, 8, anchor: min).Should().Equal(expected);

	private static List<XElement> Gridlines(XDocument document)
		=> Elements(GroupById(document, "gridlines"), "line");

	private static int HorizontalGridlineCount(XDocument document)
		=> Gridlines(document).Count(line => Number(line, "y1") == Number(line, "y2"));
}
