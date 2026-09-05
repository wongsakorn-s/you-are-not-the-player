using Game.Sim.Entities;
using Game.Sim.Player;

namespace Game.Client.Godot.Presentation;

/// <summary>
/// Says how exposed the host is in the words a person in the hotel would use.
/// </summary>
/// <remarks>
/// Deliberately never prints a score or a dimension name. The player-facing
/// information pass took raw confidence and suspicion vectors out of the UI, and
/// exposure has to follow the same rule: the player should read the room, not a
/// meter.
/// </remarks>
public static class ExposureFormatter
{
    public static string FormatBadge(ExposureReport report, bool useThai)
    {
        ArgumentNullException.ThrowIfNull(report);
        return FormatLevel(report.Level, useThai);
    }

    /// <summary>The name of a tier, for places that hold a level and not a report.</summary>
    public static string FormatLevel(ExposureLevel level, bool useThai)
    {
        return level switch
        {
            ExposureLevel.Cornered => useThai ? "จนมุม" : "CORNERED",
            ExposureLevel.Watched => useThai ? "ถูกจับตา" : "WATCHED",
            ExposureLevel.Noticed => useThai ? "มีคนสังเกต" : "NOTICED",
            _ => useThai ? "ยังไม่มีใครสนใจ" : "UNNOTICED",
        };
    }

    /// <summary>One line for the HUD: who, and what they have.</summary>
    public static string FormatSummary(
        ExposureReport report,
        Func<EntityId, string> displayName,
        bool useThai)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(displayName);

        if (report.Level == ExposureLevel.Unnoticed || report.MostSuspicious is null)
        {
            return useThai
                ? "ยังไม่มีใครจับตาคุณเป็นพิเศษ"
                : "Nobody is paying you special attention.";
        }

        string who = displayName(report.MostSuspicious.Observer);
        string reason = report.LeadingReason is null
            ? string.Empty
            : DescribeRule(report.LeadingReason.RuleId, useThai);
        string others = report.Spread <= 1
            ? string.Empty
            : useThai
                ? $" และอีก {report.Spread - 1} คนเริ่มสังเกตเช่นกัน"
                : $", and {report.Spread - 1} other(s) have started noticing";

        string core = report.Level switch
        {
            ExposureLevel.Cornered => useThai
                ? $"{who} มั่นใจพอที่จะพูดออกมาแล้วว่าคุณผิดปกติ"
                : $"{who} is sure enough to say out loud that something is wrong with you",
            ExposureLevel.Watched => useThai
                ? $"{who} กำลังจับตาคุณอยู่"
                : $"{who} is keeping track of you",
            _ => useThai
                ? $"{who} จำบางอย่างเกี่ยวกับคุณไว้"
                : $"{who} has filed something away about you",
        };

        return string.IsNullOrEmpty(reason)
            ? $"{core}{others}."
            : useThai
                ? $"{core} — {reason}{others}"
                : $"{core} — {reason}{others}.";
    }

    /// <summary>The full list, for the case file's "how do I look" page.</summary>
    public static string FormatDetail(
        ExposureReport report,
        Func<EntityId, string> displayName,
        bool useThai)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(displayName);

        if (report.Observers.Count == 0)
        {
            return useThai
                ? "คืนนี้คุณยังเดินผ่านโรงแรมโดยไม่มีใครเก็บอะไรเกี่ยวกับคุณไว้เลย\n\n" +
                  "จำไว้ว่าทุกอย่างที่คุณทำเพื่อสืบ ก็เป็นสิ่งที่คนอื่นมองเห็นได้เหมือนกัน"
                : "So far you have moved through the hotel without anyone keeping a note on you.\n\n" +
                  "Remember that everything you do to investigate is also something other people can see.";
        }

        var lines = new List<string>();
        foreach (ObserverExposure observer in report.Observers)
        {
            string[] why = report.Reasons
                .Where(reason => reason.Observer == observer.Observer)
                .Select(reason => DescribeRule(reason.RuleId, useThai))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            string stance = observer.Score >= ExposureReport.CorneredThreshold
                ? useThai ? "พร้อมจะพูดออกมา" : "ready to say it out loud"
                : observer.Score >= ExposureReport.WatchedThreshold
                ? useThai ? "กำลังจับตา" : "keeping track"
                : useThai ? "จำเอาไว้เฉย ๆ" : "just filing it away";

            lines.Add($"{displayName(observer.Observer)}  •  {stance}");
            lines.AddRange(why.Select(item => $"    - {item}"));
        }

        string footer = report.PlayerLikePeak >= ExposureReport.WatchedThreshold
            ? useThai
                ? "\nสิ่งที่พวกเขาเห็นไม่ใช่แค่ \u201cน่าสงสัย\u201d แต่เป็น \u201cไม่เหมือนคนที่อยู่ที่นี่จริง\u201d"
                : "\nWhat they have on you does not read as \u201csuspicious\u201d. It reads as \u201cnot someone who lives here\u201d."
            : string.Empty;

        return string.Join('\n', lines) + footer;
    }

    private static string DescribeRule(string ruleId, bool useThai) => ruleId switch
    {
        "restricted_area_entry" => useThai
            ? "มีคนเห็นคุณเข้าไปในพื้นที่ที่คุณไม่ควรเข้า"
            : "you were seen entering somewhere you have no reason to be",
        "detected_loot_sweep" => useThai
            ? "คุณรื้อค้นหลายจุดรวดเดียวจนดูไม่เหมือนคนทำงาน"
            : "you searched too many places too quickly to look like staff",
        "detected_boundary_testing" => useThai
            ? "คุณไล่ลองเปิดประตูราวกับกำลังหาขอบของบางอย่าง"
            : "you tried door after door, as if looking for the edge of something",
        "detected_repeat_interaction" => useThai
            ? "คุณทำสิ่งเดิมซ้ำ ๆ โดยไม่มีเหตุผล"
            : "you repeated the same action with no reason to",
        "detected_role_neglect" => useThai
            ? "คุณหายไปจากหน้าที่ของตัวเองนานเกินไป"
            : "you were away from your own duties for too long",
        "witnessed_suspicious_tampering" => useThai
            ? "มีคนเห็นคุณยุ่งกับของที่ไม่ใช่ของคุณ"
            : "you were seen handling something that is not yours",
        "witnessed_theft" => useThai
            ? "มีคนเห็นคุณหยิบของบางอย่างไป"
            : "you were seen taking something",
        "witnessed_night_activity" => useThai
            ? "คุณเคลื่อนไหวในเวลาที่คนอื่นควรจะนอน"
            : "you were moving at an hour when everyone else should be asleep",
        "witnessed_secret_meeting" => useThai
            ? "มีคนเห็นคุณนัดเจอใครบางคนเงียบ ๆ"
            : "you were seen meeting someone quietly",
        "detected_save_reload_anomaly" or "detected_the_blink_anomaly" => useThai
            ? "มีคนเห็นบางอย่างเกี่ยวกับคุณที่ไม่ควรเป็นไปได้"
            : "someone saw something about you that should not be possible",
        _ => useThai
            ? "มีบางอย่างเกี่ยวกับคุณที่พวกเขาอธิบายไม่ได้"
            : "there is something about you they cannot explain",
    };
}
