using System.Drawing;
using System.Globalization;
using System.Xml.Linq;
using static PanoramicData.ChartMagic.Test.Support.RenderedChart;

namespace PanoramicData.ChartMagic.Test;

/// <summary>
/// Pyramids, which are drawn as a stack of segments widening to a base, with no axes (issue 76).
/// </summary>
public class PyramidTests
{
	private static ChartSpecification Pyramid(string? labelText = null)
		=> new()
		{
			SeriesList =
			[
				new()
				{
					ChartType = SeriesChartType.Pyramid,
					LabelText = labelText,
					Points =
					[
						new ChartPoint("Top", 0, 10, Color.SteelBlue),
						new ChartPoint("Middle", 1, 30, Color.SeaGreen),
						new ChartPoint("Base", 2, 60, Color.Goldenrod)
					]
				}
			]
		};

	private static List<XElement> Segments(XDocument document)
		=> Elements(GroupById(document, "pyramid"), "path");

	private static List<double[]> Outline(XElement segment)
		=> [.. segment.Attribute("d")!.Value
			.Split(['M', 'L', 'Z'], StringSplitOptions.RemoveEmptyEntries)
			.Select(pair => pair.Trim().Split(' ').Select(n => double.Parse(n, CultureInfo.InvariantCulture)).ToArray())];

	private static double WidthAt(XElement segment, bool top)
	{
		var points = Outline(segment);
		var y = top ? points.Min(p => p[1]) : points.Max(p => p[1]);
		var xs = points.Where(p => Math.Abs(p[1] - y) < 0.01).Select(p => p[0]).ToList();
		return xs.Max() - xs.Min();
	}

	[Fact]
	public void Pyramid_DrawsOneSegmentPerPoint_InThePointColours()
	{
		var segments = Segments(Render(Pyramid()));

		segments.Select(segment => segment.Attribute("fill")!.Value)
			.Should().Equal(Color.SteelBlue.ToHex(), Color.SeaGreen.ToHex(), Color.Goldenrod.ToHex());
	}

	[Fact]
	public void Pyramid_DrawsNoAxes()
	{
		var document = Render(Pyramid());

		FindGroupById(document, "xAxis").Should().BeNull();
		FindGroupById(document, "yAxis").Should().BeNull();
		FindGroupById(document, "funnel").Should().BeNull("a pyramid is not a funnel");
	}

	[Fact]
	public void Pyramid_ComesToAPointAtTheTop_AndWidensDownwards()
	{
		var segments = Segments(Render(Pyramid()));

		WidthAt(segments[0], top: true).Should().BeApproximately(0, 0.01, "the apex is a point");
		WidthAt(segments[0], top: false).Should().BeApproximately(WidthAt(segments[1], top: true), 0.01);
		WidthAt(segments[1], top: false).Should().BeApproximately(WidthAt(segments[2], top: true), 0.01);
		WidthAt(segments[2], top: false).Should().BeGreaterThan(WidthAt(segments[1], top: false));
	}

	[Fact]
	public void Pyramid_SegmentHeights_AreProportionalToTheValues()
	{
		var heights = Segments(Render(Pyramid()))
			.Select(segment => Outline(segment).Max(p => p[1]) - Outline(segment).Min(p => p[1]))
			.ToList();

		(heights[2] / heights[0]).Should().BeApproximately(6, 0.01);
		(heights[1] / heights[0]).Should().BeApproximately(3, 0.01);
	}

	[Fact]
	public void Pyramid_LabelsEachSegment_ToTheRight_WithALeaderLine()
	{
		var pyramid = GroupById(Render(Pyramid("#VALX: #VALY")), "pyramid");

		Elements(pyramid, "text").Select(label => label.Value).Should().Equal("Top: 10", "Middle: 30", "Base: 60");
		Elements(pyramid, "line").Should().HaveCount(3);
	}

	[Fact]
	public void Pyramid_LegendNamesEverySegment()
		=> LabelTexts(Render(Pyramid()), "legend").Should().Equal("Top", "Middle", "Base");
}
