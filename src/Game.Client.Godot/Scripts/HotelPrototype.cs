using System.Globalization;
using Game.Client.Godot.Adapters;
using Game.Client.Godot.Configuration;
using Game.Client.Godot.Debug;
using Game.Client.Godot.World;
using Game.Sim.Actions;
using Game.Sim.Entities;
using Game.Sim.Events;
using Game.Sim.Locations;
using Game.Sim.Logging;
using Game.Sim.Memory;
using Game.Sim.Scenarios;
using Game.Sim.Suspicion;
using Godot;

namespace Game.Client.Godot.Scripts;

public sealed partial class HotelPrototype : Node3D
{
    private static readonly Color[] ActorColors = [
        new Color("77bdfb"),
        new Color("f5a65b"),
        new Color("77dd77"),
        new Color("c9a0ff"),
        new Color("ff7f9c"),
        new Color("ffe066"),
    ];

    private readonly GodotWorldAdapter _worldAdapter = new();
    private readonly List<WorldEvent> _inputEvents = [];
    private readonly List<WorldEvent> _liveMovementEvents = [];
    private BasementReplayAdapter? _replay;
    private GodotInputActionAdapter? _inputActions;
    private GodotLiveMovementAdapter? _liveMovement;
    private HotelWorldDefinition? _hotel;
    private DebugHud? _hud;
    private RestrictedDoorNode? _basementDoor;
    private EntityId[] _actors = [];
    private int _selectedActorIndex;

    public override void _Ready()
    {
        _hotel = LoadHotelDefinition();
        BuildEnvironment(_hotel);
        BasementScenarioResult result = RunSimulation();
        _replay = new BasementReplayAdapter(result);
        InitializeRuntimeAdapters(result);
        _actors = result.Actors.Select(actor => actor.Entity).ToArray();
        BuildActorViews();

        _hud = new DebugHud();
        AddChild(_hud);
        _worldAdapter.LocationConfirmed += OnLocationConfirmed;
        _worldAdapter.NavigationFailed += OnNavigationFailed;
        _worldAdapter.Synchronize(_liveMovement!.LogicalLocations, immediate: true);
        RefreshHud();
        GD.Print($"Hotel slice ready: actors={_actors.Length} events={result.Events.Count} seed={result.Seed}");
    }

    public override void _Process(double delta)
    {
        if (_replay?.Update(delta) == true)
        {
            HandleReplayEvents(_replay.DrainNewEvents());
            RefreshHud();
        }
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key ||
            _replay is null)
        {
            return;
        }

        switch (key.Keycode)
        {
            case Key.F1:
                _replay.TogglePause();
                break;
            case Key.Space:
                _replay.Step();
                HandleReplayEvents(_replay.DrainNewEvents());
                break;
            case Key.F2:
                _replay.SetSpeed(2.0f);
                break;
            case Key.F3:
                _replay.SetSpeed(10.0f);
                break;
            case Key.Tab:
                _selectedActorIndex = (_selectedActorIndex + 1) % _actors.Length;
                break;
            case Key.E:
                ExecuteInteraction();
                break;
            case Key.M:
                ExecuteMoveToNextLocation();
                break;
            case Key.F4:
                DumpSelectedActor();
                break;
            case Key.F5:
                DumpEventTrace();
                break;
            case Key.R:
                _replay.Reset();
                InitializeRuntimeAdapters(_replay.Result);
                _inputEvents.Clear();
                _liveMovementEvents.Clear();
                _basementDoor?.ResetClosed();
                _worldAdapter.SetLocationAccess(BasementScenario.Basement, isAccessible: false);
                _worldAdapter.Synchronize(_liveMovement!.LogicalLocations, immediate: true);
                break;
            default:
                return;
        }

