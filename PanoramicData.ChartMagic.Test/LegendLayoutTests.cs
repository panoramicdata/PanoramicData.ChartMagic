using PanoramicData.ChartMagic.Renderers;
using System.Xml.Linq;
using static PanoramicData.ChartMagic.Test.Support.ChartFixtures;
using static PanoramicData.ChartMagic.Test.Support.RenderedChart;

namespace PanoramicData.ChartMagic.Test;

/// <summary>
/// Tests for the legend layout in issue #35.
/// </summary>
/// <remarks>
/// The numbers here were measured off reference renders rather than chosen, and the remarks on
/// each test say which render and what the previous layout produced instead.
/// </remarks>
public class LegendLayoutTests
{
	[Fact]
	public void LegendLabels_DoNotOverlap()
	{
		var legend = GroupById(Render(ColumnChart(SeriesChartType.Column, 3)), "legend");

		var labels = Elements(legend, "text").ToList();
		labels.Should().HaveCount(3);

		// Issue #35: the labels used to be spaced by a fraction of their intended distance and
		// sat on top of one another. Each label needs at least its own width of room, and at
		// the default font size "Series 1" is about eight characters wide. A row too narrow for
		// all three wraps, so a pair may instead be a line apart.
		const double FontSize = 20;
		var minimumSpacing = 8 * FontSize * 0.5;
		for (var i = 0; i < labels.Count; i++)
		{
			for (var j = i + 1; j < labels.Count; j++)
			{
				var apartAcross = Math.Abs(Number(labels[j], "x") - Number(labels[i], "x")) > minimumSpacing;
				var apartDown = Math.Abs(Number(labels[j], "y") - Number(labels[i], "y")) >= FontSize;
				(apartAcross || apartDown).Should().BeTrue("legend labels must not overlap");
			}
		}
	}

	[Fact]
	public void RowLegend_TooNarrowForItsEntries_WrapsOntoAnotherRow()
	{
		// MS-26600: three entries in a narrow row legend ran off its edge, cutting "Memory" to "Memor"
		// and dropping "Disk" altogether.
		var specification = ColumnChart(SeriesChartType.Column, 3);
		string[] names = ["CPU", "Memory", "Disk"];
		for (var index = 0; index < names.Length; index++)
		{
			specification.SeriesList[index].LegendText = names[index];
		}

		specification.LegendStyle = LegendStyle.Row;
		specification.LegendXPositionPercent = 80;
		specification.LegendWidthPercent = 20;
		specification.LegendHeightPercent = 100;
		specification.LegendFontSize = 12;

		var document = Render(specification, 720, 400);
		var labels = Elements(GroupById(document, "legend"), "text").ToList();

		labels.Select(label => label.Value).Should().Equal(names);
		labels.Select(label => Number(label, "y")).Distinct().Should().HaveCountGreaterThan(1, "the entries continue on another row");
		labels.Should().AllSatisfy(label =>
			(Number(label, "x") + TextMeasure.Width(label.Value, Number(label, "font-size"))).Should().BeLessThanOrEqualTo(720 * 0.2, "every label stays inside the legend"));
	}

	[Fact]
	public void StackedChart_ListsItsSeriesTopDown_InTheOrderTheStackReads()
	{
		// MS-26596: the Microsoft chart control lists a stack from its top. Plain columns keep their order.
		var stacked = ColumnChart(SeriesChartType.StackedColumn, 3);
		var plain = ColumnChart(SeriesChartType.Column, 3);
		stacked.LegendStyle = plain.LegendStyle = LegendStyle.Column;

		var plainOrder = LegendOrder(plain);
		plainOrder.Should().HaveCount(3);
		LegendOrder(stacked).Should().Equal(Enumerable.Reverse(plainOrder));
	}

	[Fact]
	public void LegendItemOrder_OverridesTheStackedReversal()
	{
		var stacked = ColumnChart(SeriesChartType.StackedColumn, 3);
		stacked.LegendStyle = LegendStyle.Column;
		var chart = stacked.ToChart();
		chart.Legends[0].ItemOrder = LegendItemOrder.SameAsSeriesOrder;

		var legendTexts = Elements(GroupById(Render(chart), "legend"), "text")
			.OrderBy(t => Number(t, "y"))
			.Select(t => t.Value);

		var plain = ColumnChart(SeriesChartType.Column, 3);
		plain.LegendStyle = LegendStyle.Column;
		legendTexts.Should().Equal(LegendOrder(plain));
	}

	[Fact]
	public void LongLegendText_IsWrappedAtTheThreshold()
	{
		// MS-26626: LegendTextWrapThreshold, 25 characters by default as in the Microsoft chart control.
		var specification = ColumnChart(SeriesChartType.Column, 1);
		specification.SeriesList[0].LegendText = "Average processor utilisation";
		specification.LegendStyle = LegendStyle.Column;

		var lines = Elements(GroupById(Render(specification), "legend"), "text").Select(t => t.Value).ToList();

		lines.Should().Equal("Average processor", "utilisation");
	}

