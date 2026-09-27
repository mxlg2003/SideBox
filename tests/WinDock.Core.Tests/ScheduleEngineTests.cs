using WinDock.Core;
using Xunit;

namespace WinDock.Core.Tests;

public class ScheduleEngineTests
{
    [Fact]
    public void Days_remaining_format_calculations()
    {
        var today = new DateOnly(2026, 10, 1);

        using (UiLocale.Push(UiLanguage.Chinese))
        {
            Assert.Equal("就是今天！", ScheduleFormat.DaysRemaining(today, today));
            Assert.Equal("还有 3 天", ScheduleFormat.DaysRemaining(new DateOnly(2026, 10, 4), today));
            Assert.Equal("已过 2 天", ScheduleFormat.DaysRemaining(new DateOnly(2026, 9, 29), today));
        }
    }

    [Fact]
    public void Time_remaining_format_calculations()
    {
        var now = new DateTime(2026, 10, 1, 14, 0, 0);

        Assert.Equal("30分钟后", ScheduleFormat.TimeRemaining(now.AddMinutes(30), now));
        Assert.Equal("2小时后", ScheduleFormat.TimeRemaining(now.AddHours(2), now));
        Assert.Equal("15分前", ScheduleFormat.TimeRemaining(now.AddMinutes(-15), now));
    }

    [Fact]
    public void Engine_picks_next_upcoming_event()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var now = DateTime.Today.AddHours(14); // 14:00 today

        var pastEvent = new ScheduleItem
        {
            Title = "早会",
            Date = today,
            Time = new TimeSpan(9, 30, 0),
            IsCompleted = true
        };

        var nextEvent = new ScheduleItem
        {
            Title = "技术评审",
            Date = today,
            Time = new TimeSpan(15, 0, 0),
            IsCompleted = false
        };

        var laterEvent = new ScheduleItem
        {
            Title = "下班周报",
            Date = today,
            Time = new TimeSpan(18, 0, 0),
            IsCompleted = false
        };

        var engine = new ScheduleEngine(new ScheduleSettings
        {
            Items = [pastEvent, nextEvent, laterEvent]
        });

        var upcoming = engine.GetNextUpcomingEvent(now);
        Assert.NotNull(upcoming);
        Assert.Equal("技术评审", upcoming.Title);

