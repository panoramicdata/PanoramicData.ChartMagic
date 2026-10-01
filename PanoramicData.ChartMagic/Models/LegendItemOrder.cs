namespace PanoramicData.ChartMagic.Models;

/// <summary>
/// The order a legend lists its entries in.
/// </summary>
public enum LegendItemOrder
{
	/// <summary>
	/// Series order, reversed when the series are stacked, so the legend reads top to bottom in
	/// the order the stack does. This is what the Microsoft chart control does by default.
	/// </summary>
	Auto,

	/// <summary>Always the order the series were added in.</summary>
	SameAsSeriesOrder,

	/// <summary>Always the reverse of the order the series were added in.</summary>
	ReversedSeriesOrder
}
