using Game.Sim.Random;

namespace Game.Client.Godot.Gameplay;

public enum ShiftBeatKind
{
    Routine,
    PowerFlicker,
    AnonymousCall,
    MissingMasterKey,
    ImpossibleFootsteps,
    FinalWarning,
}

/// <summary>
/// One thing the night puts in front of the player.
/// </summary>
/// <remarks>
/// Text carries {actor} and {room} rather than names: the director does not know
/// what the cast is called, which language is being read, or what the player has
/// named themselves. The client fills those in from the catalogue.
/// </remarks>
public sealed record ShiftBeat(
    long Tick,
    ShiftBeatKind Kind,
    string EnglishText,
    string ThaiText,
    string? ActorId = null,
    string? DestinationId = null);

/// <summary>
/// Paces the shift: what the hotel does while the player is investigating.
/// </summary>
/// <remarks>
/// This used to be ten hand-written beats in a fixed order at fixed minutes,
/// with one room chosen by the seed. Every one of fifteen played nights ran the
/// same beats in the same order with the same forty-three minute silence in the
/// middle, which is a script rather than a shift. The seed now picks which beats
/// happen, who they involve, where, and when, so a second playthrough is a
/// different night rather than the same night with a different culprit.
/// </remarks>
public sealed class NightShiftDirector
{
    public const int DeadlineTick = 360;

    /// <summary>Nothing before this: the player needs a moment to arrive.</summary>
    private const long OpeningQuiet = 18;

    /// <summary>Dawn warning lands here, and nothing is scheduled after it.</summary>
    private const long FinalWarningTick = DeadlineTick - 30;

    private static readonly string[] Cast = ["anna", "bob", "charlie", "dana", "evelyn"];

    private static readonly string[] Rooms =
        ["lobby", "hallway", "kitchen", "room-201", "garden", "office"];

    private static readonly (ShiftBeatKind Kind, string English, string Thai)[] Atmosphere =
    [
        (ShiftBeatKind.PowerFlicker,
            "The lights dip. When they come back the CCTV clock is a minute behind.",
            "ไฟหรี่ลงวูบหนึ่ง พอกลับมา นาฬิกากล้องวงจรปิดช้าไปหนึ่งนาที"),
        (ShiftBeatKind.PowerFlicker,
            "Every light on this floor goes out together, then comes back one by one.",
            "ไฟทั้งชั้นดับพร้อมกัน แล้วค่อยติดกลับมาทีละดวง"),
        (ShiftBeatKind.AnonymousCall,
            "The desk phone rings once. Nobody answers on the other end, but somebody is breathing.",
            "โทรศัพท์ที่เคาน์เตอร์ดังครั้งเดียว ปลายสายไม่มีใครพูด แต่มีเสียงหายใจ"),
        (ShiftBeatKind.AnonymousCall,
            "A caller whispers that one of them is not acting alone, then hangs up.",
            "สายปริศนากระซิบว่าคนหนึ่งในนั้นไม่ได้ลงมือคนเดียว แล้ววางสาย"),
        (ShiftBeatKind.AnonymousCall,
            "An outside line asks for a guest who checked out four years ago.",
            "สายจากข้างนอกขอสายแขกที่เช็กเอาต์ไปเมื่อสี่ปีก่อน"),
        (ShiftBeatKind.MissingMasterKey,
            "The master-key hook is empty. It was not empty an hour ago.",
            "ตะขอกุญแจมาสเตอร์ว่างเปล่า เมื่อชั่วโมงก่อนมันยังไม่ว่าง"),
        (ShiftBeatKind.MissingMasterKey,
            "A key is back on its hook, still warm, with nobody near the board.",
            "กุญแจกลับมาอยู่บนตะขอแล้ว ยังอุ่นอยู่ ทั้งที่ไม่มีใครอยู่ใกล้แผงกุญแจ"),
        (ShiftBeatKind.MissingMasterKey,
            "The guest logbook has a line in it that you did not write.",
            "สมุดทะเบียนแขกมีบรรทัดที่คุณไม่ได้เขียน"),
        (ShiftBeatKind.ImpossibleFootsteps,
            "Footsteps come from the basement and from Room 201 at the same time.",
            "เสียงฝีเท้าดังจากชั้นใต้ดินและจากห้อง 201 พร้อมกัน"),
        (ShiftBeatKind.ImpossibleFootsteps,
            "The lift rises to a floor nobody called it to, and opens.",
            "ลิฟต์ขึ้นไปชั้นที่ไม่มีใครกด แล้วประตูก็เปิดออก"),
        (ShiftBeatKind.ImpossibleFootsteps,
            "A door closes down the corridor. The corridor is empty in both directions.",
            "ประตูปลายทางเดินปิดลง ทางเดินว่างเปล่าทั้งสองฝั่ง"),
        (ShiftBeatKind.ImpossibleFootsteps,
            "Your reflection in the lobby glass is half a second late.",
            "เงาของคุณในกระจกล็อบบี้ขยับช้ากว่าตัวจริงราวครึ่งวินาที"),
    ];

