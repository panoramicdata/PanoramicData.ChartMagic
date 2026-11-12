using System.Drawing;
using System.Xml.Linq;
using static PanoramicData.ChartMagic.Test.Support.RenderedChart;

namespace PanoramicData.ChartMagic.Test;

public partial class PieTests
{
	[Fact]
	public void PieChart_LabelKeywordsAreSubstituted()
	{
		var specification = PieChart(values: [25, 25, 25, 25]);
		specification.SeriesList[0].LabelText = "#VALX: #PERCENT";

		SliceLabels(specification)
			.Should()
			.Equal(
				["Q1: 25.00%", "Q2: 25.00%", "Q3: 25.00%", "Q4: 25.00%"],
				"#PERCENT carries two decimal places, as it does in the renderer this matches - a "
					+ "reference render of the same chart showed 34.00%, 26.00% and 18.00%");
	}

	/// <summary>
	/// Slices below the threshold are combined into one - unless there is only one of them, in
	/// which case replacing it with a combined slice of the same size would hide its identity and
	/// gain nothing, so it is left alone.
	/// </summary>
	[Theory]
	[InlineData(new[] { 60d, 30, 5, 5 }, "Everything else")]
	[InlineData(new[] { 60d, 35, 5 }, "Q3")]
	public void PieChart_CollectsSeveralSlicesBelowTheThresholdButNotOne(
		double[] values,
		string expectedLastLegendEntry)
	{
		var specification = PieChart(values: values);
		specification.PieCollectedThresholdPercent = 10;
		specification.PieCollectedLabel = "Everything else";

		// Either way there are three wedges: two collected into one, or three left as they are.
		WedgePaths(specification).Should().HaveCount(3);
		LabelTexts(Render(specification), "legend")
			.Should()
			.Equal(["Q1", "Q2", expectedLastLegendEntry]);
	}

	[Fact]
	public void PieChart_IgnoresPointsWithNoValue()
	{
		var specification = PieChartOf(
			SeriesChartType.Pie,
			PieLabelStyle.Inside,
			[
				new("Q1", 0, 50, Color.SteelBlue),
				new("Q2", 1, null, Color.SeaGreen),
				new("Q3", 2, 50, Color.Goldenrod)
			]);

		WedgePaths(specification).Should().HaveCount(2, "a point with no value has no slice");
	}

	[Fact]
	public void PieChart_WithNoUsableValuesDrawsNothingRatherThanThrowing()
	{
		var specification = PieChartOf(
			SeriesChartType.Pie,
			PieLabelStyle.Inside,
			[new("Q1", 0, 0), new("Q2", 1, null)]);

		Elements(PieGroup(specification) ?? new XElement("empty"), "path").Should().BeEmpty();
	}

	[Fact]
	public void PieChart_WedgeCoordinatesAreCultureIndependent()
	{
		// A comma-decimal culture would produce a path no SVG parser can read.
		FirstWedgePath(PieChart(values: [33, 33, 34]))
			.Should()
			.MatchRegex(@"^M[\d.]+ [\d.]+ L[\d.]+ [\d.]+ A[\d.]+ [\d.]+ 0 [01] 1 [\d.]+ [\d.]+ Z$");
	}
}
