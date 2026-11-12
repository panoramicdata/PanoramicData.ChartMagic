using System.Drawing;
using System.Xml.Linq;
using static PanoramicData.ChartMagic.Test.Support.RenderedChart;

namespace PanoramicData.ChartMagic.Test;

/// <summary>
/// Tests for pie and doughnut rendering.
/// </summary>
/// <remarks>
/// A pie takes a different path through the renderer from everything else: no axes, no
/// gridlines, a wedge per point rather than a shape per series, and a legend describing slices.
/// The geometry assertions use the placement measured against DocMagic: a 65%-wide chart area
/// with an inner plot inset 10%, so an 800x400 image gives a 468x360 plot, and the pie is
/// centred in it at 0.95 of its shorter side. Coordinates are local to the inner plot group.
/// </remarks>
public partial class PieTests
{
	/// <summary>Half of the 468-wide inner plot.</summary>
	private const double CentreX = 234;

	/// <summary>Half of the 360-high inner plot.</summary>
	private const double CentreY = 180;

	/// <summary>0.95 * min(468, 360) / 2, the factor measured from DocMagic output.</summary>
	private const double Radius = 171;

	private static readonly string[] Quarters = ["Q1", "Q2", "Q3", "Q4"];

	private static readonly Color[] Colours =
		[Color.SteelBlue, Color.SeaGreen, Color.Goldenrod, Color.IndianRed];

	private static ChartSpecification PieChart(
		SeriesChartType chartType = SeriesChartType.Pie,
		double[]? values = null,
		PieLabelStyle labelStyle = PieLabelStyle.Inside)
	{
		values ??= [25, 25, 25, 25];

		return PieChartOf(
			chartType,
			labelStyle,
			[.. values.Select((value, index) => new ChartPoint(
				Quarters[index % Quarters.Length],
				index,
				value,
				Colours[index % Colours.Length]))]);
	}

	private static ChartSpecification PieChartOf(
		SeriesChartType chartType,
		PieLabelStyle labelStyle,
		List<ChartPoint> points)
		=> new()
		{
			SeriesList =
			[
				new()
				{
					ChartType = chartType,
					PieLabelStyle = labelStyle,
					Points = points
				}
			]
		};

	/// <summary>The pie group, or null where the chart drew none.</summary>
	private static XElement? PieGroup(ChartSpecification specification)
		=> FindGroupById(Render(specification), "pie");

	/// <summary>The <c>d</c> attribute of every wedge, in the order they are drawn.</summary>
	private static List<string> WedgePaths(ChartSpecification specification)
		=> [.. Elements(PieGroup(specification)!, "path").Select(path => path.Attribute("d")!.Value)];

	/// <summary>The <c>d</c> attribute of the first wedge.</summary>
	private static string FirstWedgePath(ChartSpecification specification)
		=> WedgePaths(specification)[0];

	/// <summary>The text drawn on or beside the slices.</summary>
	private static List<string> SliceLabels(ChartSpecification specification)
		=> [.. Elements(PieGroup(specification)!, "text").Select(text => text.Value)];

	/// <summary>
	/// The arc command an inner radius produces, as a fraction of the pie radius. A doughnut hole
	/// is what the ring does not occupy, so a ring of 60 leaves a hole of 0.4.
	/// </summary>
	private static string HoleArc(double holeFraction)
		=> FormattableString.Invariant($"A{Radius * holeFraction:F2} {Radius * holeFraction:F2}");

	/// <summary>A point on the pie's edge, at a clock position.</summary>
	private static string EdgePoint(double offsetX, double offsetY)
		=> FormattableString.Invariant($"{CentreX + offsetX:F2} {CentreY + offsetY:F2}");

	[Fact]
	public void PieChart_DrawsOneWedgePerPoint()
	{
		var pie = PieGroup(PieChart());

		pie.Should().NotBeNull("a pie chart draws into its own group");
		Elements(pie!, "path").Should().HaveCount(4, "one wedge per data point");
	}

	[Fact]
	public void PieChart_WedgesTakeTheColourOfTheirPoint()
	{
		Elements(PieGroup(PieChart())!, "path")
			.Select(w => w.Attribute("fill")!.Value)
			.Should()
			.Equal(["#4682B4", "#2E8B57", "#DAA520", "#CD5C5C"],
				"a pie is coloured per slice, not per series");
	}

	[Fact]
	public void PieChart_FirstWedgeStartsAtThreeOClockAndSweepsClockwise()
	{
		var first = FirstWedgePath(PieChart());

		// Four equal slices. Established by rendering quarters through both renderers: the
		// Microsoft chart control starts at three o'clock, so the first quarter runs from
		// there round to six o'clock.
		first.Should().StartWith(
			FormattableString.Invariant($"M{EdgePoint(0, 0)} L{EdgePoint(Radius, 0)}"),
			"a wedge is drawn from the centre out to the start of its arc, at three o'clock");

		first.Should().Contain(
			FormattableString.Invariant($"1 {EdgePoint(0, Radius)}"),
			"a quarter slice from three o'clock ends at the bottom, drawn clockwise");
	}

