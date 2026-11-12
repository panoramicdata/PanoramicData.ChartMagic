using System.Globalization;
using static PanoramicData.ChartMagic.Test.Support.ChartFixtures;
using static PanoramicData.ChartMagic.Test.Support.RenderedChart;

namespace PanoramicData.ChartMagic.Test;

/// <summary>
/// A date X axis, where XValueType says the X values are dates (issue 76).
/// </summary>
/// <remarks>
/// An X value is an OLE Automation date, which is how the Microsoft chart control holds one: days
/// since 30 December 1899. Dates that are unevenly spaced are drawn unevenly spaced.
/// </remarks>
public class DateAxisTests
{
	private static readonly DateTime First = new(2026, 1, 1);

	private static double Day(int offset) => First.AddDays(offset).ToOADate();

	private static ChartSpecification Dated(ChartValueType valueType = ChartValueType.DateTime)
	{
		var specification = SingleSeries(
			SeriesChartType.Line,
			[new(null, Day(0), 10), new(null, Day(1), 24), new(null, Day(9), 17)]);
		specification.XValueType = valueType;
		return specification;
	}

	[Fact]
	public void DateAxis_LabelsTicksAsDates()
	{
		var labels = LabelTexts(Render(Dated()), "xAxis");

		labels.Should().NotBeEmpty();
		labels.Should().AllSatisfy(label => DateTime.TryParse(label, CultureInfo.InvariantCulture, out _).Should().BeTrue($"'{label}' reads as a date"));
		labels.Should().Contain("01/01/2026").And.Contain("01/10/2026");
	}

	[Fact]
	public void DateAxis_TicksFallOnWholeDays()
		=> LabelTexts(Render(Dated()), "xAxis").Select(label => DateTime.Parse(label, CultureInfo.InvariantCulture))
			.Should().AllSatisfy(date => date.TimeOfDay.Should().Be(TimeSpan.Zero));

	[Fact]
	public void DateAxis_SpacesUnevenDatesInProportionToTheTimeBetweenThem()
	{
		var path = Elements(GroupById(Render(Dated()), "series0"), "path")[0].Attribute("d")!.Value;
		var xs = path.Split(['M', 'L'], StringSplitOptions.RemoveEmptyEntries)
			.Select(pair => double.Parse(pair.Trim().Split(' ')[0], CultureInfo.InvariantCulture))
			.ToList();

		((xs[1] - xs[0]) / (xs[2] - xs[0])).Should().BeApproximately(1d / 9, 0.005, "one day of nine");
	}

	[Fact]
	public void DateAxis_FollowsALabelFormat()
	{
		var specification = Dated();
		specification.XAxisLabelFormat = "yyyy-MM-dd";

		LabelTexts(Render(specification), "xAxis").Should().Contain("2026-01-01");
	}

	[Fact]
	public void DateAxis_FollowsAnIntervalInWeeks()
	{
		var specification = Dated();
		specification.XAxisInterval = 1;
		specification.XAxisIntervalType = DateTimeIntervalType.Weeks;

		LabelTexts(Render(specification), "xAxis").Should().Equal("01/01/2026", "01/08/2026");
	}

	[Fact]
	public void NumericXValueType_LeavesTheAxisNumeric()
	{
		var specification = Dated(ChartValueType.Double);

		NumericLabels(Render(specification), "xAxis").Should().NotBeEmpty();
	}
}
