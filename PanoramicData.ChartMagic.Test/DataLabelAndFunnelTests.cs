using System.Drawing;
using System.Globalization;
using System.Xml.Linq;
using static PanoramicData.ChartMagic.Test.Support.ChartFixtures;
using static PanoramicData.ChartMagic.Test.Support.RenderedChart;

namespace PanoramicData.ChartMagic.Test;

/// <summary>
/// Data labels on charts drawn against axes, and funnel charts.
/// </summary>
/// <remarks>
/// Both rendered nothing and reported success. Label text was drawn only for pies, so a column,
/// bar or line chart asking for #VAL labels had none; and Funnel was a chart type with nothing to
/// draw it, so a funnel came out as an empty plot. The placements asserted here are the ones
/// measured against DocMagic on Magic Suite's [List.Graph:] examples, and every assertion was
/// checked against the renderer before the change to confirm that it fails there.
/// </remarks>
public class DataLabelAndFunnelTests
{
	private static List<XElement> DataLabels(XDocument document)
		=> FindGroupById(document, "dataLabels") is { } group ? Elements(group, "text") : [];

	private static double Y(XElement element) => Number(element, "y");

	private static double X(XElement element) => Number(element, "x");

	// ---------------------------------------------------------------------
	// Data labels
	// ---------------------------------------------------------------------

	[Fact]
	public void ColumnChart_WithLabelText_LabelsEveryColumnWithItsValue()
	{
		var specification = SingleSeries(SeriesChartType.Column, Points(10, 24, 17, 31), s => s.LabelText = "#VAL");

		DataLabels(Render(specification)).Select(label => label.Value)
			.Should().Equal(["10", "24", "17", "31"], "#VAL is the Microsoft chart shorthand for #VALY");
	}

	[Fact]
	public void ColumnChart_WithoutLabelText_DrawsNoLabels()
		=> DataLabels(Render(SingleSeries(SeriesChartType.Column, Points(10, 24, 17, 31))))
			.Should().BeEmpty("the Microsoft chart control draws no data labels unless asked to");

	[Fact]
	public void ColumnChart_LabelSitsCentredAboveItsColumn()
	{
		var document = Render(SingleSeries(SeriesChartType.Column, Points(10, 24, 17, 31), s => s.LabelText = "#VAL"));
		var column = Elements(GroupById(document, "series0"), "rect")[0];
		var label = DataLabels(document)[0];

		label.Attribute("text-anchor")!.Value.Should().Be("middle");
		X(label).Should().BeApproximately(X(column) + (Number(column, "width") / 2), 0.5);
		Y(label).Should().BeLessThan(Y(column), "a column's label sits above its top");
	}

	[Fact]
	public void BarChart_LabelSitsBeyondTheEndOfItsBar_ReadingOutwards()
	{
		var document = Render(SingleSeries(SeriesChartType.Bar, Points(10, 24, 17, 31), s => s.LabelText = "#VALX"));
		var bar = Elements(GroupById(document, "series0"), "rect")[0];
		var label = DataLabels(document)[0];

		label.Value.Should().Be("Jan", "#VALX is the category");
		label.Attribute("text-anchor")!.Value.Should().Be("start");
		X(label).Should().BeGreaterThan(X(bar) + Number(bar, "width"), "a bar's label sits beyond its end");
	}

	[Fact]
	public void LineChart_LabelSitsAboveItsPoint()
	{
		var document = Render(SingleSeries(SeriesChartType.Line, Points(10, 24, 17, 31), s => s.LabelText = "#VALY"));

		var labels = DataLabels(document);
		labels.Select(label => label.Value).Should().Equal("10", "24", "17", "31");

		// The largest value is drawn highest, so its label is the highest too.
		labels.MinBy(Y)!.Value.Should().Be("31");
	}

	[Fact]
	public void StackedColumn_LabelSitsInsideItsSegment()
	{
		var specification = ColumnChart(SeriesChartType.StackedColumn, 2);
		specification.SeriesList.ForEach(series => series.LabelText = "#VAL");

		var document = Render(specification);
		var segment = Elements(GroupById(document, "series0"), "rect")[0];
		var label = DataLabels(document)[0];

		Y(label).Should().BeInRange(Y(segment), Y(segment) + Number(segment, "height"));
	}

	[Fact]
	public void DataLabels_SubstituteEveryKeyword()
	{
		var specification = SingleSeries(
			SeriesChartType.Column,
			Points(25, 75),
			s => s.LabelText = "#VALX #VALY #VAL #PERCENT #TOTAL");

		DataLabels(Render(specification)).Select(label => label.Value)
			.Should().Equal("Jan 25 25 25.00% 100", "Feb 75 75 75.00% 100");
	}