	[Fact]
	public void LegendLabelTooWideForTheImage_IsShortenedWithAnEllipsis_AfterUsingTheInset()
	{
		// MS-26593: a long label in the right-hand legend ran off the image. It takes the inset
		// first, and is shortened only when it still would not fit.
		var specification = ColumnChart(SeriesChartType.Column, 2);
		specification.SeriesList[0].LegendText = "Manchester";
		specification.SeriesList[1].LegendText = "Wolverhampton and Walsall";
		specification.LegendStyle = LegendStyle.Column;
		specification.LegendXPositionPercent = 80;
		specification.LegendWidthPercent = 20;
		specification.LegendFontSize = 12;

		var document = Render(specification, 720, 400);
		var labels = Elements(GroupById(document, "legend"), "text").ToList();

		labels[0].Value.Should().Be("Manchester", "it fits once the inset is given up");
		labels.Should().AllSatisfy(label =>
			(576 + Number(label, "x") + TextMeasure.Width(label.Value, Number(label, "font-size"))).Should().BeLessThanOrEqualTo(720));
		labels.Select(label => label.Value).Should().Contain(text => text.EndsWith("...", StringComparison.Ordinal));
	}

	[Fact]
	public void TableLegend_TooShortForOneColumn_UsesSeveral()
	{
		// MS-26599: the Table style threw NotSupportedException.
		var specification = ColumnChart(SeriesChartType.Column, 4);
		specification.LegendStyle = LegendStyle.Table;
		specification.LegendXPositionPercent = 0;
		specification.LegendYPositionPercent = 85;
		specification.LegendWidthPercent = 100;
		specification.LegendHeightPercent = 15;
		specification.LegendFontSize = 12;

		var labels = Elements(GroupById(Render(specification, 720, 400), "legend"), "text").ToList();

		labels.Should().HaveCount(4);
		labels.Select(label => Number(label, "x")).Distinct().Should().HaveCountGreaterThan(1, "the entries are spread over columns");
	}

	[Fact]
	public void ColumnLegend_TooShortForItsEntries_KeepsThemALineApart()
	{
		// MS-26677: a 10% legend drew three entries on top of one another.
		var specification = ColumnChart(SeriesChartType.Column, 3);
		specification.LegendStyle = LegendStyle.Column;
		specification.LegendHeightPercent = 10;
		specification.LegendWidthPercent = 20;
		specification.LegendFontSize = 12;

		var ys = Elements(GroupById(Render(specification, 720, 400), "legend"), "text")
			.Select(label => Number(label, "y"))
			.Order()
			.ToList();

		for (var index = 1; index < ys.Count; index++)
		{
			(ys[index] - ys[index - 1]).Should().BeGreaterThanOrEqualTo(12 * 0.9, "a line apart at least");
		}
	}

	[Fact]
	public void PieLegend_HonoursTheRowStyle()
	{
		// MS-26623: a pie legend was always one column whatever the style.
		var specification = SingleSeries(SeriesChartType.Pie, Points(34, 26, 18, 13));
		specification.LegendStyle = LegendStyle.Row;
		specification.LegendXPositionPercent = 0;
		specification.LegendYPositionPercent = 85;
		specification.LegendWidthPercent = 100;
		specification.LegendHeightPercent = 15;

		var labels = Elements(GroupById(Render(specification, 720, 400), "legend"), "text").ToList();

		labels.Select(label => Number(label, "y")).Distinct().Should().ContainSingle("one row holds every slice");
	}

	private static List<string> LegendOrder(ChartSpecification specification)
		=> [.. Elements(GroupById(Render(specification), "legend"), "text")
			.OrderBy(t => Number(t, "y"))
			.Select(t => t.Value)];


	[Fact]
	public void LegendLabels_AreNotOutlinedInBlack()
	{
		var legend = GroupById(Render(ColumnChart(SeriesChartType.Column, 2)), "legend");

		Elements(legend, "text").Should().AllSatisfy(
			t => t.Attribute("stroke").Should().BeNull(
				"issue #35: a transparent stroke colour became a black outline on every label"));
	}

	[Fact]
	public void LegendLabels_UseTheLegendTextWhenGiven()
	{
		var specification = ColumnChart(SeriesChartType.Column, 1);
		specification.SeriesList[0].LegendText = "Widgets sold";

		LabelTexts(Render(specification), "legend").Should().Contain("Widgets sold");
	}

