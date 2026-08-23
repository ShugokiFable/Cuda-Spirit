using CudaSpirit.Core.Models;

namespace CudaSpirit.Core.Services.Bosses;

/// <summary>One recurring slot in the weekly boss timetable.</summary>
public sealed record BossSlot(string Name, BossKind Kind, DayOfWeek Day, int Hour, int Minute, string Notes = "");

/// <summary>
/// Computes upcoming world/field boss spawns from a weekly timetable. Slot times are stored in the
/// server reference timezone (NA table = UTC-7 year-round, EU table = CET/UTC+1 year-round - the
/// game does not observe DST); <see cref="BossScheduleService"/> converts them to the account's
/// local time for display. The NA/EU PC rotation uses paired spawns (two bosses share a window)
/// and fixed daily Garmoth lines. Verify against the official wiki if spawns look off:
/// https://www.naeu.playblackdesert.com/Wiki?wikiNo=83 (also mirrored at garmoth.com/boss-timer).
/// </summary>
public sealed class BossScheduleService
{
    private readonly IReadOnlyList<BossSlot> _slots;
    private readonly TimeSpan _fromServerOffset;

    public BossScheduleService(IReadOnlyList<BossSlot>? slots = null, Region region = Region.NA)
    {
        _slots = slots ?? (region == Region.EU ? EuSchedule : NaSchedule);
        // ponytail: fixed offsets (NA=UTC-7, EU=UTC+1) - the game ignores DST; revisit if PA ever changes server time rules.
        _fromServerOffset = region == Region.EU ? TimeSpan.FromHours(1) : TimeSpan.FromHours(-7);
    }

    /// <summary>Return the next occurrence of every boss, ordered by soonest first.</summary>
    public IReadOnlyList<BossEvent> GetUpcoming(int max = 12, DateTimeOffset? nowOverride = null)
    {
        var now = nowOverride ?? DateTimeOffset.Now;
        var events = new List<BossEvent>();

        foreach (var slot in _slots)
        {
            var next = NextOccurrence(slot, now);
            events.Add(new BossEvent
            {
                Name = slot.Name,
                Kind = slot.Kind,
                NextSpawn = next,
                Notes = slot.Notes
            });
        }

        return events
            .OrderBy(e => e.NextSpawn)
            .Take(max)
            .ToList();
    }

    private DateTimeOffset NextOccurrence(BossSlot slot, DateTimeOffset now)
    {
        // Build the slot time in the server reference zone, then convert to local for display.
        var serverNow = now.ToOffset(_fromServerOffset);
        var candidate = new DateTimeOffset(serverNow.Year, serverNow.Month, serverNow.Day,
            slot.Hour, slot.Minute, 0, _fromServerOffset);
        int daysAhead = ((int)slot.Day - (int)candidate.DayOfWeek + 7) % 7;
        candidate = candidate.AddDays(daysAhead);

        // If it's today but already passed (allow a 15-min "live" grace), roll a week.
        if (candidate <= now.AddMinutes(-15))
            candidate = candidate.AddDays(7);

        return candidate.ToLocalTime();
    }

    // (hour, minute, "BossA|BossB") per day. Source: garmoth.com/boss-timer NA/EU tables, Aug 2026.
    private static readonly Dictionary<DayOfWeek, (int Hour, int Minute, string Bosses)[]> NaTable = new()
    {
        [DayOfWeek.Monday] = new[] { (0, 0, "Sangoon|Karanda"), (10, 0, "Sangoon|Nouver"), (12, 0, "Garmoth"), (14, 0, "Uturi|Kutum"), (17, 0, "Golden Pig King|Nouver"), (20, 15, "Bulgasal|Kzarka"), (21, 15, "Garmoth"), (22, 15, "Sangoon|Karanda") },
        [DayOfWeek.Tuesday] = new[] { (10, 0, "Bulgasal|Kutum"), (12, 0, "Garmoth"), (14, 0, "Golden Pig King|Nouver"), (17, 0, "Uturi|Kzarka"), (20, 15, "Quint|Muraka"), (21, 15, "Garmoth"), (22, 15, "Golden Pig King|Kzarka") },
        [DayOfWeek.Wednesday] = new[] { (10, 0, "Sangoon|Karanda"), (12, 0, "Garmoth"), (14, 0, "Bulgasal|Offin"), (17, 0, "Vell"), (20, 15, "Uturi|Nouver"), (21, 15, "Garmoth"), (22, 15, "Uturi|Nouver") },
        [DayOfWeek.Thursday] = new[] { (0, 0, "Golden Pig King|Kzarka"), (12, 0, "Garmoth"), (14, 0, "Sangoon|Karanda"), (17, 0, "Bulgasal|Kutum"), (20, 15, "Quint|Muraka"), (21, 15, "Garmoth"), (22, 15, "Golden Pig King|Karanda") },
        [DayOfWeek.Friday] = new[] { (0, 0, "Bulgasal|Nouver"), (10, 0, "Uturi|Kutum"), (12, 0, "Garmoth"), (14, 0, "Bulgasal|Kzarka"), (17, 0, "Sangoon|Offin"), (20, 15, "Golden Pig King|Kutum"), (21, 15, "Garmoth"), (22, 15, "Bulgasal|Kzarka") },
        [DayOfWeek.Saturday] = new[] { (0, 0, "Uturi|Offin"), (10, 0, "Black Shadow"), (12, 0, "Garmoth"), (14, 0, "Black Shadow"), (17, 0, "Sangoon|Karanda"), (22, 15, "Bulgasal|Nouver") },
        [DayOfWeek.Sunday] = new[] { (0, 0, "Golden Pig King|Kutum"), (10, 0, "Uturi|Kzarka"), (12, 0, "Garmoth"), (14, 0, "Vell"), (17, 0, "Sangoon|Karanda"), (17, 15, "Garmoth"), (20, 15, "Sangoon|Karanda"), (21, 15, "Garmoth"), (22, 15, "Uturi|Kutum") },
    };

