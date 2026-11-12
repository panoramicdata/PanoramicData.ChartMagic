using System.Drawing;
using System.Text.Json;

namespace PanoramicData.ChartMagic.Test;

/// <summary>
/// Pins the serialised form of a series specification: names, values and property order.
/// </summary>
public class SeriesSpecificationSerialisationTests
{
	/// <summary>Captured from the implementation before the shared-settings refactor.</summary>
	private const string Expected = """{"Points":[{"XValueString":"A","XValue":1,"YValue":2.5,"Color":{"R":1,"G":2,"B":3,"A":255,"IsKnownColor":false,"IsEmpty":false,"IsNamedColor":false,"IsSystemColor":false,"Name":"ff010203"},"LegendText":"legend A"},{"XValueString":null,"XValue":2,"YValue":null,"Color":null,"LegendText":null}],"ChartType":17,"IsXValueIndexed":true,"FillColor":{"R":20,"G":30,"B":40,"A":10,"IsKnownColor":false,"IsEmpty":false,"IsNamedColor":false,"IsSystemColor":false,"Name":"a141e28"},"StrokeColor":{"R":50,"G":60,"B":70,"A":255,"IsKnownColor":false,"IsEmpty":false,"IsNamedColor":false,"IsSystemColor":false,"Name":"ff323c46"},"StrokeWidth":3.5,"StrokeLineCapStyle":2,"StrokeLineJoinStyle":1,"LabelText":"label","LegendText":"legend","FontSize":11,"FontColor":{"R":80,"G":90,"B":100,"A":255,"IsKnownColor":false,"IsEmpty":false,"IsNamedColor":false,"IsSystemColor":false,"Name":"ff505a64"},"StrokeStyle":1,"MarkerStyle":3,"MarkerStrokeColor":{"R":110,"G":120,"B":130,"A":255,"IsKnownColor":false,"IsEmpty":false,"IsNamedColor":false,"IsSystemColor":false,"Name":"ff6e7882"},"MarkerFillColor":{"R":140,"G":150,"B":160,"A":255,"IsKnownColor":false,"IsEmpty":false,"IsNamedColor":false,"IsSystemColor":false,"Name":"ff8c96a0"},"MarkerStrokeWidth":1.25,"MarkerSize":7,"DoughnutRadiusPercent":45,"PieLabelStyle":1,"PieLineColor":{"R":170,"G":180,"B":190,"A":255,"IsKnownColor":false,"IsEmpty":false,"IsNamedColor":false,"IsSystemColor":false,"Name":"ffaab4be"},"PieStartAngleDegrees":90,"PieCollectedThresholdPercent":5,"PieCollectedColor":{"R":200,"G":210,"B":220,"A":255,"IsKnownColor":false,"IsEmpty":false,"IsNamedColor":false,"IsSystemColor":false,"Name":"ffc8d2dc"},"PieCollectedLabel":"other"}""";

	[Fact]
	public void FullyPopulatedSeriesSpecification_SerialisesToTheKnownJson()
	{
		var json = JsonSerializer.Serialize(PopulatedSpecification());

		json.Should().Be(Expected);
	}

	private static SeriesSpecification PopulatedSpecification() => new()
	{
		Points = [new("A", 1, 2.5, Color.FromArgb(1, 2, 3), "legend A"), new(null, 2, null)],
		ChartType = SeriesChartType.Doughnut,
		IsXValueIndexed = true,
		FillColor = Color.FromArgb(10, 20, 30, 40),
		StrokeColor = Color.FromArgb(50, 60, 70),
		StrokeWidth = 3.5,
		StrokeLineCapStyle = StrokeLineCapStyle.Square,
		StrokeLineJoinStyle = StrokeLineJoinStyle.Bevel,
		LabelText = "label",
		LegendText = "legend",
		FontSize = 11,
		FontColor = Color.FromArgb(80, 90, 100),
		StrokeStyle = ChartDashStyle.Dash,
		MarkerStyle = MarkerStyle.Diamond,
		MarkerStrokeColor = Color.FromArgb(110, 120, 130),
		MarkerFillColor = Color.FromArgb(140, 150, 160),
		MarkerStrokeWidth = 1.25,
		MarkerSize = 7,
		DoughnutRadiusPercent = 45,
		PieLabelStyle = PieLabelStyle.Outside,
		PieLineColor = Color.FromArgb(170, 180, 190),
		PieStartAngleDegrees = 90,
		PieCollectedThresholdPercent = 5,
		PieCollectedColor = Color.FromArgb(200, 210, 220),
		PieCollectedLabel = "other"
	};
}
