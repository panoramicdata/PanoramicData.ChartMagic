using System.Drawing;
using System.Globalization;
using System.Xml.Linq;
using static PanoramicData.ChartMagic.Test.Support.ChartFixtures;
using static PanoramicData.ChartMagic.Test.Support.RenderedChart;

namespace PanoramicData.ChartMagic.Test;

/// <summary>
/// Radar and polar charts, which are drawn on a circular web rather than against axes (issue 76).
/// </summary>
public class RadialTests
{
	private static ChartSpecification Radar()
		=> SingleSeries(SeriesChartType.Radar, Points(10, 24, 17, 31), series => series.StrokeColor = Color.Crimson);

	private static ChartSpecification Polar()
		=> SingleSeries(
			SeriesChartType.Polar,
			[new(null, 0, 10), new(null, 90, 20), new(null, 180, 30), new(null, 270, 40)]);

	private static List<XElement> Spokes(XDocument document, string web)
		=> Elements(GroupById(document, web), "line");

	private static List<double[]> Vertices(XElement path)
		=> [.. path.Attribute("d")!.Value
			.Split(['M', 'L', 'Z'], StringSplitOptions.RemoveEmptyEntries)
			.Select(pair => pair.Trim().Split(' ').Select(n => double.Parse(n, CultureInfo.InvariantCulture)).ToArray())];

	[Theory]
	[InlineData("Radar", "radar")]
	[InlineData("Polar", "polar")]
	public void RadialChart_DrawsNoAxes(string type, string group)
	{
		var document = Render(type == "Radar" ? Radar() : Polar());

		GroupById(document, group).Should().NotBeNull();
		FindGroupById(document, "xAxis").Should().BeNull();
		FindGroupById(document, "yAxis").Should().BeNull();
	}

	[Fact]
	public void Radar_DrawsOneSpokePerCategory_ClockwiseFromTwelveOClock()
	{
		var spokes = Spokes(Render(Radar()), "radarWeb");

		spokes.Should().HaveCount(4);
		var (cx, cy) = (Number(spokes[0], "x1"), Number(spokes[0], "y1"));
		spokes.Should().AllSatisfy(spoke =>
		{
			Number(spoke, "x1").Should().Be(cx);
			Number(spoke, "y1").Should().Be(cy);
		});
		Number(spokes[0], "x2").Should().BeApproximately(cx, 0.01);
		Number(spokes[0], "y2").Should().BeLessThan(cy, "the first category points up");
		Number(spokes[1], "x2").Should().BeGreaterThan(cx, "the second is a quarter turn clockwise");
		Number(spokes[2], "y2").Should().BeGreaterThan(cy);
		Number(spokes[3], "x2").Should().BeLessThan(cx);
	}

	[Fact]
	public void Radar_LabelsEachCategoryBeyondTheRim()
	{
		var document = Render(Radar());
		var spokes = Spokes(document, "radarWeb");

		var labels = Elements(GroupById(document, "radarWeb"), "text").Where(text => Categories.Contains(text.Value)).ToList();

		labels.Select(label => label.Value).Should().Equal(Categories);
		Number(labels[0], "y").Should().BeLessThan(Number(spokes[0], "y2"), "the top label sits above the end of its spoke");
	}

	[Fact]
	public void Radar_DrawsConcentricRingsAtTheValueTicks()
	{
		var rings = Elements(GroupById(Render(Radar()), "radarWeb"), "path");

		rings.Should().HaveCount(7, "ticks at 5, 10 ... 35 on a 0 to 35 scale; the centre is the origin");
		rings.Should().AllSatisfy(ring => Vertices(ring).Should().HaveCount(4, "a ring has a vertex on every spoke"));
	}

	[Fact]
	public void Radar_DrawsTheSeriesAsAClosedPolygon_ScaledFromTheCentre()
	{
		var document = Render(Radar());
		var spokes = Spokes(document, "radarWeb");
		var (cx, cy) = (Number(spokes[0], "x1"), Number(spokes[0], "y1"));

		var polygon = Elements(GroupById(document, "series0"), "path").Should().ContainSingle().Subject;
		var vertices = Vertices(polygon);

		polygon.Attribute("d")!.Value.Should().EndWith("Z");
		vertices.Should().HaveCount(4);
		var radii = vertices.Select(v => Math.Sqrt(Math.Pow(v[0] - cx, 2) + Math.Pow(v[1] - cy, 2))).ToList();
		(radii[3] / radii[0]).Should().BeApproximately(3.1, 0.02, "31 against 10 on a scale that starts at zero");
		vertices[0][0].Should().BeApproximately(cx, 0.01, "the first category lies on the upward spoke");
	}

	[Fact]
	public void Radar_LegendNamesTheSeries()
		=> LabelTexts(Render(Radar()), "legend").Should().Equal("Series 1");

	[Fact]
	public void Polar_DrawsASpokeEveryThirtyDegrees_AndLabelsThem()
	{
		var document = Render(Polar());

		Spokes(document, "polarWeb").Should().HaveCount(12);
		Elements(GroupById(document, "polarWeb"), "text").Select(text => text.Value)
			.Should().Contain(["0", "90", "180", "270"]);
	}

	[Fact]
	public void Polar_PlacesEachPointAtItsAngleClockwiseFromTwelveOClock_AndItsValueOutFromTheCentre()
	{
		var document = Render(Polar());
		var spokes = Spokes(document, "polarWeb");
		var (cx, cy) = (Number(spokes[0], "x1"), Number(spokes[0], "y1"));

		var line = Elements(GroupById(document, "series0"), "path").Should().ContainSingle().Subject;
		var vertices = Vertices(line);

		line.Attribute("d")!.Value.Should().NotContain("Z", "a polar series is an open line");
		vertices[0][0].Should().BeApproximately(cx, 0.01);
		vertices[0][1].Should().BeLessThan(cy, "0 degrees is straight up");
		vertices[1][1].Should().BeApproximately(cy, 0.01);
		vertices[1][0].Should().BeGreaterThan(cx, "90 degrees is to the right");
		var first = cy - vertices[0][1];
		var third = Math.Abs(vertices[2][1] - cy);
		(third / first).Should().BeApproximately(3, 0.02, "30 against 10");
	}

	private static readonly string[] Categories = ["Jan", "Feb", "Mar", "Apr"];
}
