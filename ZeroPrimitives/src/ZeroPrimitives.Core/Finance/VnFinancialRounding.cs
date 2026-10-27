using System;
using System.Runtime.CompilerServices;

namespace ZeroPrimitives.Finance
{
    /// <summary>
    /// Financial calculations and rounding rules according to Vietnamese Accounting Standards (VAS & Circular 200/2014/TT-BTC).
    /// Provides banker's rounding, commercial rounding (AwayFromZero), and VAT line-item reconciliation.
    /// </summary>
    public static class VnFinancialRounding
    {
        /// <summary>
        /// Rounds Vietnamese Dong (VND) currency amount to the specified decimal precision (default 0 digits).
        /// Follows commercial rounding (MidpointRounding.AwayFromZero) as standard in Vietnamese invoicing and retail.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static decimal RoundVnd(decimal amount, int decimals = 0)
        {
            return Math.Round(amount, decimals, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// Calculates VAT amount given a subtotal and VAT percentage rate (e.g. 8m, 10m).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static decimal CalculateVat(decimal subTotal, decimal vatRatePercent, int decimals = 0)
        {
            if (subTotal == 0m || vatRatePercent == 0m) return 0m;
            decimal rawVat = subTotal * (vatRatePercent / 100m);
            return Math.Round(rawVat, decimals, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// Calculates trade discount amount given subtotal and discount percentage rate.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static decimal CalculateDiscount(decimal subTotal, decimal discountRatePercent, int decimals = 0)
        {
            if (subTotal == 0m || discountRatePercent == 0m) return 0m;
            decimal rawDiscount = subTotal * (discountRatePercent / 100m);
            return Math.Round(rawDiscount, decimals, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// Reconciles the 1-VND rounding difference between sum of individual line VATs and invoice header VAT.
        /// Returns the discrepancy in VND (headerVat - sumLineVat).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static decimal GetVatRoundingDiscrepancy(decimal headerVat, decimal sumLineVat)
        {
            return headerVat - sumLineVat;
        }
    }
}
