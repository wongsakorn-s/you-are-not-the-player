using Game.Client.Godot.Adapters;
using Game.Client.Godot.Configuration;
using Game.Client.Godot.Presentation;
using Game.Client.Godot.World;
using Game.Sim.Actions;
using Game.Sim.Entities;
using Game.Sim.Events;
using Game.Sim.Locations;
using Game.Sim.Objects;
using Game.Sim.Player;
using Game.Sim.Routines;
using Game.Sim.Scenarios;
using Game.Sim.Suspicion;
using Godot;

namespace Game.Client.Godot.Scripts;

public sealed partial class Hotel2DPrototype : Node2D
{
    private const float MapLeft = 35.0f;
    private const float MapTop = 82.0f;
    private const float MapWidth = 850.0f;
    private const float MapHeight = 600.0f;

    private readonly Godot2DWorldAdapter _worldAdapter = new();
    private readonly Dictionary<EntityId, CharacterDefinition> _characters = [];
    private readonly Dictionary<LocationId, Vector2> _locationMarkers = [];

    private BasementRealtimeAdapter? _simulation;
    private HotelWorldDefinition? _hotel;
    private PlayableCaseDefinition? _caseDefinition;
    private Label? _clockLabel;
    private Label? _statusLabel;
    private Label? _eventLabel;
    private OptionButton? _actorSelector;
    private OptionButton? _objectSelector;
    private Button? _talkButton;
    private Button? _inspectButton;
    private InvestigationOverlay? _investigationOverlay;
    private IReadOnlyList<EntityId> _presentActors = [];
    private IReadOnlyList<InteractiveObject> _presentObjects = [];
    private EntityId _humanHost;
    private bool _smokeEnabled;
    private bool _smokeInvestigationCompleted;
    private bool _smokeMoveRequested;
    private bool _smokeMoveCompleted;
    private double _smokeElapsed;

    public override void _Ready()
    {
        _smokeEnabled = OS.GetCmdlineUserArgs().Contains(
            "--smoke-2d",
            StringComparer.OrdinalIgnoreCase);

        _hotel = LoadHotelDefinition();
        CharacterCatalogDefinition catalog = LoadCharacterCatalog();
        _caseDefinition = LoadCaseDefinition();
        _caseDefinition.ValidateReferences(catalog, _hotel);
        _humanHost = new EntityId(_caseDefinition.HumanHost);

        foreach (CharacterDefinition character in catalog.Characters)
        {
            _characters.Add(new EntityId(character.Id), character);
        }

        BuildPresentation();
        _simulation = CreateSimulation(_caseDefinition.Seed);
        _simulation.PlayerController.SetPlayerEntity(_humanHost);
        BuildCharacterTokens();

        _worldAdapter.LocationConfirmed += OnLocationConfirmed;
        _worldAdapter.Synchronize(GetCoreLocations(), immediate: true);
        RefreshStatus("Click a room to move George. The other characters follow the simulation.");
        RefreshContextActions();
        GD.Print(
            $"HOTEL_2D_READY case={_caseDefinition.CaseId} rooms={_hotel.Locations.Length} " +
            $"actors={_characters.Count} host={_humanHost.Value}");
    }

    public override void _Process(double delta)
    {
        if (_simulation?.Update(delta) == true)
        {
            HandleSimulationChanges();
        }

        if (_clockLabel is not null && _simulation is not null)
        {
            _clockLabel.Text = $"SIM TIME  00:{_simulation.CurrentTick:00}   |   PHASE  {_simulation.Phase}";
        }

        RunSmokeTest(delta);
    }

