using System.Globalization;

namespace WinDock.Core;

public enum ScheduleKind
{
    Event,
    Countdown
}

public enum ScheduleColorId
{
    Orange = 0,
    Green = 1,
    Blue = 2,
    Purple = 3,
    Red = 4,
    Amber = 5
}

public static class ScheduleColors
{
    public const int Count = 6;

    public static readonly IReadOnlyList<ScheduleColorId> All =
    [
        ScheduleColorId.Orange,
        ScheduleColorId.Green,
        ScheduleColorId.Blue,
        ScheduleColorId.Purple,
        ScheduleColorId.Red,
        ScheduleColorId.Amber
    ];

    public static uint Argb(ScheduleColorId id, bool isLight = false) => id switch
    {
        ScheduleColorId.Orange => isLight ? 0xFFE35B00 : 0xFFFF8C00,
        ScheduleColorId.Green  => isLight ? 0xFF107C41 : 0xFF34C759,
        ScheduleColorId.Blue   => isLight ? 0xFF0067B8 : 0xFF0A84FF,
        ScheduleColorId.Purple => isLight ? 0xFF744DA9 : 0xFFAF52DE,
        ScheduleColorId.Red    => isLight ? 0xFFD13438 : 0xFFFF453A,
        ScheduleColorId.Amber  => isLight ? 0xFFD97706 : 0xFFFFB900,
        _ => isLight ? 0xFFE35B00 : 0xFFFF8C00
    };

    public static string DefaultTitle(ScheduleColorId id) => id switch
    {
        ScheduleColorId.Orange => UiText.CategoryWork,
        ScheduleColorId.Green  => UiText.CategoryPersonal,
        ScheduleColorId.Blue   => UiText.CategoryTeam,
        ScheduleColorId.Purple => UiText.CategoryImportant,
        ScheduleColorId.Red    => UiText.CategoryUrgent,
        ScheduleColorId.Amber  => UiText.CategoryStudy,
        _ => UiText.CategoryWork
    };
}

public static class ScheduleLayouts
{
    public const string NextEvent = "nextEvent";
    public const string DateNext = "dateNext";
    public const string DateAgenda = "dateAgenda";
    public const string Date = "date";

    public static readonly IReadOnlyList<string> All = [NextEvent, DateNext, DateAgenda, Date];

    public static string Normalize(string? layout) => layout switch
    {
        NextEvent => NextEvent,
        DateNext => DateNext,
        Date => Date,
        _ => DateAgenda
    };

    public static string Title(string layout) => Normalize(layout) switch
    {
        NextEvent => UiText.ScheduleLayoutNext,
        DateNext => UiText.ScheduleLayoutDateNext,
        Date => UiText.ScheduleLayoutDate,
        _ => UiText.ScheduleLayoutAgenda
    };
}

public sealed record ScheduleCategory
{
    public ScheduleColorId ColorId { get; init; } = ScheduleColorId.Orange;

    public string Title { get; init; } = "";

    public static IReadOnlyList<ScheduleCategory> CreateDefaults() =>
    [
        new() { ColorId = ScheduleColorId.Orange, Title = "" },
        new() { ColorId = ScheduleColorId.Green,  Title = "" },
        new() { ColorId = ScheduleColorId.Blue,   Title = "" },
        new() { ColorId = ScheduleColorId.Purple, Title = "" },
        new() { ColorId = ScheduleColorId.Red,    Title = "" },
        new() { ColorId = ScheduleColorId.Amber,  Title = "" }
    ];
}

public sealed record ScheduleItem
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public string Title { get; init; } = "";

    public ScheduleKind Kind { get; init; } = ScheduleKind.Event;

    public DateOnly Date { get; init; } = DateOnly.FromDateTime(DateTime.Today);

    public TimeSpan? Time { get; init; }

    public bool IsAllDay { get; init; }

    public ScheduleColorId ColorId { get; init; } = ScheduleColorId.Orange;

    public bool IsCompleted { get; init; }

    public string Remark { get; init; } = "";
}

public sealed record ScheduleSettings
{
    public const int MaxItems = 100;

    public IReadOnlyList<ScheduleCategory> Categories { get; init; } = ScheduleCategory.CreateDefaults();

    public IReadOnlyList<ScheduleItem> Items { get; init; } = [];

