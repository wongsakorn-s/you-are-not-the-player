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
    private static readonly EntityId[] Actors = [
        BasementScenario.Anna,
        BasementScenario.Bob,
        BasementScenario.Charlie,
        BasementScenario.Dana,
        BasementScenario.Evelyn,
        BasementScenario.George,
    ];
    private static readonly Color[] ActorColors = [
        new Color("77bdfb"),
        new Color("f5a65b"),
        new Color("77dd77"),
        new Color("c9a0ff"),
        new Color("ff7f9c"),
        new Color("ffe066"),
    ];

    private readonly GodotWorldAdapter _worldAdapter = new();
    private readonly Dictionary<EntityId, MovementSnapshot> _lastMovements = [];
    private BasementRealtimeAdapter? _simulation;
    private HotelWorldDefinition? _hotel;
    private DebugHud? _hud;
    private RestrictedDoorNode? _basementDoor;
    private int _selectedActorIndex;
    private int _interactionSequence;
    private bool _completionLogged;

    public override void _Ready()
    {
        _hotel = LoadHotelDefinition();
        BuildEnvironment(_hotel);
        _simulation = CreateSimulation();
        BuildActorViews();

        _hud = new DebugHud();
        AddChild(_hud);
        _worldAdapter.LocationConfirmed += OnLocationConfirmed;
        _worldAdapter.NavigationFailed += OnNavigationFailed;
        _worldAdapter.Synchronize(GetCoreLocations(), immediate: true);
        RefreshHud();
        GD.Print($"Real-time hotel ready: actors={Actors.Length} seed={_simulation.Seed}");
    }

    public override void _Process(double delta)
    {
        if (_simulation?.Update(delta) == true)
        {
            HandleSimulationChanges();
        }
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key ||
            _simulation is null)
        {
            return;
        }

        switch (key.Keycode)
        {
            case Key.F1:
                _simulation.TogglePause();
                _worldAdapter.SetMovementPaused(_simulation.IsPaused);
                break;
            case Key.Space:
                _simulation.Step();
                HandleSimulationChanges();
                break;
            case Key.F2:
                _simulation.SetSpeed(2.0f);
                _worldAdapter.SetMovementSpeed(2.0f);
                break;
            case Key.F3:
                _simulation.SetSpeed(10.0f);
                _worldAdapter.SetMovementSpeed(10.0f);
                break;
            case Key.Tab:
                _selectedActorIndex = (_selectedActorIndex + 1) % Actors.Length;
                break;
            case Key.E:
                ExecuteInteraction();
                break;
            case Key.F4:
                DumpSelectedActor();
                break;
            case Key.F5:
                DumpEventTrace();
                break;
            case Key.R:
                ResetSimulation();
                break;
            default:
                return;
        }

        RefreshHud();
        GetViewport().SetInputAsHandled();
    }

    private static BasementRealtimeAdapter CreateSimulation()
    {
        string rulesPath = ResolveRulesPath();
        InMemorySuspicionRuleRepository rules = JsonSuspicionRuleParser.Parse(
            File.ReadAllText(rulesPath));
        BasementScenarioSession session = new BasementScenario(rules).CreateSession(
            new BasementScenarioOptions(seed: 481_516, ticks: 16));
        return new BasementRealtimeAdapter(session);
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

    private void HandleSimulationChanges()
    {
        if (_simulation is null)
        {
            return;
        }

        foreach (WorldEvent worldEvent in _simulation.DrainNewEvents())
        {
            GD.Print(FormatEvent(worldEvent));
        }

        foreach (MovementSnapshot movement in _simulation.DrainNewMovements())
        {
            if (movement.Destination == BasementScenario.Basement)
            {
                OpenBasementDoor();
            }

            _lastMovements[movement.Actor] = movement;
            _worldAdapter.RequestMove(movement.Actor, movement.Destination);
            GD.Print(
                $"Movement requested: id={movement.RequestId.Value} actor={movement.Actor.Value} " +
                $"route={string.Join("->", movement.Route.Select(location => location.Value))}");
        }

        if (_simulation.IsComplete && !_completionLogged)
        {
            _completionLogged = true;
            GD.Print(
                $"Real-time Basement Test complete: tick={_simulation.CurrentTick} " +
                $"events={_simulation.Events.Count} decisions={_simulation.Decisions.Count}");
        }

        RefreshHud();
    }

    private void OnLocationConfirmed(EntityId actor, LocationId location)
    {
        if (_simulation is null)
        {
            return;
        }

        MovementSnapshot? pending = _simulation.GetPendingMovement(actor, location);
        if (pending is null)
        {
            return;
        }

        MovementSnapshot completed = _simulation.CompleteMovement(pending.RequestId);
        _lastMovements[actor] = completed;
        GD.Print(
            $"Movement completed and core committed: id={completed.RequestId.Value} " +
            $"actor={actor.Value} location={location.Value} tick={_simulation.CurrentTick}");

        if (actor == BasementScenario.Anna &&
            _simulation.Phase == BasementSessionPhase.ExplorerMovement)
        {
            CloseBasementDoor();
        }

        HandleSimulationChanges();
    }

    private void OnNavigationFailed(EntityId actor, LocationId location)
    {
        if (_simulation is null)
        {
            return;
        }

        MovementSnapshot? pending = _simulation.GetPendingMovement(actor, location);
        if (pending is null)
        {
            return;
        }

        MovementSnapshot failed = _simulation.FailMovement(pending.RequestId);
        _lastMovements[actor] = failed;
        _worldAdapter.CancelMove(actor);
        GD.PushWarning(
            $"Movement failed: id={failed.RequestId.Value} actor={actor.Value} " +
            $"location={location.Value}");
        HandleSimulationChanges();
    }

    private void ExecuteInteraction()
    {
        if (_simulation is null)
        {
            return;
        }

        EntityId actor = Actors[_selectedActorIndex];
        string interactionId;
        if (_basementDoor is { IsOpen: false })
        {
            interactionId = "basement-door";
            OpenBasementDoor();
        }
        else
        {
            _interactionSequence++;
            interactionId = $"godot-interact-{_interactionSequence.ToString(CultureInfo.InvariantCulture)}";
        }

        _simulation.Interact(actor, interactionId);
        HandleSimulationChanges();
    }

    private void ResetSimulation()
    {
        _simulation = CreateSimulation();
        _lastMovements.Clear();
        _interactionSequence = 0;
        _completionLogged = false;
        _worldAdapter.SetMovementPaused(isPaused: false);
        _worldAdapter.SetMovementSpeed(1.0f);
        CloseBasementDoor();
        _worldAdapter.Synchronize(GetCoreLocations(), immediate: true);
        GD.Print("Real-time hotel session reset");
    }

    private Dictionary<EntityId, LocationId> GetCoreLocations()
    {
        if (_simulation is null)
        {
            return new Dictionary<EntityId, LocationId>();
        }

        return Actors.ToDictionary(actor => actor, _simulation.GetLogicalLocation);
    }

    private void RefreshHud()
    {
        if (_simulation is null || _hud is null)
        {
            return;
        }

        EntityId selected = Actors[_selectedActorIndex];
        LocationId confirmed = _worldAdapter.ConfirmedLocations[selected];
        LocationId requested = _worldAdapter.GetRequestedLocation(selected);
        string physicalLocation = _worldAdapter.IsInTransit(selected)
            ? $"{confirmed.Value} -> {requested.Value} (moving)"
            : confirmed.Value;
        LocationId coreLocation = _simulation.GetLogicalLocation(selected);
        string movementText = _lastMovements.TryGetValue(selected, out MovementSnapshot? movement)
            ? movement.FailureReason == MovementFailureReason.None
                ? movement.Status.ToString()
                : $"{movement.Status}: {movement.FailureReason}"
            : "Idle";
        int episodic = _simulation.GetMemories(selected).Count(memory =>
            memory.Kind == MemoryKind.Episodic);
        int social = _simulation.GetMemories(selected).Count(memory =>
            memory.Kind == MemoryKind.Social);
        string suspicion = GetSuspicionText(selected);
        string state = _simulation.IsComplete
            ? "COMPLETE"
            : _simulation.IsPaused ? "PAUSED" : _simulation.Phase.ToString();
        string fingerprint = WorldEventTrace.ComputeSha256(_simulation.Events)[..12];

        _hud.SetStatus(string.Format(
            CultureInfo.InvariantCulture,
            "YOU ARE NOT THE PLAYER — REAL-TIME HOTEL\n\n" +
            "Seed: {0}\nTick: {1} (minimum {2})\nState: {3}   Speed: x{4:0}\n" +
            "Live fingerprint: {5}…\n\nInspecting: {6}\nPhysical: {7}\n" +
            "Core location: {8}\nMovement: {9}\n" +
            "Memory: {10} episodic / {11} social\n{12}",
            _simulation.Seed,
            _simulation.CurrentTick,
            _simulation.MinimumTicks,
            state,
            _simulation.Speed,
            fingerprint,
            selected.Value,
            physicalLocation,
            coreLocation.Value,
            movementText,
            episodic,
            social,
            suspicion));

        string recentEvents = string.Join(
            '\n',
            _simulation.Events
                .TakeLast(13)
                .Reverse()
                .Select(FormatEvent));
        _hud.SetEvents("LIVE WORLD EVENTS\n\n" +
            (recentEvents.Length == 0 ? "Waiting for simulation…" : recentEvents));
    }

    private string GetSuspicionText(EntityId selected)
    {
        if (_simulation is null ||
            (selected != BasementScenario.Anna && selected != BasementScenario.Bob))
        {
            return "Suspicion about George: none";
        }

        SuspicionSnapshot snapshot = _simulation.GetSuspicion(selected, BasementScenario.George);
        if (snapshot.Evidence.Count == 0)
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
        if (_simulation is null)
        {
            return;
        }

        EntityId selected = Actors[_selectedActorIndex];
        GD.Print(
            $"actor={selected.Value} core={_simulation.GetLogicalLocation(selected).Value} " +
            $"physical={_worldAdapter.ConfirmedLocations[selected].Value} " +
            $"moving={_worldAdapter.IsInTransit(selected)} " +
            $"memories={_simulation.GetMemories(selected).Count}");
    }

    private void DumpEventTrace()
    {
        if (_simulation is null)
        {
            return;
        }

        string path = ProjectSettings.GlobalizePath("user://realtime-basement-events.jsonl");
        using var output = new StreamWriter(path, append: false);
        WorldEventTrace.WriteJsonl(_simulation.Events, output);
        GD.Print($"Live event trace written to {path}");
    }

    private void OpenBasementDoor()
    {
        if (_basementDoor is { IsOpen: true })
        {
            return;
        }

        _basementDoor?.Open();
        _worldAdapter.SetLocationAccess(BasementScenario.Basement, isAccessible: true);
        GD.Print("Restricted door opened");
    }

    private void CloseBasementDoor()
    {
        _basementDoor?.ResetClosed();
        _worldAdapter.SetLocationAccess(BasementScenario.Basement, isAccessible: false);
        GD.Print("Restricted door closed");
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

        AddChild(new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-55.0f, -25.0f, 0.0f),
            LightEnergy = 1.2f,
            ShadowEnabled = true,
        });

        var camera = new Camera3D
        {
            Position = new Vector3(15.0f, 15.0f, 18.0f),
            Current = true,
        };
        AddChild(camera);
        camera.LookAt(Vector3.Zero);

        AddChild(new WorldEnvironment
        {
            Environment = new global::Godot.Environment
            {
                BackgroundMode = global::Godot.Environment.BGMode.Color,
                BackgroundColor = new Color("101722"),
                AmbientLightSource = global::Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color("a7b8cf"),
                AmbientLightEnergy = 0.65f,
            },
        });
    }

    private void BuildActorViews()
    {
        for (int index = 0; index < Actors.Length; index++)
        {
            EntityId actor = Actors[index];
            var view = new NpcActorNode();
            view.Initialize(actor.Value, ActorColors[index % ActorColors.Length]);
            AddChild(view);
            _worldAdapter.RegisterActor(actor, view, GetActorOffset(index));
        }
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
        AddChild(new MeshInstance3D
        {
            Name = nodeName,
            Position = position,
            Mesh = new BoxMesh
            {
                Size = size,
                Material = material,
            },
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

    private static Vector3 GetActorOffset(int index)
    {
        int column = index % 3;
        int row = index / 3;
        return new Vector3((column - 1) * 1.4f, 0.0f, (row - 0.5f) * 1.2f);
    }

    private static string FormatEvent(WorldEvent worldEvent) =>
        string.Format(
            CultureInfo.InvariantCulture,
            "[{0:00}] {1} {2} @ {3}",
            worldEvent.Time.Tick,
            worldEvent.Actor.Value,
            worldEvent.Type,
            worldEvent.Location.Value);
}
