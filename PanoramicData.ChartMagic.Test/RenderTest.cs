using System.Drawing;
using System.Xml.Linq;
using SkiaSharp;

namespace PanoramicData.ChartMagic.Test;

/// <summary>
/// The shared fixture for the suites that render to a file: a four-series chart exercising
/// lines, areas, stacked areas, markers, a legend and annotations.
/// </summary>
public class RenderTest
{
	readonly static int ChartXCount = 20;

	protected static void SaveFile(ChartSpecification chartSpecification, FileInfo fileInfo)
	{
		var chart = chartSpecification.ToChart();
		using var fileStream = new FileStream(fileInfo.FullName, FileMode.Create, FileAccess.Write);
		chart.SaveImage(fileStream, Enum.Parse<ChartImageFormat>(fileInfo.FullName.Split('.').Last(), true), 1280, 720, false);
	}

	protected static FileInfo GetTempFileName(ChartImageFormat chartImageFormat)
	{
		var tempFileName = Path.GetTempFileName();
		var tempFile = new FileInfo(tempFileName);
		var newTempFileName = tempFileName + "." + chartImageFormat.ToString().ToLowerInvariant();
		tempFile.MoveTo(newTempFileName);
		return new(newTempFileName);
	}

	/// <summary>
	/// Renders a specification to PNG and hands back the bytes, cleaning up after itself.
	/// </summary>
	/// <remarks>
	/// Rendering goes through a file rather than a stream so that the pixel assertions run
	/// against exactly the bytes a consumer would get from <c>SaveImage</c>, which is where both
	/// issue #27 and issue #60 showed up.
	/// </remarks>
	protected static byte[] RenderToPngBytes(ChartSpecification specification)
	{
		var fileInfo = GetTempFileName(ChartImageFormat.Png);
		try
		{
			SaveFile(specification, fileInfo);
			return File.ReadAllBytes(fileInfo.FullName);
		}
		finally
		{
			fileInfo.Refresh();
			if (fileInfo.Exists)
			{
				fileInfo.Delete();
			}
		}
	}

	/// <summary>
	/// How many distinct colours an image holds, sampled, so a flat fill reads as one.
	/// </summary>
	protected static int CountDistinctColours(byte[] imageBytes)
	{
		using var bitmap = SKBitmap.Decode(imageBytes);
		bitmap.Should().NotBeNull("the output should be a decodable image");

		var colours = new HashSet<SKColor>();

		// Sampling every fourth pixel keeps this fast while remaining far more than enough to
		// distinguish a real chart from a flat fill.
		for (var x = 0; x < bitmap.Width; x += 4)
		{
			for (var y = 0; y < bitmap.Height; y += 4)
			{
				colours.Add(bitmap.GetPixel(x, y));
			}
		}

		return colours.Count;
	}

	/// <summary>
	/// How many elements of each name an SVG file holds.
	/// </summary>
	protected static Dictionary<string, int> SvgCensus(FileInfo file)
		=> XDocument.Load(file.FullName)
			.Descendants()
			.GroupBy(element => element.Name.LocalName)
			.ToDictionary(group => group.Key, group => group.Count());

	/// <summary>
	/// Requires the file to hold the drawn basic chart, so a blank render fails: for SVG its
	/// series paths, markers and labels, and for raster its many colours.
	/// </summary>
	protected static void AssertBasicChartDrawn(FileInfo file, ChartImageFormat format)
	{
		file.Refresh();
		file.Exists.Should().BeTrue();
		if (format != ChartImageFormat.Svg)
		{
			CountDistinctColours(File.ReadAllBytes(file.FullName)).Should().BeGreaterThan(32, "four series, a legend and annotations are not a flat fill");
			return;
		}

		var census = SvgCensus(file);
		census.GetValueOrDefault("path").Should().BeGreaterThanOrEqualTo(4, "each of the four series draws a path");
		census.GetValueOrDefault("use").Should().BeGreaterThanOrEqualTo(20, "the series carry markers");
		census.GetValueOrDefault("text").Should().BeGreaterThanOrEqualTo(7, "the legend, annotations and axes are labelled");
		file.Length.Should().BeGreaterThan(10_000, "an empty chart is under 2 KB");
	}

