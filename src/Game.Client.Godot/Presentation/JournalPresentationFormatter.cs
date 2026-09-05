using System.Text;
using Game.Sim.Entities;
using Game.Sim.Events;
using Game.Sim.Locations;
using Game.Sim.Memory;
using Game.Sim.Player;
using Game.Sim.Suspicion;

namespace Game.Client.Godot.Presentation;

public sealed record TimelineFilter(
    EntityId? Subject = null,
    LocationId? Location = null,
    MemoryKind? Kind = null,
    EventType? EventType = null,
    long? MinimumTick = null)
{
    public bool IsEmpty =>
        Subject is null &&
        Location is null &&
        Kind is null &&
        EventType is null &&
        MinimumTick is null;

    public string Description(
        Func<EntityId, string> displayEntity,
        Func<LocationId, string> displayLocation,
        bool useThai = false)
    {
        var parts = new List<string>();
        if (Subject is { } subject)
        {
            parts.Add(useThai
                ? $"เรื่องของ {displayEntity(subject)}"
                : $"about {displayEntity(subject)}");
        }

        if (Location is { } location)
        {
            parts.Add(useThai
                ? $"ใน {displayLocation(location)}"
                : $"inside {displayLocation(location)}");
        }

        if (Kind is { } kind)
        {
            parts.Add(kind == MemoryKind.Episodic
                ? useThai ? "สิ่งที่จอร์จเห็นเอง" : "what George saw"
                : useThai ? "สิ่งที่คนอื่นเล่า" : "what others said");
        }

        if (EventType is not null)
        {
            parts.Add(useThai ? "เหตุการณ์ประเภทเดียวกัน" : "the same kind of event");
        }

        if (MinimumTick is { } minimumTick)
        {
            parts.Add(useThai
                ? $"ตั้งแต่เวลา {JournalPresentationFormatter.FormatClock(minimumTick)}"
                : $"since {JournalPresentationFormatter.FormatClock(minimumTick)}");
        }

        return parts.Count == 0
            ? useThai ? "ทุกเบาะแส" : "all clues"
            : string.Join(" • ", parts);
    }
}

public sealed record JournalPage(
    string Text,
    int PageNumber,
    int PageCount,
    int TotalClues);

public static class JournalPresentationFormatter
{
    public static JournalPage FormatPage(
        PlayerJournal journal,
        Func<EntityId, string> displayEntity,
        Func<LocationId, string> displayLocation,
        TimelineFilter? filter = null,
        int pageIndex = 0,
        int pageSize = 3,
        bool useThai = false)
    {
        ArgumentNullException.ThrowIfNull(journal);
        ArgumentNullException.ThrowIfNull(displayEntity);
        ArgumentNullException.ThrowIfNull(displayLocation);

        PlayerJournalEntry[] entries = (filter is null
                ? journal.Entries
                : journal.Entries.Where(entry => Matches(entry, filter)))
            .OrderByDescending(entry => entry.EventTime.Tick)
            .ToArray();
        int safePageSize = Math.Max(1, pageSize);
        int pageCount = Math.Max(1, (int)Math.Ceiling(entries.Length / (double)safePageSize));
        int safePageIndex = Math.Clamp(pageIndex, 0, pageCount - 1);
        PlayerJournalEntry[] pageEntries = entries
            .Skip(safePageIndex * safePageSize)
            .Take(safePageSize)
            .ToArray();

        var text = new StringBuilder();
        _ = text.AppendLine(useThai
            ? $"ขณะนี้อยู่ที่ {displayLocation(journal.CurrentLocation)}  •  เวลา {FormatClock(journal.CurrentTime.Tick)}"
            : $"Currently at {displayLocation(journal.CurrentLocation)}  •  {FormatClock(journal.CurrentTime.Tick)}");
        // "Clues 0-0 of 0" is a worse way of saying the file is empty than saying
        // nothing at all, and an empty file is now normal at the start of a shift.
        if (entries.Length > 0)
        {
            _ = text.AppendLine(useThai
                ? $"เบาะแส {(safePageIndex * safePageSize) + 1}-{Math.Min(entries.Length, (safePageIndex + 1) * safePageSize)} จาก {entries.Length}"
                : $"Clues {(safePageIndex * safePageSize) + 1}-{Math.Min(entries.Length, (safePageIndex + 1) * safePageSize)} of {entries.Length}");
        }
        if (filter is not null && !filter.IsEmpty)
        {
            _ = text.AppendLine(useThai
                ? $"มุมมอง: {filter.Description(displayEntity, displayLocation, useThai: true)}"
                : $"View: {filter.Description(displayEntity, displayLocation)}");
        }

        _ = text.AppendLine();
        if (pageEntries.Length == 0)
        {
            _ = text.AppendLine(useThai
                ? "ยังไม่มีเบาะแสในมุมมองนี้ ลองคุยกับคนหรือสำรวจวัตถุเพิ่ม"
                : "There are no clues in this view yet. Talk to someone or inspect an object.");
        }
        else
        {
            foreach (PlayerJournalEntry entry in pageEntries)
            {
                _ = text.AppendLine($"[{FormatClock(entry.EventTime.Tick)}] " +
                    FormatHeadline(entry, displayEntity, displayLocation, useThai));
                _ = text.AppendLine(useThai
                    ? $"ที่มา: {FormatSource(entry, displayEntity, useThai)} • {ReliabilityLabel(entry.Confidence, useThai)}"
                    : $"Source: {FormatSource(entry, displayEntity, useThai)} • {ReliabilityLabel(entry.Confidence, useThai)}");
                _ = text.AppendLine();
            }
        }

        return new JournalPage(text.ToString().TrimEnd(), safePageIndex + 1, pageCount, entries.Length);
    }

