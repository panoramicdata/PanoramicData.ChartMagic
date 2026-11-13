using System.Drawing;
using System.Text.Json;

namespace PanoramicData.ChartMagic.Test;

/// <summary>
/// Pins the serialised form of a chart point, which gained an optional list of Y values.
/// </summary>
public class ChartPointSerialisationTests
{
	/// <summary>Captured before YValues existed.</summary>
	private const string PopulatedBefore = """{"XValueString":"A","XValue":1,"YValue":2.5,"Color":{"R":1,"G":2,"B":3,"A":255,"IsKnownColor":false,"IsEmpty":false,"IsNamedColor":false,"IsSystemColor":false,"Name":"ff010203"},"LegendText":"legend A"}""";

	/// <summary>Captured before YValues existed.</summary>
	private const string EmptyBefore = """{"XValueString":null,"XValue":2,"YValue":null,"Color":null,"LegendText":null}""";

	[Fact]
	public void PointWithoutYValues_SerialisesAsItAlwaysDid()
	{
		JsonSerializer.Serialize(new ChartPoint("A", 1, 2.5, Color.FromArgb(1, 2, 3), "legend A")).Should().Be(PopulatedBefore);
		JsonSerializer.Serialize(new ChartPoint(null, 2, null)).Should().Be(EmptyBefore);
	}

	[Fact]
	public void PointWithYValues_WritesThemLast_AndReadsThemBack()
	{
		var point = new ChartPoint(null, 2, null) { YValues = [1, 2, 3, 4, 5, 6] };

		var json = JsonSerializer.Serialize(point);

		json.Should().Be("""{"XValueString":null,"XValue":2,"YValue":null,"Color":null,"LegendText":null,"YValues":[1,2,3,4,5,6]}""");
		JsonSerializer.Deserialize<ChartPoint>(json)!.YValues.Should().Equal(1, 2, 3, 4, 5, 6);
	}
}
