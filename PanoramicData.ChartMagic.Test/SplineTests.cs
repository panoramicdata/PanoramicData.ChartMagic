using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using static PanoramicData.ChartMagic.Test.Support.ChartFixtures;
using static PanoramicData.ChartMagic.Test.Support.RenderedChart;

namespace PanoramicData.ChartMagic.Test;

/// <summary>
/// Spline and spline area, which draw a smooth curve through their points rather than straight
/// segments (issue 76).
/// </summary>
public partial class SplineTests
{
	[GeneratedRegex(@"-?\d+(\.\d+)?")]
	private static partial Regex Number();

	private static List<XElement> Paths(SeriesChartType chartType)
		=> Elements(GroupById(Render(SingleSeries(chartType, Points(10, 24, 17, 31))), "series0"), "path");

	private static double[] Numbers(XElement path)
		=> [.. Number().Matches(path.Attribute("d")!.Value).Select(m => double.Parse(m.Value, CultureInfo.InvariantCulture))];

	[Fact]
	public void Spline_DrawsItsLineAsCurves()
	{
		var path = Paths(SeriesChartType.Spline).Should().ContainSingle().Subject;

		path.Attribute("d")!.Value.Should().StartWith("M").And.Contain("C").And.NotContain("L");
	}

	[Fact]
	public void Spline_PassesThroughEveryPointOfTheStraightLine()
	{
		var straight = Numbers(Paths(SeriesChartType.Line).Single());
		var curved = Numbers(Paths(SeriesChartType.Spline).Single());

		var straightPoints = straight.Chunk(2).ToList();
		var curvedEnds = new List<double[]> { curved[..2] };
		curvedEnds.AddRange(curved[2..].Chunk(6).Select(segment => segment[4..]));

		curvedEnds.Should().HaveCount(straightPoints.Count);
		curvedEnds.Zip(straightPoints).Should().AllSatisfy(pair =>
		{
			pair.First[0].Should().BeApproximately(pair.Second[0], 0.01);
			pair.First[1].Should().BeApproximately(pair.Second[1], 0.01);
		});
	}

	[Fact]
	public void Spline_ControlPointsFollowACardinalSplineOfTensionOneHalf()
	{
		var points = Numbers(Paths(SeriesChartType.Line).Single()).Chunk(2).ToList();
		var segments = Numbers(Paths(SeriesChartType.Spline).Single())[2..].Chunk(6).ToList();

		// The middle segment runs from point 1 to point 2, with tangents (P2 - P0) / 6 and (P3 - P1) / 6.
		segments[1][0].Should().BeApproximately(points[1][0] + ((points[2][0] - points[0][0]) / 6), 0.02);
		segments[1][1].Should().BeApproximately(points[1][1] + ((points[2][1] - points[0][1]) / 6), 0.02);
		segments[1][2].Should().BeApproximately(points[2][0] - ((points[3][0] - points[1][0]) / 6), 0.02);
		segments[1][3].Should().BeApproximately(points[2][1] - ((points[3][1] - points[1][1]) / 6), 0.02);
	}

	[Fact]
	public void SplineArea_FillsBeneathACurvedOutline_AndTracesItsLine()
	{
		var paths = Paths(SeriesChartType.SplineArea);

		paths.Should().HaveCount(2, "a fill and a line, as an area has");
		paths[0].Attribute("d")!.Value.Should().Contain("C").And.EndWith("Z");
		paths[1].Attribute("d")!.Value.Should().Contain("C").And.NotContain("L");
	}

	[Fact]
	public void Area_StillDrawsStraightSegments()
		=> Paths(SeriesChartType.Area).Should().AllSatisfy(path => path.Attribute("d")!.Value.Should().NotContain("C"));
}
