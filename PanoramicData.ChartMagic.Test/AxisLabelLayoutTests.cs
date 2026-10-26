using PanoramicData.ChartMagic.Renderers;
using System.Globalization;
using System.Xml.Linq;
using PanoramicData.ChartMagic.Test.Support;
using static PanoramicData.ChartMagic.Test.Support.RenderedChart;

namespace PanoramicData.ChartMagic.Test;

/// <summary>
/// MS-27510 and MS-26633: axis labels and titles never overlap one another and are never clipped.
/// </summary>
/// <remarks>
/// Every box is the measured text, a line of its font size high, rotated as drawn and placed in
/// image coordinates, so an assertion here is about what a reader sees.
/// </remarks>
public class AxisLabelLayoutTests
{
	private const int Width = 600;
	private const int Height = 320;

	private static readonly string[] Days = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];

	private static ChartSpecification Chart(SeriesChartType chartType, string[] categories, double fontSize = 26.67)
		=> new()
		{
			ChartAreaWidthPercent = 80,
			InnerPlotXPositionPercent = 10,
			InnerPlotYPositionPercent = 15,
			InnerPlotWidthPercent = 85,
			InnerPlotHeightPercent = 75,
			XAxisFontSize = fontSize,
			YAxisFontSize = fontSize,
			SeriesList =
			[
				new()
				{
					ChartType = chartType,
					Points = [.. categories.Select((category, index) => new ChartPoint(category, index, (index % 5) + 1))]
				}
			]
		};

	private sealed record Box(string Text, double Left, double Top, double Right, double Bottom, double Angle, double FontSize, (double X, double Y) Anchor);

	/// <summary>The boxes of the text in an axis group, in image coordinates.</summary>
	private static List<Box> Boxes(XDocument document, string axisId)
		=> [.. Elements(GroupById(document, axisId), "text").Select(BoxOf)];

	private static Box BoxOf(XElement text)
	{
		var fontSize = Number(text, "font-size");
		var width = TextMeasure.Width(text.Value, fontSize);
		var x = Number(text, "x");
		var y = Number(text, "y");
		var start = text.Attribute("text-anchor")!.Value switch
		{
			"start" => 0,
			"end" => -width,
			_ => -width / 2
		};

		var angle = 0d;
		if (text.Attribute("transform")?.Value is { } transform)
		{
			angle = double.Parse(transform["rotate(".Length..].Split(' ')[0], CultureInfo.InvariantCulture);
		}

		var (offsetX, offsetY) = text.Ancestors().Select(Translation).Aggregate((0d, 0d), (sum, t) => (sum.Item1 + t.X, sum.Item2 + t.Y));
		var radians = angle * Math.PI / 180;
		var corners = new[] { (start, -0.8 * fontSize), (start + width, -0.8 * fontSize), (start, 0.2 * fontSize), (start + width, 0.2 * fontSize) }
			.Select(corner => (
				X: x + offsetX + (corner.Item1 * Math.Cos(radians)) - (corner.Item2 * Math.Sin(radians)),
				Y: y + offsetY + (corner.Item1 * Math.Sin(radians)) + (corner.Item2 * Math.Cos(radians))))
			.ToList();

		return new Box(
			text.Value,
			corners.Min(c => c.X),
			corners.Min(c => c.Y),
			corners.Max(c => c.X),
			corners.Max(c => c.Y),
			angle,
			fontSize,
			(x + offsetX, y + offsetY));
	}

	private static bool IsTitle(XElement text) => text.Attribute("id")!.Value.EndsWith("Title", StringComparison.Ordinal);

	private static List<Box> Labels(XDocument document, string axisId)
		=> [.. Elements(GroupById(document, axisId), "text").Where(text => !IsTitle(text)).Select(BoxOf)];

	private static Box Title(XDocument document, string axisId)
		=> BoxOf(Elements(GroupById(document, axisId), "text").Single(IsTitle));

	/// <summary>
	/// Whether two labels overlap. Labels drawn at the same angle are parallel bands a line high, so
	/// rotated ones are apart when their baselines are a line apart.
	/// </summary>
	private static bool Overlap(Box a, Box b)
	{
		if (a.Angle == 0 && b.Angle == 0)
		{
			return a.Left < b.Right && b.Left < a.Right && a.Top < b.Bottom && b.Top < a.Bottom;
		}

		var radians = a.Angle * Math.PI / 180;
		var across = Math.Abs(((b.Anchor.X - a.Anchor.X) * -Math.Sin(radians)) + ((b.Anchor.Y - a.Anchor.Y) * Math.Cos(radians)));
		return across < a.FontSize;
	}

	private static void ShouldNotOverlap(List<Box> labels)
	{
		for (var i = 0; i < labels.Count; i++)
		{
			for (var j = i + 1; j < labels.Count; j++)
			{
				Overlap(labels[i], labels[j]).Should().BeFalse($"'{labels[i].Text}' and '{labels[j].Text}' must not overlap");
			}
		}
	}

	private static void ShouldBeInsideTheImage(IEnumerable<Box> boxes)
		=> boxes.Should().AllSatisfy(box =>
		{
			box.Left.Should().BeGreaterThanOrEqualTo(0, $"'{box.Text}' must not be cut off on the left");
			box.Top.Should().BeGreaterThanOrEqualTo(0, $"'{box.Text}' must not be cut off at the top");
			box.Right.Should().BeLessThanOrEqualTo(Width, $"'{box.Text}' must not be cut off on the right");
			box.Bottom.Should().BeLessThanOrEqualTo(Height + 0.01, $"'{box.Text}' must not be cut off at the bottom");
		});

	private static XDocument Render(ChartSpecification specification) => RenderedChart.Render(specification, Width, Height);

	[Theory]
	[InlineData("Device 1", "Device 2", "Device 3", "Device 4")]
	[InlineData("2026-01-01", "2026-01-02", "2026-01-03")]
	public void LabelsThatWouldOverlap_AreTurnedAndKeptApart(params string[] categories)
	{
		var labels = Labels(Render(Chart(SeriesChartType.Column, categories)), "xAxis");

		labels.Should().HaveCount(categories.Length).And.AllSatisfy(label => label.Angle.Should().Be(-45));
		labels.Select(label => label.Text).Should().Equal(categories, "they fit once turned, so none is shortened");
		ShouldNotOverlap(labels);
		ShouldBeInsideTheImage(labels);
	}

	[Fact]
	public void LabelsThatFit_StayLevel()
	{
		var document = Render(Chart(SeriesChartType.Column, Days, 16));

		Labels(document, "xAxis").Should().AllSatisfy(label => label.Angle.Should().Be(0));
		Translation(GroupById(document, "innerPlot")).Should().Be((48d, 32d), "a chart whose labels fit keeps its margins");
	}

	[Fact]
	public void TheXTitle_SitsBelowTheLabels()
	{
		var specification = Chart(SeriesChartType.Column, ["Mon", "Tue", "Wed"]);
		specification.XAxisTitle = "Day";
		specification.YAxisTitle = "Percent";
		var document = RenderedChart.Render(specification, 500, Height);

		var title = Title(document, "xAxis");
		Labels(document, "xAxis").Should().AllSatisfy(label =>
			title.Top.Should().BeGreaterThanOrEqualTo(label.Bottom, $"'Day' must be below '{label.Text}'"));
		title.Bottom.Should().BeLessThanOrEqualTo(Height + 0.01);
	}

	[Fact]
	public void TheXTitle_SitsBelowRotatedLabels()
	{
		var specification = Chart(SeriesChartType.Column, ["Device 1", "Device 2", "Device 3", "Device 4"]);
		specification.XAxisTitle = "Device";
		var document = Render(specification);

		var title = Title(document, "xAxis");
		Labels(document, "xAxis").Should().AllSatisfy(label =>
			title.Top.Should().BeGreaterThanOrEqualTo(label.Bottom, $"'Device' must be below '{label.Text}'"));
		ShouldBeInsideTheImage([title]);
	}

	[Fact]
	public void AVeryLongLabel_IsShortenedWithAnEllipsis_AndStaysInsideTheImage()
	{
		var specification = Chart(SeriesChartType.Column, ["A very long device name for the first server", "Another quite long device name here", "Short"]);
		specification.XAxisTitle = "Device";
		var document = Render(specification);

		var labels = Labels(document, "xAxis");
		labels[0].Text.Should().EndWith("...");
		labels[^1].Text.Should().Be("Short");
		ShouldNotOverlap(labels);
		ShouldBeInsideTheImage([.. labels, Title(document, "xAxis")]);
		Translation(GroupById(document, "innerPlot")).Y.Should().BeGreaterThan(0);
		GroupById(document, "xAxis").Elements().First(e => e.Name.LocalName == "rect").Attribute("height")!.Value
			.Should().Match(height => double.Parse(height, CultureInfo.InvariantCulture) <= Height * 0.5 + 0.01, "the axis takes at most half the chart area");
	}

	[Fact]
	public void ManyCategories_AreLabelledEveryNth()
	{
		var categories = Enumerable.Range(1, 30).Select(day => $"2026-01-{day:00}").ToArray();
		var document = Render(Chart(SeriesChartType.Column, categories));

		var labels = Labels(document, "xAxis");
		labels.Count.Should().BeInRange(2, categories.Length - 1, "some categories go unlabelled");
		Elements(GroupById(document, "xAxis"), "line").Should().HaveCountGreaterThan(categories.Length, "every category keeps its tick");
		ShouldNotOverlap(labels);
		ShouldBeInsideTheImage(labels);
	}

	[Fact]
	public void YLabels_And_YTitle_AreNeverClipped_EvenWithAnExplicitPlotPosition()
	{
		// MS-26633: the ticket's layout, where "3,500%" lost its first digit and "Percent" was dropped.
		var specification = new ChartSpecification
		{
			ChartAreaWidthPercent = 80,
			InnerPlotXPositionPercent = 10,
			InnerPlotYPositionPercent = 15,
			InnerPlotWidthPercent = 85,
			InnerPlotHeightPercent = 75,
			XAxisFontSize = 16,
			YAxisFontSize = 16,
			YAxisLabelFormat = "P0",
			XAxisTitle = "Day",
			YAxisTitle = "Percent",
			SeriesList = [new() { ChartType = SeriesChartType.Line, Points = [.. Days.Select((day, index) => new ChartPoint(day, index, 12 + (index * 3)))] }]
		};
		var chart = specification.ToChart();
		chart.Culture = CultureInfo.GetCultureInfo("en-GB");
		var document = RenderedChart.Render(chart, 720, 380);

		var labels = Labels(document, "yAxis");
		labels.Should().Contain(label => label.Text == "3,500%");
		labels.Should().AllSatisfy(label => label.Left.Should().BeGreaterThanOrEqualTo(0, $"'{label.Text}' must be whole"));

		var title = Elements(GroupById(document, "yAxis"), "text").Single(IsTitle);
		Number(title, "font-size").Should().Be(16, "the title keeps its size");
		var titleBox = BoxOf(title);
		titleBox.Left.Should().BeGreaterThanOrEqualTo(0);
		labels.Should().AllSatisfy(label => titleBox.Right.Should().BeLessThanOrEqualTo(label.Left));
	}

	[Fact]
	public void BarChartCategories_AreNeverClipped_AndTakeAtMostHalfTheChartArea()
	{
		var specification = Chart(SeriesChartType.Bar, ["A very long device name for the first server", "Another quite long device name here", "Short"]);
		specification.YAxisTitle = "Device";
		var document = Render(specification);

		var labels = Labels(document, "yAxis");
		labels.Should().Contain(label => label.Text.EndsWith("...", StringComparison.Ordinal));
		labels.Should().Contain(label => label.Text == "Short");
		labels.Should().AllSatisfy(label => label.Left.Should().BeGreaterThanOrEqualTo(0, $"'{label.Text}' must not be cut off"));
		ShouldNotOverlap(labels);
		TranslationX(GroupById(document, "innerPlot")).Should().BeLessThanOrEqualTo((Width * 0.8 * 0.5) + 0.01);
	}

	[Fact]
	public void BarChartWithManyCategories_LabelsEveryNth()
	{
		var categories = Enumerable.Range(1, 30).Select(day => $"Day {day}").ToArray();
		var labels = Labels(Render(Chart(SeriesChartType.Bar, categories)), "yAxis");

		labels.Count.Should().BeInRange(2, categories.Length - 1);
		ShouldNotOverlap(labels);
	}

	[Fact]
	public void Rendering_LeavesTheChartAsItWas_SoASecondRenderIsTheSame()
	{
		// A margin grown by the first render must not be the starting point of the next.
		var specification = Chart(SeriesChartType.Bar, ["A very long device name for the first server", "Another quite long device name here", "Short"]);
		specification.YAxisTitle = "Device";
		var chart = specification.ToChart();
		var plot = chart.ChartArea.InnerPlot;
		var before = (plot.XPositionPercent, plot.YPositionPercent, plot.WidthPercent, plot.HeightPercent, chart.ChartArea.YAxis.WidthPercent, chart.ChartArea.XAxis.HeightPercent);

		var first = RenderedChart.Render(chart, Width, Height).ToString();

		(plot.XPositionPercent, plot.YPositionPercent, plot.WidthPercent, plot.HeightPercent, chart.ChartArea.YAxis.WidthPercent, chart.ChartArea.XAxis.HeightPercent)
			.Should().Be(before);
		RenderedChart.Render(chart, Width, Height).ToString().Should().Be(first);
	}

	[Fact]
	public void ANarrowPlot_GivesAtMostHalfItsWidthToTheLabels_AndStillRenders()
	{
		var specification = Chart(SeriesChartType.Bar, ["A very long device name for the first server", "Another quite long device name here", "Short"]);
		specification.InnerPlotWidthPercent = 20;
		var document = Render(specification);

		var plotWidth = Number(Elements(GroupById(document, "innerPlot"), "rect")[0], "width");
		plotWidth.Should().BeApproximately(Width * 0.8 * 0.2 / 2, 0.5, "the plot gives up at most half of itself");
		Labels(document, "yAxis").Should().AllSatisfy(label => label.Left.Should().BeGreaterThanOrEqualTo(0, $"'{label.Text}' must not be cut off"));
	}

	[Fact]
	public void ValueAxisNumbers_AreNeverShortened()
	{
		var specification = Chart(SeriesChartType.Column, ["Mon", "Tue"]);
		specification.SeriesList[0].Points = [new("Mon", 0, 123_456_789_012), new("Tue", 1, 223_456_789_012)];
		specification.YAxisTitle = "Bytes";
		specification.InnerPlotWidthPercent = 20;

		Labels(Render(specification), "yAxis").Should().AllSatisfy(label => label.Text.Should().NotContain("..."));
	}

	[Fact]
	public void AShortPlot_GivesAtMostHalfItsHeightToTheLabels_AndStillRenders()
	{
		var categories = Enumerable.Range(1, 12).Select(index => $"A long category name {index}").ToArray();
		var specification = Chart(SeriesChartType.Column, categories);
		specification.InnerPlotHeightPercent = 30;
		var document = Render(specification);

		var plotHeight = Number(Elements(GroupById(document, "innerPlot"), "rect")[0], "height");
		plotHeight.Should().BeApproximately(Height * 0.3 / 2, 0.5, "the plot gives up at most half of itself");
		ShouldNotOverlap(Labels(document, "xAxis"));
	}

	[Theory]
	[InlineData(15)]
	[InlineData(75)]
	public void RotatedLabelsNearTheLeftEdge_AreShortenedToStayInsideTheImage(int plotY)
	{
		// The strip already deep enough at 75, and needing to grow at 15.
		var specification = Chart(SeriesChartType.Column, ["Medium length name", "Medium length two", "Medium length three"]);
		specification.InnerPlotXPositionPercent = 3;
		specification.InnerPlotYPositionPercent = plotY;
		specification.InnerPlotHeightPercent = 100 - plotY - 10;
		var chart = specification.ToChart();

		var first = RenderedChart.Render(chart, Width, Height);
		var labels = Labels(first, "xAxis");

		labels.Should().AllSatisfy(label => label.Angle.Should().Be(-45));
		ShouldBeInsideTheImage(labels);
		RenderedChart.Render(chart, Width, Height).ToString().Should().Be(first.ToString(), "a second render lays out the same");
	}

	[Fact]
	public void RotatedValueAxisNumbers_AreNeverShortened()
	{
		var specification = Chart(SeriesChartType.Bar, ["Mon", "Tue"]);
		specification.SeriesList[0].Points = [new("Mon", 0, 123_456_789_012), new("Tue", 1, 223_456_789_012)];

		Labels(RenderedChart.Render(specification, 300, Height), "xAxis").Should().AllSatisfy(label => label.Text.Should().NotContain("..."));
	}
}