    public string Layout { get; init; } = ScheduleLayouts.DateAgenda;

    public static ScheduleSettings Default { get; } = new();

    public static ScheduleSettings Normalize(ScheduleSettings? settings)
    {
        var value = settings ?? Default;

        var cats = new List<ScheduleCategory>(ScheduleColors.Count);
        var existingCats = (value.Categories ?? []).ToDictionary(c => c.ColorId, c => c.Title);
        foreach (var colorId in ScheduleColors.All)
        {
            var title = existingCats.TryGetValue(colorId, out var custom) ? (custom ?? "").Trim() : "";
            cats.Add(new ScheduleCategory { ColorId = colorId, Title = title });
        }

        var list = new List<ScheduleItem>();
        foreach (var item in value.Items ?? [])
        {
            if (string.IsNullOrWhiteSpace(item.Title))
            {
                continue;
            }

            var colorId = Enum.IsDefined(item.ColorId) ? item.ColorId : ScheduleColorId.Orange;

            list.Add(item with
            {
                Title = item.Title.Trim(),
                Remark = (item.Remark ?? "").Trim(),
                ColorId = colorId
            });
        }

        if (list.Count > MaxItems)
        {
            list.RemoveRange(0, list.Count - MaxItems);
        }

        return value with
        {
            Categories = cats,
            Items = list,
            Layout = ScheduleLayouts.Normalize(value.Layout)
        };
    }

    public static bool TryAdd(ScheduleSettings settings, ScheduleItem item, DateOnly today, out ScheduleSettings next)
    {
        next = Normalize(settings);
        if (string.IsNullOrWhiteSpace(item.Title))
        {
            return false;
        }

        var list = next.Items.ToList();
        while (list.Count >= MaxItems)
        {
            var expired = list
                .Where(entry => entry.Date < today)
                .OrderBy(entry => entry.Date)
                .ThenBy(entry => entry.Id, StringComparer.Ordinal)
                .FirstOrDefault();
            if (expired is null)
            {
                return false;
            }

            list.Remove(expired);
        }

        list.Add(item);
        next = Normalize(settings with { Items = list });
        return true;
    }

    public string GetCategoryTitle(ScheduleColorId colorId)
    {
        var cat = Categories.FirstOrDefault(c => c.ColorId == colorId);
        if (cat != null && !string.IsNullOrWhiteSpace(cat.Title))
        {
            return cat.Title;
        }
        return ScheduleColors.DefaultTitle(colorId);
    }

    public static string StoredCategoryTitle(ScheduleColorId colorId, string? edited)
    {
        var title = (edited ?? "").Trim();
        if (title.Length == 0 || string.Equals(title, ScheduleColors.DefaultTitle(colorId), StringComparison.Ordinal))
        {
            return "";
        }

        return title;
    }

    public static bool Same(ScheduleSettings left, ScheduleSettings right)
    {
        if (!string.Equals(ScheduleLayouts.Normalize(left.Layout), ScheduleLayouts.Normalize(right.Layout), StringComparison.Ordinal)
            || left.Items.Count != right.Items.Count
            || left.Categories.Count != right.Categories.Count)
        {
            return false;
        }

        for (var i = 0; i < left.Categories.Count; i++)
        {
            var ca = left.Categories[i];
            var cb = right.Categories[i];
            if (ca.ColorId != cb.ColorId || ca.Title != cb.Title)
            {
                return false;
            }
        }

        for (var i = 0; i < left.Items.Count; i++)
        {
            var a = left.Items[i];
            var b = right.Items[i];
            if (a.Id != b.Id
                || a.Title != b.Title
                || a.Kind != b.Kind
                || a.Date != b.Date
                || a.Time != b.Time
                || a.IsAllDay != b.IsAllDay
                || a.ColorId != b.ColorId
                || a.IsCompleted != b.IsCompleted
                || a.Remark != b.Remark)
            {
                return false;
            }
        }

        return true;
    }
}