    private static readonly Dictionary<DayOfWeek, (int Hour, int Minute, string Bosses)[]> EuTable = new()
    {
        [DayOfWeek.Monday] = new[] { (6, 0, "Sangoon|Nouver"), (8, 0, "Garmoth"), (10, 0, "Uturi|Kutum"), (13, 0, "Golden Pig King|Nouver"), (16, 15, "Bulgasal|Kzarka"), (17, 15, "Garmoth"), (18, 15, "Sangoon|Karanda"), (20, 0, "Uturi|Offin") },
        [DayOfWeek.Tuesday] = new[] { (6, 0, "Bulgasal|Kutum"), (8, 0, "Garmoth"), (10, 0, "Golden Pig King|Nouver"), (13, 0, "Uturi|Kzarka"), (16, 15, "Quint|Muraka"), (17, 15, "Garmoth"), (18, 15, "Golden Pig King|Kzarka"), (20, 0, "Golden Pig King|Kzarka") },
        [DayOfWeek.Wednesday] = new[] { (6, 0, "Sangoon|Karanda"), (8, 0, "Garmoth"), (10, 0, "Bulgasal|Offin"), (13, 0, "Vell"), (16, 15, "Uturi|Nouver"), (17, 15, "Garmoth"), (18, 15, "Uturi|Nouver"), (20, 0, "Golden Pig King|Kzarka") },
        [DayOfWeek.Thursday] = new[] { (8, 0, "Garmoth"), (10, 0, "Sangoon|Karanda"), (13, 0, "Bulgasal|Kutum"), (16, 15, "Quint|Muraka"), (17, 15, "Garmoth"), (18, 15, "Golden Pig King|Karanda"), (20, 0, "Bulgasal|Nouver") },
        [DayOfWeek.Friday] = new[] { (6, 0, "Uturi|Kutum"), (8, 0, "Garmoth"), (10, 0, "Bulgasal|Kzarka"), (13, 0, "Sangoon|Offin"), (16, 15, "Golden Pig King|Kutum"), (17, 15, "Garmoth"), (18, 15, "Bulgasal|Kzarka"), (20, 0, "Uturi|Offin") },
        [DayOfWeek.Saturday] = new[] { (6, 0, "Golden Pig King|Nouver"), (8, 0, "Garmoth"), (10, 0, "Black Shadow"), (13, 0, "Sangoon|Karanda"), (18, 15, "Bulgasal|Nouver"), (20, 0, "Golden Pig King|Kutum") },
        [DayOfWeek.Sunday] = new[] { (6, 0, "Uturi|Kzarka"), (8, 0, "Garmoth"), (10, 0, "Vell"), (13, 0, "Sangoon|Karanda"), (13, 15, "Garmoth"), (16, 15, "Sangoon|Karanda"), (17, 15, "Garmoth"), (18, 15, "Uturi|Kutum"), (20, 0, "Sangoon|Karanda") },
    };

    /// <summary>NA PC world-boss rotation (server time UTC-7, no DST).</summary>
    public static readonly IReadOnlyList<BossSlot> NaSchedule = BuildSchedule(NaTable);

    /// <summary>EU PC world-boss rotation (server time CET/UTC+1, no DST).</summary>
    public static readonly IReadOnlyList<BossSlot> EuSchedule = BuildSchedule(EuTable);

    private static IReadOnlyList<BossSlot> BuildSchedule(Dictionary<DayOfWeek, (int Hour, int Minute, string Bosses)[]> table)
    {
        var slots = new List<BossSlot>();
        foreach (var (day, entries) in table)
        {
            foreach (var (hour, minute, bosses) in entries)
            {
                var names = bosses.Split('|');
                var kind = names.Any(n => n is "Quint" or "Muraka") ? BossKind.Field : BossKind.World;
                foreach (var name in names)
                {
                    var note = names.Length > 1
                        ? $"Shares the {hour:00}:{minute:00} window with {string.Join(" & ", names.Where(n => n != name))}"
                        : "";
                    slots.Add(new BossSlot(name, kind, day, hour, minute, note));
                }
            }
        }
        return slots;
    }
}