    public static string FormatPeopleToWatch(
        PlayerJournal journal,
        Func<EntityId, string> displayEntity,
        bool useThai = false)
    {
        ArgumentNullException.ThrowIfNull(journal);
        ArgumentNullException.ThrowIfNull(displayEntity);

        if (journal.SuspicionSnapshots.Count == 0)
        {
            return useThai
                ? "ยังไม่มีใครมีพฤติกรรมผิดปกติชัดเจน\n\nเดินสำรวจ คุย และเปรียบเทียบคำให้การก่อน"
                : "No one stands out yet.\n\nExplore, talk, and compare what people say first.";
        }

        var text = new StringBuilder();
        _ = text.AppendLine(useThai ? "สามคนที่ควรจับตาตอนนี้" : "Three people worth watching now");
        _ = text.AppendLine();
        foreach (SuspicionSnapshot snapshot in journal.SuspicionSnapshots
                     .OrderByDescending(GetTotalSuspicion)
                     .Take(3))
        {
            float total = GetTotalSuspicion(snapshot);
            _ = text.AppendLine(string.Concat(
                displayEntity(snapshot.Subject),
                " — ",
                ConcernLabel(total, useThai)));
            string reasons = SuspicionReasons(snapshot, useThai);
            _ = text.AppendLine(useThai
                ? $"เพราะ: {(string.IsNullOrEmpty(reasons) ? "ยังไม่มีเหตุผลที่ชัดเจน" : reasons)}"
                : $"Why: {(string.IsNullOrEmpty(reasons) ? "no clear reason yet" : reasons)}");
            _ = text.AppendLine();
        }

        return text.ToString().TrimEnd();
    }

