namespace PanoramicData.ChartMagic.Renderers;

internal sealed partial class PlotGeometry
{
	/// <summary>
	/// The position of a value along the value axis: down the Y axis for columns, across the
	/// X axis for bars.
	/// </summary>
	internal double ValueToPixels(double yValue)
	{
		if (!IsHorizontalPlot)
		{
			return YToPixels(yValue);
		}

		return IsNearlyZero(_yDisplayRange)
			? 0
			: Math.Round(Width * (yValue - _yDisplayStart) / _yDisplayRange, 2);
	}

	/// <summary>
	/// Where zero sits on the value axis, clamped into the plot. Columns and bars are drawn
	/// from here, so when zero falls outside the plot they are drawn from the nearest edge.
	/// </summary>
	internal double ValueAxisOrigin => Math.Clamp(
		ValueToPixels(YIsLogarithmic ? YLogMinimum : 0),
		0,
		IsHorizontalPlot ? Width : Height);

	internal double XToPixels(double xValue)
	{
		if (IsCategorical)
		{
			var index = _categories.IndexOf(xValue);
			if (index < 0)
			{
				index = 0;
			}

			return Math.Round(CategoryOffset(index) * BandWidth, 2);
		}

		return IsNearlyZero(_xDisplayRange)
			? 0
			: Math.Round(Width * (xValue - _xDisplayStart) / _xDisplayRange, 2);
	}

	internal double YToPixels(double yValue)
	{
		if (YIsLogarithmic)
		{
			if (IsNearlyZero(_yLogRange))
			{
				return Height;
			}

			// A non-positive value has no place on a logarithmic axis; pin it to the floor
			// rather than producing a NaN that silently removes the whole path.
			var clamped = yValue <= 0 ? YLogMinimum : yValue;
			return Math.Round(Height * (1 - ((Math.Log10(clamped) - _yLogStart) / _yLogRange)), 2);
		}

		return IsNearlyZero(_yDisplayRange)
			? Height
			: Math.Round(Height * (1 - ((yValue - _yDisplayStart) / _yDisplayRange)), 2);
	}
}
