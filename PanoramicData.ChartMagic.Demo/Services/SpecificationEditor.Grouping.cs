namespace PanoramicData.ChartMagic.Demo.Services;

public static partial class SpecificationEditor
{
	/// <summary>
	/// Groups properties by what they configure, taken from the name. Grouping 130 rows is what
	/// makes them navigable; the alternative is one alphabetical wall.
	/// </summary>
	private static string GroupOf(string name) => name switch
	{
		_ when name.StartsWith("ChartArea", StringComparison.Ordinal) => "2 Chart area",
		_ when name.StartsWith("InnerPlot", StringComparison.Ordinal) => "3 Inner plot",
		_ when name.StartsWith("Legend", StringComparison.Ordinal) => "4 Legend",
		_ when name.StartsWith("XAxis", StringComparison.Ordinal) => "5 X axis",
		_ when name.StartsWith("YAxis", StringComparison.Ordinal) || name.StartsWith("UseYAxis", StringComparison.Ordinal) => "6 Y axis",
		_ when name.StartsWith("Axis", StringComparison.Ordinal) => "7 Axis, both",
		_ when name.StartsWith("Pie", StringComparison.Ordinal) || name.StartsWith("Doughnut", StringComparison.Ordinal) => "8 Pie and doughnut",
		_ when name.Contains("3d", StringComparison.Ordinal) => "9 Three dimensional",
		_ when name.StartsWith("Label", StringComparison.Ordinal) || name.StartsWith("Palette", StringComparison.Ordinal) => "A Labels and palette",
		_ when name.StartsWith("Chart", StringComparison.Ordinal) => "1 Chart",
		_ => "B Other"
	};
}