	/// <summary>
	/// A column legend matches the reference render: rectangular swatches sized from the font,
	/// spread down the legend, inset from its left edge.
	/// </summary>
	/// <remarks>
	/// Every number here was measured off a reference render of the same chart - 720 by 400, a
	/// legend occupying the right 20% at full height, three series at font size 12 - rather than
	/// chosen. That render put 32 by 14 swatches at x 594, with row centres at y 69.5, 198.5 and
	/// 327.5.
	///
	/// This drew 9 by 9 swatches at x 622 with centres 20 apart around the middle: less than a
	/// third of the swatch area, and three entries huddled in the centre of an otherwise empty
	/// legend. It is the residual difference on every case in the corpus that draws a legend, which
	/// is most of them.
	///
	/// Tolerances are two pixels: the reference is a rasterised render measured by colour
	/// threshold, so its own edges are only good to about a pixel.
	/// </remarks>
	[Fact]
	public void ColumnLegend_MatchesTheMeasuredReferenceLayout()
	{
		const double ImageWidth = 720;
		const double ImageHeight = 400;
		const double FontSize = 12;

		var specification = ColumnChart(SeriesChartType.Column, 3);
		specification.LegendStyle = LegendStyle.Column;
		specification.LegendXPositionPercent = 80;
		specification.LegendYPositionPercent = 0;
		specification.LegendWidthPercent = 20;
		specification.LegendHeightPercent = 100;
		specification.LegendFontSize = FontSize;
		specification.ChartAreaWidthPercent = 80;

		var document = Render(specification, (int)ImageWidth, (int)ImageHeight);
		var entries = SwatchesOf(document, ImageWidth * 0.2)
			.OrderBy(r => Number(r, "y"))
			.ToList();
		entries.Should().HaveCount(3);

		foreach (var entry in entries)
		{
			Number(entry, "width").Should().BeApproximately(32, 2, "the reference swatch is 32 wide");
			Number(entry, "height").Should().BeApproximately(14, 2, "the reference swatch is 14 tall");
			Number(entry, "x").Should().BeApproximately(
				ImageWidth * 0.2 * 0.12,
				2,
				"the reference inset the swatch 18 pixels into a 144-wide legend");
		}

		// Centres, which is where the spread shows.
		var centres = entries
			.Select(r => Number(r, "y") + (Number(r, "height") / 2))
			.ToList();

		centres[0].Should().BeApproximately(69.5, 2);
		centres[1].Should().BeApproximately(198.5, 2);
		centres[2].Should().BeApproximately(327.5, 2);
	}

	/// <summary>
	/// A row legend packs its entries and centres them, leaving room for each label.
	/// </summary>
	/// <remarks>
	/// Two things at once, because they were broken by the same line. The reference render packs
	/// the entries and centres the result rather than giving each an equal share of the width; and
	/// a slot sized without reference to what goes in it put the next swatch on top of the
	/// previous label once the swatch became a rectangle rather than a small square. It was plainly
	/// visible in the demo: "Memor" then a coloured block over the rest of the word.
	///
	/// The room-for-the-label assertion deliberately uses a smaller per-character estimate than
	/// the layout does, so it checks that room was left rather than restating how much.
	/// </remarks>
	[Fact]
	public void RowLegend_PacksEntriesWithoutOverlappingTheirLabels()
	{
		const double FontSize = 12;
		string[] labels = ["CPU", "Memory", "Disk"];

		var specification = ColumnChart(SeriesChartType.Column, 3);
		for (var index = 0; index < labels.Length; index++)
		{
			specification.SeriesList[index].LegendText = labels[index];
		}

		specification.LegendStyle = LegendStyle.Row;
		specification.LegendXPositionPercent = 0;
		specification.LegendYPositionPercent = 0;
		specification.LegendWidthPercent = 100;
		specification.LegendHeightPercent = 15;
		specification.LegendFontSize = FontSize;

		var swatches = SwatchesOf(Render(specification), Width / 2.0)
			.OrderBy(r => Number(r, "x"))
			.ToList();

		swatches.Should().HaveCount(labels.Length);

		// Each entry leaves room for its own label before the next one starts.
		for (var index = 0; index < swatches.Count - 1; index++)
		{
			var swatchWidth = Number(swatches[index], "width");
			var advance = Number(swatches[index + 1], "x") - Number(swatches[index], "x");

			advance.Should().BeGreaterThan(
				swatchWidth + (labels[index].Length * FontSize * 0.4),
				$"the entry for {labels[index]} has to clear its own label");
		}

		// And the row sits in the middle of the legend rather than starting at its edge.
		var first = Number(swatches[0], "x");
		var last = Number(swatches[^1], "x") + Number(swatches[^1], "width");

		first.Should().BeGreaterThan(0, "a centred row does not start hard against the edge");
		((first + last) / 2).Should().BeApproximately(
			Width / 2.0,
			Width * 0.12,
			"the packed row is centred, allowing for the label of the last entry not being measured");
	}

	/// <summary>
	/// The entry swatches of the legend.
	/// </summary>
	/// <remarks>
	/// The legend group carries a background rect of its own, which has no position and spans the
	/// whole legend, so the entries are the positioned rectangles narrower than the legend is.
	/// </remarks>
	private static List<XElement> SwatchesOf(XDocument document, double narrowerThan)
		=> [.. Elements(GroupById(document, "legend"), "rect")
			.Where(r => r.Attribute("x") is not null && r.Attribute("y") is not null)
			.Where(r => Number(r, "width") < narrowerThan)];
}
