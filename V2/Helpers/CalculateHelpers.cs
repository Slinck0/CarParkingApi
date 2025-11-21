using V2.Models;

public class Helpers
{
    public static (decimal price, int hours, int extraDays) CalculatePrice(ParkingLot lot, DateTime start, DateTime end)
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
}