public static class ScheduleFormat
{
    public static string TimeRemaining(DateTime eventDateTime, DateTime now)
    {
        var diff = eventDateTime - now;
        if (diff.TotalSeconds < 0)
        {
            var ago = -diff;
            if (ago.TotalMinutes < 60)
            {
                return $"{Math.Max(1, (int)ago.TotalMinutes)}分前";
            }
            return $"{(int)ago.TotalHours}小时前";
        }

        if (diff.TotalMinutes < 1)
        {
            return "即将开始";
        }

        if (diff.TotalMinutes < 60)
        {
            return $"{(int)diff.TotalMinutes}分钟后";
        }

        if (diff.TotalHours < 24)
        {
            var h = (int)diff.TotalHours;
            var m = diff.Minutes;
            return m > 0 ? $"{h}小时{m}分后" : $"{h}小时后";
        }

        return $"{(int)diff.TotalDays}天后";
    }

    public static string DaysRemaining(DateOnly targetDate, DateOnly today)
    {
        var diff = targetDate.DayNumber - today.DayNumber;
        if (diff == 0)
        {
            return UiText.ScheduleTodayIsDay;
        }

        if (diff > 0)
        {
            return string.Format(UiText.ScheduleDaysRemainingFormat, diff);
        }

        return string.Format(UiText.ScheduleDaysPassedFormat, -diff);
    }

    public static bool TryParseClock(string text, out TimeSpan time)
    {
        time = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var value = text.Trim();
        string[] formats = ["H\\:mm", "HH\\:mm", "h\\:mm", "hh\\:mm"];
        if (TimeSpan.TryParseExact(value, formats, CultureInfo.InvariantCulture, out var parsed)
            && parsed >= TimeSpan.Zero
            && parsed < TimeSpan.FromDays(1))
        {
            time = parsed;
            return true;
        }

        if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var hour)
            && hour is >= 0 and <= 23)
        {
            time = TimeSpan.FromHours(hour);
            return true;
        }

        return false;
    }
}

public sealed class ScheduleEngine
{
    public static readonly TimeSpan DefaultClock = new(9, 0, 0);

    public ScheduleSettings Settings { get; private set; }

    public ScheduleEngine(ScheduleSettings? settings = null)
    {
        Settings = ScheduleSettings.Normalize(settings);
    }

    public void ApplySettings(ScheduleSettings? settings)
    {
        Settings = ScheduleSettings.Normalize(settings);
    }

    public ScheduleItem? GetNextUpcomingEvent(DateTime now)
    {
        var today = DateOnly.FromDateTime(now);
        var currentTime = now.TimeOfDay;

        return Settings.Items
            .Where(i => i.Kind == ScheduleKind.Event && !i.IsCompleted && i.Date == today)
            .OrderBy(ClockOf)
            .FirstOrDefault(i => ClockOf(i) >= currentTime.Subtract(TimeSpan.FromMinutes(30)));
    }

    public ScheduleItem? GetNearestCountdown(DateOnly today)
    {
        return Settings.Items
            .Where(i => i.Kind == ScheduleKind.Countdown && !i.IsCompleted)
            .OrderBy(i => Math.Abs(i.Date.DayNumber - today.DayNumber))
            .FirstOrDefault();
    }

    public (string Title, string Subtitle, bool IsCountdown) GetDockSummary(DateTime now)
    {
        var upcoming = GetNextUpcomingEvent(now);
        if (upcoming != null)
        {
            var eventTime = upcoming.Date.ToDateTime(TimeOnly.FromTimeSpan(ClockOf(upcoming)));
            var sub = ScheduleFormat.TimeRemaining(eventTime, now);

            return (upcoming.Title, sub, false);
        }

        var today = DateOnly.FromDateTime(now);
        var countdown = GetNearestCountdown(today);
        if (countdown != null)
        {
            var sub = ScheduleFormat.DaysRemaining(countdown.Date, today);
            return (countdown.Title, sub, true);
        }

        return (UiText.ScheduleNoEvents, UiText.ScheduleRelax, false);
    }

    public IReadOnlyList<ScheduleItem> GetTodayUpcomingEvents(DateTime now, int limit = 3)
    {
        var today = DateOnly.FromDateTime(now);
        return Settings.Items
            .Where(i => i.Kind == ScheduleKind.Event && !i.IsCompleted && i.Date == today)
            .OrderBy(ClockOf)
            .Take(limit)
            .ToList();
    }

