namespace PanoramicData.ChartMagic.Renderers;

internal static partial class TickGenerator
{
	/// <summary>
	/// The steps, in days, a date axis chooses between: a day, a few days, weeks, months and years.
	/// </summary>
	private static readonly double[] DayMultipliers = [1, 2, 5, 7, 14, 30, 61, 91, 183, 365, 730, 1826];

	/// <summary>
	/// Ticks across a range of OLE Automation dates, on whole days.
	/// </summary>
	/// <param name="min">The first date of the range, in days.</param>
	/// <param name="max">The last date of the range, in days.</param>
	/// <param name="interval">The step in units of <paramref name="intervalType"/>, or null for a readable one.</param>
	/// <param name="intervalType">What the interval counts: days, weeks, months or years.</param>
	/// <param name="targetCount">Roughly how many ticks to aim for without an interval.</param>
	internal static IReadOnlyList<double> Dates(double min, double max, double? interval, DateTimeIntervalType intervalType, int targetCount)
	{
		if (!IsPlottableRange(min, max))
		{
			return [min];
		}

		var step = interval is > 0 ? interval.Value * DaysPer(intervalType) : DayStep(max - min, targetCount);
		var ticks = StepsAcross(min, max, Math.Max(step, 1), Math.Ceiling(min));
		return ticks.Count == 0 ? [min] : ticks;
	}

	/// <summary>
	/// How many days one unit of an interval type is. Months and years are their mean lengths, so
	/// a tick lands on a whole day rather than on a calendar boundary.
	/// </summary>
	private static double DaysPer(DateTimeIntervalType intervalType) => intervalType switch
	{
		DateTimeIntervalType.Weeks => 7,
		DateTimeIntervalType.Months => 30.4375,
		DateTimeIntervalType.Years => 365.25,
		_ => 1
	};

	/// <summary>
	/// The readable step, in days, whose tick count comes closest to the target.
	/// </summary>
	private static double DayStep(double rangeDays, int targetCount)
		=> DayMultipliers.MinBy(step => Math.Abs((rangeDays / step) - Math.Max(targetCount, 2)));
}
