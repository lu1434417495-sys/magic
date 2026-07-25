using System;

internal static class ItemPriceRules
{
    internal const int BasisPointsDenominator = 10000;

    internal static int ApplyBasisPoints(int price, int priceBasisPoints)
    {
        long normalizedPrice = Math.Max((long)price, 0L);
        long normalizedBasisPoints = Math.Max((long)priceBasisPoints, 0L);
        long roundedPrice =
            (
                normalizedPrice * normalizedBasisPoints
                + BasisPointsDenominator / 2L
            )
            / BasisPointsDenominator;
        return (int)Math.Min(roundedPrice, int.MaxValue);
    }
}