    private readonly ShiftBeat[] _beats;
    private int _nextBeatIndex;

    public NightShiftDirector(ulong seed)
    {
        var random = new Pcg32SimRandom(seed, sequence: 91UL);
        var beats = new List<ShiftBeat>();

        // Two or three people are seen heading somewhere. Enough motion that the
        // hotel looks staffed, few enough that the director is not steering a
        // cast that has its own night to get through.
        int errands = 2 + random.NextInt(0, 2);
        int oddities = 5 + random.NextInt(0, 3);
        int total = errands + oddities;

        foreach (string actor in Draw(random, Cast, errands))
        {
            string room = Rooms[random.NextInt(0, Rooms.Length)];
            (string english, string thai) = Errand(random);
            beats.Add(new ShiftBeat(
                Spread(random, beats.Count, total),
                ShiftBeatKind.Routine,
                english,
                thai,
                actor,
                room));
        }

        // Things that happen to the building rather than to a person, drawn from
        // a pool of twelve so that no two nights share a run of them.
        foreach ((ShiftBeatKind kind, string english, string thai) in
            Draw(random, Atmosphere, oddities))
        {
            beats.Add(new ShiftBeat(
                Spread(random, beats.Count, total),
                kind,
                english,
                thai));
        }

        beats.Sort((left, right) => left.Tick.CompareTo(right.Tick));
        beats.Add(new ShiftBeat(
            FinalWarningTick,
            ShiftBeatKind.FinalWarning,
            "Dawn is close. Whatever you are going to say, say it before the shift ends.",
            "ใกล้รุ่งเช้าแล้ว จะพูดอะไรก็ต้องพูดก่อนหมดกะ"));
        _beats = [.. beats];
    }

    /// <summary>Every beat this night will contain, for tests and reports.</summary>
    public IReadOnlyList<ShiftBeat> Beats => _beats;

    public IReadOnlyList<ShiftBeat> CollectDue(long currentTick)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(currentTick);

        var due = new List<ShiftBeat>();
        while (_nextBeatIndex < _beats.Length && _beats[_nextBeatIndex].Tick <= currentTick)
        {
            due.Add(_beats[_nextBeatIndex]);
            _nextBeatIndex++;
        }

        return due;
    }

    private static (string English, string Thai) Errand(Pcg32SimRandom random) =>
        random.NextInt(0, 4) switch
        {
            0 => ("{actor} heads for {room} without saying why.",
                "{actor} มุ่งหน้าไป{room}โดยไม่บอกว่าทำไม"),
            1 => ("{actor} is going to {room}, and takes the long way round.",
                "{actor} กำลังไป{room} และเลือกเดินอ้อม"),
            2 => ("{actor} says they will be in {room} if anyone needs them.",
                "{actor} บอกว่าจะอยู่ที่{room} ถ้าใครต้องการตัว"),
            _ => ("{actor} slips away toward {room}.",
                "{actor} เลี่ยงออกไปทาง{room}"),
        };

    /// <summary>
    /// Places the nth of count beats somewhere inside its own slice of the night.
    /// </summary>
    /// <remarks>
    /// Even slices keep the gaps roughly equal. The old fixed list left a
    /// forty-three minute stretch with nothing in it, in the same place, every
    /// night; the jitter inside a slice stops the pacing being audible.
    /// </remarks>
    private static long Spread(Pcg32SimRandom random, int index, int count)
    {
        long span = FinalWarningTick - OpeningQuiet;
        long slice = span / Math.Max(1, count);
        long start = OpeningQuiet + (slice * index);
        return Math.Min(FinalWarningTick - 6, start + random.NextInt(0, (int)Math.Max(1, slice)));
    }

    private static T[] Draw<T>(Pcg32SimRandom random, IReadOnlyList<T> pool, int count)
    {
        // Fisher-Yates over a copy, so a night never repeats a beat and the order
        // is part of what the seed decides.
        T[] shuffled = [.. pool];
        for (int index = shuffled.Length - 1; index > 0; index--)
        {
            int swap = random.NextInt(0, index + 1);
            (shuffled[index], shuffled[swap]) = (shuffled[swap], shuffled[index]);
        }

        return [.. shuffled.Take(Math.Min(count, shuffled.Length))];
    }
}
