using System.Drawing;
using System.Xml.Linq;
using SkiaSharp;
using static PanoramicData.ChartMagic.Test.Support.RenderedChart;

namespace PanoramicData.ChartMagic.Test;

/// <summary>
/// Box plots, where each point carries six Y values in the Microsoft chart control's order: lower
/// whisker, upper whisker, lower box, upper box, average, median (issue 76).
/// </summary>
public class BoxPlotTests : RenderTest
{
	private static ChartSpecification BoxPlot(params ChartPoint[] points)
		=> new()
		{
			SeriesList =
			[
				new()
				{
					ChartType = SeriesChartType.BoxPlot,
					FillColor = Color.FromArgb(255, 165, 0),
					StrokeColor = Color.Black,
					StrokeWidth = 2,
					Points = [.. points]
				}
			]
		};

	private static ChartPoint Box(string name, int x, params double[] values) => new(name, x, null) { YValues = values };

	private static ChartSpecification Standard() => BoxPlot(
		Box("A", 0, 2, 18, 6, 14, 10, 9),
		Box("B", 1, 1, 30, 8, 20, 12, 11),
		Box("C", 2, 4, 12, 5, 9, 7, 6));

	private static XElement Series(XDocument document) => GroupById(document, "series0");

	private static XElement PointGroup(XDocument document, int index) => GroupById(document, $"boxPlot{index}");

	private static List<XElement> Boxes(XDocument document) => Elements(Series(document), "rect");

	/// <summary>The pixels one unit of value spans, read from the first box, which runs 6 to 14.</summary>
	private static double PixelsPerUnit(XDocument document) => Number(Boxes(document)[0], "height") / 8;

	[Fact]
	public void BoxPlot_DrawsOneBoxPerPoint_InTheSeriesFill()
	{
		var boxes = Boxes(Render(Standard()));

		boxes.Should().HaveCount(3);
		boxes.Should().AllSatisfy(box => box.Attribute("style")!.Value.Should().Contain("#FFA500"));
	}

	[Fact]
	public void BoxPlot_BoxSpansTheLowerAndUpperBoxValues()
	{
		var boxes = Boxes(Render(Standard()));

		(Number(boxes[1], "height") / Number(boxes[0], "height")).Should().BeApproximately(12d / 8, 0.01, "8 to 20 against 6 to 14");
		(Number(boxes[2], "height") / Number(boxes[0], "height")).Should().BeApproximately(4d / 8, 0.01, "5 to 9 against 6 to 14");
	}

	[Fact]
	public void BoxPlot_WhiskersRunFromTheBoxToTheWhiskerValues()
	{
		var document = Render(Standard());
		var scale = PixelsPerUnit(document);
		var box = Boxes(document)[0];
		var whiskers = Elements(PointGroup(document, 0), "line")
			.Where(line => Number(line, "x1") == Number(line, "x2"))
			.OrderBy(line => Number(line, "y1"))
			.ToList();

		whiskers.Should().HaveCount(2);
		var (upper, lower) = (whiskers[0], whiskers[1]);
		Number(upper, "y2").Should().BeApproximately(Number(box, "y"), 0.05, "the upper whisker starts at the upper box");
		(Number(upper, "y2") - Number(upper, "y1")).Should().BeApproximately(4 * scale, 0.05, "18 above 14");
		Number(lower, "y1").Should().BeApproximately(Number(box, "y") + Number(box, "height"), 0.05);
		(Number(lower, "y2") - Number(lower, "y1")).Should().BeApproximately(4 * scale, 0.05, "6 above 2");
		Number(upper, "x1").Should().BeApproximately(Number(box, "x") + (Number(box, "width") / 2), 0.05, "whiskers rise from the middle of the box");
	}

