namespace PanoramicData.ChartMagic.Renderers;

/// <summary>
/// Chooses the values at which an axis is labelled and gridded.
/// </summary>
/// <remarks>
/// Issue #31: there was nowhere that decided this, because nothing drew ticks. An axis needs
/// values a reader recognises - 0, 20, 40 rather than 3.7, 23.7, 43.7 - so the step is snapped
/// to 1, 2, 2.5 or 5 times a power of ten and the first tick is aligned to a multiple of it.
/// </remarks>
internal static partial class TickGenerator
{
	private static readonly double[] NiceMultipliers = [1, 2, 2.5, 5, 10];

	private const int MaximumTicks = 1000;

	/// <summary>
	/// Ticks across a linear range, at a caller-supplied interval or a readable one.
	/// </summary>
	/// <param name="min">The start of the range.</param>
	/// <param name="max">The end of the range.</param>
	/// <param name="interval">The step, or null for a readable one.</param>
	/// <param name="targetCount">Roughly how many ticks to aim for without an interval.</param>
	/// <param name="anchor">A value the ticks are counted from, or null for multiples of the step.</param>
	internal static IReadOnlyList<double> Linear(double min, double max, double? interval, int targetCount, double? anchor = null)
	{
		if (!IsPlottableRange(min, max))
		{
			return [min];
		}

		var ticks = StepsAcross(min, max, UsableStep(min, max, interval, targetCount), anchor ?? 0);
		return ticks.Count == 0 ? [min] : ticks;
	}

	/// <summary>
	/// Whether a range can be divided into ticks at all.
	/// </summary>
	private static bool IsPlottableRange(double min, double max)
		=> !double.IsNaN(min)
			&& !double.IsNaN(max)
			&& !double.IsInfinity(min)
			&& !double.IsInfinity(max)
			&& max > min;

	/// <summary>
	/// The interval the caller asked for, or a readable one where they gave none.
	/// </summary>
	/// <remarks>
	/// A caller-supplied interval is also refused where it is so small relative to the range that
	/// it would generate unbounded ticks.
	/// </remarks>
	private static double UsableStep(double min, double max, double? interval, int targetCount)
	{
		var step = interval is > 0
			? interval.Value
			: NiceStep(max - min, targetCount);

		return step > 0 && (max - min) / step <= MaximumTicks
			? step
			: NiceStep(max - min, targetCount);
	}

	/// <summary>
	/// The steps from the anchor that fall within the range.
	/// </summary>
	private static List<double> StepsAcross(double min, double max, double step, double anchor)
	{
		// The anchor's own decimals too, so steps of 4 from 5.5 keep their half.
		var fraction = Math.Abs(anchor % 1);
		var decimals = Math.Max(DecimalsFor(step), fraction == 0 ? 0 : DecimalsFor(fraction));
		var ticks = new List<double>();

		// A tolerance of one part in a billion of the step, so a tick that lands exactly on
		// the minimum or the maximum is not dropped by floating-point drift.
		var tolerance = step * 1e-9;
		for (var value = anchor + (Math.Ceiling(((min - anchor) / step) - 1e-9) * step); value <= max + tolerance; value += step)
		{
			ticks.Add(Math.Round(value, decimals));
			if (ticks.Count >= MaximumTicks)
			{
				break;
			}
		}

		return ticks;
	}

	/// <summary>
	/// How many decimal places a tick at this step needs, so that a step of 0.25 labels as
	/// 0.25 and a step of 20 does not label as 20.00.
	/// </summary>
	internal static int DecimalsFor(double step)
	{
		if (step <= 0)
		{
			return 0;
		}

		var decimals = (int)Math.Ceiling(-Math.Log10(step)) + 1;
		return Math.Clamp(decimals, 0, 10);
	}
}