	protected ChartSpecification BasicChartSpecification = new()
	{
		ChartAreaBackgroundColor = Color.Silver,

		LegendBackgroundColor = Color.LightBlue,
		LegendBorderColor = Color.Blue,
		LegendBorderLineDashStyle = ChartDashStyle.DashDotDot,
		LegendStyle = LegendStyle.Row,

		InnerPlotBorderColor = Color.Gray,

		XAxisBackgroundColor = Color.Pink,
		YAxisBackgroundColor = Color.Purple,

		AnnotationList = CornerAnnotations(),

		SeriesList =
		[
			RedSeriesSpecification,
			GreenSeriesSpecification,
			BlueSeriesSpecification,
			VioletSeriesSpecification
		]
	};

	/// <summary>
	/// One annotation in each of three corners, which between them cover every combination of
	/// horizontal and vertical alignment.
	/// </summary>
	private static List<AnnotationSpec> CornerAnnotations() =>
	[
		new()
		{
			Text = "Top Left",
			XPositionPercent = 0,
			YPositionPercent = 100,
			VerticalAlignment = VerticalAlignment.Top,
			HorizontalAlignment = HorizontalAlignment.Left,
			FillColor = Color.DarkGray,
			StrokeColor = Color.White,
			StrokeWidth = 2
		},
		new()
		{
			Text = "Middle Center",
			XPositionPercent = 50,
			YPositionPercent = 50,
			VerticalAlignment = VerticalAlignment.Middle,
			HorizontalAlignment = HorizontalAlignment.Center,
			FillColor = Color.DarkGray,
			StrokeColor = Color.Red,
		},
		new()
		{
			Text = "Bottom Right",
			XPositionPercent = 100,
			YPositionPercent = 0,
			VerticalAlignment = VerticalAlignment.Bottom,
			HorizontalAlignment = HorizontalAlignment.Right,
			StrokeColor = Color.White,
			FillColor = Color.DarkGray,
			FontFamily = "Arial",
			FontWeight = FontWeight.Bold
		}
	];

	/// <summary>
	/// A sine wave of the given amplitude about the given level, over the shared X range.
	/// </summary>
	private static List<ChartPoint> SineWave(double level, double amplitude)
		=> [.. Enumerable
			.Range(1, ChartXCount)
			.Select(i => new ChartPoint(
				null,
				i,
				level + (amplitude * Math.Sin((float)i / ChartXCount * 2 * Math.PI))))];

	internal readonly static SeriesSpecification RedSeriesSpecification = new()
	{
		StrokeColor = Color.Red,
		ChartType = SeriesChartType.Line,
		LabelText = "Woo",
		StrokeWidth = 3,
		IsXValueIndexed = true,
		LegendText = "Yay",
		Points = SineWave(25, 3),
		MarkerStyle = MarkerStyle.Circle,
		MarkerFillColor = Color.White
	};

	internal readonly static SeriesSpecification GreenSeriesSpecification = new()
	{
		StrokeColor = Color.Green,
		FillColor = Color.DarkGreen,
		ChartType = SeriesChartType.Area,
		LabelText = "Woo2",
		IsXValueIndexed = true,
		LegendText = "Yay2",
		Points = SineWave(15, 3),
		MarkerStyle = MarkerStyle.Circle
	};

	internal readonly static SeriesSpecification BlueSeriesSpecification = new()
	{
		StrokeColor = Color.Blue,
		StrokeStyle = ChartDashStyle.DashDotDot,
		FillColor = Color.DarkBlue,
		ChartType = SeriesChartType.StackedArea,
		LabelText = "Woo3A",
		StrokeWidth = 1,
		IsXValueIndexed = true,
		LegendText = "Yay3A",
		Points = SineWave(2, 2),
		MarkerStyle = MarkerStyle.Circle
	};

	internal readonly static SeriesSpecification VioletSeriesSpecification = new()
	{
		StrokeColor = Color.Violet,
		FillColor = Color.DarkViolet,
		ChartType = SeriesChartType.StackedArea,
		LabelText = "Woo3B",
		StrokeWidth = 1,
		IsXValueIndexed = true,
		LegendText = "Yay3B",
		Points = SineWave(2, 2),
		MarkerStyle = MarkerStyle.Circle
	};
}