	[Fact]
	public void PieChart_SliceSizeFollowsTheValue()
	{
		// 50, 25, 25: the first slice is a half, so its arc is the only one flagged as large.
		var wedges = WedgePaths(PieChart(values: [50, 25, 25]));

		wedges.Should().HaveCount(3);

		// Starting at three o'clock, a half circle ends at nine.
		wedges[0].Should().Contain(EdgePoint(-Radius, 0));

		// And the next quarter ends at twelve.
		wedges[1].Should().Contain(EdgePoint(0, -Radius));
	}

	[Fact]
	public void PieChart_StartAngleRotatesTheWholePie()
	{
		var specification = PieChart();
		specification.PieStartAngleDegrees = 90;

		// Started a quarter turn on from three o'clock, the first slice begins at six.
		FirstWedgePath(specification).Should().StartWith(
			FormattableString.Invariant($"M{EdgePoint(0, 0)} L{EdgePoint(0, Radius)}"));
	}

	[Fact]
	public void PieChart_ASingleValueDrawsACompleteCircle()
	{
		// One slice sweeps the full 360 degrees, which cannot be drawn as a single arc because
		// its start and end points coincide.
		var wedges = WedgePaths(PieChart(values: [100]));

		wedges.Should().HaveCount(1);
		wedges[0].Split('A').Should().HaveCount(3, "a full circle is drawn as two half arcs");
		wedges[0].Should().NotContain("NaN");
	}

	[Fact]
	public void Doughnut_LeavesAHoleAtTheDefaultRadius()
	{
		var path = FirstWedgePath(PieChart(SeriesChartType.Doughnut));

		// A doughnut wedge is a band: out along one radius, round, back along the other. It never
		// visits the centre, which a pie wedge always does.
		path.Should().NotStartWith(FormattableString.Invariant($"M{EdgePoint(0, 0)}"));

		// The default radius of 60 is the width of the RING, so the hole is the other 40% - not
		// 60%, as this asserted while the renderer made the same mistake. Measured against the
		// reference render: at 285 pixels across, its default doughnut left a hole 116 wide, which
		// is 40.7%.
		path.Should().Contain(
			HoleArc(0.4),
			"the inner arc runs at the hole radius, which is what the ring does not occupy");
	}

	/// <summary>
	/// A narrower ring leaves a bigger hole, whether the radius arrives as a number or a string.
	/// </summary>
	/// <remarks>
	/// Measured: asking the reference renderer for 30 left a hole 202 of 285 pixels wide, or
	/// 70.9%. Reading the number as the hole itself inverted the shape - a request for a thin ring
	/// drew a fat one.
	///
	/// The specification declares DoughnutRadius as an object because the corresponding Microsoft
	/// chart custom property is a string, and callers pass either, so both are exercised here.
	/// </remarks>
	[Theory]
	[InlineData(30d)]
	[InlineData("30")]
	public void Doughnut_NarrowerRingLeavesABiggerHole(object radius)
	{
		var specification = PieChart(SeriesChartType.Doughnut);
		specification.DoughnutRadius = radius;

		FirstWedgePath(specification).Should().Contain(HoleArc(0.7));
	}

	[Fact]
	public void PieChart_DrawsNoAxes()
	{
		var document = Render(PieChart());

		FindGroupById(document, "xAxis").Should().BeNull("a pie has no axes");
		FindGroupById(document, "yAxis").Should().BeNull("a pie has no axes");
		FindGroupById(document, "gridlines").Should().BeNull("nor gridlines");
	}

	[Fact]
	public void PieChart_LegendNamesEverySlice()
	{
		var document = Render(PieChart());
		var legend = FindGroupById(document, "legend");

		legend.Should().NotBeNull();
		LabelTexts(document, "legend").Should().Equal(Quarters,
			"a pie legend describes slices, not series");
		Elements(legend!, "rect").Should().HaveCount(
			5,
			"one swatch per slice, plus the legend background");
	}

	[Fact]
	public void PieChart_LabelsShowTheCategoryNameByDefault()
	{
		// Measured against DocMagic: with no label text set, the Microsoft chart control labels a
		// pie slice with its X value, so the names appear rather than the numbers.
		SliceLabels(PieChart(values: [10, 20, 30, 40])).Should().Equal(Quarters);
	}

	[Fact]
	public void PieChart_LabelStyleDisabledDrawsNoLabels()
	{
		var specification = PieChart(labelStyle: PieLabelStyle.Disabled);

		SliceLabels(specification).Should().BeEmpty();
		WedgePaths(specification).Should().HaveCount(4, "the slices are still drawn");
	}
}
