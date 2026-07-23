using CudaSpirit.Core.Models;

namespace CudaSpirit.Core.Services.Bosses;

/// <summary>One recurring slot in the weekly boss timetable.</summary>
public sealed record BossSlot(string Name, BossKind Kind, DayOfWeek Day, int Hour, int Minute, string Notes = "");

/// <summary>
/// Computes upcoming world/field boss spawns from a weekly timetable. Times are stored in the
/// account's local time zone. The default table approximates the NA/EU world-boss rotation - edit
/// <see cref="DefaultSchedule"/> (or load your own) to match your server exactly.
/// </summary>
public sealed class BossScheduleService
{
    private readonly IReadOnlyList<BossSlot> _slots;

    public BossScheduleService(IReadOnlyList<BossSlot>? slots = null)
        => _slots = slots ?? DefaultSchedule;

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

    private static DateTimeOffset NextOccurrence(BossSlot slot, DateTimeOffset now)
    {
        // Start from today at the slot time, then walk forward to the right weekday.
        var candidate = new DateTimeOffset(now.Year, now.Month, now.Day, slot.Hour, slot.Minute, 0, now.Offset);
        int daysAhead = ((int)slot.Day - (int)candidate.DayOfWeek + 7) % 7;
        candidate = candidate.AddDays(daysAhead);

        // If it's today but already passed (allowing a 15-min "live" grace), roll a week.
        if (candidate <= now.AddMinutes(-15))
            candidate = candidate.AddDays(7);

        return candidate;
    }

    /// <summary>
    /// Approximate NA/EU world-boss timetable. Replace with your server's exact schedule.
    /// (World bosses share a rotating pool; treat these as representative slots.)
    /// </summary>
    public static readonly IReadOnlyList<BossSlot> DefaultSchedule = new List<BossSlot>
    {
        new("Kzarka",   BossKind.World, DayOfWeek.Monday,    2,  0, "Ferrid / Serendia"),
        new("Kutum",    BossKind.World, DayOfWeek.Monday,   15,  0, "Scarlet Sand Chamber"),
        new("Karanda",  BossKind.World, DayOfWeek.Tuesday,   0, 15, "Northern wheat plantation"),
        new("Nouver",   BossKind.World, DayOfWeek.Tuesday,  16,  0, "Great desert"),
        new("Offin",    BossKind.World, DayOfWeek.Wednesday, 2,  0, "Kamasylvia"),
        new("Vell",     BossKind.World, DayOfWeek.Sunday,   16,  0, "Open sea - weekly"),
        new("Garmoth",  BossKind.World, DayOfWeek.Saturday, 20,  0, "Drieghan - heart drops"),
        new("Quint",    BossKind.Field, DayOfWeek.Thursday, 12,  0, "Quint Hill"),
        new("Muraka",   BossKind.Field, DayOfWeek.Friday,   12,  0, "Balenos"),
    };
}
