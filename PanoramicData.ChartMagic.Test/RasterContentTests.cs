using SkiaSharp;

namespace PanoramicData.ChartMagic.Test;

/// <summary>
/// Guards issue #27: raster output that contained no chart content.
/// </summary>
/// <remarks>
/// The existing format tests assert only that a file was written, so a renderer that produced a
/// valid image of a single flat colour passed them. These tests look at the pixels instead.
/// </remarks>
public class RasterContentTests : RenderTest
{
	/// <summary>
	/// A chart with four series, a legend, annotations and axis backgrounds cannot legitimately
	/// consist of a handful of colours. Before #27 was fixed this produced exactly one.
	/// </summary>
	[Fact]
	public void Png_ContainsManyDistinctColours()
	{
		var distinctColours = CountDistinctColours(RenderToPngBytes(BasicChartSpecification));

		distinctColours.Should().BeGreaterThan(
			32,
			"a chart with four series, a legend and annotations cannot be a flat fill");
	}

	/// <summary>
	/// The specific failure mode of #27: one element magnified until it filled the frame, so
	/// every pixel was identical.
	/// </summary>
	[Fact]
	public void Png_IsNotASingleFlatColour()
		=> CountDistinctColours(RenderToPngBytes(BasicChartSpecification))
			.Should()
			.BeGreaterThan(1, "the image should not be one flat colour");

	/// <summary>
	/// Content must reach the middle of the canvas. A chart drawn at the wrong scale can still
	/// have many colours while being confined to a corner.
	/// </summary>
	[Fact]
	public void Png_HasContentAcrossTheCanvas()
	{
		using var bitmap = SKBitmap.Decode(RenderToPngBytes(BasicChartSpecification));

		var centre = bitmap.GetPixel(bitmap.Width / 2, bitmap.Height / 2);
		var quarter = bitmap.GetPixel(bitmap.Width / 4, bitmap.Height / 2);
		var threeQuarters = bitmap.GetPixel(bitmap.Width * 3 / 4, bitmap.Height / 2);

		var sampled = new HashSet<SKColor> { centre, quarter, threeQuarters };
		sampled.Count.Should().BeGreaterThan(
			1,
			"three widely separated samples should not all be the same colour");
	}

	/// <summary>
	/// Marks must be drawn where the series are: the red line series leaves red pixels, and the
	/// blank background is silver.
	/// </summary>
	[Fact]
	public void Png_DrawsTheRedSeriesOverTheBackground()
	{
		using var bitmap = SKBitmap.Decode(RenderToPngBytes(BasicChartSpecification));

		var redPixels = 0;
		for (var x = 0; x < bitmap.Width; x++)
		{
			for (var y = 0; y < bitmap.Height; y++)
			{
				var pixel = bitmap.GetPixel(x, y);
				if (pixel.Red > 200 && pixel.Green < 60 && pixel.Blue < 60)
				{
					redPixels++;
				}
			}
		}

		redPixels.Should().BeGreaterThan(200, "a three pixel wide red line crosses the plot");
	}
}
