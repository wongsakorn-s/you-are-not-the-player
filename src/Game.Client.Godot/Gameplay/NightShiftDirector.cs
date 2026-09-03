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

public sealed record ShiftBeat(
    long Tick,
    ShiftBeatKind Kind,
    string EnglishText,
    string ThaiText,
    string? ActorId = null,
    string? DestinationId = null);

public sealed class NightShiftDirector
{
    public const int DeadlineTick = 360;

    private readonly ShiftBeat[] _beats;
    private int _nextBeatIndex;

    public NightShiftDirector(ulong seed)
    {
        var random = new Pcg32SimRandom(seed, sequence: 91UL);
        string suspiciousRoom = random.NextInt(0, 2) == 0 ? "room-201" : "garden";
        string suspiciousRoomEnglish = suspiciousRoom == "room-201" ? "Room 201" : "the garden";
        string suspiciousRoomThai = suspiciousRoom == "room-201" ? "ห้อง 201" : "สวนด้านนอก";

        _beats =
        [
            new(24, ShiftBeatKind.Routine,
                "Elias begins kitchen inventory.",
                "เอเลียสเริ่มตรวจวัตถุดิบในห้องครัว",
                "dana", "kitchen"),
            new(52, ShiftBeatKind.Routine,
                "Mira returns to the manager office.",
                "มิรากลับไปตรวจเอกสารที่ห้องผู้จัดการ",
                "evelyn", "office"),
            new(78, ShiftBeatKind.PowerFlicker,
                "The hotel lights flicker. The CCTV clock loses one minute.",
                "ไฟทั้งโรงแรมกะพริบ นาฬิกากล้องวงจรปิดหายไปหนึ่งนาที"),
            new(105, ShiftBeatKind.Routine,
                $"Clara slips away toward {suspiciousRoomEnglish}.",
                $"คลาราแอบเดินไปทาง{suspiciousRoomThai}",
                "charlie", suspiciousRoom),
            new(148, ShiftBeatKind.AnonymousCall,
                "An anonymous caller whispers: 'One of them is not acting alone.'",
                "สายปริศนากระซิบว่า ‘หนึ่งในพวกเขาไม่ได้ลงมือเพียงลำพัง’"),
            new(188, ShiftBeatKind.Routine,
                "Elias leaves the kitchen without signing the logbook.",
                "เอเลียสออกจากห้องครัวโดยไม่ลงชื่อในสมุดเวร",
                "dana", "hallway"),
            new(222, ShiftBeatKind.MissingMasterKey,
                "The master-key hook is empty. Someone moved it during the blackout.",
                "ตะขอกุญแจมาสเตอร์ว่างเปล่า มีคนย้ายมันระหว่างไฟดับ"),
            new(256, ShiftBeatKind.Routine,
                "Mira searches the lobby for the missing master key.",
                "มิราออกค้นหากุญแจมาสเตอร์ที่ล็อบบี้",
                "evelyn", "lobby"),
            new(292, ShiftBeatKind.ImpossibleFootsteps,
                "Footsteps echo from the basement and Room 201 at the same time.",
                "เสียงฝีเท้าดังจากชั้นใต้ดินและห้อง 201 พร้อมกัน"),
            new(330, ShiftBeatKind.FinalWarning,
                "Dawn is near. Make your deduction before the shift ends.",
                "ใกล้รุ่งเช้าแล้ว เตรียมสรุปคดีก่อนหมดเวลา"),
        ];
    }

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
}