    private void BuildPresentation()
    {
        if (_hotel is null || _caseDefinition is null)
        {
            throw new InvalidOperationException("Content must be loaded before building the presentation.");
        }

        var background = new ColorRect
        {
            Color = new Color("10131a"),
            Position = Vector2.Zero,
            Size = new Vector2(1280.0f, 720.0f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = -10,
        };
        AddChild(background);

        AddLabel(
            _caseDefinition.Title.ToUpperInvariant(),
            new Vector2(35.0f, 20.0f),
            new Vector2(850.0f, 42.0f),
            26,
            new Color("f1d18a"));

        _clockLabel = AddLabel(
            "SIM TIME  00:00",
            new Vector2(930.0f, 24.0f),
            new Vector2(315.0f, 34.0f),
            17,
            new Color("b8c2d8"));

        BuildPortalLines();
        foreach (HotelLocationDefinition location in _hotel.Locations)
        {
            Vector2 marker = ToScreenPosition(location.Marker);
            var locationId = new LocationId(location.Id);
            _locationMarkers.Add(locationId, marker);
            _worldAdapter.RegisterLocation(locationId, marker);

            Vector2 buttonSize = location.Id == "garden"
                ? new Vector2(230.0f, 58.0f)
                : new Vector2(172.0f, 58.0f);
            var button = new Button
            {
                Name = $"Room-{location.Id}",
                Text = location.Restricted
                    ? $"{location.DisplayName}\n[RESTRICTED]"
                    : location.DisplayName,
                Position = marker - (buttonSize / 2.0f),
                Size = buttonSize,
                TooltipText = $"Move {_caseDefinition.HumanHost} to {location.DisplayName}",
            };
            button.AddThemeColorOverride("font_color", Colors.White);
            button.AddThemeColorOverride("font_hover_color", Colors.White);
            button.AddThemeFontSizeOverride("font_size", 13);
            Color roomColor = new(location.Color);
            var normalStyle = new StyleBoxFlat
            {
                BgColor = roomColor.Darkened(0.28f),
                BorderColor = roomColor.Lightened(0.25f),
                BorderWidthLeft = 2,
                BorderWidthTop = 2,
                BorderWidthRight = 2,
                BorderWidthBottom = 2,
                CornerRadiusTopLeft = 6,
                CornerRadiusTopRight = 6,
                CornerRadiusBottomLeft = 6,
                CornerRadiusBottomRight = 6,
            };
            var hoverStyle = (StyleBoxFlat)normalStyle.Duplicate();
            hoverStyle.BgColor = roomColor.Darkened(0.12f);
            button.AddThemeStyleboxOverride("normal", normalStyle);
            button.AddThemeStyleboxOverride("hover", hoverStyle);
            button.Pressed += () => ExecutePlayerMove(locationId);
            AddChild(button);
        }

        var panel = new ColorRect
        {
            Color = new Color("1b2130"),
            Position = new Vector2(910.0f, 76.0f),
            Size = new Vector2(340.0f, 606.0f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        AddChild(panel);

        AddLabel("YOUR ROLE", new Vector2(935.0f, 100.0f), new Vector2(290.0f, 30.0f), 15, new Color("8091b3"));
        CharacterDefinition host = _characters.GetValueOrDefault(_humanHost) ??
            throw new InvalidOperationException($"Human host '{_humanHost}' is missing.");
        AddLabel(
            $"{host.DisplayName}\n{host.Role}",
            new Vector2(935.0f, 132.0f),
            new Vector2(290.0f, 64.0f),
            22,
            new Color(host.Color));
        AddLabel("OBJECTIVE", new Vector2(935.0f, 215.0f), new Vector2(290.0f, 30.0f), 15, new Color("8091b3"));
        AddLabel(
            _caseDefinition.Objective,
            new Vector2(935.0f, 248.0f),
            new Vector2(290.0f, 105.0f),
            16,
            Colors.White,
            autowrap: true);
        AddLabel("CASE FEED", new Vector2(935.0f, 345.0f), new Vector2(290.0f, 30.0f), 15, new Color("8091b3"));
        _eventLabel = AddLabel(
            "No witnessed event yet.",
            new Vector2(935.0f, 375.0f),
            new Vector2(290.0f, 62.0f),
            15,
            new Color("cbd5e1"),
            autowrap: true);
        _statusLabel = AddLabel(
            string.Empty,
            new Vector2(935.0f, 438.0f),
            new Vector2(290.0f, 48.0f),
            14,
            new Color("f1d18a"),
            autowrap: true);

        AddLabel("INVESTIGATE", new Vector2(935.0f, 492.0f), new Vector2(290.0f, 26.0f), 15, new Color("8091b3"));
        _actorSelector = new OptionButton
        {
            Position = new Vector2(935.0f, 520.0f),
            Size = new Vector2(180.0f, 36.0f),
            TooltipText = "Characters currently in the same room",
        };
        AddChild(_actorSelector);
        _talkButton = AddActionButton(
            "TALK",
            new Vector2(1122.0f, 520.0f),
            new Vector2(103.0f, 36.0f),
            OpenSelectedConversation);

        _objectSelector = new OptionButton
        {
            Position = new Vector2(935.0f, 562.0f),
            Size = new Vector2(180.0f, 36.0f),
            TooltipText = "Objects currently in the same room",
        };
        AddChild(_objectSelector);
        _inspectButton = AddActionButton(
            "INSPECT",
            new Vector2(1122.0f, 562.0f),
            new Vector2(103.0f, 36.0f),
            InspectSelectedObject);
        _ = AddActionButton(
            "OPEN DETECTIVE JOURNAL",
            new Vector2(935.0f, 612.0f),
            new Vector2(290.0f, 44.0f),
            OpenJournal);

        _investigationOverlay = new InvestigationOverlay();
        _investigationOverlay.Closed += OnInvestigationOverlayClosed;
        AddChild(_investigationOverlay);
    }

    private void BuildPortalLines()
    {
        if (_hotel is null)
        {
            return;
        }

        foreach (HotelPortalDefinition portal in _hotel.Portals)
        {
            HotelLocationDefinition from = _hotel.Locations.Single(location => location.Id == portal.From);
            HotelLocationDefinition to = _hotel.Locations.Single(location => location.Id == portal.To);
            var line = new Line2D
            {
                Points = [ToScreenPosition(from.Marker), ToScreenPosition(to.Marker)],
                Width = portal.RequiresAccess ? 4.0f : 2.0f,
                DefaultColor = portal.RequiresAccess
                    ? new Color("a44747")
                    : new Color("3b465d"),
                ZIndex = -5,
            };
            AddChild(line);
        }
    }

    private void BuildCharacterTokens()
    {
        int index = 0;
        foreach ((EntityId actor, CharacterDefinition definition) in _characters)
        {
            var token = new CharacterToken2D { ZIndex = 10 };
            token.Initialize(actor.Value, definition.DisplayName, new Color(definition.Color));
            AddChild(token);

            Vector2 offset = new(
                -54.0f + ((index % 3) * 54.0f),
                32.0f + ((index / 3) * 24.0f));
            _worldAdapter.RegisterActor(actor, token, offset);
            index++;
        }
    }

    private void HandleSimulationChanges()
    {
        if (_simulation is null)
        {
            return;
        }

        foreach (WorldEvent worldEvent in _simulation.DrainNewEvents())
        {
            if (_eventLabel is not null)
            {
                _eventLabel.Text =
                    $"T{worldEvent.Time.Tick:00}  {DisplayName(worldEvent.Actor)}\n" +
                    $"{worldEvent.Type} @ {worldEvent.Location.Value}";
            }
        }

        foreach (MovementSnapshot movement in _simulation.DrainNewMovements())
        {
            _worldAdapter.RequestMove(movement.Actor, movement.Destination);
        }

        RefreshContextActions();
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

        _ = _simulation.CompleteMovement(pending.RequestId);
        RefreshStatus($"{DisplayName(actor)} arrived at {DisplayLocation(location)}.");
        if (_smokeEnabled && actor == _humanHost && location == new LocationId("garden"))
        {
            _smokeMoveCompleted = true;
        }

        HandleSimulationChanges();
    }

    private void RefreshContextActions()
    {
        if (_simulation is null ||
            _actorSelector is null ||
            _objectSelector is null ||
            _talkButton is null ||
            _inspectButton is null)
        {
            return;
        }

        _simulation.PlayerController.SetPlayerEntity(_humanHost);
        _presentActors = _simulation.PlayerController.GetPresentActors();
        _presentObjects = _simulation.GetPresentObjects();

        _actorSelector.Clear();
        foreach (EntityId actor in _presentActors)
        {
            _actorSelector.AddItem(DisplayName(actor));
        }

        bool hasActors = _presentActors.Count > 0;
        _actorSelector.Disabled = !hasActors;
        _talkButton.Disabled = !hasActors;
        if (!hasActors)
        {
            _actorSelector.AddItem("Nobody is here");
        }

        _objectSelector.Clear();
        foreach (InteractiveObject obj in _presentObjects)
        {
            _objectSelector.AddItem(obj.DisplayName);
        }

        bool hasObjects = _presentObjects.Count > 0;
        _objectSelector.Disabled = !hasObjects;
        _inspectButton.Disabled = !hasObjects;
        if (!hasObjects)
        {
            _objectSelector.AddItem("Nothing to inspect");
        }
    }

    private void OpenSelectedConversation()
    {
        if (_actorSelector is null || _presentActors.Count == 0)
        {
            return;
        }

        int selected = Math.Clamp(_actorSelector.Selected, 0, _presentActors.Count - 1);
        OpenConversation(_presentActors[selected]);
    }

    private void OpenConversation(EntityId partner)
    {
        if (_simulation is null || _investigationOverlay is null)
        {
            return;
        }

        var choices = new List<InvestigationChoice>
        {
            new("Ask about their schedule", () => ExecuteDialogue(
                partner,
                new DialogueRequest(
                    DialogueActionKind.InquireSchedule,
                    _humanHost,
                    partner))),
        };

        foreach (EntityId subject in _characters.Keys
                     .Where(actor => actor != partner && actor != _humanHost)
                     .Take(3))
        {
            EntityId selectedSubject = subject;
            choices.Add(new InvestigationChoice(
                $"Ask about {DisplayName(selectedSubject)}",
                () => ExecuteDialogue(
                    partner,
                    new DialogueRequest(
                        DialogueActionKind.AskAboutSubject,
                        _humanHost,
                        partner,
                        subject: selectedSubject))));
        }

        if (_presentObjects.Count > 0)
        {
            InteractiveObject objectToAsk = _presentObjects[0];
            choices.Add(new InvestigationChoice(
                $"Ask about {objectToAsk.DisplayName}",
                () => ExecuteObjectInquiry(partner, objectToAsk)));
        }

        IReadOnlyList<PlayerJournalEntry> entries = _simulation.GetPlayerJournal(_humanHost).Entries;
        PlayerJournalEntry? evidence = entries.Count > 0 ? entries[0] : null;
        if (evidence is not null)
        {
            choices.Add(new InvestigationChoice(
                "Confront with latest evidence",
                () => ExecuteEvidenceConfrontation(partner, evidence)));
        }

        ShowInvestigationScreen(
            DisplayName(partner),
            $"{DisplayName(partner)} waits for your question. Choose what George should ask.",
            new Color(_characters[partner].Color),
            choices);
    }

    private void ExecuteDialogue(EntityId partner, DialogueRequest request)
    {
        if (_simulation is null)
        {
            return;
        }

        DialogueOutcome outcome = _simulation.Talk(request);
        ShowDialogueOutcome(partner, outcome);
    }

    private void ExecuteObjectInquiry(EntityId partner, InteractiveObject obj)
    {
        if (_simulation is null)
        {
            return;
        }

        DialogueOutcome outcome = _simulation.InquireObject(partner, obj.Id);
        ShowDialogueOutcome(partner, outcome);
    }

    private void ExecuteEvidenceConfrontation(EntityId partner, PlayerJournalEntry evidence)
    {
        if (_simulation is null)
        {
            return;
        }

        DialogueOutcome outcome = _simulation.ConfrontWithEvidence(partner, evidence.Id);
        ShowDialogueOutcome(partner, outcome);
    }

    private void ShowDialogueOutcome(EntityId partner, DialogueOutcome outcome)
    {
        string body = outcome.Succeeded
            ? LocalizeText(outcome.Text)
            : outcome.FailureReason ?? "The conversation could not continue.";
        ShowInvestigationScreen(
            DisplayName(partner),
            body,
            new Color(_characters[partner].Color),
            [new InvestigationChoice("Ask something else", () => OpenConversation(partner))]);
        RefreshStatus(outcome.Succeeded
            ? $"Conversation with {DisplayName(partner)} recorded."
            : body);
        HandleSimulationChanges();
    }

    private void InspectSelectedObject()
    {
        if (_simulation is null || _objectSelector is null || _presentObjects.Count == 0)
        {
            return;
        }

        int selected = Math.Clamp(_objectSelector.Selected, 0, _presentObjects.Count - 1);
        InteractiveObject obj = _presentObjects[selected];
        ObjectActionResult result = _simulation.InspectObject(obj.Id);
        string body = result.DiscoveredClue is null
            ? result.Message
            : $"{result.Message}\n\nCLUE RECORDED\n{result.DiscoveredClue}";
        ShowInvestigationScreen(
            "INVESTIGATION",
            body,
            result.Succeeded ? new Color("77dd77") : new Color("e06c75"));
        RefreshStatus(result.Succeeded
            ? $"Inspected {obj.DisplayName}."
            : result.Message);
        HandleSimulationChanges();
    }

    private void OpenJournal()
    {
        if (_simulation is null)
        {
            return;
        }

        PlayerJournal journal = _simulation.GetPlayerJournal(_humanHost);
        string body = JournalPresentationFormatter.Format(journal, DisplayName, DisplayLocation);
        ShowInvestigationScreen(
            "DETECTIVE JOURNAL",
            body,
            new Color("77bdfb"));
    }

    private void ShowInvestigationScreen(
        string title,
        string body,
        Color accent,
        IReadOnlyList<InvestigationChoice>? choices = null)
    {
        if (_simulation is null || _investigationOverlay is null)
        {
            return;
        }

        _simulation.SetPaused(true);
        _worldAdapter.SetMovementPaused(true);
        _investigationOverlay.ShowScreen(title, body, accent, choices);
    }

    private void OnInvestigationOverlayClosed()
    {
        if (_simulation is null || _smokeEnabled)
        {
            return;
        }

        _simulation.SetPaused(false);
        _worldAdapter.SetMovementPaused(false);
        RefreshContextActions();
    }

    private string LocalizeText(string text)
    {
        string localized = text;
        foreach ((EntityId actor, CharacterDefinition character) in _characters)
        {
            localized = localized.Replace(
                actor.Value,
                character.DisplayName,
                StringComparison.OrdinalIgnoreCase);
        }

        return localized;
    }

    private void ExecutePlayerMove(LocationId destination)
    {
        if (_simulation is null)
        {
            return;
        }

        _simulation.PlayerController.SetPlayerEntity(_humanHost);
        NpcMovementExecution execution = _simulation.PlayerMove(destination);
        if (execution.Status == NpcMovementExecutionStatus.Pending && execution.Movement is not null)
        {
            _worldAdapter.RequestMove(_humanHost, destination);
            RefreshStatus($"Moving {DisplayName(_humanHost)} to {DisplayLocation(destination)}...");
        }
        else if (execution.Status == NpcMovementExecutionStatus.Failed)
        {
            RefreshStatus($"No accessible route to {DisplayLocation(destination)}.");
        }

        HandleSimulationChanges();
    }

    private void RunSmokeTest(double delta)
    {
        if (!_smokeEnabled || _simulation is null || _hotel is null)
        {
            return;
        }

        _smokeElapsed += delta;
        if (!_smokeInvestigationCompleted && !RunInvestigationSmoke())
        {
            return;
        }

        if (!_smokeMoveRequested)
        {
            _smokeMoveRequested = true;
            _simulation.SetPaused(true);
            _worldAdapter.SetMovementSpeed(10.0f);
            ExecutePlayerMove(new LocationId("garden"));
        }

        if (_smokeMoveCompleted)
        {
            GD.Print(
                $"HOTEL_2D_SMOKE_PASS rooms={_hotel.Locations.Length} " +
                $"actors={_characters.Count} host={_humanHost.Value} " +
                "dialogue=pass inspect=pass journal=pass");
            GetTree().Quit(0);
        }
        else if (_smokeElapsed >= 8.0)
        {
            GD.PushError("HOTEL_2D_SMOKE_FAIL movement did not complete within 8 seconds");
            GetTree().Quit(1);
        }
    }

    private bool RunInvestigationSmoke()
    {
        if (_simulation is null || _investigationOverlay is null)
        {
            return false;
        }

        try
        {
            _simulation.PlayerController.SetPlayerEntity(_humanHost);
            IReadOnlyList<EntityId> actors = _simulation.PlayerController.GetPresentActors();
            IReadOnlyList<InteractiveObject> objects = _simulation.GetPresentObjects();
            if (actors.Count == 0 || objects.Count == 0)
            {
                throw new InvalidOperationException(
                    "Opening location must contain an actor and an interactive object.");
            }

            DialogueOutcome dialogue = _simulation.Talk(new DialogueRequest(
                DialogueActionKind.InquireSchedule,
                _humanHost,
                actors[0]));
            ObjectActionResult inspection = _simulation.InspectObject(objects[0].Id);
            PlayerJournal journal = _simulation.GetPlayerJournal(_humanHost);
            string journalText = JournalPresentationFormatter.Format(
                journal,
                DisplayName,
                DisplayLocation);
            if (!dialogue.Succeeded || !inspection.Succeeded || string.IsNullOrWhiteSpace(journalText))
            {
                throw new InvalidOperationException(
                    "Investigation smoke validation did not produce a valid result.");
            }

            _investigationOverlay.ShowScreen(
                "SMOKE JOURNAL",
                journalText,
                new Color("77bdfb"));
            if (!_investigationOverlay.IsOpen)
            {
                throw new InvalidOperationException("Investigation overlay did not open.");
            }

            _investigationOverlay.HideScreen();
            HandleSimulationChanges();
            _smokeInvestigationCompleted = true;
            return true;
        }
        catch (Exception error)
        {
            GD.PushError($"HOTEL_2D_SMOKE_FAIL investigation: {error.Message}");
            GetTree().Quit(1);
            return false;
        }
    }

    private Dictionary<EntityId, LocationId> GetCoreLocations() =>
        _characters.Keys.ToDictionary(actor => actor, actor => _simulation!.GetLogicalLocation(actor));

    private string DisplayName(EntityId actor) =>
        _characters.TryGetValue(actor, out CharacterDefinition? character)
            ? character.DisplayName
            : actor.Value;

    private string DisplayLocation(LocationId location) =>
        _hotel?.Locations.SingleOrDefault(item => item.Id == location.Value)?.DisplayName ??
        location.Value;

    private void RefreshStatus(string message)
    {
        if (_statusLabel is not null)
        {
            _statusLabel.Text = message;
        }
    }

    private Label AddLabel(
        string text,
        Vector2 position,
        Vector2 size,
        int fontSize,
        Color color,
        bool autowrap = false)
    {
        var label = new Label
        {
            Text = text,
            Position = position,
            Size = size,
            AutowrapMode = autowrap
                ? TextServer.AutowrapMode.WordSmart
                : TextServer.AutowrapMode.Off,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        AddChild(label);
        return label;
    }

    private Button AddActionButton(
        string text,
        Vector2 position,
        Vector2 size,
        Action pressed)
    {
        var button = new Button
        {
            Text = text,
            Position = position,
            Size = size,
        };
        button.Pressed += pressed;
        AddChild(button);
        return button;
    }

    private Vector2 ToScreenPosition(WorldPoint point)
    {
        if (_hotel is null)
        {
            throw new InvalidOperationException("Hotel definition is not loaded.");
        }

        NavigationSurfaceDefinition bounds = _hotel.Navigation;
        float normalizedX = (point.X - bounds.MinimumX) / (bounds.MaximumX - bounds.MinimumX);
        float normalizedY = (bounds.MaximumZ - point.Z) / (bounds.MaximumZ - bounds.MinimumZ);
        return new Vector2(
            MapLeft + (normalizedX * MapWidth),
            MapTop + (normalizedY * MapHeight));
    }

    private static BasementRealtimeAdapter CreateSimulation(ulong seed)
    {
        InMemorySuspicionRuleRepository rules = JsonSuspicionRuleParser.Parse(
            File.ReadAllText(ResolveContentPath("SuspicionRules", "mvp.json")));
        BasementScenarioSession session = new BasementScenario(rules).CreateSession(
            new BasementScenarioOptions(seed, ticks: 16));
        return new BasementRealtimeAdapter(session);
    }

    private static HotelWorldDefinition LoadHotelDefinition() =>
        HotelWorldDefinitionParser.Parse(
            File.ReadAllText(ResolveContentPath("Hotel", "hotel-world.json")));

    private static CharacterCatalogDefinition LoadCharacterCatalog() =>
        CharacterCatalogDefinitionParser.Parse(
            File.ReadAllText(ResolveContentPath("Characters", "characters.json")));

    private static PlayableCaseDefinition LoadCaseDefinition() =>
        PlayableCaseDefinitionParser.Parse(
            File.ReadAllText(ResolveContentPath("Cases", "first-playable-case.json")));

    private static string ResolveContentPath(string directory, string fileName)
    {
        string outputPath = Path.Combine(AppContext.BaseDirectory, "Data", directory, fileName);
        if (File.Exists(outputPath))
        {
            return outputPath;
        }

        string sourcePath = Path.GetFullPath(Path.Combine(
            ProjectSettings.GlobalizePath("res://"),
            "..",
            "Game.Content",
            "Data",
            directory,
            fileName));
        return File.Exists(sourcePath)
            ? sourcePath
            : throw new FileNotFoundException(
                $"Content file '{directory}/{fileName}' was not found.",
                sourcePath);
    }
}
