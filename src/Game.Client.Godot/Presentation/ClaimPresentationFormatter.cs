using Game.Sim.Entities;
using Game.Sim.Locations;
using Game.Sim.Player;

namespace Game.Client.Godot.Presentation;

/// <summary>
/// Renders what people said, and which of those statements the player can prove
/// wrong.
/// </summary>
/// <remarks>
/// A claim is never labelled true or false here. The page shows the statement and
/// whether the player holds a clue that disagrees with it - working out whether
/// that clue is good enough is the decision being asked of them.
/// </remarks>
public static class ClaimPresentationFormatter
{
    public static string FormatClaims(
        IReadOnlyList<AlibiClaim> claims,
        IReadOnlyList<Contradiction> contradictions,
        Func<EntityId, string> displayName,
        Func<LocationId, string> displayLocation,
        bool useThai)
    {
        ArgumentNullException.ThrowIfNull(claims);
        ArgumentNullException.ThrowIfNull(contradictions);
        ArgumentNullException.ThrowIfNull(displayName);
        ArgumentNullException.ThrowIfNull(displayLocation);

        if (claims.Count == 0)
        {
            return useThai
                ? "ยังไม่มีใครบอกคุณว่าตัวเองอยู่ที่ไหน\n\n" +
                  "ถามเรื่องตารางงานของแต่ละคน แล้วคำตอบจะถูกบันทึกไว้ที่นี่เพื่อเทียบกับสิ่งที่คุณเห็นเอง"
                : "Nobody has told you where they were yet.\n\n" +
                  "Ask people about their shift and their answers land here, " +
                  "ready to be compared against what you saw yourself.";
        }

        var lines = new List<string>();
        foreach (AlibiClaim claim in claims.OrderByDescending(claim => claim.StatedAt.Tick))
        {
            Contradiction[] against = contradictions
                .Where(item => item.Claim.Id == claim.Id)
                .ToArray();

            lines.Add(
                $"[{JournalPresentationFormatter.FormatClock(claim.ClaimedTime.Tick)}] " +
                (useThai
                    ? $"{displayName(claim.Speaker)} บอกว่าอยู่ที่{displayLocation(claim.ClaimedLocation)}"
                    : $"{displayName(claim.Speaker)} said they were at {displayLocation(claim.ClaimedLocation)}"));

            if (against.Length == 0)
            {
                lines.Add(useThai
                    ? "    ยังไม่มีอะไรที่ขัดกับคำพูดนี้"
                    : "    Nothing you have contradicts this");
                continue;
            }

            Contradiction best = against[0];
            lines.Add(useThai
                ? $"    ขัดกับ: คุณมีเบาะแสว่าเขาอยู่ที่{displayLocation(best.Evidence.Location!.Value)}"
                : $"    Conflicts with: you have a clue placing them at {displayLocation(best.Evidence.Location!.Value)}");
            lines.Add(best.EvidenceIsFirstHand
                ? useThai
                    ? "    เบาะแสนี้จอร์จเห็นเอง"
                    : "    You saw this yourself"
                : useThai
                    ? "    เบาะแสนี้ได้ยินต่อกันมา อาจคลาดเคลื่อนได้"
                    : "    This one is second-hand and may already be wrong");
        }

        return string.Join('\n', lines);
    }

    public static string DescribeChallengeRisk(bool evidenceIsFirstHand, bool useThai) =>
        evidenceIsFirstHand
            ? useThai
                ? "คุณเห็นด้วยตาตัวเอง"
                : "You saw it yourself"
            : useThai
                ? "คุณได้ยินมาจากคนอื่น เรื่องนี้อาจผิดตั้งแต่ต้นทาง"
                : "You heard this from someone else; it may have been wrong before it reached you";
}