        RefreshHud();
        GetViewport().SetInputAsHandled();
    }

    private static BasementScenarioResult RunSimulation()
    {
        string rulesPath = ResolveRulesPath();
        InMemorySuspicionRuleRepository rules = JsonSuspicionRuleParser.Parse(
            File.ReadAllText(rulesPath));
        return new BasementScenario(rules).Run(
            new BasementScenarioOptions(seed: 481_516, ticks: 16));
    }

    private static HotelWorldDefinition LoadHotelDefinition()
    {
        string outputPath = Path.Combine(
            AppContext.BaseDirectory,
            "Data",
            "Hotel",
            "hotel-world.json");
        if (File.Exists(outputPath))
        {
            return HotelWorldDefinitionParser.Parse(File.ReadAllText(outputPath));
        }

        string sourcePath = Path.GetFullPath(Path.Combine(
            ProjectSettings.GlobalizePath("res://"),
            "..",
            "Game.Content",
            "Data",
            "Hotel",
            "hotel-world.json"));
        return File.Exists(sourcePath)
            ? HotelWorldDefinitionParser.Parse(File.ReadAllText(sourcePath))
            : throw new FileNotFoundException("Hotel world data was not found.", sourcePath);
    }

    private static string ResolveRulesPath()
    {
        string outputPath = Path.Combine(
            AppContext.BaseDirectory,
            "Data",
            "SuspicionRules",
            "mvp.json");
        if (File.Exists(outputPath))
        {
            return outputPath;
        }

        string sourcePath = Path.GetFullPath(Path.Combine(
            ProjectSettings.GlobalizePath("res://"),
            "..",
            "Game.Content",
            "Data",
            "SuspicionRules",
            "mvp.json"));
        return File.Exists(sourcePath)
            ? sourcePath
            : throw new FileNotFoundException("Suspicion rule data was not found.", sourcePath);
    }

    private void BuildEnvironment(HotelWorldDefinition hotel)
    {
        foreach (HotelLocationDefinition location in hotel.Locations)
        {
            _worldAdapter.RegisterLocation(
                new LocationId(location.Id),
                location.Marker.ToVector3(),
                location.Restricted);
            AddBox(
                $"{location.Id}-floor",
                location.FloorPosition.ToVector3(),
                location.FloorSize.ToVector3(),
                new Color(location.Color));
            AddRoomLabel(
                location.DisplayName,
                new Vector3(location.Marker.X, 0.05f, location.Marker.Z + 1.5f));
        }

        BuildNavigationRegion(hotel.Navigation);

        HotelDoorDefinition door = hotel.Portals
            .Single(portal => portal.Id == "basement-door")
            .Door ?? throw new InvalidOperationException("Basement portal requires a door definition.");
        _basementDoor = new RestrictedDoorNode();
        _basementDoor.Initialize(
            door.Position.ToVector3(),
            door.Size.ToVector3(),
            new Color(door.Color));
        AddChild(_basementDoor);

        var light = new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-55.0f, -25.0f, 0.0f),
            LightEnergy = 1.2f,
            ShadowEnabled = true,
        };
        AddChild(light);

        var camera = new Camera3D
        {
            Position = new Vector3(15.0f, 15.0f, 18.0f),
            Current = true,
        };
        AddChild(camera);
        camera.LookAt(Vector3.Zero);

        var environment = new WorldEnvironment
        {
            Environment = new global::Godot.Environment
            {
                BackgroundMode = global::Godot.Environment.BGMode.Color,
                BackgroundColor = new Color("101722"),
                AmbientLightSource = global::Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color("a7b8cf"),
                AmbientLightEnergy = 0.65f,
            },
        };
        AddChild(environment);
    }

    private void BuildActorViews()
    {
        for (int index = 0; index < _actors.Length; index++)
        {
            EntityId actor = _actors[index];
            var view = new NpcActorNode();
            view.Initialize(actor.Value, ActorColors[index % ActorColors.Length]);
            AddChild(view);
            _worldAdapter.RegisterActor(actor, view, GetActorOffset(index));
        }
    }

    private void RefreshHud()
    {
        if (_replay is null || _hud is null || _actors.Length == 0)
        {
            return;
        }

        EntityId selected = _actors[_selectedActorIndex];
        LocationId confirmedLocation = _worldAdapter.ConfirmedLocations[selected];
        LocationId requestedLocation = _worldAdapter.GetRequestedLocation(selected);
        string locationText = _worldAdapter.IsInTransit(selected)
            ? $"{confirmedLocation.Value} -> {requestedLocation.Value} (moving)"
            : confirmedLocation.Value;
        LocationId coreLocation = _liveMovement?.GetLogicalLocation(selected) ?? confirmedLocation;
        MovementSnapshot? movement = _liveMovement?.GetLastMovement(selected);
        string movementText = movement is null
            ? "Idle"
            : movement.FailureReason == MovementFailureReason.None
                ? movement.Status.ToString()
                : $"{movement.Status}: {movement.FailureReason}";
        int episodic = _replay.Result.Memories.Count(memory =>
            memory.Owner == selected && memory.Memory.Kind == MemoryKind.Episodic);
        int social = _replay.Result.Memories.Count(memory =>
            memory.Owner == selected && memory.Memory.Kind == MemoryKind.Social);
        string suspicion = GetSuspicionText(selected, _replay.Result);
        string state = _replay.IsComplete
            ? "COMPLETE"
            : _replay.IsPaused ? "PAUSED" : "RUNNING";

        _hud.SetStatus(string.Format(
            CultureInfo.InvariantCulture,
            "YOU ARE NOT THE PLAYER — HOTEL SLICE\n\n" +
            "Seed: {0}\nTick: {1}/{2}\nState: {3}   Speed: x{4:0}\n" +
            "Fingerprint: {5}…\n\nInspecting: {6}\nPhysical: {7}\n" +
            "Core location: {8}\nMovement: {9}\n" +
            "Memory: {10} episodic / {11} social\n{12}",
            _replay.Result.Seed,
            _replay.CurrentTick,
            _replay.Result.CompletedAt.Tick,
            state,
            _replay.Speed,
            WorldEventTrace.ComputeSha256(_replay.Result.Events)[..12],
            selected.Value,
            locationText,
            coreLocation.Value,
            movementText,
            episodic,
            social,
            suspicion));

        string recentEvents = string.Join(
            '\n',
            _replay.VisibleEvents
                .Concat(_inputEvents)
                .Concat(_liveMovementEvents)
                .OrderBy(worldEvent => worldEvent.Time)
                .ThenBy(worldEvent => worldEvent.Id.Value)
                .TakeLast(13)
                .Reverse()
                .Select(FormatEvent));
        _hud.SetEvents("RECENT WORLD EVENTS\n\n" +
            (recentEvents.Length == 0 ? "Waiting for simulation…" : recentEvents));
    }

    private static string GetSuspicionText(EntityId selected, BasementScenarioResult result)
    {
        SuspicionSnapshot? snapshot = selected == BasementScenario.Anna
            ? result.AnnaSuspicion
            : selected == BasementScenario.Bob ? result.BobSuspicion : null;
        if (snapshot is null)
        {
            return "Suspicion about George: none";
        }

        SuspicionVector vector = snapshot.Vector;
        return string.Format(
            CultureInfo.InvariantCulture,
            "Suspicion about George:\n  role deviation {0:0.00}\n" +
            "  secrecy {1:0.00}\n  evidence {2}",
            vector.RoleDeviation,
            vector.Secrecy,
            snapshot.Evidence.Count);
    }

    private void DumpSelectedActor()
    {
        if (_replay is null || _actors.Length == 0)
        {
            return;
        }

        EntityId selected = _actors[_selectedActorIndex];
        int memories = _replay.Result.Memories.Count(memory => memory.Owner == selected);
        LocationId location = _worldAdapter.ConfirmedLocations[selected];
        GD.Print($"actor={selected.Value} location={location.Value} moving={_worldAdapter.IsInTransit(selected)} memories={memories}");
    }

    private void ExecuteInteraction()
    {
        if (_replay is null || _inputActions is null || _actors.Length == 0)
        {
            return;
        }

        EntityId selected = _actors[_selectedActorIndex];
        LocationId location = _worldAdapter.ConfirmedLocations[selected];
        if (_basementDoor is { IsOpen: false } && location == BasementScenario.Lobby)
        {
            _inputEvents.AddRange(_inputActions.Interact(
                selected,
                location,
                interactionId: "basement-door"));
            OpenBasementDoor();
            return;
        }

        _inputEvents.AddRange(_inputActions.Interact(selected, location));
    }

    private void ExecuteMoveToNextLocation()
    {
        if (_replay is null || _liveMovement is null || _hotel is null || _actors.Length == 0)
        {
            return;
        }

        EntityId actor = _actors[_selectedActorIndex];
        LocationId current = _liveMovement.GetLogicalLocation(actor);
        int currentIndex = Array.FindIndex(
            _hotel.Locations,
            location => string.Equals(location.Id, current.Value, StringComparison.Ordinal));
        if (currentIndex < 0)
        {
            throw new InvalidOperationException(
                $"Core location '{current}' is missing from the hotel definition.");
        }

        int nextIndex = (currentIndex + 1) % _hotel.Locations.Length;
        StartLiveMovement(
            actor,
            new LocationId(_hotel.Locations[nextIndex].Id),
            _replay.CurrentTick);
    }

    private void DumpEventTrace()
    {
        if (_replay is null)
        {
            return;
        }

        string path = ProjectSettings.GlobalizePath("user://basement-events.jsonl");
        using var output = new StreamWriter(path, append: false);
        WorldEventTrace.WriteJsonl(
            _replay.Result.Events.Concat(_inputEvents).Concat(_liveMovementEvents),
            output);
        GD.Print($"Event trace written to {path}");
    }

    private static string FormatEvent(WorldEvent worldEvent) =>
        string.Format(
            CultureInfo.InvariantCulture,
            "[{0:00}] {1} {2} @ {3}",
            worldEvent.Time.Tick,
            worldEvent.Actor.Value,
            worldEvent.Type,
            worldEvent.Location.Value);

    private static Vector3 GetActorOffset(int index)
    {
        int column = index % 3;
        int row = index / 3;
        return new Vector3((column - 1) * 1.4f, 0.0f, (row - 0.5f) * 1.2f);
    }

    private void HandleReplayEvents(IReadOnlyList<WorldEvent> events)
    {
        if (_basementDoor is { IsOpen: false } &&
            events.Any(worldEvent => worldEvent.Type == EventType.BoundaryProbe))
        {
            OpenBasementDoor();
        }

        foreach (WorldEvent worldEvent in events.Where(
                     worldEvent => worldEvent.Type == EventType.EnterLocation))
        {
            RequestLiveMovement(worldEvent);
        }
    }

    private void OpenBasementDoor()
    {
        _basementDoor?.Open();
        _worldAdapter.SetLocationAccess(BasementScenario.Basement, isAccessible: true);
        _liveMovement?.SetPortalAccess("basement-door", isAccessible: true);
        GD.Print("Restricted door opened: basement navigation released");
    }

    private void OnLocationConfirmed(EntityId actor, LocationId location)
    {
        if (_replay is not null && _liveMovement is not null)
        {
            _liveMovementEvents.AddRange(_liveMovement.CompleteMove(
                actor,
                location,
                _replay.CurrentTick));
        }

        GD.Print($"Navigation arrived and core committed: actor={actor.Value} location={location.Value}");
        RefreshHud();
    }

    private void OnNavigationFailed(EntityId actor, LocationId location)
    {
        if (_replay is not null && _liveMovement is not null)
        {
            _liveMovement.FailMove(actor, location, _replay.CurrentTick);
        }

        GD.PushWarning($"Navigation failed: actor={actor.Value} location={location.Value}");
        RefreshHud();
    }

    private void RequestLiveMovement(WorldEvent worldEvent)
    {
        StartLiveMovement(
            worldEvent.Actor,
            worldEvent.Location,
            worldEvent.Time.Tick);
    }

    private void StartLiveMovement(EntityId actor, LocationId destination, long tick)
    {
        if (_liveMovement is null)
        {
            return;
        }

        MovementSnapshot movement = _liveMovement.RequestMove(actor, destination, tick);
        if (movement.Status == MovementStatus.Navigating)
        {
            _worldAdapter.RequestMove(actor, destination);
            GD.Print(
                $"Movement navigating: request={movement.RequestId.Value} actor={actor.Value} " +
                $"route={string.Join("->", movement.Route.Select(location => location.Value))}");
        }
        else if (movement.Status == MovementStatus.Failed)
        {
            GD.Print(
                $"Movement rejected: request={movement.RequestId.Value} actor={actor.Value} " +
                $"reason={movement.FailureReason}");
        }
    }

    private void InitializeRuntimeAdapters(BasementScenarioResult result)
    {
        if (_hotel is null)
        {
            throw new InvalidOperationException("Hotel world must be loaded before runtime adapters.");
        }

        long firstEventId = checked(result.Events.Max(worldEvent => worldEvent.Id.Value) + 1);
        var eventIds = new SequentialEventIdGenerator(firstEventId);
        _liveMovement = new GodotLiveMovementAdapter(result, _hotel, eventIds);
        _inputActions = new GodotInputActionAdapter(result, eventIds);
    }

    private void BuildNavigationRegion(NavigationSurfaceDefinition navigation)
    {
        var navigationMesh = new NavigationMesh();
        navigationMesh.SetVertices([
            new Vector3(navigation.MinimumX, navigation.Height, navigation.MinimumZ),
            new Vector3(navigation.MinimumX, navigation.Height, navigation.MaximumZ),
            new Vector3(navigation.MaximumX, navigation.Height, navigation.MaximumZ),
            new Vector3(navigation.MaximumX, navigation.Height, navigation.MinimumZ),
        ]);
        navigationMesh.AddPolygon([0, 1, 2, 3]);
        AddChild(new NavigationRegion3D
        {
            Name = "HotelNavigationRegion",
            NavigationMesh = navigationMesh,
        });
    }

    private void AddBox(string nodeName, Vector3 position, Vector3 size, Color color)
    {
        var material = new StandardMaterial3D
        {
            AlbedoColor = color,
            Roughness = 0.85f,
        };
        var mesh = new BoxMesh
        {
            Size = size,
            Material = material,
        };
        AddChild(new MeshInstance3D
        {
            Name = nodeName,
            Position = position,
            Mesh = mesh,
        });
    }

    private void AddRoomLabel(string text, Vector3 position)
    {
        AddChild(new Label3D
        {
            Text = text,
            Position = position,
            RotationDegrees = new Vector3(-90.0f, 0.0f, 0.0f),
            FontSize = 34,
            OutlineSize = 10,
            Modulate = new Color("d9e7ff"),
        });
    }
}
