namespace PanoramicData.ChartMagic.Renderers;

internal static partial class TickGenerator
{
	/// <summary>
	/// Chooses the readable step whose tick count comes closest to the target.
	/// </summary>
	/// <remarks>
	/// Snapping the raw step upwards, as this did, is too coarse. A range of 31 with a target
	/// of eight gives a raw step of 3.9, which snapped up to 5 - correct - but a range of 31
	/// with a target of six gives 5.2, which snapped up to 10 and halved the number of labels.
	/// Choosing by resulting tick count instead lands on 5 either way, which is what the
	/// Microsoft chart control produces for the same data.
	/// </remarks>
	internal static double NiceStep(double range, int targetCount)
	{
		if (range <= 0)
		{
			return 1;
		}

		var target = Math.Max(targetCount, 2);
		var exponent = Math.Floor(Math.Log10(range / target));

		var best = Math.Pow(10, exponent);
		var bestDistance = double.MaxValue;

		// One decade either side of the estimate is ample: the candidates within it span a
		// factor of a hundred.
		for (var power = exponent - 1; power <= exponent + 1; power++)
		{
			var magnitude = Math.Pow(10, power);
			foreach (var multiplier in NiceMultipliers)
			{
				var step = multiplier * magnitude;
				var count = range / step;
				if (count is < 1 or > MaximumTicks)
				{
					continue;
				}

				var distance = Math.Abs(count - target);
				if (distance < bestDistance)
				{
					bestDistance = distance;
					best = step;
				}
			}
		}

		return best;
	}

	/// <summary>
	/// The most intervals a value axis is allowed to be divided into.
	/// </summary>
	/// <remarks>
	/// Measured, not chosen. Four reference renders of the same size gave five, six, seven and
	/// seven intervals, and in each case the next smaller readable step would have given nine or
	/// more - so the rule is a ceiling rather than a target.
	/// </remarks>
	private const int MaximumIntervals = 7;

	/// <summary>
	/// The step and bounds for a value axis covering this data.
	/// </summary>
	/// <remarks>
	/// The step and the bounds cannot be chosen separately, because each depends on the other:
	/// the bounds are the multiples of the step that lie just beyond the data, and whether a step
	/// is acceptable depends on how many intervals those bounds then span. So the readable steps
	/// are tried in ascending order and the first whose bounds span few enough intervals wins.
	///
	/// This replaced a step chosen from the larger of the two data extremes, which was right for
	/// data that does not cross zero and wrong as soon as it does. Measured against reference
	/// renders:
	///
	/// <list type="bullet">
	/// <item>-11 to 26 gave a step of 10 over -20 to 30, where the old rule gave 2.5.</item>
	/// <item>-30 to 12 gave 10 over -40 to 20.</item>
	/// <item>-2 to 9 gave 2 over -4 to 10.</item>
	/// <item>0 to 30 gave 5 over 0 to 35, which the old rule also produced.</item>
	/// </list>
	///
	/// All four fall out of this one rule, including the positive case that the old one got right.
	/// </remarks>
	internal static (double Step, double Start, double End) LinearBounds(double dataMinimum, double dataMaximum)
	{
		var span = dataMaximum - dataMinimum;
		if (span <= 0 || double.IsNaN(span) || double.IsInfinity(span))
		{
			var fallback = Math.Abs(dataMaximum) > 0 ? Math.Abs(dataMaximum) : 1;
			return (fallback, Math.Min(0, dataMinimum), Math.Max(fallback, dataMaximum));
		}

		// From well below any plausible step for this span to well above, so the first acceptable
		// one is always found.
		var lowestPower = (int)Math.Floor(Math.Log10(span)) - 3;

		for (var power = lowestPower; power <= lowestPower + 7; power++)
		{
			var magnitude = Math.Pow(10, power);
			foreach (var multiplier in NiceMultipliers)
			{
				var step = multiplier * magnitude;
				var (start, end) = BoundsFor(dataMinimum, dataMaximum, step);

				if ((end - start) / step <= MaximumIntervals + 1e-9)
				{
					return (step, start, end);
				}
			}
		}

		// Unreachable for finite data, but a sane answer beats an exception.
		var last = NiceStep(span, MaximumIntervals);
		var (fallbackStart, fallbackEnd) = BoundsFor(dataMinimum, dataMaximum, last);
		return (last, fallbackStart, fallbackEnd);
	}

	/// <summary>
	/// Where an axis at this step starts and ends for this data.
	/// </summary>
	/// <remarks>
	/// Zero-based whenever the data does not go below it, which is what the reference renderer
	/// does: a column chart of positive values stands on the axis rather than floating above a
	/// cropped one.
	/// </remarks>
	private static (double Start, double End) BoundsFor(double dataMinimum, double dataMaximum, double step)
	{
		var start = dataMinimum >= 0 ? 0 : -NextStepAbove(-dataMinimum, step);
		var end = NextStepAbove(dataMaximum, step);
		return (start, end);
	}

	/// <summary>
	/// The smallest multiple of the step strictly greater than the value.
	/// </summary>
	/// <remarks>
	/// Strictly greater, so a data maximum sitting exactly on a tick still gains a step of
	/// headroom. Measured against DocMagic: a peak of 30 produced an axis to 35, and a peak of
	/// 32 also produced 35 - which rules out plain rounding up to the next tick, since that
	/// would have left the first at 30 with the topmost column touching the frame.
	/// </remarks>
	internal static double NextStepAbove(double value, double step)
	{
		if (step <= 0)
		{
			return value;
		}

		var multiples = Math.Floor(value / step) + 1;
		return Math.Round(multiples * step, DecimalsFor(step));
	}

	/// <summary>
	/// The largest multiple of the step less than or equal to the value.
	/// </summary>
	internal static double StepAtOrBelow(double value, double step)
	=> step <= 0 ? value : Math.Round(Math.Floor(value / step) * step, DecimalsFor(step));
}
