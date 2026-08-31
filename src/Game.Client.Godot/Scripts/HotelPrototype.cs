using System.Globalization;
using Game.Client.Godot.Adapters;
using Game.Client.Godot.Audio;
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
    private readonly Dictionary<string, InteractiveObjectNode> _objectNodes = [];
    private BasementRealtimeAdapter? _simulation;
    private HotelWorldDefinition? _hotel;
    private DebugHud? _hud;
    private HotelAudioController? _audioController;
    private RestrictedDoorNode? _basementDoor;
    private EntityId _possessedPlayerActor = BasementScenario.George;
    private int _selectedActorIndex;
    private int _interactionSequence;
    private bool _completionLogged;

    public override void _Ready()
    {
        _hotel = LoadHotelDefinition();
        BuildEnvironment(_hotel);
        _simulation = CreateSimulation();
        BuildActorViews();

        _audioController = new HotelAudioController();
        AddChild(_audioController);

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
            case Key.P:
                _possessedPlayerActor = Actors[_selectedActorIndex];
                _simulation.PlayerController.SetPlayerEntity(_possessedPlayerActor);
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
            GD.Print($"[Player Controller] Moving {_possessedPlayerActor.Value} to {destination.Value}...");
        }
        else if (execution.Status == NpcMovementExecutionStatus.Failed)
        {
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

        _audioController?.PlayDialogueChime();
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

        _audioController?.PlayDialogueChime();
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
                _audioController?.PlayClimaxAlert();
                GD.Print($"\n[!!!] CLIMAX TRIGGERED: The coalition has gathered in the Lobby to confront you!");
                GD.Print($"Press [Z] to Confess Reality, [X] to Deny/Counter-Accuse, [C] to Flee!");
                _simulation.TriggerConfrontation(BasementScenario.Lobby);
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

        _audioController?.PlayDialogueChime();
        ClimaxResolution resolution = _simulation.ResolveClimax(choice, _possessedPlayerActor);
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

        string savePath = ProjectSettings.GlobalizePath("user://quicksave.json");
        SessionSnapshot snapshot = _simulation.CaptureSnapshot();
        SessionSnapshotSerializer.SaveToFile(snapshot, savePath);
        GD.Print($"[QuickSave] Snapshot saved to {savePath} (Tick: {snapshot.Metadata.CurrentTick}, Phase: {snapshot.Metadata.Phase})");
    }

    private void ExecuteQuickLoad()
    {
        string savePath = ProjectSettings.GlobalizePath("user://quicksave.json");
        if (!File.Exists(savePath))
        {
            GD.PrintErr($"[QuickLoad] No quicksave found at {savePath}");
            return;
        }

        SessionSnapshot snapshot = SessionSnapshotSerializer.LoadFromFile(savePath);
        string rulesPath = ResolveRulesPath();
        InMemorySuspicionRuleRepository rules = JsonSuspicionRuleParser.Parse(File.ReadAllText(rulesPath));

        _simulation = BasementRealtimeAdapter.FromSnapshot(snapshot, rules);
        _lastMovements.Clear();
        _worldAdapter.SetMovementPaused(_simulation.IsPaused);
        _worldAdapter.SetMovementSpeed(_simulation.Speed);
        if (_simulation.GetLogicalLocation(BasementScenario.George) == BasementScenario.Basement ||
            _simulation.GetLogicalLocation(BasementScenario.Anna) == BasementScenario.Basement)
        {
            OpenBasementDoor();
        }
        else
        {
            CloseBasementDoor();
        }

        _worldAdapter.Synchronize(GetCoreLocations(), immediate: true);
        _simulation.TriggerSaveReloadAnomaly(_possessedPlayerActor);
        _audioController?.PlayAnomalyWarp();
        GD.Print($"[QuickLoad] Restored session from {savePath} (Tick: {snapshot.Metadata.CurrentTick}, Phase: {snapshot.Metadata.Phase})");
        GD.Print($"[REALITY ANOMALY] NPCs in the room sense an unnatural temporal shift (Déjà Vu)!");
        HandleSimulationChanges();
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

        if (_objectNodes.TryGetValue(targetObj.Id, out InteractiveObjectNode? objNode))
        {
            objNode.PlayInteractionSound(targetObj.Kind);
        }

        GD.Print($"\n=== [INTERACTIVE OBJECT: {targetObj.DisplayName}] ===");
        GD.Print($"Status: {(result.Succeeded ? "SUCCESS" : "FAILED")} | Kind: {targetObj.Kind} | Locked: {targetObj.IsLocked}");
        GD.Print($"Message: {result.Message}");
        if (!string.IsNullOrEmpty(result.DiscoveredClue))
        {
            GD.Print($"Discovered Clue: {result.DiscoveredClue}");
        }
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
            "Seed: {0} | Tick: {1} | State: {2} | Speed: x{3:0}\n" +
            "Live fingerprint: {4}…\n\n" +
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
            suspicion));

        string recentEvents = string.Join(
            '\n',
            _simulation.Events
                .TakeLast(13)
                .Reverse()
                .Select(FormatEvent));
        _hud.SetEvents("LIVE WORLD EVENTS\n\n" +
            (recentEvents.Length == 0 ? "Waiting for simulation…" : recentEvents));

        UpdateNpcVisualEmotions();
    }

    private void UpdateNpcVisualEmotions()
    {
        if (_simulation is null)
        {
            return;
        }

        AccusationCoalition? coalition = _simulation.EvaluateConspiracy(_possessedPlayerActor);

        for (int i = 0; i < Actors.Length; i++)
        {
            EntityId actor = Actors[i];
            NpcActorNode? view = _worldAdapter.GetActorView(actor);
            if (view is null)
            {
                continue;
            }

            if (actor == _possessedPlayerActor)
            {
                view.SetEmotionBubble("🎮 PLAYER", new Color(0.2f, 0.9f, 1.0f));
            }
            else if (coalition is not null && coalition.Members.Contains(actor))
            {
                view.SetEmotionBubble("👥 COALITION", new Color(1.0f, 0.35f, 0.2f));
            }
            else if (_simulation.GetMemories(actor).Any(m => m.EventType == EventType.RealityAnomaly))
            {
                view.SetEmotionBubble("⚡ DÉJÀ VU", new Color(0.85f, 0.4f, 1.0f));
            }
            else if (_simulation.GetSuspicion(actor, _possessedPlayerActor).Evidence.Count > 0)
            {
                view.SetEmotionBubble("❓ SUSPICIOUS", new Color(1.0f, 0.85f, 0.2f));
            }
            else
            {
                view.ClearEmotionBubble();
            }
        }

        foreach ((string objId, InteractiveObjectNode node) in _objectNodes)
        {
            InteractiveObject? obj = _simulation.Objects.GetObject(objId);
            if (obj is not null)
            {
                node.UpdateState(obj);
            }
        }
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

        HotelObjectRegistry defaultObjects = HotelObjectRegistry.CreateDefaultHotelObjects();
        foreach (InteractiveObject obj in defaultObjects.AllObjects)
        {
            HotelLocationDefinition? locDef = hotel.Locations.FirstOrDefault(l => l.Id == obj.Location.Value);
            if (locDef is not null)
            {
                Vector3 objPos = new(
                    locDef.Marker.X - 1.6f,
                    locDef.Marker.Y + 0.3f,
                    locDef.Marker.Z - 1.2f);

                var objNode = new InteractiveObjectNode();
                objNode.Initialize(obj, objPos);
                AddChild(objNode);
                _objectNodes[obj.Id] = objNode;
            }
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