    public static string Format(
        PlayerJournal journal,
        Func<EntityId, string> displayEntity,
        Func<LocationId, string> displayLocation,
        TimelineFilter? filter = null,
        bool useThai = false)
    {
        ArgumentNullException.ThrowIfNull(journal);
        ArgumentNullException.ThrowIfNull(displayEntity);
        ArgumentNullException.ThrowIfNull(displayLocation);

        var text = new StringBuilder();
        _ = text.AppendLine(useThai
            ? $"ตอนนี้อยู่ที่ {displayLocation(journal.CurrentLocation)}  •  เวลา {FormatClock(journal.CurrentTime.Tick)}"
            : $"Now at {displayLocation(journal.CurrentLocation)}  •  {FormatClock(journal.CurrentTime.Tick)}");
        if (filter is not null && !filter.IsEmpty)
        {
            _ = text.AppendLine(useThai
                ? $"กำลังแสดง: {filter.Description(displayEntity, displayLocation, useThai: true)}"
                : $"Showing: {filter.Description(displayEntity, displayLocation)}");
        }

        _ = text.AppendLine();
        _ = text.AppendLine(useThai ? "เบาะแสที่จอร์จจำได้" : "CLUES GEORGE REMEMBERS");

        IEnumerable<PlayerJournalEntry> entries = journal.Entries;
        if (filter is not null)
        {
            entries = entries.Where(entry => Matches(entry, filter));
        }

        PlayerJournalEntry[] filteredEntries = entries.ToArray();
        if (filteredEntries.Length == 0)
        {
            _ = text.AppendLine(useThai
                ? filter is null || filter.IsEmpty
                    ? "ยังไม่มีเบาะแส ลองคุยกับคนหรือตรวจวัตถุในโรงแรม"
                    : "ไม่มีเบาะแสตรงกับสิ่งที่เลือก"
                : filter is null || filter.IsEmpty
                    ? "No clues yet. Talk to someone or inspect an object."
                    : "No clues match this view.");
        }
        else
        {
            foreach (PlayerJournalEntry entry in filteredEntries)
            {
                _ = text.AppendLine($"• [{FormatClock(entry.EventTime.Tick)}] " +
                    FormatHeadline(entry, displayEntity, displayLocation, useThai));
                _ = text.AppendLine($"  {FormatSource(entry, displayEntity, useThai)}  •  " +
                    ReliabilityLabel(entry.Confidence, useThai));
            }
        }

        _ = text.AppendLine();
        _ = text.AppendLine(useThai ? "คนที่ควรจับตา" : "PEOPLE TO WATCH");
        if (journal.SuspicionSnapshots.Count == 0)
        {
            _ = text.AppendLine(useThai
                ? "ยังไม่มีพฤติกรรมผิดปกติชัดเจน"
                : "No clearly suspicious behavior yet.");
        }
        else
        {
            foreach (SuspicionSnapshot suspicion in journal.SuspicionSnapshots
                         .OrderByDescending(GetTotalSuspicion))
            {
                _ = text.AppendLine($"• {displayEntity(suspicion.Subject)} — " +
                    ConcernLabel(GetTotalSuspicion(suspicion), useThai));
                string reasons = SuspicionReasons(suspicion, useThai);
                if (!string.IsNullOrEmpty(reasons))
                {
                    _ = text.AppendLine(useThai ? $"  เพราะ: {reasons}" : $"  Why: {reasons}");
                }
            }
        }

        return text.ToString().TrimEnd();
    }

    public static string FormatHeadline(
        PlayerJournalEntry entry,
        Func<EntityId, string> displayEntity,
        Func<LocationId, string> displayLocation,
        bool useThai = false)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(displayEntity);
        ArgumentNullException.ThrowIfNull(displayLocation);

