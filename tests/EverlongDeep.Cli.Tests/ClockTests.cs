namespace EverlongDeep.Cli.Tests;

public class ClockTests
{
    [Fact]
    public void At_ReadsATickAsHoursMinutesAndSeconds_ThePastWithASign()
    {
        Clock.At(0).ShouldBe("00:00:00");
        Clock.At((27 * 3_600) + (5 * 60) + 12).ShouldBe("27:05:12");
        Clock.At(-3 * 3_600).ShouldBe("-03:00:00");
        Clock.At(-1).ShouldBe("-00:00:01");
    }

    [Fact]
    public void Length_ReadsWholeMinutes_AndTheSecondsOver()
    {
        Clock.Length(240).ShouldBe("4 min");
        Clock.Length(204).ShouldBe("3 min 24 s");
    }

    [Fact]
    public void Ago_ReadsHoursUnderADay_ThenDays()
    {
        Clock.Ago(now: 3_599, tick: 0).ShouldBe("this hour");
        Clock.Ago(now: 3_600, tick: 0).ShouldBe("1 hour ago");
        Clock.Ago(now: 0, tick: -23 * 3_600).ShouldBe("23 hours ago");
        Clock.Ago(now: 0, tick: -36 * 3_600).ShouldBe("2 days ago");
    }
}