	[Fact]
	public void PieChart_AlsoSubstitutesVal()
	{
		// #VAL was never substituted for a pie either: it is a prefix of #VALX and #VALY, which
		// were, and it was left on the chart as the literal text "#VAL".
		var specification = new ChartSpecification
		{
			SeriesList =
			[
				new() { ChartType = SeriesChartType.Pie, LabelText = "#VAL", Points = Points(40, 60) }
			]
		};

		Elements(GroupById(Render(specification), "pie"), "text").Select(label => label.Value)
			.Should().Equal("40", "60");
	}

	// ---------------------------------------------------------------------
	// Funnels
	// ---------------------------------------------------------------------

	private static ChartSpecification Funnel(string? labelText = null)
		=> new()
		{
			SeriesList =
			[
				new()
				{
					ChartType = SeriesChartType.Funnel,
					LabelText = labelText,
					Points =
					[
						new ChartPoint("Prospect", 0, 100000, Color.SteelBlue),
						new ChartPoint("Opportunity", 1, 30000, Color.SeaGreen),
						new ChartPoint("Quoted", 2, 30000, Color.Goldenrod)
					]
				}
			]
		};

	private static List<XElement> Segments(XDocument document)
		=> Elements(GroupById(document, "funnel"), "path");

	/// <summary>The top and bottom of a segment, from its outline.</summary>
	private static (double Top, double Bottom) VerticalExtent(XElement segment)
	{
		var ys = segment.Attribute("d")!.Value
			.Split(['M', 'L', 'Z'], StringSplitOptions.RemoveEmptyEntries)
			.Select(pair => double.Parse(pair.Trim().Split(' ')[1], CultureInfo.InvariantCulture))
			.ToList();
		return (ys.Min(), ys.Max());
	}

	private static double WidthAtTop(XElement segment)
	{
		var points = segment.Attribute("d")!.Value
			.Split(['M', 'L', 'Z'], StringSplitOptions.RemoveEmptyEntries)
			.Select(pair => pair.Trim().Split(' ').Select(n => double.Parse(n, CultureInfo.InvariantCulture)).ToArray())
			.ToList();
		return points[1][0] - points[0][0];
	}

	[Fact]
	public void Funnel_DrawsOneSegmentPerPoint_InThePointColours()
	{
		var segments = Segments(Render(Funnel()));

		segments.Should().HaveCount(3);
		segments.Select(segment => segment.Attribute("fill")!.Value)
			.Should().Equal(Color.SteelBlue.ToHex(), Color.SeaGreen.ToHex(), Color.Goldenrod.ToHex());
	}

	[Fact]
	public void Funnel_SegmentHeights_AreProportionalToTheValues()
	{
		// FunnelStyle YIsHeight, the Microsoft chart default: 100000 against 30000.
		var heights = Segments(Render(Funnel()))
			.Select(VerticalExtent)
			.Select(extent => extent.Bottom - extent.Top)
			.ToList();

		(heights[0] / heights[1]).Should().BeApproximately(100000d / 30000d, 0.01);
		heights[1].Should().BeApproximately(heights[2], 0.01);
	}

	[Fact]
	public void Funnel_SegmentsAreStackedTopToBottom_AndNarrowDownwards()
	{
		var segments = Segments(Render(Funnel()));

		VerticalExtent(segments[0]).Bottom.Should().BeApproximately(VerticalExtent(segments[1]).Top, 0.01);
		VerticalExtent(segments[1]).Bottom.Should().BeApproximately(VerticalExtent(segments[2]).Top, 0.01);
		WidthAtTop(segments[0]).Should().BeGreaterThan(WidthAtTop(segments[1]));
		WidthAtTop(segments[1]).Should().BeGreaterThan(WidthAtTop(segments[2]));
	}

	[Fact]
	public void Funnel_LabelsEachSegmentFromTheLabelText_ToTheRightOfTheFunnel()
	{
		var document = Render(Funnel("#VALX: GBP #VALY"));
		var funnel = GroupById(document, "funnel");

		var labels = Elements(funnel, "text");
		labels.Select(label => label.Value)
			.Should().Equal("Prospect: GBP 100000", "Opportunity: GBP 30000", "Quoted: GBP 30000");

		var rightmostSegmentEdge = Segments(document)
			.SelectMany(segment => segment.Attribute("d")!.Value
				.Split(['M', 'L', 'Z'], StringSplitOptions.RemoveEmptyEntries)
				.Select(pair => double.Parse(pair.Trim().Split(' ')[0], CultureInfo.InvariantCulture)))
			.Max();
		labels.Should().AllSatisfy(label => X(label).Should().BeGreaterThan(rightmostSegmentEdge));

		Elements(funnel, "line").Should().HaveCount(3, "each label is joined to its segment by a leader line");
	}

	[Fact]
	public void Funnel_DrawsNoAxes()
		=> FindGroupById(Render(Funnel()), "xAxis").Should().BeNull("a funnel is not drawn against axes");
}