        var (title, sub, isCountdown) = engine.GetDockSummary(now);
        Assert.Equal("技术评审", title);
        Assert.Equal("1小时后", sub);
        Assert.False(isCountdown);
    }

    [Fact]
    public void Engine_falls_back_to_countdown_when_no_today_events()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var targetDate = today.AddDays(10);

        var countdown = new ScheduleItem
        {
            Title = "国庆长假",
            Kind = ScheduleKind.Countdown,
            Date = targetDate
        };

        var engine = new ScheduleEngine(new ScheduleSettings
        {
            Items = [countdown]
        });

        using (UiLocale.Push(UiLanguage.Chinese))
        {
            var (title, sub, isCountdown) = engine.GetDockSummary(DateTime.Now);
            Assert.Equal("国庆长假", title);
            Assert.Equal("还有 10 天", sub);
            Assert.True(isCountdown);
        }
    }

    [Fact]
    public void Toggle_complete_and_remove_items()
    {
        var item = new ScheduleItem
        {
            Id = "test-1",
            Title = "买咖啡",
            IsCompleted = false
        };

        var engine = new ScheduleEngine();
        engine.Add(item);
        Assert.Single(engine.Settings.Items);

        engine.ToggleComplete("test-1");
        Assert.True(engine.Settings.Items[0].IsCompleted);

        engine.ToggleComplete("test-1");
        Assert.False(engine.Settings.Items[0].IsCompleted);

        engine.Remove("test-1");
        Assert.Empty(engine.Settings.Items);
    }

    [Fact]
    public void Normalization_ensures_six_categories_and_defaults()
    {
        var settings = ScheduleSettings.Normalize(new ScheduleSettings
        {
            Categories = [new ScheduleCategory { ColorId = ScheduleColorId.Orange, Title = "My Work" }]
        });

        Assert.Equal(6, settings.Categories.Count);
        Assert.Equal("My Work", settings.GetCategoryTitle(ScheduleColorId.Orange));

        using (UiLocale.Push(UiLanguage.Chinese))
        {
            Assert.Equal("个人", settings.GetCategoryTitle(ScheduleColorId.Green));
            Assert.Equal("团队", settings.GetCategoryTitle(ScheduleColorId.Blue));
            Assert.Equal("重要", settings.GetCategoryTitle(ScheduleColorId.Purple));
            Assert.Equal("紧急", settings.GetCategoryTitle(ScheduleColorId.Red));
            Assert.Equal("学习", settings.GetCategoryTitle(ScheduleColorId.Amber));
        }
    }

    [Fact]
    public void Update_and_reset_category_titles()
    {
        var engine = new ScheduleEngine();
        engine.UpdateCategoryTitle(ScheduleColorId.Blue, "跨国项目对齐");
        Assert.Equal("跨国项目对齐", engine.Settings.GetCategoryTitle(ScheduleColorId.Blue));

        engine.ResetCategoriesToDefault();
        using (UiLocale.Push(UiLanguage.Chinese))
        {
            Assert.Equal("团队", engine.Settings.GetCategoryTitle(ScheduleColorId.Blue));
        }
    }

    [Fact]
    public void Events_are_sorted_by_time_ascending_and_all_day_separated()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var e1 = new ScheduleItem { Title = "设计评审", Time = new TimeSpan(10, 30, 0), ColorId = ScheduleColorId.Orange, Date = today };
        var e2 = new ScheduleItem { Title = "午餐聚会", Time = new TimeSpan(12, 0, 0), ColorId = ScheduleColorId.Green, Date = today };
        var e3 = new ScheduleItem { Title = "全天团建", IsAllDay = true, ColorId = ScheduleColorId.Blue, Date = today };
        var e4 = new ScheduleItem { Title = "晚间复盘", Time = new TimeSpan(18, 0, 0), ColorId = ScheduleColorId.Purple, Date = today };
        var e5 = new ScheduleItem { Title = "下午对齐", Time = new TimeSpan(14, 0, 0), ColorId = ScheduleColorId.Blue, Date = today };

        var engine = new ScheduleEngine(new ScheduleSettings
        {
            Items = [e4, e1, e3, e2, e5]
        });

        var (timed, allDay) = engine.GetTodayEvents(today);
        Assert.Equal(4, timed.Count);
        Assert.Equal("设计评审", timed[0].Title); // 10:30
        Assert.Equal("午餐聚会", timed[1].Title); // 12:00
        Assert.Equal("下午对齐", timed[2].Title); // 14:00
        Assert.Equal("晚间复盘", timed[3].Title); // 18:00

        Assert.Single(allDay);
        Assert.Equal("全天团建", allDay[0].Title);
    }

    [Fact]
    public void Upcoming_events_limited_to_max_three()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var now = DateTime.Today.AddHours(9); // 09:00

        var e1 = new ScheduleItem { Title = "第1件事", Time = new TimeSpan(10, 0, 0), Date = today };
        var e2 = new ScheduleItem { Title = "第2件事", Time = new TimeSpan(11, 0, 0), Date = today };
        var e3 = new ScheduleItem { Title = "第3件事", Time = new TimeSpan(13, 0, 0), Date = today };
        var e4 = new ScheduleItem { Title = "第4件事", Time = new TimeSpan(15, 0, 0), Date = today };

        var engine = new ScheduleEngine(new ScheduleSettings
        {
            Items = [e1, e2, e3, e4]
        });

        var upcoming = engine.GetTodayUpcomingEvents(now, limit: 3);
        Assert.Equal(3, upcoming.Count);
        Assert.Equal("第1件事", upcoming[0].Title);
        Assert.Equal("第2件事", upcoming[1].Title);
        Assert.Equal("第3件事", upcoming[2].Title);
    }

    [Theory]
    [InlineData("9:00", 9, 0)]
    [InlineData("14:30", 14, 30)]
    [InlineData("9", 9, 0)]
    public void Clock_parse_accepts_hours_and_hm(string text, int hours, int minutes)
    {
        Assert.True(ScheduleFormat.TryParseClock(text, out var time));
        Assert.Equal(new TimeSpan(hours, minutes, 0), time);
    }

    [Theory]
    [InlineData("9.5")]
    [InlineData("25")]
    [InlineData("abc")]
    public void Clock_parse_rejects_day_spans_and_junk(string text)
    {
        Assert.False(ScheduleFormat.TryParseClock(text, out _));
    }

    [Fact]
    public void Add_rejects_when_full_of_today_items()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var items = Enumerable.Range(0, ScheduleSettings.MaxItems)
            .Select(i => new ScheduleItem { Title = $"item-{i}", Date = today })
            .ToArray();
        var engine = new ScheduleEngine(new ScheduleSettings { Items = items });

        Assert.False(engine.TryAdd(new ScheduleItem { Title = "overflow", Date = today }, today, out var next));
        Assert.Equal(ScheduleSettings.MaxItems, next.Items.Count);
        Assert.DoesNotContain(next.Items, item => item.Title == "overflow");
    }

    [Fact]
    public void Add_drops_oldest_expired_when_full()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var items = Enumerable.Range(0, ScheduleSettings.MaxItems)
            .Select(i => new ScheduleItem
            {
                Title = $"item-{i}",
                Date = i == 0 ? today.AddDays(-3) : today
            })
            .ToArray();
        var engine = new ScheduleEngine(new ScheduleSettings { Items = items });

        Assert.True(engine.TryAdd(new ScheduleItem { Title = "newest", Date = today }, today, out var next));
        Assert.Equal(ScheduleSettings.MaxItems, next.Items.Count);
        Assert.Contains(next.Items, item => item.Title == "newest");
        Assert.DoesNotContain(next.Items, item => item.Title == "item-0");
    }
}
