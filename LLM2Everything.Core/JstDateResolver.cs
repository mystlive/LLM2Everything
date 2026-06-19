namespace LLM2Everything.Core;

public sealed class JstDateResolver
{
    public static readonly TimeSpan JstOffset = TimeSpan.FromHours(9);

    public DateTimeOffset ToJst(DateTimeOffset value) => value.ToOffset(JstOffset);

    public DateRange Today(DateTimeOffset now)
    {
        var jst = ToJst(now);
        var start = new DateTimeOffset(jst.Year, jst.Month, jst.Day, 0, 0, 0, JstOffset);
        return new(start, start.AddDays(1).AddTicks(-1), "今日");
    }

    public DateRange Yesterday(DateTimeOffset now)
    {
        var today = Today(now);
        var start = today.Start!.Value.AddDays(-1);
        return new(start, today.Start.Value.AddTicks(-1), "昨日");
    }

    public DateRange ThisWeek(DateTimeOffset now)
    {
        var jst = ToJst(now);
        var daysFromMonday = ((int)jst.DayOfWeek + 6) % 7;
        var startDate = jst.Date.AddDays(-daysFromMonday);
        var start = new DateTimeOffset(startDate.Year, startDate.Month, startDate.Day, 0, 0, 0, JstOffset);
        return new(start, jst, "今週");
    }

    public DateRange LastWeek(DateTimeOffset now)
    {
        var thisWeek = ThisWeek(now);
        var start = thisWeek.Start!.Value.AddDays(-7);
        return new(start, thisWeek.Start.Value.AddTicks(-1), "先週");
    }

    public DateRange DayOfThisWeek(DateTimeOffset now, DayOfWeek day)
    {
        var week = ThisWeek(now);
        var start = week.Start!.Value.AddDays(DaysFromMonday(day));
        return DayRange(start, $"今週{JapaneseWeekday(day)}");
    }

    public DateRange DayOfLastWeek(DateTimeOffset now, DayOfWeek day)
    {
        var week = LastWeek(now);
        var start = week.Start!.Value.AddDays(DaysFromMonday(day));
        return DayRange(start, $"先週{JapaneseWeekday(day)}");
    }

    public DateRange PastDays(DateTimeOffset now, int days) => new(ToJst(now).AddDays(-days), ToJst(now), $"過去{days}日");
    public DateRange PastHours(DateTimeOffset now, int hours) => new(ToJst(now).AddHours(-hours), ToJst(now), $"過去{hours}時間");

    public DateRange DayOfCurrentMonth(DateTimeOffset now, int day)
    {
        var jst = ToJst(now);
        var start = new DateTimeOffset(jst.Year, jst.Month, day, 0, 0, 0, JstOffset);
        return new(start, start.AddDays(1).AddTicks(-1), $"{day}日");
    }

    private static DateRange DayRange(DateTimeOffset start, string label) =>
        new(start, start.AddDays(1).AddTicks(-1), label);

    private static int DaysFromMonday(DayOfWeek day) => ((int)day + 6) % 7;

    private static string JapaneseWeekday(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "月曜日",
        DayOfWeek.Tuesday => "火曜日",
        DayOfWeek.Wednesday => "水曜日",
        DayOfWeek.Thursday => "木曜日",
        DayOfWeek.Friday => "金曜日",
        DayOfWeek.Saturday => "土曜日",
        DayOfWeek.Sunday => "日曜日",
        _ => throw new ArgumentOutOfRangeException(nameof(day))
    };
}