    public (IReadOnlyList<ScheduleItem> Timed, IReadOnlyList<ScheduleItem> AllDay) GetTodayEvents(DateOnly today)
    {
        var timed = Settings.Items
            .Where(i => i.Kind == ScheduleKind.Event && i.Date == today && !i.IsAllDay && i.Time != null)
            .OrderBy(i => i.IsCompleted)
            .ThenBy(i => i.Time!.Value)
            .ToList();

        var allDay = Settings.Items
            .Where(i => i.Kind == ScheduleKind.Event && i.Date == today && (i.IsAllDay || i.Time == null))
            .OrderBy(i => i.IsCompleted)
            .ToList();

        return (timed, allDay);
    }

    public IReadOnlyList<ScheduleItem> GetOverdueEvents(DateOnly today)
    {
        return Settings.Items
            .Where(i => i.Kind == ScheduleKind.Event && !i.IsCompleted && i.Date < today)
            .OrderBy(i => i.Date)
            .ThenBy(ClockOf)
            .ToList();
    }

    public IReadOnlyList<ScheduleItem> GetUpcomingEvents(DateOnly today)
    {
        return Settings.Items
            .Where(i => i.Kind == ScheduleKind.Event && !i.IsCompleted && i.Date > today)
            .OrderBy(i => i.Date)
            .ThenBy(ClockOf)
            .ToList();
    }

    public static TimeSpan ClockOf(ScheduleItem item)
    {
        if (!item.IsAllDay && item.Time is { } time && time >= TimeSpan.Zero && time < TimeSpan.FromDays(1))
        {
            return new TimeSpan(time.Hours, time.Minutes, 0);
        }

        return DefaultClock;
    }

    public ScheduleSettings UpdateCategoryTitle(ScheduleColorId colorId, string newTitle)
    {
        var cats = Settings.Categories.Select(c => c.ColorId == colorId ? c with { Title = (newTitle ?? "").Trim() } : c).ToList();
        Settings = ScheduleSettings.Normalize(Settings with { Categories = cats });
        return Settings;
    }

    public ScheduleSettings ResetCategoriesToDefault()
    {
        var cats = ScheduleColors.All.Select(c => new ScheduleCategory { ColorId = c, Title = "" }).ToList();
        Settings = ScheduleSettings.Normalize(Settings with { Categories = cats });
        return Settings;
    }

    public ScheduleSettings Add(ScheduleItem item)
    {
        TryAdd(item, DateOnly.FromDateTime(DateTime.Today), out var next);
        return next;
    }

    public bool TryAdd(ScheduleItem item, DateOnly today, out ScheduleSettings next)
    {
        if (!ScheduleSettings.TryAdd(Settings, item, today, out next))
        {
            next = Settings;
            return false;
        }

        Settings = next;
        return true;
    }

    public ScheduleSettings ToggleComplete(string id)
    {
        var list = Settings.Items.Select(i => i.Id == id ? i with { IsCompleted = !i.IsCompleted } : i).ToList();
        Settings = ScheduleSettings.Normalize(Settings with { Items = list });
        return Settings;
    }

    public ScheduleSettings Remove(string id)
    {
        var list = Settings.Items.Where(i => i.Id != id).ToList();
        Settings = ScheduleSettings.Normalize(Settings with { Items = list });
        return Settings;
    }

    public bool TryUpdate(string id, string title, DateOnly date, TimeSpan? time, bool isAllDay, out ScheduleSettings next)
    {
        next = Settings;
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        var found = false;
        var list = new List<ScheduleItem>(Settings.Items.Count);
        foreach (var item in Settings.Items)
        {
            if (!string.Equals(item.Id, id, StringComparison.Ordinal))
            {
                list.Add(item);
                continue;
            }

            found = true;
            if (item.Kind == ScheduleKind.Countdown)
            {
                list.Add(item with
                {
                    Title = title.Trim(),
                    Date = date
                });
                continue;
            }

            var timed = !isAllDay
                && time is { } clock
                && clock >= TimeSpan.Zero
                && clock < TimeSpan.FromDays(1);
            list.Add(item with
            {
                Title = title.Trim(),
                Date = date,
                Time = timed ? new TimeSpan(time!.Value.Hours, time.Value.Minutes, 0) : null,
                IsAllDay = !timed
            });
        }

        if (!found)
        {
            return false;
        }

        Settings = ScheduleSettings.Normalize(Settings with { Items = list });
        next = Settings;
        return true;
    }
}
