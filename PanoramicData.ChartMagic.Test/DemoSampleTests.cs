using PanoramicData.ChartMagic.Demo.Services;
using System.Xml.Linq;

namespace PanoramicData.ChartMagic.Test;

/// <summary>
/// The demo's sample gallery, rendered the way the page renders it.
/// </summary>
/// <remarks>
/// The renderer tests prove a chart type draws. These prove the demo actually offers it: a sample
/// missing from the gallery, or built so that it draws nothing, is invisible to every other test.
/// </remarks>
public class DemoSampleTests
{
	private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";

	private static ChartSample FunnelSample()
		=> SampleCharts.All.Should().ContainSingle(sample => sample.Title == "Funnel").Subject;

	[Fact]
	public void Funnel_IsInTheGallery_AsWorking()
		=> FunnelSample().Status.Should().Be(SampleStatus.Working);

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void Funnel_RendersOneLabelledSegmentPerStage_InBothThemes(bool dark)
	{
		var sample = FunnelSample();
		var stages = sample.Specification.SeriesList.Single().Points.Count;

		var funnel = RenderFunnel(sample.Specification, dark);

		stages.Should().BeGreaterThan(2, "a funnel of two stages does not show it narrowing");
		funnel.Elements(Svg + "path").Where(path => ((string?)path.Attribute("id"))?.StartsWith("funnelSegment", StringComparison.Ordinal) == true)
			.Should().HaveCount(stages);
		funnel.Descendants(Svg + "text").Where(text => ((string?)text.Attribute("id"))?.StartsWith("funnelLabel", StringComparison.Ordinal) == true)
			.Select(text => text.Value)
			.Should().HaveCount(stages).And.AllSatisfy(label => label.Should().NotBeNullOrWhiteSpace());
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void SeriesLabels_TakeTheThemeLabelColour_SoTheyAreReadableOnADarkPage(bool dark)
	{
		// A series' label colour defaults to black, which is invisible against the dark theme.
		// Found on the funnel sample, but it applied to every sample with series labels.
		var theme = dark ? ChartTheme.Dark : ChartTheme.Light;
		var expected = $"#{theme.AxisLabel.R:X2}{theme.AxisLabel.G:X2}{theme.AxisLabel.B:X2}";

		var funnel = RenderFunnel(FunnelSample().Specification, dark);

		ColouredFunnelLabels(funnel).Should().NotBeEmpty().And.AllBe(expected);
	}

	[Fact]
	public void SeriesLabels_ASampleThatSetsItsOwnLabelColour_KeepsIt()
	{
		var specification = SpecificationEditor.Clone(FunnelSample().Specification);
		specification.SeriesList[0].FontColor = System.Drawing.Color.Red;

		var funnel = RenderFunnel(specification, dark: true);

		ColouredFunnelLabels(funnel).Should().NotBeEmpty().And.AllBe("#FF0000", "the theme fills in defaults only, as it does for every other colour");
	}

	private static XElement RenderFunnel(ChartSpecification specification, bool dark)
	{
		var document = XDocument.Parse(SampleCharts.ToSvg(specification, dark ? ChartTheme.Dark : ChartTheme.Light));
		return document.Descendants(Svg + "g").Single(group => (string?)group.Attribute("id") == "funnel");
	}

	private static IEnumerable<string> ColouredFunnelLabels(XElement funnel)
		=> funnel.Descendants(Svg + "text")
			.Where(text => ((string?)text.Attribute("id"))?.StartsWith("funnelLabel", StringComparison.Ordinal) == true)
			.Select(text => ((string?)text.Attribute("fill"))?.ToUpperInvariant())!;
}
