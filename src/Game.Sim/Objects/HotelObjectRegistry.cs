using Game.Sim.Locations;

namespace Game.Sim.Objects;

public sealed class HotelObjectRegistry
{
    private readonly Dictionary<string, InteractiveObject> _objects = new(StringComparer.OrdinalIgnoreCase);

    public HotelObjectRegistry(IEnumerable<InteractiveObject>? initialObjects = null)
    {
        if (initialObjects is not null)
        {
            foreach (InteractiveObject obj in initialObjects)
            {
                Register(obj);
            }
        }
    }

    public IReadOnlyList<InteractiveObject> AllObjects => _objects.Values
        .OrderBy(o => o.Id, StringComparer.Ordinal)
        .ToArray();

    public void Register(InteractiveObject obj)
    {
        ArgumentNullException.ThrowIfNull(obj);
        _objects[obj.Id] = obj;
    }

    public InteractiveObject? GetObject(string id) =>
        _objects.TryGetValue(id, out InteractiveObject? obj) ? obj : null;

    public IReadOnlyList<InteractiveObject> GetObjectsInLocation(LocationId location) =>
        _objects.Values
            .Where(o => o.Location == location)
            .OrderBy(o => o.Id, StringComparer.Ordinal)
            .ToArray();

    public static HotelObjectRegistry CreateDefaultHotelObjects()
    {
        var registry = new HotelObjectRegistry();

        // Lobby objects
        registry.Register(new InteractiveObject(
            id: "lobby-reception-bell",
            location: new LocationId("lobby"),
            displayName: "Brass Reception Bell",
            kind: InteractiveObjectKind.Inspection,
            clueDescription: "A polished bell on the marble desk. Rings with a clear, sharp chime."));

        registry.Register(new InteractiveObject(
            id: "lobby-guest-registry",
            location: new LocationId("lobby"),
            displayName: "Guest Logbook",
            kind: InteractiveObjectKind.Registry,
            isLocked: false,
            clueDescription: "The registry lists guests in rooms 101-305. A page dated yesterday has a torn entry.",
            isSuspiciousToTamper: true));

        // Kitchen objects
        registry.Register(new InteractiveObject(
            id: "kitchen-pantry-safe",
            location: new LocationId("kitchen"),
            displayName: "Kitchen Wall Safe",
            kind: InteractiveObjectKind.Safe,
            isLocked: true,
            requiredKeyId: "chef-key",
            clueDescription: "Inside the safe lies an old duplicate key marked 'BASEMENT MASTER'.",
            isSuspiciousToTamper: true));

        registry.Register(new InteractiveObject(
            id: "kitchen-service-knife-block",
            location: new LocationId("kitchen"),
            displayName: "Culinary Knife Block",
            kind: InteractiveObjectKind.Inspection,
            clueDescription: "A heavy wooden block holding sharp knives. One slot is suspiciously vacant."));

        // Room 201 objects
        registry.Register(new InteractiveObject(
            id: "room201-briefcase",
            location: new LocationId("room-201"),
            displayName: "Locked Leather Briefcase",
            kind: InteractiveObjectKind.Safe,
            isLocked: true,
            requiredKeyId: "briefcase-code",
            clueDescription: "Contains encrypted correspondence detailing clandestine midnight meetings.",
            isSuspiciousToTamper: true));

        registry.Register(new InteractiveObject(
            id: "room201-nightstand-drawer",
            location: new LocationId("room-201"),
            displayName: "Nightstand Drawer",
            kind: InteractiveObjectKind.Inspection,
            clueDescription: "A handwritten hotel postcard with coordinates scribbled in pencil: 'Under the Garden Statue'."));

        // Basement objects
        registry.Register(new InteractiveObject(
            id: "basement-incriminating-ledger",
            location: new LocationId("basement"),
            displayName: "Hidden Black Ledger",
            kind: InteractiveObjectKind.Contraband,
            isLocked: false,
            clueDescription: "A black ledger documenting unauthorized surveillance on hotel residents.",
            isSuspiciousToTamper: true));

        registry.Register(new InteractiveObject(
            id: "basement-fusebox",
            location: new LocationId("basement"),
            displayName: "Main Electrical Fusebox",
            kind: InteractiveObjectKind.Switch,
            clueDescription: "The central power breaker for the hotel corridors. Can disrupt lighting.",
            isSuspiciousToTamper: true));

        // Garden objects
        registry.Register(new InteractiveObject(
            id: "garden-statue-stash",
            location: new LocationId("garden"),
            displayName: "Hollow Marble Statue",
            kind: InteractiveObjectKind.Safe,
            isLocked: false,
            clueDescription: "Hidden inside the hollow base is an ornate silver key labeled 'CHEF PRIVATE KEY'.",
            isSuspiciousToTamper: true));

        // Security Room objects
        registry.Register(new InteractiveObject(
            id: "security-cctv-terminal",
            location: new LocationId("security-room"),
            displayName: "CCTV Surveillance Terminal",
            kind: InteractiveObjectKind.Terminal,
            isLocked: true,
            requiredKeyId: "security-passcode",
            clueDescription: "The surveillance feed displays timestamped camera archives of the restricted basement.",
            isSuspiciousToTamper: true));

        // Office objects
        registry.Register(new InteractiveObject(
            id: "office-manager-desk",
            location: new LocationId("office"),
            displayName: "Manager Executive Desk",
            kind: InteractiveObjectKind.Registry,
            isLocked: false,
            clueDescription: "Staff roster and incident reports indicating suspicious late-night behavior around Room 201.",
            isSuspiciousToTamper: true));

        return registry;
    }
}
