using System.Globalization;
using PanoramicData.ChartMagic.Renderers;
using static PanoramicData.ChartMagic.Test.Support.ChartFixtures;
using static PanoramicData.ChartMagic.Test.Support.RenderedChart;

namespace PanoramicData.ChartMagic.Test;

/// <summary>
/// MS-27509: a .NET format string in braces after a label keyword, as in #PERCENT{P0}.
/// </summary>
public class DataLabelFormatTests
{
	private static readonly CultureInfo EnGb = CultureInfo.GetCultureInfo("en-GB");

	private static readonly ChartPoint Point = new("Mon", 0, 1234.5);

	private static string? Substitute(string text, CultureInfo? culture = null, double percentage = 43.5897, double total = 4567.5)
		=> DataLabelText.Substitute(text, Point, 1234.5, "Cost", percentage, total, culture ?? EnGb);

	[Theory]
	[InlineData("#PERCENT{P0}", "44%")]
	[InlineData("#PERCENT{P1}", "43.6%")]
	[InlineData("#VAL{C2}", "£1,234.50")]
	[InlineData("#VALY{N1}", "1,234.5")]
	[InlineData("#VAL{F3}", "1234.500")]
	[InlineData("#TOTAL{N0}", "4,568")]
	[InlineData("#val{c2}", "£1,234.50")]
	public void AFormat_IsAppliedToTheKeywordBeforeIt(string text, string expected)
		=> Substitute(text).Should().Be(expected);

	[Theory]
	[InlineData("#PERCENT", "43.59%")]
	[InlineData("#VAL", "1234.5")]
	[InlineData("#VALY", "1234.5")]
	[InlineData("#VALX", "Mon")]
	[InlineData("#TOTAL", "4567.5")]
	public void AKeywordWithoutAFormat_IsUnchanged(string text, string expected)
		=> Substitute(text).Should().Be(expected);

	[Fact]
	public void TheFormat_UsesTheCulture()
		=> Substitute("#VAL{N1} #PERCENT{P0}", CultureInfo.GetCultureInfo("de-DE")).Should().Be("1.234,5 44 %");

	[Theory]
	[InlineData("#VAL{Z9}", "1234.5")]
	[InlineData("#PERCENT{Z9}", "43.59%")]
	public void AnUnknownFormat_FallsBackToTheUnformattedValue(string text, string expected)
		=> Substitute(text).Should().Be(expected);

	[Theory]
	[InlineData("#VAL{}", "1234.5")]
	[InlineData("#PERCENT{}", "43.59%")]
	public void EmptyBraces_AreNoFormat(string text, string expected)
		=> Substitute(text).Should().Be(expected);

	[Fact]
	public void CombinedText_FormatsEachKeyword()
		=> Substitute("#VALX\n(#PERCENT{P1})").Should().Be("Mon\n(43.6%)");

	[Fact]
	public void ANumericXValue_TakesTheFormat()
		=> DataLabelText.Substitute("#VALX{N2}", new ChartPoint(null, 3, 1), 1, null, 100, 1, EnGb).Should().Be("3.00");

	[Fact]
	public void AStringXValue_IgnoresTheFormat()
		=> Substitute("#VALX{N2}").Should().Be("Mon");

	[Fact]
	public void ATextKeyword_KeepsTheBracesAsWritten()
		=> Substitute("#SER{N2}").Should().Be("Cost{N2}");

	[Fact]
	public void ASubstitutedValue_IsNotSearchedForKeywords()
		=> DataLabelText.Substitute("#SER", Point, 1, "#VAL", 100, 1, EnGb).Should().Be("#VAL");

	[Fact]
	public void PieChart_PercentWithFormat_WritesWholePercentages()
	{
		var specification = new ChartSpecification
		{
			SeriesList =
			[
				new()
				{
					ChartType = SeriesChartType.Pie,
					LabelText = "#PERCENT{P0}",
					Points = [new("London", 0, 34), new("Manchester", 1, 26), new("Leeds", 2, 18)]
				}
			]
		};
		var chart = specification.ToChart();
		chart.Culture = EnGb;

		LabelTexts(Render(chart), "pie").Should().Equal("44%", "33%", "23%");
	}

	[Fact]
	public void ColumnChart_ValueWithCurrencyFormat_WritesTheCulturesCurrency()
	{
		var chart = SingleSeries(SeriesChartType.Column, Points(1234.5, 2345.75, 987.25), s => s.LabelText = "#VAL{C2}").ToChart();
		chart.Culture = EnGb;

		LabelTexts(Render(chart), "dataLabels").Should().Equal("£1,234.50", "£2,345.75", "£987.25");
	}
}
