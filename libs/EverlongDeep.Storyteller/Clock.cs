using System.Globalization;

namespace EverlongDeep.Storyteller;

/// <summary>Time as the Storyteller reads it, where the simulation only counts ticks: 1 tick = 1 second.</summary>
public static class Clock
{
    private const int _ticksPerMinute = 60;
    public const int TicksPerHour = 60 * _ticksPerMinute;
    private const int _hoursPerDay = 24;

    /// <summary><paramref name="tick"/> as hours, minutes and seconds since the run began, "27:05:12"; the past is "-03:00:00".</summary>
    public static string At(long tick)
    {
        long ticks = Math.Abs(tick);
        string sign = tick < 0 ? "-" : "";
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{sign}{ticks / TicksPerHour:00}:{ticks % TicksPerHour / _ticksPerMinute:00}:{ticks % _ticksPerMinute:00}");
    }

    /// <summary>How long <paramref name="ticks"/> last, in whole minutes and the seconds over: "4 min", "3 min 24 s" or "30 s".</summary>
    public static string Length(long ticks)
    {
        long minutes = ticks / _ticksPerMinute;
        long seconds = ticks % _ticksPerMinute;
        return (minutes, seconds) switch
        {
            (_, 0) => $"{minutes} min",
            (0, _) => $"{seconds} s",
            _ => $"{minutes} min {seconds} s",
        };
    }

    /// <summary>How long before <paramref name="now"/> <paramref name="tick"/> was: "this hour", "5 hours ago" or "9 days ago".</summary>
    public static string Ago(long now, long tick)
    {
        long hours = (now - tick) / TicksPerHour;
        if (hours < 1)
        {
            return "this hour";
        }

        if (hours < _hoursPerDay)
        {
            return hours == 1 ? "1 hour ago" : $"{hours} hours ago";
        }

        long days = (long)Math.Round((double)hours / _hoursPerDay, MidpointRounding.AwayFromZero);
        return days == 1 ? "1 day ago" : $"{days} days ago";
    }
}
