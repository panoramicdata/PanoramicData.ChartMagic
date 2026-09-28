namespace PanoramicData.ChartMagic.Renderers.RenderModels;

/// <summary>
/// Where one point's data label goes, and which way its text runs from there.
/// </summary>
/// <param name="Point">The point being labelled.</param>
/// <param name="Value">The value of the point, which is what #VAL and #VALY show.</param>
/// <param name="X">The anchor, in pixels across the inner plot.</param>
/// <param name="Y">The anchor, in pixels down the inner plot.</param>
/// <param name="HorizontalAlignment">How the text sits horizontally against the anchor.</param>
/// <param name="VerticalAlignment">How the text sits vertically against the anchor.</param>
internal sealed record DataLabelAnchor(
	ChartPoint Point,
	double Value,
	double X,
	double Y,
	HorizontalAlignment HorizontalAlignment,
	VerticalAlignment VerticalAlignment);