        string subject = entry.Subject is { } actor
            ? displayEntity(actor)
            : useThai ? "ใครบางคน" : "Someone";
        string location = entry.Location is { } room
            ? displayLocation(room)
            : useThai ? "บริเวณที่ไม่ทราบ" : "an unknown area";
        return entry.EventType switch
        {
            EventType.EnterLocation => useThai
                ? $"{subject} เข้าไปใน{location}"
                : $"{subject} entered {location}",
            EventType.LeaveLocation => useThai
                ? $"{subject} ออกจาก{location}"
                : $"{subject} left {location}",
            EventType.SecretMeeting => useThai
                ? $"{subject} เกี่ยวข้องกับการนัดลับที่{location}"
                : $"{subject} was linked to a secret meeting in {location}",
            EventType.Theft => useThai
                ? $"{subject} เกี่ยวข้องกับของที่หายไปใน{location}"
                : $"{subject} was linked to something missing in {location}",
            EventType.RoleDutyMissed => useThai
                ? $"{subject} ไม่ได้ทำหน้าที่ตามปกติที่{location}"
                : $"{subject} abandoned their normal duty in {location}",
            EventType.RealityAnomaly => useThai
                ? $"การเคลื่อนไหวของ {subject} ดูเป็นไปไม่ได้ที่{location}"
                : $"{subject} appeared to move impossibly in {location}",
            EventType.Interaction => useThai
                ? $"{subject} แตะหรือตรวจบางอย่างใน{location}"
                : $"{subject} handled something in {location}",
            _ => useThai
                ? $"พบพฤติกรรมน่าสนใจของ {subject} ที่{location}"
                : $"Something notable involved {subject} in {location}",
        };
    }

    public static string FormatClock(long tick)
    {
        int minuteOfDay = (int)(((23L * 60L) + Math.Max(0L, tick)) % (24L * 60L));
        return $"{minuteOfDay / 60:00}:{minuteOfDay % 60:00}";
    }

    private static string FormatSource(
        PlayerJournalEntry entry,
        Func<EntityId, string> displayEntity,
        bool useThai) =>
        entry.InformationSource is { } source
            ? useThai
                ? $"ได้ยินจาก {displayEntity(source)}"
                : $"Heard from {displayEntity(source)}"
            : useThai
                ? "จอร์จเห็นด้วยตัวเอง"
                : "George saw this himself";

    private static string ReliabilityLabel(float confidence, bool useThai) => confidence switch
    {
        >= 0.9f => useThai ? "น่าเชื่อถือมาก" : "Very reliable",
        >= 0.7f => useThai ? "ค่อนข้างน่าเชื่อถือ" : "Likely reliable",
        >= 0.45f => useThai ? "ยังไม่แน่ใจ" : "Uncertain",
        _ => useThai ? "เป็นเพียงเบาะแสอ่อน ๆ" : "Weak lead",
    };

    private static string ConcernLabel(float suspicion, bool useThai) => suspicion switch
    {
        >= 20.0f => useThai ? "น่าสงสัยมาก" : "Highly suspicious",
        >= 10.0f => useThai ? "ควรจับตา" : "Worth watching",
        >= 5.0f => useThai ? "มีบางอย่างผิดปกติ" : "Something feels off",
        _ => useThai ? "มีข้อสงสัยเล็กน้อย" : "Slight concern",
    };

    private static string SuspicionReasons(SuspicionSnapshot snapshot, bool useThai)
    {
        var reasons = new List<(float Score, string Text)>
        {
            (snapshot.Vector.Secrecy, useThai ? "ปกปิดข้อมูล" : "hiding information"),
            (snapshot.Vector.RoleDeviation, useThai ? "ไม่ทำตามหน้าที่" : "acting outside their role"),
            (snapshot.Vector.Deception, useThai ? "คำพูดไม่น่าไว้ใจ" : "untrustworthy statements"),
            // What was seen, not what it means. "Acting aware of being controlled"
            // is the answer to the question the player is being asked, printed on
            // the page that is supposed to be their working notes.
            (snapshot.Vector.ImpossibleBehavior, useThai ? "อยู่ในที่ที่ไปไม่ถึง" : "was somewhere they could not have reached"),
            (snapshot.Vector.MetaBehavior, useThai ? "ตอบสนองต่อสิ่งที่ไม่มีใครพูด" : "answered something nobody said"),
            (snapshot.Vector.Criminality, useThai ? "เกี่ยวข้องกับการกระทำผิด" : "possible wrongdoing"),
        };
        return string.Join(
            ", ",
            reasons
                .Where(reason => reason.Score > 0.0f)
                .OrderByDescending(reason => reason.Score)
                .Take(2)
                .Select(reason => reason.Text));
    }

    private static bool Matches(PlayerJournalEntry entry, TimelineFilter filter) =>
        (filter.Subject is null || entry.Subject == filter.Subject) &&
        (filter.Location is null || entry.Location == filter.Location) &&
        (filter.Kind is null || entry.Kind == filter.Kind) &&
        (filter.EventType is null || entry.EventType == filter.EventType) &&
        (filter.MinimumTick is null || entry.EventTime.Tick >= filter.MinimumTick);

    private static float GetTotalSuspicion(SuspicionSnapshot snapshot) =>
        snapshot.Vector.Criminality +
        snapshot.Vector.Secrecy +
        snapshot.Vector.RoleDeviation +
        snapshot.Vector.MetaBehavior +
        snapshot.Vector.ImpossibleBehavior +
        snapshot.Vector.Deception;
}
