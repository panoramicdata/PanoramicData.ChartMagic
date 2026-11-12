using System.Drawing;
using System.Globalization;
using PanoramicData.ChartMagic.Renderers;
using System.Xml.Linq;
using static PanoramicData.ChartMagic.Test.Support.RenderedChart;

namespace PanoramicData.ChartMagic.Test;

public partial class PieTests
{
	public static TheoryData<double[]> OutsideLabelValues =>
	[
		new double[] { 25, 25, 25, 25 },
		new double[] { 34, 26, 18, 13, 9 },
		new double[] { 40, 30, 10, 5, 5, 4, 3, 2, 1 },
		new double[] { 1, 1, 1, 97 }
	];

	private static ChartSpecification OutsidePie(double[] values, Color? lineColor = null)
	{
		string[] names = ["London", "Manchester", "Leeds", "Bristol", "Glasgow", "Cardiff", "Belfast", "York", "Bath"];
		var specification = PieChartOf(
			SeriesChartType.Pie,
			PieLabelStyle.Outside,
			[.. values.Select((value, index) => new ChartPoint(names[index], index, value, Colours[index % Colours.Length]))]);
		specification.SeriesList[0].PieLineColor = lineColor ?? Color.Red;
		return specification;
	}

	/// <summary>The centre and radius of the pie, read from its first wedge.</summary>
	private static (double X, double Y, double Radius) PieCircle(XElement pie)
	{
		var wedge = Elements(pie, "path")[0].Attribute("d")!.Value;
		var centre = wedge[1..wedge.IndexOf('L')].Trim().Split(' ');
		return (
			double.Parse(centre[0], CultureInfo.InvariantCulture),
			double.Parse(centre[1], CultureInfo.InvariantCulture),
			double.Parse(wedge.Split('A')[1].Trim().Split(' ')[0], CultureInfo.InvariantCulture));
	}

	/// <summary>The box a slice label occupies: its measured width, and a line of its font height.</summary>
	private static (double Left, double Top, double Right, double Bottom) LabelBox(XElement text)
	{
		var fontSize = Number(text, "font-size");
		var width = TextMeasure.Width(text.Value, fontSize);
		var x = Number(text, "x");
		var middle = Number(text, "y") - (fontSize * 0.32);
		var left = text.Attribute("text-anchor")!.Value switch
		{
			"start" => x,
			"end" => x - width,
			_ => x - (width / 2)
		};
		return (left, middle - (fontSize / 2), left + width, middle + (fontSize / 2));
	}

	private static double DistanceFromCircleCentre((double X, double Y, double Radius) circle, (double Left, double Top, double Right, double Bottom) box)
	{
		var dx = Math.Max(Math.Max(box.Left - circle.X, 0), circle.X - box.Right);
		var dy = Math.Max(Math.Max(box.Top - circle.Y, 0), circle.Y - box.Bottom);
		return Math.Sqrt((dx * dx) + (dy * dy));
	}

	/// <summary>
	/// MS-26610: every label outside the pie, wholly, not only its anchor.
	/// </summary>
	/// <remarks>
	/// Placed by anchor alone, labels on the far side of six o'clock were anchored the wrong way
	/// and ran back across the slices, and the anchor test this replaced still passed.
	/// </remarks>
	[Theory]
	[MemberData(nameof(OutsideLabelValues))]
	public void PieChart_LabelStyleOutside_EveryLabelLiesWhollyOutsideThePie(double[] values)
	{
		var pie = PieGroup(OutsidePie(values))!;
		var circle = PieCircle(pie);

		var texts = Elements(pie, "text");
		texts.Should().HaveCount(values.Length);
		texts.Should().AllSatisfy(text =>
			DistanceFromCircleCentre(circle, LabelBox(text)).Should().BeGreaterThan(circle.Radius, $"'{text.Value}' must not lie over the pie"));
	}

	[Theory]
	[MemberData(nameof(OutsideLabelValues))]
	public void PieChart_LabelStyleOutside_JoinsEachLabelToItsSliceInThePieLineColour(double[] values)
	{
		var pie = PieGroup(OutsidePie(values))!;
		var circle = PieCircle(pie);
		var texts = Elements(pie, "text");
		var leaders = Elements(pie, "polyline");

		leaders.Should().HaveCount(texts.Count, "one leader line per label");
		foreach (var (leader, text) in leaders.Zip(texts))
		{
			leader.Attribute("stroke")!.Value.Should().Be("#FF0000");
			var points = leader.Attribute("points")!.Value.Split(' ')
				.Select(point => point.Split(',').Select(n => double.Parse(n, CultureInfo.InvariantCulture)).ToArray())
				.ToList();

			var start = points[0];
			Math.Sqrt(Math.Pow(start[0] - circle.X, 2) + Math.Pow(start[1] - circle.Y, 2))
				.Should().BeApproximately(circle.Radius, 0.5, "the line starts on the slice's rim");

			var end = points[^1];
			var box = LabelBox(text);
			end[1].Should().BeApproximately((box.Top + box.Bottom) / 2, 0.5, "the line ends level with its label");
			Math.Min(Math.Abs(end[0] - box.Left), Math.Abs(end[0] - box.Right))
				.Should().BeLessThan(4, $"the line for '{text.Value}' ends beside it");
		}
	}

	[Theory]
	[MemberData(nameof(OutsideLabelValues))]
	public void PieChart_LabelStyleOutside_LabelsDoNotOverlapEachOther(double[] values)
	{
		var boxes = Elements(PieGroup(OutsidePie(values))!, "text").Select(LabelBox).ToList();

		for (var i = 0; i < boxes.Count; i++)
		{
			for (var j = i + 1; j < boxes.Count; j++)
			{
				var overlaps = boxes[i].Left < boxes[j].Right && boxes[j].Left < boxes[i].Right
					&& boxes[i].Top < boxes[j].Bottom - 0.01 && boxes[j].Top < boxes[i].Bottom - 0.01;
				overlaps.Should().BeFalse($"labels {i} and {j} overlap");
			}
		}
	}

	[Fact]
	public void PieChart_LabelStyleOutside_WithATransparentLineColour_DrawsNoLines()
		=> Elements(PieGroup(OutsidePie([34, 26, 18], Color.Transparent))!, "polyline").Should().BeEmpty();

	[Fact]
	public void PieChart_LabelStyleOutside_ManySmallSlices_KeepEveryLabelInsideTheImage()
	{
		// Pushed apart downwards only, labels of a run of small slices fell off the bottom of the image.
		var specification = PieChartOf(
			SeriesChartType.Pie,
			PieLabelStyle.Outside,
			[.. new double[] { 20, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 70 }.Select((value, index) => new ChartPoint($"Item {index}", index, value))]);
		var document = Render(specification);
		var plot = GroupById(document, "innerPlot");
		var top = plot.AncestorsAndSelf().Sum(TranslationY);

		var boxes = Elements(GroupById(document, "pie"), "text").Select(LabelBox).ToList();
		boxes.Should().HaveCount(12);
		boxes.Should().AllSatisfy(box =>
		{
			(box.Top + top).Should().BeGreaterThanOrEqualTo(0);
			(box.Bottom + top).Should().BeLessThanOrEqualTo(Height);
		});
	}

	[Fact]
	public void PieChart_LabelStyleOutside_DoesNotShrinkThePie()
	{
		// Measured on the Windows renderer: 283 pixels across with outside labels, 285 with inside.
		var inside = PieCircle(PieGroup(PieChart())!);
		var outside = PieCircle(PieGroup(OutsidePie([25, 25, 25, 25]))!);

		outside.Radius.Should().Be(inside.Radius);
	}
}
