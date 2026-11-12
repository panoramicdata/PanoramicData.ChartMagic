namespace PanoramicData.ChartMagic.Test;

public class SvgTests : RenderTest
{
	/// <summary>
	/// A chart with no series still draws its frame: a blank document would have no rectangles.
	/// </summary>
	[Fact]
	public void EmptyChart_Succeeds()
	{
		var fileInfo = GetTempFileName(ChartImageFormat.Svg);
		try
		{
			SaveFile(new(), fileInfo);

			var census = SvgCensus(fileInfo);
			census.GetValueOrDefault("svg").Should().Be(1);
			census.GetValueOrDefault("rect").Should().BeGreaterThanOrEqualTo(1, "the chart background is drawn");
			census.GetValueOrDefault("line").Should().BeGreaterThanOrEqualTo(2, "the axes are drawn");
			census.GetValueOrDefault("path").Should().Be(0, "there are no series to draw");
		}
		finally
		{
			fileInfo.Delete();
		}
	}

	[Fact]
	public void BasicChartSpecification_Succeeds()
	{
		var fileInfo = GetTempFileName(ChartImageFormat.Svg);
		try
		{
			SaveFile(BasicChartSpecification, fileInfo);

			AssertBasicChartDrawn(fileInfo, ChartImageFormat.Svg);
			File.ReadAllText(fileInfo.FullName).Should().ContainAll("Yay", "Top Left", "Middle Center", "Bottom Right");
		}
		finally
		{
			fileInfo.Delete();
		}
	}
}
