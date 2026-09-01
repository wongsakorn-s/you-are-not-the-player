using System.Globalization;
using System.Text.Json;
using Game.Client.Godot.Adapters;
using Game.Client.Godot.Configuration;
using Game.Client.Godot.Debug;
using Game.Client.Godot.World;
using Game.Sim.Actions;
using Game.Sim.Conspiracy;
using Game.Sim.Entities;
using Game.Sim.Events;
using Game.Sim.Locations;
using Game.Sim.Logging;
using Game.Sim.Memory;
using Game.Sim.Objects;
using Game.Sim.Player;
using Game.Sim.Routines;
using Game.Sim.Scenarios;
using Game.Sim.Snapshots;
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
    private static readonly LocationId[] LocationShortcuts = [
        new("lobby"),
        new("hallway"),
        new("kitchen"),
        new("room-201"),
        new("basement"),
        new("garden"),
        new("security-room"),
        new("office"),
    ];

    private readonly GodotWorldAdapter _worldAdapter = new();
    private readonly Dictionary<EntityId, MovementSnapshot> _lastMovements = [];
    private BasementRealtimeAdapter? _simulation;
    private HotelWorldDefinition? _hotel;
    private DebugHud? _hud;
    private RestrictedDoorNode? _basementDoor;
    private EntityId _possessedPlayerActor = BasementScenario.George;
    private int _selectedActorIndex = Actors.Length - 1;
    private int _interactionSequence;
    private bool _completionLogged;
    private string _feedbackText = "Ready. Explore the hotel and gather evidence.";
    private bool _feedbackIsError;
    private ClimaxResolution? _climaxResolution;
    private bool _smokePlaythroughEnabled;
    private bool _smokePlaythroughCompleted;
    private double _smokeElapsedSeconds;

    public override void _Ready()
    {
        _smokePlaythroughEnabled = OS.GetCmdlineUserArgs().Contains(
            "--smoke-playthrough",
            StringComparer.OrdinalIgnoreCase);
        _hotel = LoadHotelDefinition();
        BuildEnvironment(_hotel);
        _simulation = CreateSimulation();
        BuildActorViews();

        _hud = new DebugHud();
        AddChild(_hud);
        _worldAdapter.LocationConfirmed += OnLocationConfirmed;
        _worldAdapter.NavigationFailed += OnNavigationFailed;
        _worldAdapter.Synchronize(GetCoreLocations(), immediate: true);
        ShowFeedback(_feedbackText);
        RefreshHud();
        GD.Print($"Real-time hotel ready: actors={Actors.Length} seed={_simulation.Seed}");
    }

    public override void _Process(double delta)
    {
        if (_simulation?.Update(delta) == true)
        {
            HandleSimulationChanges();
        }

        RunSmokePlaythrough(delta);
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
                ShowFeedback(_simulation.IsPaused ? "Simulation paused." : "Simulation resumed.");
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
            case Key.P:
                _possessedPlayerActor = Actors[_selectedActorIndex];
                _simulation.PlayerController.SetPlayerEntity(_possessedPlayerActor);
                ShowFeedback($"Now possessing {_possessedPlayerActor.Value}.");
                GD.Print($"[Player Controller] Now controlling {_possessedPlayerActor.Value}");
                break;
            case Key.Key1:
                ExecutePlayerMove(LocationShortcuts[0]);
                break;
            case Key.Key2:
                ExecutePlayerMove(LocationShortcuts[1]);
                break;
            case Key.Key3:
                ExecutePlayerMove(LocationShortcuts[2]);
                break;
            case Key.Key4:
                ExecutePlayerMove(LocationShortcuts[3]);
                break;
            case Key.Key5:
                ExecutePlayerMove(LocationShortcuts[4]);
                break;
            case Key.Key6:
                ExecutePlayerMove(LocationShortcuts[5]);
                break;
            case Key.Key7:
                ExecutePlayerMove(LocationShortcuts[6]);
                break;
            case Key.Key8:
                ExecutePlayerMove(LocationShortcuts[7]);
                break;
            case Key.O:
                ExecuteObjectInteraction();
                break;
            case Key.T:
                ExecutePlayerDialogue();
                break;
            case Key.Y:
                ExecutePlayerInquireObject();
                break;
            case Key.J:
                ExecuteDumpJournal();
                break;
            case Key.K:
                ExecuteCheckConspiracy();
                break;
            case Key.Z:
                ExecuteClimaxChoice(PlayerClimaxChoice.ConfessReality);
                break;
            case Key.X:
                ExecuteClimaxChoice(PlayerClimaxChoice.DenyAndCounter);
                break;
            case Key.C:
                ExecuteClimaxChoice(PlayerClimaxChoice.Flee);
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
            case Key.F6:
                ExecuteQuickSave();
                break;
            case Key.F7:
                ExecuteQuickLoad();
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

    private void ExecutePlayerMove(LocationId destination)
    {
        if (_simulation is null)
        {
            return;
        }

        _simulation.PlayerController.SetPlayerEntity(_possessedPlayerActor);
        if (destination == BasementScenario.Basement)
        {
            OpenBasementDoor();
        }

        NpcMovementExecution execution = _simulation.PlayerMove(destination);
        if (execution.Status == NpcMovementExecutionStatus.Pending && execution.Movement is not null)
        {
            _lastMovements[_possessedPlayerActor] = execution.Movement;
            _worldAdapter.RequestMove(_possessedPlayerActor, destination);
            ShowFeedback($"Moving {_possessedPlayerActor.Value} to {destination.Value}.");
            GD.Print($"[Player Controller] Moving {_possessedPlayerActor.Value} to {destination.Value}...");
        }
        else if (execution.Status == NpcMovementExecutionStatus.Failed)
        {
            ShowFeedback($"Cannot move to {destination.Value}; route or access is unavailable.", isError: true);
            GD.Print($"[Player Controller] Move to {destination.Value} failed (route or access unavailable).");
        }

        HandleSimulationChanges();
    }

    private void ExecutePlayerDialogue()
    {
        if (_simulation is null)
        {
            return;
        }

        _simulation.PlayerController.SetPlayerEntity(_possessedPlayerActor);
        IReadOnlyList<EntityId> present = _simulation.PlayerController.GetPresentActors();
        if (present.Count == 0)
        {
            ShowFeedback("Nobody is here to talk to.", isError: true);
            GD.Print($"[Dialogue] No other characters present in {_simulation.PlayerController.CurrentLocation.Value} to speak with.");
            return;
        }

        EntityId partner = present[0];
        EntityId subject = _possessedPlayerActor == BasementScenario.George ? BasementScenario.Anna : BasementScenario.George;
        DialogueOutcome outcome = _simulation.Talk(new DialogueRequest(
            DialogueActionKind.AskAboutSubject,
            requester: _possessedPlayerActor,
            partner: partner,
            subject: subject));

        ShowFeedback(outcome.Succeeded ? $"Spoke with {partner.Value}." : outcome.FailureReason ?? "Dialogue failed.", !outcome.Succeeded);
        GD.Print($"[Dialogue with {partner.Value}] {outcome.Text}");
        HandleSimulationChanges();
    }

    private void ExecutePlayerInquireObject()
    {
        if (_simulation is null)
        {
            return;
        }

        _simulation.PlayerController.SetPlayerEntity(_possessedPlayerActor);
        IReadOnlyList<EntityId> present = _simulation.PlayerController.GetPresentActors();
        if (present.Count == 0)
        {
            ShowFeedback("Nobody is here to question.", isError: true);
            GD.Print($"[Inquiry] No other characters present in {_simulation.PlayerController.CurrentLocation.Value} to speak with.");
            return;
        }

        EntityId partner = present[0];
        var objects = _simulation.GetPresentObjects();
        DialogueOutcome outcome;
        if (objects.Count > 0)
        {
            outcome = _simulation.InquireObject(partner, objects[0].Id);
        }
        else
        {
            PlayerJournal journal = _simulation.GetPlayerJournal(_possessedPlayerActor);
            if (journal.Entries.Count > 0)
            {
                outcome = _simulation.ConfrontWithEvidence(partner, journal.Entries[0].Id);
            }
            else
            {
                outcome = _simulation.Talk(new DialogueRequest(
                    DialogueActionKind.InquireSchedule,
                    requester: _possessedPlayerActor,
                    partner: partner));
            }
        }

        ShowFeedback(outcome.Succeeded ? $"Inquiry with {partner.Value} completed." : outcome.FailureReason ?? "Inquiry failed.", !outcome.Succeeded);
        GD.Print($"\n=== [INQUIRY WITH {partner.Value.ToUpperInvariant()}] ===");
        GD.Print($"Result: {outcome.Text}");
        if (outcome.TransferredMemory is not null)
        {
            GD.Print($"Memory Learned: [Id: {outcome.TransferredMemory.Id.Value}] {outcome.TransferredMemory.EventType} at {outcome.TransferredMemory.Location?.Value}");
        }
        GD.Print("===================================================\n");
        HandleSimulationChanges();
    }

    private void ExecuteDumpJournal()
    {
        if (_simulation is null)
        {
            return;
        }

        PlayerJournal journal = _simulation.GetPlayerJournal(_possessedPlayerActor);
        GD.Print($"\n=== JOURNAL FOR {journal.PlayerEntity.Value.ToUpperInvariant()} (Location: {journal.CurrentLocation.Value}) ===");
        GD.Print($"Known Memories ({journal.Entries.Count}):");
        foreach (PlayerJournalEntry entry in journal.Entries)
        {
            GD.Print($"  * {entry.Summary}");
        }
        GD.Print($"Suspicions ({journal.SuspicionSnapshots.Count}):");
        foreach (SuspicionSnapshot suspicion in journal.SuspicionSnapshots)
        {
            GD.Print($"  * {suspicion.Subject.Value}: role dev {suspicion.Vector.RoleDeviation:0.0}, secrecy {suspicion.Vector.Secrecy:0.0} ({suspicion.Evidence.Count} evidence)");
        }
        GD.Print("===================================================\n");
    }

    private void ExecuteCheckConspiracy()
    {
        if (_simulation is null)
        {
            return;
        }

        AccusationCoalition? coalition = _simulation.EvaluateConspiracy(_possessedPlayerActor);
        GD.Print($"\n=== [NPC CONSPIRACY & COALITION STATUS] ===");
        if (coalition is null)
        {
            ShowFeedback("No coalition yet. Create stronger evidence or anomalies first.");
            GD.Print($"No active conspiracy or coalition formed against {_possessedPlayerActor.Value}.");
            GD.Print($"NPCs do not yet have sufficient collective suspicion to coordinate.");
        }
        else
        {
            string members = string.Join(", ", coalition.Members.Select(m => m.Value.ToUpperInvariant()));
            GD.Print($"Target: {coalition.Target.Value.ToUpperInvariant()}");
            GD.Print($"Initiator: {coalition.Initiator.Value.ToUpperInvariant()}");
            GD.Print($"Members: [{members}]");
            GD.Print($"Combined Suspicion Score: {coalition.CombinedSuspicionScore:0.0}");
            GD.Print($"Stage: {coalition.Stage}");
            GD.Print($"Consensus Reached: {coalition.ConsensusReached}");
            GD.Print($"Shared Evidence ({coalition.EvidenceSummaries.Count}):");
            foreach (string evidence in coalition.EvidenceSummaries)
            {
                GD.Print($"  * {evidence}");
            }

            if (coalition.ConsensusReached)
            {
                WorldEvent? confrontation = _simulation.TriggerConfrontation(BasementScenario.Lobby);
                if (confrontation is not null)
                {
                    ShowFeedback("Climax started: choose Z to confess, X to deny, or C to flee.");
                    GD.Print($"\n[!!!] CLIMAX TRIGGERED: The coalition has gathered in the Lobby to confront you!");
                    GD.Print($"Press [Z] to Confess Reality, [X] to Deny/Counter-Accuse, [C] to Flee!");
                }
                else if (_simulation.CanResolveClimax(_possessedPlayerActor))
                {
                    ShowFeedback("The confrontation is active: choose Z, X, or C.");
                }
            }
            else
            {
                ShowFeedback("A coalition is forming, but it has not reached consensus yet.");
            }
        }
        GD.Print("===========================================\n");
    }

    private void ExecuteClimaxChoice(PlayerClimaxChoice choice)
    {
        if (_simulation is null)
        {
            return;
        }

        if (!_simulation.CanResolveClimax(_possessedPlayerActor))
        {
            ShowFeedback("Climax choices are locked until a coalition confronts you. Press K to check.", isError: true);
            return;
        }

        ClimaxResolution resolution = _simulation.ResolveClimax(choice, _possessedPlayerActor);
        _climaxResolution = resolution;
        _simulation.SetPaused(isPaused: true);
        _worldAdapter.SetMovementPaused(isPaused: true);
        ShowFeedback($"Ending reached: {resolution.Title}. Press R to restart.");
        GD.Print($"\n=======================================================");
        GD.Print($"=== CLIMAX RESOLUTION: {resolution.Title.ToUpperInvariant()} ===");
        GD.Print($"=======================================================");
        GD.Print(resolution.NarrativeText);
        GD.Print($"Player Vindicated: {resolution.PlayerVindicated}");
        GD.Print($"Existential Awakening: {resolution.ExistentialAwakeningTriggered}");
        GD.Print($"Player Fled: {resolution.PlayerFled}");
        GD.Print($"=======================================================\n");
        HandleSimulationChanges();
    }

    private void ExecuteQuickSave()
    {
        if (_simulation is null)
        {
            return;
        }

        string savePath = GetQuickSavePath();
        try
        {
            SessionSnapshot snapshot = _simulation.CaptureSnapshot();
            SessionSnapshotSerializer.SaveToFile(snapshot, savePath);
            ShowFeedback($"QuickSave complete at tick {snapshot.Metadata.CurrentTick}.");
            GD.Print($"[QuickSave] Snapshot saved to {savePath} (Tick: {snapshot.Metadata.CurrentTick}, Phase: {snapshot.Metadata.Phase})");
        }
        catch (Exception exception) when (IsRecoverableSnapshotException(exception))
        {
            ShowFeedback($"QuickSave failed: {exception.Message}", isError: true);
            GD.PushError($"[QuickSave] {exception}");
        }
    }

    private void ExecuteQuickLoad()
    {
        string savePath = GetQuickSavePath();
        if (!File.Exists(savePath))
        {
            ShowFeedback("No QuickSave exists yet. Press F6 to create one.", isError: true);
            GD.PrintErr($"[QuickLoad] No quicksave found at {savePath}");
            return;
        }

        try
        {
            SessionSnapshot snapshot = SessionSnapshotSerializer.LoadFromFile(savePath);
            string rulesPath = ResolveRulesPath();
            InMemorySuspicionRuleRepository rules = JsonSuspicionRuleParser.Parse(File.ReadAllText(rulesPath));
            BasementRealtimeAdapter restored = BasementRealtimeAdapter.FromSnapshot(snapshot, rules);

            _simulation = restored;
            _possessedPlayerActor = restored.PlayerController.PlayerEntity;
            _selectedActorIndex = Array.IndexOf(Actors, _possessedPlayerActor);
            if (_selectedActorIndex < 0)
            {
                _selectedActorIndex = 0;
            }

            _climaxResolution = restored.LastClimaxResolution;
            _lastMovements.Clear();
            _completionLogged = restored.IsComplete;
            restored.SetPaused(_climaxResolution is not null);
            _worldAdapter.SetMovementPaused(restored.IsPaused);
            _worldAdapter.SetMovementSpeed(restored.Speed);
            if (restored.GetLogicalLocation(BasementScenario.George) == BasementScenario.Basement ||
                restored.GetLogicalLocation(BasementScenario.Anna) == BasementScenario.Basement)
            {
                OpenBasementDoor();
            }
            else
            {
                CloseBasementDoor();
            }

            _worldAdapter.Synchronize(GetCoreLocations(), immediate: true);
            if (_climaxResolution is null)
            {
                restored.TriggerSaveReloadAnomaly(_possessedPlayerActor);
                GD.Print($"[REALITY ANOMALY] NPCs in the room sense an unnatural temporal shift (Déjà Vu)!");
            }

            ShowFeedback($"QuickLoad restored tick {snapshot.Metadata.CurrentTick} as {_possessedPlayerActor.Value}.");
            GD.Print($"[QuickLoad] Restored session from {savePath} (Tick: {snapshot.Metadata.CurrentTick}, Phase: {snapshot.Metadata.Phase})");
            HandleSimulationChanges();
        }
        catch (Exception exception) when (IsRecoverableSnapshotException(exception))
        {
            ShowFeedback($"QuickLoad failed: {exception.Message}", isError: true);
            GD.PushError($"[QuickLoad] {exception}");
        }
    }

    private void ExecuteObjectInteraction()
    {
        if (_simulation is null)
        {
            return;
        }

        var presentObjects = _simulation.GetPresentObjects();
        if (presentObjects.Count == 0)
        {
            ShowFeedback("There are no interactive objects in this room.", isError: true);
            GD.Print($"[Object Inspection] No interactive objects found in this room.");
            return;
        }

        InteractiveObject targetObj = presentObjects[0];
        ObjectActionResult result = _simulation.InspectObject(targetObj.Id);
        if (!result.Succeeded && targetObj.IsLocked)
        {
            // Try to tamper / unlock
            result = _simulation.TamperObject(targetObj.Id, targetObj.RequiredKeyId);
        }

        GD.Print($"\n=== [INTERACTIVE OBJECT: {targetObj.DisplayName}] ===");
        GD.Print($"Status: {(result.Succeeded ? "SUCCESS" : "FAILED")} | Kind: {targetObj.Kind} | Locked: {targetObj.IsLocked}");
        GD.Print($"Message: {result.Message}");
        if (!string.IsNullOrEmpty(result.DiscoveredClue))
        {
            GD.Print($"Discovered Clue: {result.DiscoveredClue}");
        }
        ShowFeedback(result.Message, !result.Succeeded);
        GD.Print("===================================================\n");
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
        _possessedPlayerActor = BasementScenario.George;
        _selectedActorIndex = Actors.Length - 1;
        _simulation.PlayerController.SetPlayerEntity(_possessedPlayerActor);
        _lastMovements.Clear();
        _interactionSequence = 0;
        _completionLogged = false;
        _climaxResolution = null;
        _worldAdapter.SetMovementPaused(isPaused: false);
        _worldAdapter.SetMovementSpeed(1.0f);
        CloseBasementDoor();
        _worldAdapter.Synchronize(GetCoreLocations(), immediate: true);
        ShowFeedback("Session reset. Explore the hotel and gather evidence.");
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
            "Seed: {0} | Tick: {1} | State: {2} | Speed: x{3:0}\n" +
            "Live fingerprint: {4}…\n" +
            "Render: {13} FPS | Physics: {14} Hz | Interpolation: ON\n\n" +
            "🎮 Possessed (Player): {5}\n" +
            "👁️ Inspecting (Actor): {6}\n" +
            "Physical: {7} | Core: {8}\n" +
            "Movement: {9}\n" +
            "Memory: {10} episodic / {11} social\n{12}",
            _simulation.Seed,
            _simulation.CurrentTick,
            state,
            _simulation.Speed,
            fingerprint,
            _possessedPlayerActor.Value,
            selected.Value,
            physicalLocation,
            coreLocation.Value,
            movementText,
            episodic,
            social,
            suspicion,
            Engine.GetFramesPerSecond(),
            Engine.PhysicsTicksPerSecond));

        string recentEvents = string.Join(
            '\n',
            _simulation.Events
                .TakeLast(13)
                .Reverse()
                .Select(FormatEvent));
        _hud.SetEvents("LIVE WORLD EVENTS\n\n" +
            (recentEvents.Length == 0 ? "Waiting for simulation…" : recentEvents));
        _hud.SetObjective(GetObjectiveText());
        _hud.SetFeedback(_feedbackText, _feedbackIsError);

    }

    private string GetObjectiveText()
    {
        if (_climaxResolution is not null)
        {
            return $"ENDING — {_climaxResolution.Title} | Press R to start a new session";
        }

        AccusationCoalition? coalition = _simulation?.ActiveCoalition;
        if (coalition?.Stage == CoalitionStage.Confronting)
        {
            return "CLIMAX — Choose Z: Confess | X: Deny/Counter | C: Flee";
        }

        if (coalition?.ConsensusReached == true)
        {
            return "OBJECTIVE — Coalition consensus reached. Press K to begin the confrontation";
        }

        if (coalition is not null)
        {
            return "OBJECTIVE — A coalition is forming. Investigate, create evidence, then press K again";
        }

        return "OBJECTIVE — Explore 1-8, inspect O, talk T/Y, review J, then check conspiracy K";
    }

    private void ShowFeedback(string text, bool isError = false)
    {
        _feedbackText = text;
        _feedbackIsError = isError;
        _hud?.SetFeedback(text, isError);
    }

    private void RunSmokePlaythrough(double delta)
    {
        if (!_smokePlaythroughEnabled || _smokePlaythroughCompleted || _simulation is null)
        {
            return;
        }

        _smokeElapsedSeconds += delta;
        if (_smokeElapsedSeconds > 45.0)
        {
            FailSmokePlaythrough("Timed out before the simulation reached its playable-loop milestone.");
            return;
        }

        if (!_simulation.IsComplete || _worldAdapter.IsInTransit(_possessedPlayerActor))
        {
            return;
        }

        _smokePlaythroughCompleted = true;
        try
        {
            ExecuteObjectInteraction();
            ExecuteCheckConspiracy();
            if (!_simulation.CanResolveClimax(_possessedPlayerActor))
            {
                throw new InvalidOperationException("Coalition did not enter the confronting stage.");
            }

            ExecuteClimaxChoice(PlayerClimaxChoice.DenyAndCounter);
            if (_climaxResolution?.Choice != PlayerClimaxChoice.DenyAndCounter)
            {
                throw new InvalidOperationException("Climax resolution was not recorded.");
            }

            ExecuteQuickSave();
            string savePath = GetQuickSavePath();
            if (!File.Exists(savePath))
            {
                throw new IOException("Smoke QuickSave file was not created.");
            }

            ExecuteQuickLoad();
            if (_simulation.LastClimaxResolution?.Choice != PlayerClimaxChoice.DenyAndCounter ||
                _simulation.PlayerController.PlayerEntity != _possessedPlayerActor)
            {
                throw new InvalidDataException("QuickLoad did not preserve the ending or possessed actor.");
            }

            DeleteSmokeSave();
            GD.Print("PLAYABLE_LOOP_SMOKE_PASS");
            GetTree().Quit(exitCode: 0);
        }
        catch (Exception exception)
        {
            DeleteSmokeSave();
            FailSmokePlaythrough(exception.Message);
        }
    }

    private void FailSmokePlaythrough(string reason)
    {
        _smokePlaythroughCompleted = true;
        GD.PushError($"PLAYABLE_LOOP_SMOKE_FAIL: {reason}");
        GetTree().Quit(exitCode: 1);
    }

    private string GetQuickSavePath() => ProjectSettings.GlobalizePath(
        _smokePlaythroughEnabled ? "user://smoke-quicksave.json" : "user://quicksave.json");

    private void DeleteSmokeSave()
    {
        if (!_smokePlaythroughEnabled)
        {
            return;
        }

        string savePath = GetQuickSavePath();
        if (File.Exists(savePath))
        {
            File.Delete(savePath);
        }
    }

    private static bool IsRecoverableSnapshotException(Exception exception) =>
        exception is IOException or
            UnauthorizedAccessException or
            InvalidDataException or
            JsonException or
            ArgumentException or
            InvalidOperationException;

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
