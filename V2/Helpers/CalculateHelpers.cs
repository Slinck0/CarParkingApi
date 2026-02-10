using V2.Models;
using V2.Services;

public class CalculateHelpers
{
    public static (decimal price, int hours, int extraDays) CalculatePrice(ParkingLotModel lot, DateTime start, DateTime end)
    {
        var diff = end - start;
        var hours = (int)Math.Ceiling(diff.TotalSeconds / 3600.0);

        if (diff.TotalSeconds < 180) return (0m, hours, 0);

        if (end.Date > start.Date)
        {
            var days = (end.Date - start.Date).Days + 1;
            var dayTariff = lot.DayTariff ?? 999m;
            return (dayTariff * days, hours, days);
        }

        var hourly = lot.Tariff * hours;
        var dayCap = lot.DayTariff ?? 999m;
        if (hourly > dayCap) hourly = dayCap;
        return (hourly, hours, 0);
    }

    /// <summary>
    /// Calculates price with optional discount applied
    /// </summary>
    public static (decimal originalPrice, decimal discountAmount, decimal finalPrice)
        CalculatePriceWithDiscount(
            ParkingLotModel lot,
            DateTime start,
            DateTime end,
            DiscountModel? discount)
    {
        // Calculate original price using existing method
        var (originalPrice, hours, extraDays) = CalculatePrice(lot, start, end);

        // If no discount, return original price
        if (discount == null)
        {
            return (originalPrice, 0m, originalPrice);
        }

        // Calculate discount amount
        var discountAmount = DiscountValidationService.CalculateDiscountAmount(
            discount,
            originalPrice);

        // Calculate final price (never negative)
        var finalPrice = Math.Max(0, originalPrice - discountAmount);

        return (originalPrice, discountAmount, finalPrice);
    }
}
