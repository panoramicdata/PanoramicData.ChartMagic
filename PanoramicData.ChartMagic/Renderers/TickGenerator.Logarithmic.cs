namespace PanoramicData.ChartMagic.Renderers;

internal static partial class TickGenerator
{
	/// <summary>
	/// Decade ticks across a logarithmic range, with the intermediate 2..9 subdivisions when
	/// the range is narrow enough for them to be legible.
	/// </summary>
	internal static IReadOnlyList<double> Logarithmic(double min, double max, bool includeMinor)
	{
		var low = min > 0 ? min : max > 0 ? max / 1000 : 1;
		var high = max > low ? max : low * 10;

		var firstExponent = (int)Math.Floor(Math.Log10(low));
		var lastExponent = (int)Math.Ceiling(Math.Log10(high));

		// Over more than a handful of decades the intermediate steps are too crowded to read.
		var withMinor = includeMinor && lastExponent - firstExponent <= 4;

		var ticks = new List<double>();
		for (var exponent = firstExponent; exponent <= lastExponent; exponent++)
		{
			var decade = Math.Pow(10, exponent);
			ticks.Add(decade);

			if (withMinor)
			{
				ticks.AddRange(DecadeSubdivisions(decade, high));
			}
		}

		ticks.Sort();
		return ticks;
	}

	/// <summary>
	/// The 2..9 steps within one decade that fall below the top of the range.
	/// </summary>
	private static IEnumerable<double> DecadeSubdivisions(double decade, double high)
	{
		for (var multiplier = 2; multiplier <= 9; multiplier++)
		{
			var value = decade * multiplier;
			if (value < high)
			{
				yield return value;
			}
		}
	}
}