	[Fact]
	public void BoxPlot_MedianIsALineAcrossTheBox()
	{
		var document = Render(Standard());
		var box = Boxes(document)[0];
		var bottom = Number(box, "y") + Number(box, "height");

		var median = Elements(PointGroup(document, 0), "line")
			.Single(line => Number(line, "y1") == Number(line, "y2") && Math.Abs(Number(line, "x2") - Number(line, "x1") - Number(box, "width")) < 0.05);

		Number(median, "y1").Should().BeApproximately(bottom - (3 * PixelsPerUnit(document)), 0.05, "9 is 3 above the lower box");
		Number(median, "x1").Should().BeApproximately(Number(box, "x"), 0.05);
	}

	[Fact]
	public void BoxPlot_AverageIsAMarkerAtTheCentreOfTheBox()
	{
		var document = Render(Standard());
		var box = Boxes(document)[0];
		var bottom = Number(box, "y") + Number(box, "height");

		var average = Elements(PointGroup(document, 0), "circle").Should().ContainSingle().Subject;

		Number(average, "cx").Should().BeApproximately(Number(box, "x") + (Number(box, "width") / 2), 0.05);
		Number(average, "cy").Should().BeApproximately(bottom - (4 * PixelsPerUnit(document)), 0.05, "10 is 4 above the lower box");
	}

	[Fact]
	public void BoxPlot_ValueAxisIncludesTheWhiskers()
	{
		var labels = NumericLabels(Render(Standard()), "yAxis");

		labels.Max().Should().BeGreaterThanOrEqualTo(30, "the highest upper whisker is 30");
		labels.Min().Should().Be(0);
	}

	[Fact]
	public void BoxPlot_LabelsEachBoxWithItsCategory()
		=> LabelTexts(Render(Standard()), "xAxis").Should().Equal("A", "B", "C");

	[Fact]
	public void BoxPlot_AValueBeyondTheSixthIsAnUnusualPoint()
	{
		var document = Render(BoxPlot(Box("A", 0, 2, 18, 6, 14, 10, 9, 25)));

		Elements(PointGroup(document, 0), "circle").Should().HaveCount(2, "the average marker and one unusual point");
		NumericLabels(document, "yAxis").Max().Should().BeGreaterThanOrEqualTo(25);
	}

	[Fact]
	public void BoxPlot_ANaNAverageOrMedianIsHidden()
	{
		var document = Render(BoxPlot(Box("A", 0, 2, 18, 6, 14, double.NaN, double.NaN)));

		Elements(PointGroup(document, 0), "circle").Should().BeEmpty();
		Elements(PointGroup(document, 0), "line").Where(line => Number(line, "y1") == Number(line, "y2"))
			.Should().HaveCount(2, "only the whisker caps remain");
	}

	[Fact]
	public void BoxPlot_APointWithFewerThanSixValuesDrawsNothing_AndDoesNotThrow()
	{
		var specification = BoxPlot(
			Box("A", 0, 2, 18, 6, 14, 10, 9),
			Box("B", 1, 1, 30, 8, 20, 12),
			new ChartPoint("C", 2, null),
			new ChartPoint("D", 3, 5));

		var act = () => Render(specification);

		act.Should().NotThrow();
		Boxes(Render(specification)).Should().HaveCount(1, "only the first point has six values");
	}

	[Fact]
	public void BoxPlot_WithNoPointsHavingValues_StillRenders()
	{
		var document = Render(BoxPlot(new ChartPoint("A", 0, null)));

		Boxes(document).Should().BeEmpty();
		FindGroupById(document, "yAxis").Should().NotBeNull();
	}

	[Fact]
	public void BoxPlot_Png_PaintsTheBoxesInTheSeriesFill()
	{
		using var bitmap = SKBitmap.Decode(RenderToPngBytes(Standard()));

		var orange = 0;
		for (var x = 0; x < bitmap.Width; x += 2)
		{
			for (var y = 0; y < bitmap.Height; y += 2)
			{
				var pixel = bitmap.GetPixel(x, y);
				if (pixel is { Red: 255, Green: > 155 and < 175, Blue: < 10 })
				{
					orange++;
				}
			}
		}

		orange.Should().BeGreaterThan(500, "three filled boxes cover thousands of pixels");
	}
}
