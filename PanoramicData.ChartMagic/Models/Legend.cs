namespace PanoramicData.ChartMagic.Models;

public class Legend(IChartElement parent, string name) : ChartNamedElement(parent, name)
{
	public LegendStyle Style { get; set; }

	/// <summary>The order the entries are listed in.</summary>
	public LegendItemOrder ItemOrder { get; set; }

	/// <summary>
	/// Entry text longer than this many characters is wrapped at a space. Zero turns wrapping off.
	/// </summary>
	/// <remarks>25 by default, as in the Microsoft chart control.</remarks>
	public int TextWrapThreshold { get; set; } = 25;
}
