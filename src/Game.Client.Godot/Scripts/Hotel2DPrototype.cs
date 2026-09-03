using Game.Client.Godot.Adapters;
using Game.Client.Godot.Configuration;
using Game.Client.Godot.Gameplay;
using Game.Client.Godot.Presentation;
using Game.Client.Godot.World;
using Game.Sim.Actions;
using Game.Sim.Cases;
using Game.Sim.Conspiracy;
using Game.Sim.Entities;
using Game.Sim.Events;
using Game.Sim.Locations;
using Game.Sim.Memory;
using Game.Sim.Objects;
using Game.Sim.Player;
using Game.Sim.Routines;
using Game.Sim.Scenarios;
using Game.Sim.Suspicion;
using Godot;

namespace Game.Client.Godot.Scripts;

/// <summary>Which slice of the case file the player is currently reading.</summary>
public enum JournalView
{
    All,
    LastThirtyMinutes,
    CurrentRoom,
    SeenFirstHand,
    HeardFromOthers,
}

public sealed partial class Hotel2DPrototype : Node2D
{
    private const float MapLeft = 35.0f;
    private const float MapTop = 120.0f;
    private const float MapWidth = 800.0f;
    private const float MapHeight = 500.0f;

    private readonly Godot2DWorldAdapter _worldAdapter = new();
    private readonly Dictionary<EntityId, CharacterDefinition> _characters = [];
    private readonly Dictionary<LocationId, Vector2> _locationMarkers = [];
    private readonly Dictionary<LocationId, Label> _roomLabels = [];
    private readonly List<WorldEvent> _eventHistory = [];
    private readonly List<ShiftBeat> _shiftHistory = [];
    private readonly Dictionary<string, Button> _roomButtons = [];
    private readonly Dictionary<EntityId, CharacterToken2D> _characterTokens = [];
    private readonly Dictionary<EntityId, (string English, string Thai)> _npcActivities = [];

    private BasementRealtimeAdapter? _simulation;
    private NightShiftDirector? _shiftDirector;
    private HotelWorldDefinition? _hotel;
    private PlayableCaseDefinition? _caseDefinition;
    private DialogueNarrativeFormatter? _dialogueFormatter;
    private SessionTruth? _truth;
    private Label? _exposureHeadingLabel;
    private Label? _exposureLabel;
    private ExposureLevel _lastExposureLevel = ExposureLevel.Unnoticed;
    private ExposureReport? _exposure;
    private CoalitionStage? _lastCoalitionStage;
    private long? _confrontationTick;
    private bool _climaxOpen;
    private Label? _clockLabel;
    private Label? _statusLabel;
    private Label? _eventLabel;
    private Label? _currentLocationLabel;
    private Label? _contextLabel;
    private Label? _caseTitleLabel;
    private Label? _instructionLabel;
    private Label? _roleLabel;
    private Label? _roleValueLabel;
    private Label? _objectiveHeadingLabel;
    private Label? _objectiveLabel;
    private Label? _caseFeedLabel;
    private Label? _progressHeadingLabel;
    private Label? _progressLabel;
    private Label? _alertLabel;
    private ColorRect? _alertPanel;
    private ColorRect? _atmosphereOverlay;
    private OptionButton? _actorSelector;
    private OptionButton? _objectSelector;
    private Button? _talkButton;
    private Button? _followButton;
    private Button? _inspectButton;
    private Button? _evidenceButton;
    private Button? _journalButton;
    private Button? _languageButton;
    private Button? _insightButton;
    private Button? _deduceButton;
    private InvestigationOverlay? _investigationOverlay;
    private IReadOnlyList<EntityId> _presentActors = [];
    private IReadOnlyList<InteractiveObject> _presentObjects = [];
    private EntityId? _followTarget;
    private LocationId? _followDestination;
    private EntityId? _selectedActor;
    private EntityId _humanHost;
    private bool _smokeEnabled;
    private bool _smokeInvestigationCompleted;
    private bool _smokeMoveRequested;
    private bool _smokeMoveCompleted;
    private double _smokeElapsed;
    private bool _isThai;
    private bool _gameStarted;
    private bool _gameEnded;
    private bool _largeText;
    private bool _deductionOpen;
    private bool _insightVisible;
    private long _alertHideTick;
    private bool _hasMoved;
    private bool _hasTalked;
    private bool _hasInspected;
    private bool _hasOpenedJournal;
    private bool _hasConfronted;
    private bool _hasConcluded;

    private bool _captureEnabled;
    private int _captureFrames;

    public override void _Ready()
    {
        _captureEnabled = OS.GetCmdlineUserArgs().Contains(
            "--capture-ui",
            StringComparer.OrdinalIgnoreCase);
        _smokeEnabled = OS.GetCmdlineUserArgs().Contains(
            "--smoke-2d",
            StringComparer.OrdinalIgnoreCase);
        _isThai = OS.GetCmdlineUserArgs().Contains(
            "--thai",
            StringComparer.OrdinalIgnoreCase) ||
            string.Equals(OS.GetLocaleLanguage(), "th", StringComparison.OrdinalIgnoreCase);

        _hotel = LoadHotelDefinition();
        CharacterCatalogDefinition catalog = LoadCharacterCatalog();
        DialogueCatalogDefinition dialogueCatalog = LoadDialogueCatalog();
        _caseDefinition = LoadCaseDefinition();
        _caseDefinition.ValidateReferences(catalog, _hotel);
        _humanHost = new EntityId(_caseDefinition.HumanHost);
        _dialogueFormatter = new DialogueNarrativeFormatter(dialogueCatalog);

        foreach (CharacterDefinition character in catalog.Characters)
        {
            _characters.Add(new EntityId(character.Id), character);
        }

        BuildPresentation();

        // The seed, not the content file, decides who is really being steered.
        // A replay hands us a fresh seed, so the same hotel poses a new question.
        ulong seed = _replaySeed ?? _caseDefinition.Seed;
        _truth = CaseGenerator.Generate(seed, new CaseGenerationOptions(
            _humanHost,
            _characters.Keys,
            NightShiftDirector.DeadlineTick,
            pinnedHiddenPlayer: ToEntityId(_caseDefinition.HiddenPlayer),
            pinnedIncidentCulprit: ToEntityId(_caseDefinition.IncidentCulprit),
            pinnedArchetype: _caseDefinition.ParsedPlayerArchetype));
        _simulation = CreateSimulation(seed, _truth);
        _shiftDirector = new NightShiftDirector(seed);
        _simulation.PlayerController.SetPlayerEntity(_humanHost);
        InitializeNpcActivities();
        BuildCharacterTokens();

        _worldAdapter.LocationConfirmed += OnLocationConfirmed;
        _worldAdapter.Synchronize(GetCoreLocations(), immediate: true);
        RefreshStatus(
            T(
                "Click a room to move George. The other characters follow the simulation.",
                "คลิกห้องเพื่อย้ายจอร์จ ตัวละครอื่นจะเคลื่อนไหวตามจำลอง"));
        RefreshContextActions();
        RefreshProgress();
        RefreshExposure();
        if (!_smokeEnabled)
        {
            _simulation.SetPaused(true);
            _worldAdapter.SetMovementPaused(true);
            if (_replaySeed is not null)
            {
                _replaySeed = null;
                BeginInvestigation();
            }
            else
            {
                ShowMainMenu();
            }
        }
        else
        {
            _gameStarted = true;
        }

        GD.Print(
            $"HOTEL_2D_READY case={_caseDefinition.CaseId} rooms={_hotel.Locations.Length} " +
            $"actors={_characters.Count} host={_humanHost.Value}");
    }

    public override void _Process(double delta)
    {
        if (_simulation?.Update(delta) == true)
        {
            HandleSimulationChanges();
            ProcessShiftBeats();
            RefreshExposure();
            RefreshClosingNet();
        }

        if (_clockLabel is not null && _simulation is not null)
        {
            _clockLabel.Text = _exposure is null || _exposure.Level == ExposureLevel.Unnoticed
                ? ClockText(_simulation.CurrentTick)
                : $"{ClockText(_simulation.CurrentTick)}  •  " +
                    ExposureFormatter.FormatBadge(_exposure, _isThai);
            _investigationOverlay?.SetTimeText(
                !_gameStarted
                    ? string.Empty
                    : _gameEnded
                    ? T("SHIFT ENDED", "จบกะแล้ว")
                    : $"● {ClockText(_simulation.CurrentTick)}");
        }

        UpdateFollowTarget();
        UpdateAlertVisibility();
        if (_gameStarted && !_gameEnded && ShiftDeadlineReached)
        {
            OpenFinalDeduction(deadlineReached: true);
        }

        RunSmokeTest(delta);
        RunCapture();
    }

    // Development aid: renders a few frames, saves the viewport and quits, so the
    // layout can be looked at instead of reasoned about from coordinates.
    private void RunCapture()
    {
        if (!_captureEnabled)
        {
            return;
        }

        _captureFrames++;
        if (_captureFrames == 2)
        {
            BeginInvestigation();
            return;
        }

        if (_captureFrames < 12)
        {
            return;
        }

        Image image = GetViewport().GetTexture().GetImage();
        string path = OS.GetCmdlineUserArgs()
            .SkipWhile(argument => !string.Equals(
                argument,
                "--capture-ui",
                StringComparison.OrdinalIgnoreCase))
            .Skip(1)
            .FirstOrDefault() ?? "ui.png";
        _ = image.SavePng(path);
        GD.Print($"HOTEL_2D_CAPTURE {path}");
        GetTree().Quit(0);
    }

    // The shift ends when the clock passes dawn. IsComplete alone is not enough:
    // BasementScenarioSession.TryCompleteSession also waits on the Anna/Bob milestone
    // chain and an empty movement queue, so a run where Bob never reaches the basement
    // would otherwise run past the deadline forever with the HUD clamped at 0 MIN LEFT.
    private bool ShiftDeadlineReached =>
        _simulation is not null &&
        (_simulation.IsComplete || _simulation.CurrentTick >= NightShiftDirector.DeadlineTick);

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

        var header = new ColorRect
        {
            Color = new Color("171c27"),
            Position = Vector2.Zero,
            Size = new Vector2(1280.0f, 72.0f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = -9,
        };
        AddChild(header);
        BuildMapBackdrop();

        _caseTitleLabel = AddLabel(
            CaseTitle(),
            new Vector2(28.0f, 12.0f),
            new Vector2(410.0f, 36.0f),
            24,
            new Color("f1d18a"));

        _instructionLabel = AddLabel(
            T(
                "HOTEL FLOOR PLAN  •  ROOM = MOVE  •  PERSON = TALK OR FOLLOW",
                "ผังโรงแรม  •  คลิกห้องเพื่อเดิน  •  คลิกคนเพื่อคุยหรือติดตาม"),
            new Vector2(28.0f, 45.0f),
            new Vector2(500.0f, 22.0f),
            12,
            new Color("8491aa"));

        _clockLabel = AddLabel(
            ClockText(0),
            new Vector2(560.0f, 14.0f),
            new Vector2(500.0f, 30.0f),
            22,
            new Color("e8eefc"),
            clipText: true);

        _currentLocationLabel = AddLabel(
            string.Empty,
            new Vector2(560.0f, 44.0f),
            new Vector2(500.0f, 20.0f),
            11,
            new Color("8491aa"),
            clipText: true);

        _insightButton = AddActionButton(
            InsightButtonText(),
            new Vector2(1075.0f, 18.0f),
            new Vector2(88.0f, 36.0f),
            ToggleInsightView);
        _insightButton.TooltipText = T(
            "Reveal each character's inferred current intention",
            "แสดงเจตนาปัจจุบันที่จอร์จคาดเดาจากตัวละครแต่ละคน");

        _languageButton = AddActionButton(
            MenuButtonText(),
            new Vector2(1170.0f, 18.0f),
            new Vector2(88.0f, 36.0f),
            ShowPauseMenu);
        _languageButton.TooltipText = T("Open menu and settings", "เปิดเมนูและตั้งค่า");
        _languageButton.AddThemeFontSizeOverride("font_size", 12);

        BuildPortalLines();
        foreach (HotelLocationDefinition location in _hotel.Locations)
        {
            Vector2 marker = ToScreenPosition(location.Marker);
            var locationId = new LocationId(location.Id);
            _locationMarkers.Add(locationId, marker);
            _worldAdapter.RegisterLocation(locationId, marker);

            Rect2 roomRect = ToFloorRect(location).Grow(-4.0f);
            var button = new Button
            {
                Name = $"Room-{location.Id}",
                Position = roomRect.Position,
                Size = roomRect.Size,
                TooltipText = RoomTooltip(location),
            };
            button.AddThemeColorOverride("font_color", Colors.White);
            button.AddThemeColorOverride("font_hover_color", Colors.White);
            button.AddThemeFontSizeOverride("font_size", 12);
            Color roomColor = new(location.Color);
            var normalStyle = new StyleBoxFlat
            {
                BgColor = roomColor.Darkened(0.28f),
                BorderColor = roomColor.Lightened(0.25f),
                BorderWidthLeft = 2,
                BorderWidthTop = 2,
                BorderWidthRight = 2,
                BorderWidthBottom = 2,
                CornerRadiusTopLeft = 2,
                CornerRadiusTopRight = 2,
                CornerRadiusBottomLeft = 2,
                CornerRadiusBottomRight = 2,
            };
            var hoverStyle = (StyleBoxFlat)normalStyle.Duplicate();
            hoverStyle.BgColor = roomColor.Darkened(0.12f);
            var currentStyle = (StyleBoxFlat)normalStyle.Duplicate();
            currentStyle.BgColor = roomColor.Darkened(0.05f);
            currentStyle.BorderColor = new Color("f1d18a");
            currentStyle.BorderWidthLeft = 3;
            currentStyle.BorderWidthTop = 3;
            currentStyle.BorderWidthRight = 3;
            currentStyle.BorderWidthBottom = 3;
            button.AddThemeStyleboxOverride("normal", normalStyle);
            button.AddThemeStyleboxOverride("hover", hoverStyle);
            button.AddThemeStyleboxOverride("disabled", currentStyle);
            button.AddThemeColorOverride("font_disabled_color", Colors.White);
            button.Pressed += () => ExecutePlayerMove(locationId);
            AddChild(button);
            BuildRoomFixtures(location, roomRect);
            _roomButtons[location.Id] = button;
            Label roomLabel = AddLabel(
                RoomButtonText(location),
                roomRect.Position + new Vector2(10.0f, 7.0f),
                new Vector2(Math.Max(40.0f, roomRect.Size.X - 20.0f), 34.0f),
                12,
                Colors.White,
                clipText: true);
            roomLabel.ZIndex = 4;
            _roomLabels[locationId] = roomLabel;
        }

        BuildShiftAlert();

        var panel = new ColorRect
        {
            Color = new Color("1b2130"),
            Position = new Vector2(870.0f, 82.0f),
            Size = new Vector2(390.0f, 618.0f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        AddChild(panel);

        // Laid out by advancing a cursor rather than by hand-picked coordinates.
        // The previous fixed offsets had drifted into each other - the exposure
        // block sat underneath the accusation button and was never visible at all.
        _panelCursor = PanelTop;

        _roleLabel = PanelHeading(T("YOUR ROLE", "บทบาทของคุณ"));
        CharacterDefinition host = _characters.GetValueOrDefault(_humanHost) ??
            throw new InvalidOperationException($"Human host '{_humanHost}' is missing.");
        _roleValueLabel = PanelText(RoleText(host), 50.0f, 15, new Color(host.Color));
        PanelGap();

        // Directly under the identity block: this is who you are, and this is how
        // the rest of the hotel has started to read you. It is the one number on
        // screen that the player can change by choosing to act differently.
        _exposureHeadingLabel = PanelHeading(T("HOW YOU LOOK", "คนอื่นมองคุณอย่างไร"));
        _exposureLabel = PanelText(string.Empty, 50.0f, 12, new Color("8091b3"));
        PanelGap();

        _objectiveHeadingLabel = PanelHeading(T("CURRENT OBJECTIVE", "เป้าหมายปัจจุบัน"));
        _objectiveLabel = PanelText(ObjectiveText(), 42.0f, 12, Colors.White);

        // No heading of its own: "what to do next" is the same thought as the
        // objective, and a separate header for it cost a line the panel needed.
        _progressHeadingLabel = null;
        _progressLabel = PanelText(string.Empty, 30.0f, 11, new Color("8fa3c4"));
        PanelGap();

        _caseFeedLabel = PanelHeading(T("WHAT JUST HAPPENED", "สิ่งที่เพิ่งเกิด"));
        _eventLabel = PanelText(
            T("No witnessed event yet.", "ยังไม่มีเหตุการณ์ที่พบเห็น"),
            28.0f,
            12,
            new Color("cbd5e1"));
        _statusLabel = PanelText(string.Empty, 40.0f, 12, new Color("f1d18a"));
        PanelGap();

        _contextLabel = PanelHeading(T("PRESENT HERE", "อยู่ที่นี่"));
        _actorSelector = new OptionButton
        {
            Position = new Vector2(PanelLeft, _panelCursor),
            Size = new Vector2(PanelWidth - 123.0f, 32.0f),
            TooltipText = T("Characters currently in the same room", "ตัวละครที่อยู่ห้องเดียวกัน"),
            MouseDefaultCursorShape = Control.CursorShape.PointingHand,
        };
        _actorSelector.AddThemeFontSizeOverride("font_size", 13);
        _actorSelector.ItemSelected += OnActorSelected;
        AddChild(_actorSelector);
        _actorSelector.Visible = false;
        _talkButton = AddActionButton(
            T("1  TALK", "1  คุย"),
            new Vector2(PanelLeft + PanelWidth - 117.0f, _panelCursor),
            new Vector2(117.0f, 32.0f),
            OpenSelectedConversation);
        _talkButton.TooltipText = T("Ask the selected character", "ถามตัวละครที่เลือก");
        _talkButton.Visible = false;
        _panelCursor += 36.0f;

        _followButton = AddActionButton(
            T("2  FOLLOW SELECTED", "2  ติดตามคนที่เลือก"),
            new Vector2(PanelLeft, _panelCursor),
            new Vector2(PanelWidth, 30.0f),
            ToggleFollowSelected);
        _followButton.TooltipText = T(
            "Follow the selected character between rooms",
            "ติดตามตัวละครที่เลือกเมื่อย้ายห้อง");
        _followButton.Visible = false;
        _panelCursor += 38.0f;

        // Kept off-screen: nothing reads its selection any more, but the refresh
        // path still fills it and removing it would touch several call sites.
        _objectSelector = new OptionButton
        {
            Position = new Vector2(-1000.0f, -1000.0f),
            Size = new Vector2(1.0f, 1.0f),
            Visible = false,
        };
        AddChild(_objectSelector);

        _inspectButton = PanelButton(
            T("LOOK AROUND", "สำรวจห้อง"),
            OpenInspectionChoices,
            T("See what George can inspect in this room", "ดูสิ่งที่จอร์จตรวจสอบได้ในห้องนี้"));
        _journalButton = PanelButton(
            T("OPEN CASE FILE", "เปิดแฟ้มคดี"),
            OpenJournal,
            T("Review the clues George remembers", "ทบทวนเบาะแสที่จอร์จจำได้"));
        _evidenceButton = PanelButton(
            T("5  USE A CLUE", "5  ใช้เบาะแส"),
            OpenEvidenceSelection,
            T("Select evidence to confront someone", "เลือกหลักฐานเพื่อเผชิญหน้า"));
        _deduceButton = PanelButton(
            T("MAKE AN ACCUSATION", "กล่าวหาผู้ต้องสงสัย"),
            () => OpenFinalDeduction(deadlineReached: false),
            T(
                "Name who is secretly being controlled by the Player",
                "ระบุว่าใครกำลังถูกผู้ควบคุมบงการอย่างลับ ๆ"));

        _investigationOverlay = new InvestigationOverlay();
        _investigationOverlay.Closed += OnInvestigationOverlayClosed;
        AddChild(_investigationOverlay);
        _investigationOverlay.SetLanguage(_isThai);
        _investigationOverlay.SetComfortableText(_largeText);
    }

    private void BuildMapBackdrop()
    {
        var mapPanel = new ColorRect
        {
            Color = new Color("0b0f16"),
            Position = new Vector2(18.0f, 80.0f),
            Size = new Vector2(835.0f, 620.0f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = -8,
        };
        AddChild(mapPanel);

        AddChild(new Line2D
        {
            Points =
            [
                new Vector2(28.0f, 90.0f),
                new Vector2(842.0f, 90.0f),
                new Vector2(842.0f, 690.0f),
                new Vector2(28.0f, 690.0f),
                new Vector2(28.0f, 90.0f),
            ],
            Width = 2.0f,
            DefaultColor = new Color("2e3b52"),
            ZIndex = -6,
        });

        for (float x = 35.0f; x <= 835.0f; x += 80.0f)
        {
            AddChild(new Line2D
            {
                Points = [new Vector2(x, 92.0f), new Vector2(x, 686.0f)],
                Width = 1.0f,
                DefaultColor = new Color("26304433"),
                ZIndex = -7,
            });
        }

        for (float y = 100.0f; y <= 680.0f; y += 58.0f)
        {
            AddChild(new Line2D
            {
                Points = [new Vector2(28.0f, y), new Vector2(842.0f, y)],
                Width = 1.0f,
                DefaultColor = new Color("26304433"),
                ZIndex = -7,
            });
        }

        _atmosphereOverlay = new ColorRect
        {
            Color = new Color("00000000"),
            Position = new Vector2(18.0f, 80.0f),
            Size = new Vector2(835.0f, 620.0f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 20,
        };
        AddChild(_atmosphereOverlay);
    }

    private void BuildShiftAlert()
    {
        _alertPanel = new ColorRect
        {
            Color = new Color("372d1ddd"),
            Position = new Vector2(42.0f, 88.0f),
            Size = new Vector2(788.0f, 48.0f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 30,
            Visible = false,
        };
        AddChild(_alertPanel);

        _alertLabel = AddLabel(
            string.Empty,
            new Vector2(58.0f, 100.0f),
            new Vector2(755.0f, 28.0f),
            14,
            new Color("f6d58d"),
            clipText: true);
        _alertLabel.ZIndex = 31;
        _alertLabel.Visible = false;
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
            Rect2 fromRect = ToFloorRect(from);
            Rect2 toRect = ToFloorRect(to);
            Vector2 delta = toRect.GetCenter() - fromRect.GetCenter();
            bool joinsHorizontally = MathF.Abs(delta.X) > MathF.Abs(delta.Y);
            Vector2 doorway = portal.Door is null
                ? joinsHorizontally
                    ? new Vector2((fromRect.GetCenter().X + toRect.GetCenter().X) / 2.0f, fromRect.GetCenter().Y)
                    : new Vector2(fromRect.GetCenter().X, (fromRect.GetCenter().Y + toRect.GetCenter().Y) / 2.0f)
                : ToScreenPosition(portal.Door.Position);
            Vector2 doorSize = joinsHorizontally
                ? new Vector2(10.0f, 34.0f)
                : new Vector2(34.0f, 10.0f);
            var recess = new ColorRect
            {
                Color = new Color("080b10"),
                Position = doorway - (doorSize / 2.0f) - new Vector2(2.0f, 2.0f),
                Size = doorSize + new Vector2(4.0f, 4.0f),
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ZIndex = 2,
            };
            AddChild(recess);
            var door = new ColorRect
            {
                Color = portal.RequiresAccess ? new Color("c55a62") : new Color("8ea2c4"),
                Position = doorway - (doorSize / 2.0f),
                Size = doorSize,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ZIndex = 3,
            };
            AddChild(door);
        }
    }

    private void BuildCharacterTokens()
    {
        int index = 0;
        foreach ((EntityId actor, CharacterDefinition definition) in _characters)
        {
            var token = new CharacterToken2D { ZIndex = 10 };
            token.Initialize(
                actor.Value,
                DisplayName(actor),
                new Color(definition.Color),
                isHumanHost: actor == _humanHost);
            token.Selected += OnCharacterTokenSelected;
            token.SetActivity(ActivityText(actor));
            AddChild(token);
            _characterTokens[actor] = token;

            Vector2 offset = new(
                -34.0f + ((index % 3) * 34.0f),
                -8.0f + ((index / 3) * 34.0f));
            _worldAdapter.RegisterActor(actor, token, offset);
            index++;
        }
    }

    private void BuildRoomFixtures(HotelLocationDefinition location, Rect2 roomRect)
    {
        Color fixtureColor = new Color("0a0f18aa");
        Vector2 origin = roomRect.Position;
        Vector2 size = roomRect.Size;
        switch (location.Id)
        {
            case "lobby":
                AddMapFixture(new Rect2(origin + new Vector2(24.0f, size.Y - 24.0f), new Vector2(size.X - 48.0f, 10.0f)), fixtureColor);
                AddMapFixture(new Rect2(origin + new Vector2(16.0f, 16.0f), new Vector2(56.0f, 18.0f)), new Color("c7a75d66"));
                break;
            case "hallway":
                AddMapFixture(new Rect2(origin + new Vector2((size.X / 2.0f) - 12.0f, 8.0f), new Vector2(24.0f, size.Y - 16.0f)), new Color("8395b144"));
                break;
            case "kitchen":
                AddMapFixture(new Rect2(origin + new Vector2(14.0f, 14.0f), new Vector2(size.X - 28.0f, 12.0f)), fixtureColor);
                AddMapFixture(new Rect2(origin + new Vector2(14.0f, size.Y - 26.0f), new Vector2(74.0f, 12.0f)), fixtureColor);
                break;
            case "room-201":
                AddMapFixture(new Rect2(origin + new Vector2(size.X - 84.0f, 16.0f), new Vector2(62.0f, 30.0f)), new Color("bda8cb55"));
                AddMapFixture(new Rect2(origin + new Vector2(18.0f, size.Y - 28.0f), new Vector2(32.0f, 14.0f)), fixtureColor);
                break;
            case "security-room":
                for (int index = 0; index < 3; index++)
                {
                    AddMapFixture(new Rect2(origin + new Vector2(16.0f + (index * 34.0f), 16.0f), new Vector2(24.0f, 14.0f)), new Color("7394bd66"));
                }
                break;
            case "office":
                AddMapFixture(new Rect2(origin + new Vector2(22.0f, size.Y - 28.0f), new Vector2(size.X - 44.0f, 14.0f)), fixtureColor);
                break;
            case "basement":
                AddMapFixture(new Rect2(origin + new Vector2(20.0f, 18.0f), new Vector2(28.0f, 28.0f)), new Color("b15f6866"));
                AddMapFixture(new Rect2(origin + new Vector2(size.X - 50.0f, size.Y - 46.0f), new Vector2(28.0f, 28.0f)), new Color("b15f6866"));
                break;
            case "garden":
                AddMapFixture(new Rect2(origin + new Vector2((size.X / 2.0f) - 16.0f, 8.0f), new Vector2(32.0f, size.Y - 16.0f)), new Color("9cc68a44"));
                break;
        }
    }

    private void AddMapFixture(Rect2 rect, Color color)
    {
        AddChild(new ColorRect
        {
            Color = color,
            Position = rect.Position,
            Size = rect.Size,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 1,
        });
    }

    private void InitializeNpcActivities()
    {
        _npcActivities[new EntityId("anna")] = ("Cleaning rooms", "ทำความสะอาด");
        _npcActivities[new EntityId("bob")] = ("Lobby watch", "เฝ้าล็อบบี้");
        _npcActivities[new EntityId("charlie")] = ("Avoiding staff", "หลบเลี่ยงพนักงาน");
        _npcActivities[new EntityId("dana")] = ("Kitchen inventory", "ตรวจของในครัว");
        _npcActivities[new EntityId("evelyn")] = ("Auditing records", "ตรวจบัญชี");
    }

    private static (string English, string Thai) ActivityForDestination(LocationId destination) =>
        destination.Value switch
        {
            "kitchen" => ("Kitchen inventory", "ตรวจของในครัว"),
            "office" => ("Auditing records", "ตรวจบัญชี"),
            "room-201" => ("Private errand", "ทำธุระส่วนตัว"),
            "garden" => ("Secret errand", "ทำธุระลับ"),
            "hallway" => ("Walking rounds", "เดินตรวจพื้นที่"),
            "lobby" => ("Searching lobby", "ค้นหาที่ล็อบบี้"),
            _ => ("Changing routine", "เปลี่ยนกิจวัตร"),
        };

    private void HandleSimulationChanges()
    {
        if (_simulation is null)
        {
            return;
        }

        foreach (WorldEvent worldEvent in _simulation.DrainNewEvents())
        {
            _eventHistory.Insert(0, worldEvent);
            if (_eventHistory.Count > 2)
            {
                _eventHistory.RemoveAt(_eventHistory.Count - 1);
            }
        }

        RefreshEventFeed();

        foreach (MovementSnapshot movement in _simulation.DrainNewMovements())
        {
            _worldAdapter.RequestMove(movement.Actor, movement.Destination);
        }

        RefreshContextActions();
    }

    private void ProcessShiftBeats()
    {
        if (_simulation is null || _shiftDirector is null || !_gameStarted || _gameEnded)
        {
            return;
        }

        IReadOnlyList<ShiftBeat> beats = _shiftDirector.CollectDue(_simulation.CurrentTick);
        foreach (ShiftBeat beat in beats)
        {
            if (beat.ActorId is not null && beat.DestinationId is not null)
            {
                var actor = new EntityId(beat.ActorId);
                var destination = new LocationId(beat.DestinationId);
                _npcActivities[actor] = ActivityForDestination(destination);
                if (_characterTokens.TryGetValue(actor, out CharacterToken2D? token))
                {
                    token.SetActivity(ActivityText(actor));
                }

                NpcMovementExecution execution = _simulation.RequestNpcMove(actor, destination);
                if (execution.Status == NpcMovementExecutionStatus.Failed)
                {
                    // Both slots are stored up front and picked from later by
                    // ActivityText, so each one has to be built in its own language
                    // rather than from whatever DisplayLocation returns right now.
                    _npcActivities[actor] = (
                        $"Could not reach {DisplayLocation(destination, useThai: false)}",
                        $"ไปยัง{DisplayLocation(destination, useThai: true)}ไม่ได้");
                }
            }

            ShowShiftAlert(beat);
        }

        foreach (MovementSnapshot movement in _simulation.DrainNewMovements())
        {
            _worldAdapter.RequestMove(movement.Actor, movement.Destination);
        }
    }

    private void ShowShiftAlert(ShiftBeat beat)
    {
        if (_alertPanel is null || _alertLabel is null || _atmosphereOverlay is null)
        {
            return;
        }

        _alertPanel.Visible = true;
        _alertLabel.Visible = true;
        _alertLabel.Text = $"◆  {T(beat.EnglishText, beat.ThaiText)}";
        _alertHideTick = beat.Tick + 10;
        _shiftHistory.Insert(0, beat);
        if (_shiftHistory.Count > 4)
        {
            _shiftHistory.RemoveAt(_shiftHistory.Count - 1);
        }

        Color alertColor = beat.Kind switch
        {
            ShiftBeatKind.PowerFlicker => new Color("5887a82e"),
            ShiftBeatKind.AnonymousCall => new Color("704e9e2e"),
            ShiftBeatKind.MissingMasterKey => new Color("b8893428"),
            ShiftBeatKind.ImpossibleFootsteps => new Color("a5415b35"),
            ShiftBeatKind.FinalWarning => new Color("b33a3a38"),
            _ => new Color("d0a85b16"),
        };
        _atmosphereOverlay.Color = alertColor;
        RefreshStatus(T(beat.EnglishText, beat.ThaiText));
        RefreshEventFeed();
    }

    private void RefreshEventFeed()
    {
        if (_eventLabel is null)
        {
            return;
        }

        IEnumerable<(long Tick, string Text)> simulationEvents = _eventHistory.Select(worldEvent =>
            (worldEvent.Time.Tick, FormatEvent(worldEvent)));
        IEnumerable<(long Tick, string Text)> shiftEvents = _shiftHistory.Select(beat =>
            (beat.Tick, $"[{JournalPresentationFormatter.FormatClock(beat.Tick)}]  ◆ {T(beat.EnglishText, beat.ThaiText)}"));
        string[] lines = simulationEvents
            .Concat(shiftEvents)
            .OrderByDescending(item => item.Tick)
            .Take(2)
            .Select(item => item.Text)
            .ToArray();
        _eventLabel.Text = lines.Length == 0
            ? T("No witnessed event yet.", "ยังไม่มีเหตุการณ์ที่พบเห็น")
            : string.Join("\n", lines);
    }

    private void UpdateAlertVisibility()
    {
        if (_simulation is null || _alertPanel is null || _alertLabel is null || _atmosphereOverlay is null)
        {
            return;
        }

        if (_alertPanel.Visible && _simulation.CurrentTick >= _alertHideTick)
        {
            _alertPanel.Visible = false;
            _alertLabel.Visible = false;
            _atmosphereOverlay.Color = new Color("00000000");
        }
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
        if (actor == _humanHost)
        {
            _hasMoved = true;
            RefreshProgress();
        }

        RefreshStatus($"{DisplayName(actor)} {T("arrived at", "มาถึง")} {DisplayLocation(location)}");
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
            _followButton is null ||
            _inspectButton is null ||
            _evidenceButton is null)
        {
            return;
        }

        _simulation.PlayerController.SetPlayerEntity(_humanHost);
        _presentActors = _simulation.PlayerController.GetPresentActors();
        _presentObjects = _simulation.GetPresentObjects();

        if (_contextLabel is not null)
        {
            _contextLabel.Text =
                $"{T("IN THIS ROOM", "ในห้องนี้")}: {DisplayLocation(_simulation.PlayerController.CurrentLocation)}  •  " +
                $"{_presentActors.Count} {T("people", "คน")}";
        }

        if (_currentLocationLabel is not null)
        {
            // Left alone once the closing net has taken this line over.
            if (_confrontationTick is null)
            {
                _currentLocationLabel.Text =
                    $"{T("YOU ARE IN", "คุณอยู่ที่")} {DisplayLocation(_simulation.PlayerController.CurrentLocation).ToUpperInvariant()}";
            }
        }

        string[] adjacent = _simulation.PlayerController.GetAdjacentLocations()
            .Select(location => location.Value)
            .ToArray();
        foreach ((string id, Button roomButton) in _roomButtons)
        {
            HotelLocationDefinition? location = _hotel?.Locations.SingleOrDefault(item => item.Id == id);
            if (location is null)
            {
                continue;
            }

            bool isCurrent = id == _simulation.PlayerController.CurrentLocation.Value;
            int occupantCount = _characters.Keys.Count(actor =>
                _simulation.GetLogicalLocation(actor).Value == id);
            SetRoomLabel(location, isCurrent, occupantCount);
            roomButton.Disabled = isCurrent;

            // Rooms you cannot walk to from here are dimmed rather than left
            // looking identical. Clicking one used to be the only way to find out,
            // and being told "no accessible route" is not a floor plan.
            bool reachable = isCurrent || adjacent.Contains(id, StringComparer.Ordinal);
            roomButton.Modulate = reachable
                ? Colors.White
                : new Color(1.0f, 1.0f, 1.0f, 0.45f);
            if (_roomLabels.TryGetValue(new LocationId(id), out Label? roomLabel))
            {
                roomLabel.Modulate = roomButton.Modulate;
            }
        }

        _actorSelector.Clear();
        foreach (EntityId actor in _presentActors)
        {
            _actorSelector.AddItem(DisplayName(actor));
        }

        bool hasActors = _presentActors.Count > 0;
        int selectedActorIndex = FindPresentActorIndex(_selectedActor);
        if (hasActors)
        {
            selectedActorIndex = selectedActorIndex >= 0 ? selectedActorIndex : 0;
            _selectedActor = _presentActors[selectedActorIndex];
            _actorSelector.Select(selectedActorIndex);
        }
        else
        {
            _selectedActor = null;
        }

        UpdateCharacterTokenSelection();
        _actorSelector.Disabled = !hasActors;
        _talkButton.Disabled = !hasActors;
        _followButton.Disabled = !hasActors && _followTarget is null;
        _followButton.Text = _followTarget is { } target
            ? $"2  {T("STOP FOLLOWING", "หยุดตาม")} {DisplayName(target)}"
            : T("2  FOLLOW THIS PERSON", "2  ตามคนนี้");
        if (!hasActors)
        {
            _actorSelector.AddItem(T("Nobody is here", "ไม่มีใครอยู่ที่นี่"));
        }

        _objectSelector.Clear();
        foreach (InteractiveObject obj in _presentObjects)
        {
            _objectSelector.AddItem(DisplayObject(obj));
        }

        bool hasObjects = _presentObjects.Count > 0;
        _objectSelector.Disabled = !hasObjects;
        _inspectButton.Disabled = !hasObjects;
        _inspectButton.Text = hasObjects
            ? $"{T("LOOK AROUND", "สำรวจห้อง")}  •  {_presentObjects.Count}"
            : T("NOTHING TO INSPECT", "ไม่มีสิ่งให้ตรวจ");
        if (!hasObjects)
        {
            _objectSelector.AddItem(T("Nothing to inspect", "ไม่มีวัตถุให้ตรวจสอบ"));
        }

        _evidenceButton.Disabled = _simulation.GetPlayerJournal(_humanHost).Entries.Count == 0;
        if (_deduceButton is not null)
        {
            int clueCount = _simulation.GetPlayerJournal(_humanHost).Entries.Count;
            if (_journalButton is not null)
            {
                _journalButton.Text = $"{T("OPEN CASE FILE", "เปิดแฟ้มคดี")}  •  {clueCount}";
            }
            _deduceButton.Disabled = _gameEnded || clueCount < 2;
        }
    }

    private void OnActorSelected(long index)
    {
        if (index < 0 || index >= _presentActors.Count)
        {
            return;
        }

        _selectedActor = _presentActors[(int)index];
        UpdateCharacterTokenSelection();
        RefreshStatus(
            $"{T("Selected", "เลือก")} {DisplayName(_selectedActor.Value)} — " +
            T("choose Talk or Follow.", "เลือกคุยหรือติดตามได้เลย"));
    }

    private void OnCharacterTokenSelected(CharacterToken2D token)
    {
        var actor = new EntityId(token.ActorId);
        int index = FindPresentActorIndex(actor);
        if (index < 0 || _actorSelector is null)
        {
            RefreshStatus(
                $"{DisplayName(actor)} {T("is in another room. Move there to talk.", "อยู่ห้องอื่น เดินไปหากต้องการคุย")}");
            return;
        }

        _selectedActor = actor;
        _actorSelector.Select(index);
        UpdateCharacterTokenSelection();
        OpenCharacterActions(actor);
    }

    private void OpenCharacterActions(EntityId actor)
    {
        if (_simulation is null)
        {
            return;
        }

        PlayerJournal journal = _simulation.GetPlayerJournal(_humanHost);
        string followLabel = _followTarget == actor
            ? T("STOP FOLLOWING", "หยุดติดตาม")
            : T("FOLLOW THIS PERSON", "ติดตามคนนี้");
        var choices = new List<InvestigationChoice>
        {
            new(T("TALK", "คุย"), () => OpenConversation(actor)),
            new(followLabel, () => SetFollowTarget(actor)),
        };
        if (journal.Entries.Count > 0)
        {
            choices.Add(new InvestigationChoice(
                T("QUESTION WITH A CLUE", "ใช้เบาะแสถาม"),
                () => OpenEvidenceForPartner(actor)));
        }

        ShowInvestigationScreen(
            DisplayName(actor),
            T(
                "Choose one action. Talking reveals claims; the Case File tells you whether a clue was seen or heard.",
                "เลือกการกระทำหนึ่งอย่าง การคุยทำให้ได้คำกล่าวอ้าง ส่วนแฟ้มคดีจะบอกว่าเบาะแสนั้นเห็นเองหรือได้ยินมา"),
            new Color(_characters[actor].Color),
            choices);
    }

    private int FindPresentActorIndex(EntityId? actor)
    {
        if (actor is null)
        {
            return -1;
        }

        for (int index = 0; index < _presentActors.Count; index++)
        {
            if (_presentActors[index] == actor.Value)
            {
                return index;
            }
        }

        return -1;
    }

    private void UpdateCharacterTokenSelection()
    {
        foreach ((EntityId actor, CharacterToken2D token) in _characterTokens)
        {
            token.SetSelected(_selectedActor == actor);
        }
    }

    private void ToggleFollowSelected()
    {
        if (_actorSelector is null || _presentActors.Count == 0)
        {
            return;
        }

        int selected = Math.Clamp(_actorSelector.Selected, 0, _presentActors.Count - 1);
        SetFollowTarget(_presentActors[selected]);
    }

    private void SetFollowTarget(EntityId actor)
    {
        if (_followTarget == actor)
        {
            _followTarget = null;
            _followDestination = null;
            RefreshStatus($"{T("Stopped following", "หยุดติดตาม")} {DisplayName(actor)}");
            RefreshContextActions();
            return;
        }

        _followTarget = actor;
        _followDestination = null;
        RefreshStatus(
            $"{T("Following", "กำลังติดตาม")} {DisplayName(_followTarget.Value)}. " +
            T("George will mirror their room changes.", "จอร์จจะย้ายตามการเปลี่ยนห้องของเขา"));
        RefreshContextActions();
    }

    private void UpdateFollowTarget()
    {
        if (_simulation is null || _followTarget is null || _simulation.IsPaused || _simulation.IsComplete)
        {
            return;
        }

        if (_simulation.PlayerController.HasActiveMovement)
        {
            return;
        }

        LocationId targetLocation = _simulation.GetLogicalLocation(_followTarget.Value);
        if (targetLocation == _simulation.PlayerController.CurrentLocation)
        {
            _followDestination = null;
            return;
        }

        if (_followDestination == targetLocation)
        {
            return;
        }

        _followDestination = targetLocation;
        NpcMovementExecution execution = _simulation.PlayerMove(targetLocation);
        if (execution.Status == NpcMovementExecutionStatus.Pending && execution.Movement is not null)
        {
            _worldAdapter.RequestMove(_humanHost, targetLocation);
            RefreshStatus(
                $"{T("Following", "กำลังติดตาม")} {DisplayName(_followTarget.Value)} " +
                $"{T("to", "ไปยัง")} {DisplayLocation(targetLocation)}...");
            HandleSimulationChanges();
        }
        else if (execution.Status == NpcMovementExecutionStatus.Failed)
        {
            RefreshStatus(
                $"{T("Follow stopped: no accessible route to", "หยุดติดตาม: ไม่มีเส้นทางไปยัง")} " +
                $"{DisplayLocation(targetLocation)}");
            _followTarget = null;
            _followDestination = null;
            RefreshContextActions();
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
            new(T("Ask about their schedule", "ถามเกี่ยวกับตารางงาน"), () => ExecuteDialogue(
                partner,
                new DialogueRequest(
                    DialogueActionKind.InquireSchedule,
                    _humanHost,
                    partner))),
        };

        // Gossip about a third party is the highest-value thing anyone can give
        // you, so it is the first thing they stop giving once they have enough on
        // you. Everything else stays open: exposure raises the price of
        // investigating, it never takes the game away.
        bool refusesGossip = _exposure?.RefusesToGossipWith(partner) == true;
        if (!refusesGossip)
        {
            foreach (EntityId subject in _characters.Keys
                         .Where(actor => actor != partner && actor != _humanHost)
                         .Take(3))
            {
                EntityId selectedSubject = subject;
                choices.Add(new InvestigationChoice(
                    $"{T("Ask about", "ถามเกี่ยวกับ")} {DisplayName(selectedSubject)}",
                    () => ExecuteDialogue(
                        partner,
                        new DialogueRequest(
                            DialogueActionKind.AskAboutSubject,
                            _humanHost,
                            partner,
                            subject: selectedSubject))));
            }
        }

        if (_presentObjects.Count > 0)
        {
            InteractiveObject objectToAsk = _presentObjects[0];
            choices.Add(new InvestigationChoice(
                $"{T("Ask about", "ถามเกี่ยวกับ")} {DisplayObject(objectToAsk)}",
                () => ExecuteObjectInquiry(partner, objectToAsk)));
        }

        // Only offered when the player is actually holding something that
        // disagrees with this person's own account. Otherwise "confront" is a
        // button with no stakes and no meaning.
        Contradiction[] against = FindContradictionsAgainst(partner);
        if (against.Length > 0)
        {
            choices.Add(new InvestigationChoice(
                T("Challenge their story", "แย้งคำให้การ"),
                () => OpenChallenge(partner, against)));
        }

        choices.Add(new InvestigationChoice(
            T("Choose evidence to confront", "เลือกหลักฐานเพื่อเผชิญหน้า"),
            () => OpenEvidenceForPartner(partner)));

        ShowInvestigationScreen(
            $"{T("CONVERSATION WITH", "บทสนทนากับ")} {DisplayName(partner)}",
            T(
                "Choose a topic. Their words are not automatically true; the game will label any clue you receive separately.",
                "เลือกหัวข้อที่อยากถาม คำพูดของพวกเขาอาจไม่จริงเสมอไป และเบาะแสที่ได้จะถูกแยกบันทึกให้ชัดเจน"),
            new Color(_characters[partner].Color),
            choices);
    }

    private void ExecuteDialogue(EntityId partner, DialogueRequest request)
    {
        if (_simulation is null)
        {
            return;
        }

        if (request.Kind == DialogueActionKind.AskAboutSubject &&
            _exposure?.RefusesToGossipWith(partner) == true)
        {
            ShowRefusal(partner);
            return;
        }

        DialogueOutcome outcome = _simulation.Talk(request);
        ShowDialogueOutcome(partner, request, outcome, prefix: GuardedPrefix(partner));
    }

    private Contradiction[] FindContradictionsAgainst(EntityId partner) =>
        _simulation is null
            ? []
            : _simulation.FindContradictions(_humanHost)
                .Where(item => item.Claim.Speaker == partner)
                .ToArray();

    private void OpenChallenge(EntityId partner, IReadOnlyList<Contradiction> against)
    {
        var choices = new List<InvestigationChoice>();
        foreach (Contradiction item in against.Take(3))
        {
            Contradiction selected = item;
            string claimed = DisplayLocation(selected.Claim.ClaimedLocation);
            string seen = DisplayLocation(selected.Evidence.Location!.Value);
            choices.Add(new InvestigationChoice(
                T(
                    $"\"You said {claimed} — but you were at {seen}.\"",
                    $"\"คุณบอกว่าอยู่{claimed} แต่คุณอยู่ที่{seen}\""),
                () => ExecuteChallenge(partner, selected)));
        }

        choices.Add(new InvestigationChoice(
            T("Say nothing for now", "ยังไม่พูดอะไร"),
            () => OpenConversation(partner)));

        string risk = string.Join('\n', against.Take(3).Select(item =>
            $"• {ClaimPresentationFormatter.DescribeChallengeRisk(item.EvidenceIsFirstHand, _isThai)}"));
        ShowInvestigationScreen(
            $"{T("CHALLENGE", "แย้งคำให้การ")} — {DisplayName(partner)}",
            T(
                "If the clue is right, they have to explain themselves. If it is not, " +
                    "you have just called someone a liar in front of them.\n\n" + risk,
                "ถ้าเบาะแสถูก เขาต้องอธิบายตัวเอง " +
                    "แต่ถ้าผิด คุณเพิ่งกล่าวหาคนต่อหน้าเขา\n\n" + risk),
            new Color("f1d18a"),
            choices);
    }

    private void ExecuteChallenge(EntityId partner, Contradiction contradiction)
    {
        if (_simulation is null)
        {
            return;
        }

        DialogueOutcome outcome = _simulation.Talk(new DialogueRequest(
            DialogueActionKind.ConfrontEvidence,
            _humanHost,
            partner,
            confrontingMemoryId: contradiction.Evidence.Id));
        _hasConfronted = true;
        RefreshProgress();

        // A backfire has to be perceived before it counts against the player, so
        // give the room a moment to notice what was just said.
        for (int index = 0; index < 3; index++)
        {
            _simulation.Step();
        }

        HandleSimulationChanges();
        RefreshExposure();
        ShowChallengeOutcome(partner, outcome);
    }

    private void ShowChallengeOutcome(EntityId partner, DialogueOutcome outcome)
    {
        string who = DisplayName(partner);
        (string title, string body, string color) = outcome.Confrontation switch
        {
            ConfrontationResult.Cracked => (
                T("THEIR STORY BREAKS", "คำให้การแตก"),
                T(
                    $"{who} stops. The room goes very quiet.\n\n{outcome.Text}\n\n" +
                        "They have given up something they were holding back. It is in your case file now.",
                    $"{who} หยุด ห้องเงียบลงมาก\n\n{outcome.Text}\n\n" +
                        "เขายอมบอกสิ่งที่เก็บไว้แล้ว มันอยู่ในแฟ้มคดีของคุณแล้ว"),
                "77dd77"),
            ConfrontationResult.Backfired => (
                T("THEIR STORY HOLDS", "คำให้การไม่แตก"),
                T(
                    $"{outcome.Text}\n\n" +
                        "You called someone a liar on the strength of a story that was not true. " +
                        "People will remember that you did that.",
                    $"{outcome.Text}\n\n" +
                        "คุณกล่าวหาว่าเขาโกหก โดยอาศัยเรื่องที่ไม่จริง " +
                        "และทุกคนจะจำไว้ว่าคุณทำอย่างนั้น"),
                "e06c75"),
            _ => (
                T("NOTHING TO ANSWER FOR", "ไม่มีอะไรต้องตอบ"),
                outcome.Text,
                "8091b3"),
        };

        ShowInvestigationScreen(
            title,
            body,
            new Color(color),
            [new InvestigationChoice(
                T("Keep talking", "คุยต่อ"),
                () => OpenConversation(partner))]);
    }

    private void ShowRefusal(EntityId partner)
    {
        string who = DisplayName(partner);
        ShowInvestigationScreen(
            $"{T("CONVERSATION WITH", "บทสนทนากับ")} {who}",
            T(
                $"{who} looks at you for a moment too long.\n\n" +
                    "“I am not going to talk about other people with you. Not tonight.”\n\n" +
                    "Whatever they have been keeping about you, it is now enough that "  +
                    "they would rather say nothing.",
                $"{who} มองคุณนานกว่าปกติหนึ่งจังหวะ\n\n" +
                    "“ฉันจะไม่พูดถึงคนอื่นกับคุณ ไม่ใช่คืนนี้”\n\n" +
                    "สิ่งที่เขาเก็บไว้เกี่ยวกับคุณมากพอจะทำให้เขาเลือกที่จะเงียบแล้ว"),
            new Color("e06c75"),
            [new InvestigationChoice(
                T("Ask something else", "ถามอย่างอื่น"),
                () => OpenConversation(partner))]);
        RefreshStatus(T(
            $"{who} refused to talk about anyone else.",
            $"{who} ปฏิเสธที่จะพูดถึงคนอื่น"));
    }

    // Short of a refusal there is still a tell: the answer arrives, but it arrives
    // carefully. This is the warning shot before gossip closes off entirely.
    private string? GuardedPrefix(EntityId partner) =>
        _exposure?.IsGuardedTowards(partner) != true
            ? null
            : T(
                $"{DisplayName(partner)} answers slowly, choosing words as if you might repeat them.",
                $"{DisplayName(partner)} ตอบช้าลง เลือกคำพูดราวกับกลัวว่าคุณจะเอาไปเล่าต่อ");

    private void ExecuteObjectInquiry(EntityId partner, InteractiveObject obj)
    {
        if (_simulation is null)
        {
            return;
        }

        var request = new DialogueRequest(
            DialogueActionKind.InquireAboutObject,
            _humanHost,
            partner,
            targetObjectId: obj.Id);
        DialogueOutcome outcome = _simulation.Talk(request);
        ShowDialogueOutcome(partner, request, outcome, DisplayObject(obj));
    }

    private void ExecuteEvidenceConfrontation(EntityId partner, PlayerJournalEntry evidence)
    {
        if (_simulation is null)
        {
            return;
        }

        var request = new DialogueRequest(
            DialogueActionKind.ConfrontEvidence,
            _humanHost,
            partner,
            confrontingMemoryId: evidence.Id);
        DialogueOutcome outcome = _simulation.Talk(request);
        ShowDialogueOutcome(partner, request, outcome);
    }

    private void ShowDialogueOutcome(
        EntityId partner,
        DialogueRequest request,
        DialogueOutcome outcome,
        string? objectName = null,
        string? prefix = null)
    {
        if (outcome.Succeeded)
        {
            if (request.Kind == DialogueActionKind.ConfrontEvidence)
            {
                _hasConfronted = true;
            }
            else
            {
                _hasTalked = true;
            }

            RefreshProgress();
        }

        string body = BuildDialogueResultBody(partner, request, outcome, objectName);
        if (!string.IsNullOrEmpty(prefix))
        {
            body = $"{prefix}\n\n{body}";
        }

        ShowInvestigationScreen(
            $"{T("CONVERSATION WITH", "บทสนทนากับ")} {DisplayName(partner)}",
            body,
            new Color(_characters[partner].Color),
            [new InvestigationChoice(T("Ask something else", "ถามอย่างอื่น"), () => OpenConversation(partner))]);
        RefreshStatus(outcome.Succeeded
            ? $"{T("Conversation with", "บันทึกการสนทนากับ")} {DisplayName(partner)}"
            : body);
        HandleSimulationChanges();
    }

    private string FormatDialogue(
        EntityId partner,
        DialogueRequest request,
        DialogueOutcome outcome,
        string? objectName)
    {
        if (_dialogueFormatter is null)
        {
            return LocalizeText(outcome.Text);
        }

        return _dialogueFormatter.Format(
            partner,
            request,
            outcome,
            DisplayName,
            DisplayLocation,
            objectName,
            useThai: _isThai);
    }

    private string BuildDialogueResultBody(
        EntityId partner,
        DialogueRequest request,
        DialogueOutcome outcome,
        string? objectName)
    {
        if (!outcome.Succeeded)
        {
            return $"{T("THE CONVERSATION ENDED", "บทสนทนานี้ไปต่อไม่ได้")}\n\n" +
                LocalizeText(outcome.FailureReason ?? "The conversation could not continue.");
        }

        string spokenLine = FormatDialogue(partner, request, outcome, objectName);
        string note = request.Kind switch
        {
            DialogueActionKind.InquireSchedule => T(
                "Their account of the schedule has been added to your clue journal.",
                "คำบอกเล่าเรื่องตารางงานถูกเก็บไว้ในบันทึกเบาะแสแล้ว"),
            DialogueActionKind.ConfrontEvidence => T(
                "Their reaction is a lead, not proof. Compare it with other clues.",
                "ปฏิกิริยานี้เป็นเพียงเบาะแส ยังไม่ใช่หลักฐานยืนยัน ให้เทียบกับข้อมูลอื่น"),
            _ when outcome.TransferredMemory is not null => T(
                "A new clue was added to your journal. Check its source before you trust it.",
                "มีเบาะแสใหม่ในบันทึก ตรวจที่มาก่อนเชื่อคำบอกเล่า"),
            _ => T(
                "No new clue was confirmed, but their answer may matter later.",
                "ยังไม่มีเบาะแสที่ยืนยันได้ แต่คำตอบนี้อาจมีความหมายภายหลัง"),
        };

        return $"{T($"WHAT {DisplayName(partner)} SAYS", $"สิ่งที่ {DisplayName(partner)} พูด")}\n“{spokenLine}”\n\n" +
            $"{T("GEORGE'S NOTE", "สิ่งที่จอร์จบันทึกไว้")}\n{note}";
    }

    private void OpenInspectionChoices()
    {
        if (_simulation is null || _presentObjects.Count == 0)
        {
            return;
        }

        if (_presentObjects.Count == 1)
        {
            InspectObject(_presentObjects[0]);
            return;
        }

        var choices = _presentObjects
            .Select(obj =>
            {
                InteractiveObject selected = obj;
                return new InvestigationChoice(DisplayObject(selected), () => InspectObject(selected));
            })
            .ToList();
        ShowInvestigationScreen(
            T("LOOK AROUND", "สำรวจห้อง"),
            T(
                "Choose what George should inspect. An object may be useful, misleading, or already familiar.",
                "เลือกสิ่งที่จอร์จควรตรวจสอบ วัตถุอาจมีประโยชน์ ชวนให้เข้าใจผิด หรือเป็นสิ่งที่รู้จักอยู่แล้ว"),
            new Color("77dd77"),
            choices);
    }

    private void InspectObject(InteractiveObject obj)
    {
        if (_simulation is null)
        {
            return;
        }

        ObjectActionResult result = _simulation.InspectObject(obj.Id);
        if (result.Succeeded)
        {
            _hasInspected = true;
            RefreshProgress();
        }

        string body = result.DiscoveredClue is null
            ? $"{T("WHAT GEORGE FOUND", "สิ่งที่จอร์จพบ")}\n{LocalizeText(result.Message)}"
            : $"{T("WHAT GEORGE FOUND", "สิ่งที่จอร์จพบ")}\n{LocalizeText(result.Message)}\n\n" +
                $"{T("CLUE ADDED TO THE JOURNAL", "เบาะแสที่บันทึกไว้")}\n{LocalizeText(result.DiscoveredClue)}";
        ShowInvestigationScreen(
            T("INVESTIGATION", "สืบสวน"),
            body,
            result.Succeeded ? new Color("77dd77") : new Color("e06c75"));
        RefreshStatus(result.Succeeded
            ? $"{T("Inspected", "ตรวจสอบแล้ว")} {DisplayObject(obj)}."
            : LocalizeText(result.Message));
        HandleSimulationChanges();
    }

    private void OpenJournal()
    {
        if (_simulation is null)
        {
            return;
        }

        _hasOpenedJournal = true;
        RefreshProgress();
        OpenJournalPage(JournalView.All, pageIndex: 0);
    }

    // The shift clock keeps running while the case file is open, so every screen
    // below re-reads the journal instead of closing over one snapshot. Views are
    // passed as an enum rather than a built TimelineFilter for the same reason:
    // "last 30 minutes" has to be recomputed against the current time, otherwise
    // it silently freezes into the window that was current when it was picked.
    private void OpenJournalPage(JournalView view, int pageIndex)
    {
        if (_simulation is null)
        {
            return;
        }

        PlayerJournal journal = _simulation.GetPlayerJournal(_humanHost);
        JournalPage page = JournalPresentationFormatter.FormatPage(
            journal,
            DisplayName,
            DisplayLocation,
            BuildJournalFilter(view, journal),
            pageIndex,
            pageSize: 2,
            useThai: _isThai);
        var choices = new List<InvestigationChoice>();
        if (page.PageNumber > 1)
        {
            choices.Add(new InvestigationChoice(
                T("← PREVIOUS CLUES", "← เบาะแสก่อนหน้า"),
                () => OpenJournalPage(view, page.PageNumber - 2)));
        }

        if (page.PageNumber < page.PageCount)
        {
            choices.Add(new InvestigationChoice(
                T("NEXT CLUES →", "เบาะแสถัดไป →"),
                () => OpenJournalPage(view, page.PageNumber)));
        }

        choices.Add(new InvestigationChoice(
            T("CHANGE CLUE VIEW", "เปลี่ยนมุมมองเบาะแส"),
            () => OpenJournalViews()));
        choices.Add(new InvestigationChoice(
            T("PEOPLE TO WATCH", "คนที่ควรจับตา"),
            () => OpenPeopleToWatch(view, page.PageNumber - 1)));
        choices.Add(new InvestigationChoice(
            T("WHAT PEOPLE TOLD YOU", "สิ่งที่คนอื่นบอกคุณ"),
            () => OpenClaimsPage(view, page.PageNumber - 1)));
        choices.Add(new InvestigationChoice(
            T("HOW YOU LOOK", "คนอื่นมองคุณอย่างไร"),
            () => OpenExposurePage(view, page.PageNumber - 1)));
        ShowInvestigationScreen(
            T("CLUE JOURNAL", "บันทึกเบาะแส"),
            page.Text,
            new Color("77bdfb"),
            choices);
    }

    private void OpenJournalViews()
    {
        ShowInvestigationScreen(
            T("CHOOSE A CLUE VIEW", "เลือกมุมมองเบาะแส"),
            T(
                "Choose a small set of clues to compare. You can return here at any time.",
                "เลือกเบาะแสชุดเล็กเพื่อเปรียบเทียบ คุณกลับมาหน้านี้ได้ตลอดเวลา"),
            new Color("77bdfb"),
            [
                new InvestigationChoice(
                    T("ALL CLUES", "เบาะแสทั้งหมด"),
                    () => OpenJournalPage(JournalView.All, 0)),
                new InvestigationChoice(
                    T("LAST 30 MINUTES", "30 นาทีล่าสุด"),
                    () => OpenJournalPage(JournalView.LastThirtyMinutes, 0)),
                new InvestigationChoice(
                    T("IN THIS ROOM", "เหตุการณ์ในห้องนี้"),
                    () => OpenJournalPage(JournalView.CurrentRoom, 0)),
                new InvestigationChoice(
                    T("GEORGE SAW", "สิ่งที่จอร์จเห็นเอง"),
                    () => OpenJournalPage(JournalView.SeenFirstHand, 0)),
                new InvestigationChoice(
                    T("WHAT PEOPLE SAID", "สิ่งที่คนอื่นเล่า"),
                    () => OpenJournalPage(JournalView.HeardFromOthers, 0)),
            ]);
    }

    private void OpenPeopleToWatch(JournalView returnView, int returnPage)
    {
        if (_simulation is null)
        {
            return;
        }

        ShowInvestigationScreen(
            T("PEOPLE TO WATCH", "คนที่ควรจับตา"),
            JournalPresentationFormatter.FormatPeopleToWatch(
                _simulation.GetPlayerJournal(_humanHost),
                DisplayName,
                _isThai),
            new Color("f1d18a"),
            [new InvestigationChoice(
                T("BACK TO CLUES", "กลับไปที่เบาะแส"),
                () => OpenJournalPage(returnView, returnPage))]);
    }

    // Statements live apart from clues on purpose. A clue is something that
    // happened; a claim is something somebody said happened, and keeping the two
    // in one list is what would let the player mistake one for the other.
    private void OpenClaimsPage(JournalView returnView, int returnPage)
    {
        if (_simulation is null)
        {
            return;
        }

        IReadOnlyList<Contradiction> contradictions =
            _simulation.FindContradictions(_humanHost);
        ShowInvestigationScreen(
            T("WHAT PEOPLE TOLD YOU", "สิ่งที่คนอื่นบอกคุณ"),
            ClaimPresentationFormatter.FormatClaims(
                _simulation.Claims,
                contradictions,
                DisplayName,
                DisplayLocation,
                _isThai),
            new Color(contradictions.Count > 0 ? "f1d18a" : "77bdfb"),
            [new InvestigationChoice(
                T("BACK TO CLUES", "กลับไปที่เบาะแส"),
                () => OpenJournalPage(returnView, returnPage))],
            showPortrait: false);
    }

    private void OpenExposurePage(JournalView returnView, int returnPage)
    {
        if (_simulation is null)
        {
            return;
        }

        ExposureReport exposure = _simulation.GetExposure(_humanHost);
        _exposure = exposure;
        ShowInvestigationScreen(
            $"{T("HOW YOU LOOK", "คนอื่นมองคุณอย่างไร")}  •  " +
                ExposureFormatter.FormatBadge(exposure, _isThai),
            ExposureFormatter.FormatDetail(exposure, DisplayName, _isThai),
            new Color(ExposureColors[exposure.Level]),
            [new InvestigationChoice(
                T("BACK TO CLUES", "กลับไปที่เบาะแส"),
                () => OpenJournalPage(returnView, returnPage))],
            showPortrait: false);
    }

    private static TimelineFilter? BuildJournalFilter(JournalView view, PlayerJournal journal) =>
        view switch
        {
            JournalView.LastThirtyMinutes => new TimelineFilter(
                MinimumTick: Math.Max(0, journal.CurrentTime.Tick - 30)),
            JournalView.CurrentRoom => new TimelineFilter(Location: journal.CurrentLocation),
            JournalView.SeenFirstHand => new TimelineFilter(Kind: MemoryKind.Episodic),
            JournalView.HeardFromOthers => new TimelineFilter(Kind: MemoryKind.Social),
            _ => null,
        };

    private void OpenEvidenceSelection()
    {
        if (_simulation is null)
        {
            return;
        }

        PlayerJournal journal = _simulation.GetPlayerJournal(_humanHost);
        if (journal.Entries.Count == 0)
        {
            ShowInvestigationScreen(
                T("EVIDENCE", "หลักฐาน"),
                T("No evidence has been recorded yet. Inspect an object or witness an event first.", "ยังไม่มีหลักฐาน ตรวจสอบวัตถุหรือพบเห็นเหตุการณ์ก่อน"),
                new Color("e06c75"));
            return;
        }

        var choices = new List<InvestigationChoice>();
        foreach (PlayerJournalEntry entry in journal.Entries.Take(6))
        {
            PlayerJournalEntry selectedEntry = entry;
            choices.Add(new InvestigationChoice(
                $"{JournalPresentationFormatter.FormatClock(entry.EventTime.Tick)}  {ShortEvidenceLabel(entry)}",
                () => OpenEvidencePartnerSelection(selectedEntry)));
        }

        ShowInvestigationScreen(
            T("CHOOSE A CLUE", "เลือกเบาะแส"),
            T(
                "Choose the clue George will mention. Things seen directly are usually safer than second-hand stories.",
                "เลือกเบาะแสที่จอร์จจะนำไปพูด สิ่งที่เห็นด้วยตัวเองมักน่าเชื่อถือกว่าเรื่องที่ได้ยินต่อมา"),
            new Color("f1d18a"),
            choices);
    }

    private void OpenEvidenceForPartner(EntityId partner)
    {
        if (_simulation is null)
        {
            return;
        }

        PlayerJournal journal = _simulation.GetPlayerJournal(_humanHost);
        var choices = journal.Entries
            .Take(6)
            .Select(entry =>
            {
                PlayerJournalEntry selectedEntry = entry;
                return new InvestigationChoice(
                    $"{JournalPresentationFormatter.FormatClock(entry.EventTime.Tick)}  {ShortEvidenceLabel(entry)}",
                    () => ExecuteEvidenceConfrontation(partner, selectedEntry));
            })
            .ToList();
        ShowInvestigationScreen(
            $"{T("CHOOSE A CLUE FOR", "เลือกเบาะแสเพื่อถาม")} {DisplayName(partner)}",
            T("Choose what George should mention to this person.", "เลือกสิ่งที่จอร์จควรนำไปถามคนนี้"),
            new Color(_characters[partner].Color),
            choices);
    }

    private void OpenEvidencePartnerSelection(PlayerJournalEntry evidence)
    {
        if (_simulation is null)
        {
            return;
        }

        var choices = _presentActors
            .Select(partner => new InvestigationChoice(
                $"{T("Question", "ถาม")} {DisplayName(partner)} {T("with this clue", "ด้วยเบาะแสนี้")}",
                () => ExecuteEvidenceConfrontation(partner, evidence)))
            .ToList();
        if (choices.Count == 0)
        {
            choices.Add(new InvestigationChoice(
                T("Return to journal", "กลับไปบันทึก"),
                OpenJournal));
        }

        ShowInvestigationScreen(
            T("WHO DO YOU WANT TO QUESTION?", "ต้องการถามใคร?"),
            $"{T("Selected clue", "เบาะแสที่เลือก")}: {ShortEvidenceLabel(evidence)}\n" +
            T("Choose someone currently in the same room as George.", "เลือกคนที่อยู่ห้องเดียวกับจอร์จตอนนี้"),
            new Color("f1d18a"),
            choices);
    }

    private string ShortEvidenceLabel(PlayerJournalEntry entry)
    {
        string summary = JournalPresentationFormatter.FormatHeadline(
            entry,
            DisplayName,
            DisplayLocation,
            useThai: _isThai);
        return summary.Length <= 68 ? summary : $"{summary[..65]}...";
    }

    private void ShowInvestigationScreen(
        string title,
        string body,
        Color accent,
        IReadOnlyList<InvestigationChoice>? choices = null,
        bool allowClose = true,
        bool showPortrait = true)
    {
        if (_simulation is null || _investigationOverlay is null)
        {
            return;
        }

        _investigationOverlay.ShowScreen(title, body, accent, choices, allowClose, showPortrait);
    }

    private void OnInvestigationOverlayClosed()
    {
        if (_simulation is null)
        {
            return;
        }

        if (!_gameEnded)
        {
            _deductionOpen = false;
        }

        RefreshContextActions();
    }

    /// <summary>
    /// How long the cast takes to act once they agree. The night needs a shape,
    /// and a coalition that struck the instant it agreed would end the run before
    /// the player could see it coming - the whole point is watching it close.
    /// </summary>
    private const long ConfrontationGraceTicks = 45;

    /// <summary>
    /// Combined concern across the coalition needed before it will move. Two
    /// witnesses to one restricted-area entry score roughly 56 between them, so
    /// this takes a pattern rather than a single mistake.
    /// </summary>
    private const float ClosingNetThreshold = 90.0f;

    private void RefreshClosingNet()
    {
        if (_simulation is null || !_gameStarted || _gameEnded || _climaxOpen)
        {
            return;
        }

        AccusationCoalition? coalition = _simulation.EvaluateConspiracy(_humanHost);
        if (coalition is null || coalition.Target != _humanHost)
        {
            return;
        }

        if (coalition.Stage != _lastCoalitionStage)
        {
            AnnounceCoalition(coalition);
            _lastCoalitionStage = coalition.Stage;
        }

        // Agreement alone is not enough to arm it. Being seen once somewhere odd is
        // damning but survivable, so the bar is set in the coalition's own units,
        // well above the score two witnesses to a single incident would produce.
        // Deliberately not gated on ExposureLevel: that meter answers "how much do
        // you look like the Player", which is a different question from "how much
        // trouble are you in", and gating one on the other would let the meter
        // quietly misreport the danger.
        bool armed = coalition.ConsensusReached &&
            coalition.CombinedSuspicionScore >= ClosingNetThreshold;
        if (!armed)
        {
            return;
        }

        _confrontationTick ??= _simulation.CurrentTick + ConfrontationGraceTicks;
        long remaining = _confrontationTick.Value - _simulation.CurrentTick;
        if (remaining > 0)
        {
            // Runs after RefreshExposure, so this appends to the line it just wrote.
            // The player has to be able to watch it close, not just be told it did.
            if (_exposureLabel is not null)
            {
                _exposureLabel.Text += T(
                    $"\nThey will move on you in {remaining} minutes.",
                    $"\nพวกเขาจะลงมือกับคุณในอีก {remaining} นาที");
                _exposureLabel.AddThemeColorOverride("font_color", new Color("e06c75"));
            }

            // The line under the clock is where the player already looks for time
            // pressure, so the deadline that matters most takes it over.
            if (_currentLocationLabel is not null)
            {
                _currentLocationLabel.Text = T(
                    $"THEY WILL MOVE ON YOU IN {remaining} MINUTES",
                    $"พวกเขาจะลงมือกับคุณในอีก {remaining} นาที");
                _currentLocationLabel.AddThemeColorOverride("font_color", new Color("e06c75"));
            }

            return;
        }

        _ = _simulation.TriggerConfrontation(_simulation.GetLogicalLocation(_humanHost));
        OpenClimax(coalition);
    }

    private void AnnounceCoalition(AccusationCoalition coalition)
    {
        string who = string.Join(", ", coalition.Members
            .Where(member => member != _humanHost)
            .Select(DisplayName)
            .OrderBy(name => name, StringComparer.CurrentCulture));
        (string english, string thai) = coalition.Stage switch
        {
            CoalitionStage.ConsensusReached => (
                $"{who} have stopped comparing notes. They have agreed on something.",
                $"{who} หยุดเทียบข้อมูลกันแล้ว พวกเขาตกลงอะไรบางอย่างได้"),
            CoalitionStage.Confronting => (
                $"{who} are coming to find you.",
                $"{who} กำลังเดินมาหาคุณ"),
            _ => (
                $"{who} were talking quietly. They stopped when you came in.",
                $"{who} คุยกันเบา ๆ แล้วเงียบลงตอนคุณเดินเข้ามา"),
        };

        ShowShiftAlert(new ShiftBeat(
            _simulation?.CurrentTick ?? 0,
            ShiftBeatKind.FinalWarning,
            english,
            thai));
    }

    private void OpenClimax(AccusationCoalition coalition)
    {
        if (_simulation is null || _climaxOpen)
        {
            return;
        }

        _climaxOpen = true;
        _simulation.SetPaused(true);
        _worldAdapter.SetMovementPaused(true);

        string who = string.Join(", ", coalition.Members
            .Where(member => member != _humanHost)
            .Select(DisplayName)
            .OrderBy(name => name, StringComparer.CurrentCulture));
        ShowInvestigationScreen(
            T("THEY NAME YOU", "พวกเขาเอ่ยชื่อคุณ"),
            T(
                $"{who} block the way. Nobody raises their voice.\n\n" +
                    "They have been comparing what they saw all night, and every version " +
                    "of it ends with you. You do not get to ask any more questions.",
                $"{who} ยืนขวางทางไว้ ไม่มีใครขึ้นเสียง\n\n" +
                    "พวกเขาเทียบสิ่งที่เห็นกันมาทั้งคืน และทุกเวอร์ชันจบลงที่คุณ " +
                    "คุณไม่มีโอกาสถามอะไรอีกแล้ว"),
            new Color("e06c75"),
            [
                new InvestigationChoice(
                    T("Tell them what you really are", "บอกไปว่าคุณเป็นอะไรจริง ๆ"),
                    () => ResolveClimaxChoice(PlayerClimaxChoice.ConfessReality)),
                new InvestigationChoice(
                    T("Deny it and turn it back on them", "ปฏิเสธและย้อนกลับใส่พวกเขา"),
                    () => ResolveClimaxChoice(PlayerClimaxChoice.DenyAndCounter)),
                new InvestigationChoice(
                    T("Run", "วิ่ง"),
                    () => ResolveClimaxChoice(PlayerClimaxChoice.Flee)),
            ],
            allowClose: false,
            showPortrait: false);
    }

    private void ResolveClimaxChoice(PlayerClimaxChoice choice)
    {
        if (_simulation is null || _gameEnded)
        {
            return;
        }

        _gameEnded = true;
        _hasConcluded = true;
        RefreshProgress();
        ClimaxResolution resolution = _simulation.ResolveClimax(choice, _humanHost);

        // The player never got to make an accusation, so the case file cannot
        // close. What the night leaves behind is what they did when it was their
        // turn to be the suspect.
        ShowInvestigationScreen(
            LocalizeText(resolution.Title),
            LocalizeText(resolution.NarrativeText),
            new Color(resolution.PlayerVindicated ? "77dd77" : "e06c75"),
            [
                new InvestigationChoice(
                    T("REPLAY THE NIGHT", "เล่นกะคืนนี้ใหม่"),
                    RestartShift),
                new InvestigationChoice(
                    T("BACK TO TITLE", "กลับหน้าแรก"),
                    ReturnToTitle),
            ],
            allowClose: false,
            showPortrait: false);
    }

    private static readonly Dictionary<ExposureLevel, string> ExposureColors = new()
    {
        [ExposureLevel.Unnoticed] = "8091b3",
        [ExposureLevel.Noticed] = "d9c27a",
        [ExposureLevel.Watched] = "e2954a",
        [ExposureLevel.Cornered] = "e06c75",
    };

    private void RefreshExposure()
    {
        // Runs before the shift starts too, so the panel opens with a real reading
        // instead of a blank block the player has to wait out.
        if (_simulation is null)
        {
            return;
        }

        _exposure = _simulation.GetExposure(_humanHost);
        if (_exposureLabel is not null)
        {
            _exposureLabel.Text = ExposureFormatter.FormatSummary(_exposure, DisplayName, _isThai);
            _exposureLabel.AddThemeColorOverride(
                "font_color",
                new Color(ExposureColors[_exposure.Level]));
        }

        if (_exposureHeadingLabel is not null)
        {
            _exposureHeadingLabel.Text =
                $"{T("HOW YOU LOOK", "คนอื่นมองคุณอย่างไร")}  •  " +
                ExposureFormatter.FormatBadge(_exposure, _isThai);
        }

        // Only a rise is worth interrupting for. Exposure cannot fall inside one
        // shift - nobody forgets what they saw an hour ago - so this fires at most
        // three times a night and each one should land.
        if (_exposure.Level > _lastExposureLevel && !_gameEnded)
        {
            AnnounceExposureRise(_exposure);
        }

        _lastExposureLevel = _exposure.Level;
    }

    private void AnnounceExposureRise(ExposureReport exposure)
    {
        string summary = ExposureFormatter.FormatSummary(exposure, DisplayName, _isThai);
        ShowShiftAlert(new ShiftBeat(
            _simulation?.CurrentTick ?? 0,
            ShiftBeatKind.FinalWarning,
            summary,
            summary));
    }

    private void RefreshProgress()
    {
        if (_progressLabel is null)
        {
            return;
        }

        bool[] completed = [_hasMoved, _hasTalked, _hasInspected, _hasOpenedJournal, _hasConfronted, _hasConcluded];
        int completedCount = completed.Count(value => value);
        (string English, string Thai) nextStep = completedCount switch
        {
            0 => ("Move to a connected room", "ไปยังห้องที่เชื่อมต่อกัน"),
            1 => ("Talk to someone in your room", "คุยกับคนที่อยู่ห้องเดียวกัน"),
            2 => ("Inspect an object for a clue", "ตรวจสอบวัตถุเพื่อหาเบาะแส"),
            3 => ("Open the clue journal", "เปิดบันทึกเบาะแส"),
            4 => ("Use a clue to question someone", "ใช้เบาะแสถามใครสักคน"),
            _ => ("Make your final deduction when ready", "พร้อมแล้วให้สรุปคดี"),
        };
        if (_progressHeadingLabel is not null)
        {
            _progressHeadingLabel.Text = T("NEXT STEP", "ทำอะไรต่อ");
        }

        _progressLabel.Text = $"{T("Progress", "ความคืบหน้า")}: {completedCount}/6\n" +
            $"{T("Next", "ต่อไป")}: {T(nextStep.English, nextStep.Thai)}";
    }

    private void ShowMainMenu()
    {
        _gameStarted = false;
        _simulation?.SetPaused(true);
        _worldAdapter.SetMovementPaused(true);
        ShowInvestigationScreen(
            "YOU ARE NOT THE PLAYER",
            T(
                "Night shift. One hotel. One person is not acting on their own.\n\nFind the hidden Player before 05:00.",
                "กะกลางคืนหนึ่งคืน โรงแรมหนึ่งแห่ง และหนึ่งคนที่ไม่ได้ทำตามเจตจำนงของตัวเอง\n\nตามหาผู้ควบคุมที่ซ่อนอยู่ก่อน 05:00 น."),
            new Color("f1d18a"),
            [
                new InvestigationChoice(T("START NEW CASE", "เริ่มคดีใหม่"), ShowOnboarding),
                new InvestigationChoice(T("HOW TO PLAY", "วิธีเล่น"), ShowClueGuide),
                new InvestigationChoice(T("SETTINGS", "ตั้งค่า"), ShowSettings),
                new InvestigationChoice(T("EXIT GAME", "ออกจากเกม"), ExitGame),
            ],
            allowClose: false,
            showPortrait: false);
    }

    private void ShowSettings()
    {
        string language = _isThai ? "ภาษาไทย" : "English";
        string textSize = _largeText
            ? T("Large", "ขนาดใหญ่")
            : T("Standard", "ขนาดปกติ");
        ShowInvestigationScreen(
            T("SETTINGS", "ตั้งค่า"),
            $"{T("Interface language", "ภาษาหน้าจอ")}: {language}\n" +
            $"{T("Text size", "ขนาดตัวอักษร")}: {textSize}",
            new Color("77bdfb"),
            [
                new InvestigationChoice(
                    _isThai ? "ภาษา: ไทย" : "LANGUAGE: ENGLISH",
                    ToggleSettingLanguage),
                new InvestigationChoice(
                    _largeText ? T("USE STANDARD TEXT", "ใช้ตัวอักษรปกติ") : T("USE LARGER TEXT", "ใช้ตัวอักษรขนาดใหญ่"),
                    ToggleTextSize),
                new InvestigationChoice(T("BACK", "กลับ"), _gameStarted ? ShowPauseMenu : ShowMainMenu),
            ],
            allowClose: false,
            showPortrait: false);
    }

    private void SetMenuLanguage(bool useThai)
    {
        ApplyLanguage(useThai, showStatus: false);
        ShowSettings();
    }

    private void ToggleSettingLanguage() => SetMenuLanguage(!_isThai);

    private void ToggleTextSize()
    {
        _largeText = !_largeText;
        _investigationOverlay?.SetComfortableText(_largeText);
        ShowSettings();
    }

    private void ExitGame() => GetTree().Quit();

    private void ShowPauseMenu()
    {
        if (!_gameStarted)
        {
            ShowMainMenu();
            return;
        }

        _simulation?.SetPaused(true);
        _worldAdapter.SetMovementPaused(true);
        ShowInvestigationScreen(
            T("PAUSED", "หยุดเกมชั่วคราว"),
            T(
                "The night is paused. Review settings or return when you are ready.",
                "เวลาของกะถูกหยุดไว้ชั่วคราว ปรับตั้งค่าหรือกลับไปสืบสวนได้เมื่อพร้อม"),
            new Color("77bdfb"),
            [
                new InvestigationChoice(T("RESUME INVESTIGATION", "กลับไปสืบสวน"), ResumeInvestigation),
                new InvestigationChoice(T("SETTINGS", "ตั้งค่า"), ShowSettings),
                new InvestigationChoice(T("EXIT GAME", "ออกจากเกม"), ExitGame),
            ],
            allowClose: false,
            showPortrait: false);
    }

    private void ResumeInvestigation()
    {
        _simulation?.SetPaused(false);
        _worldAdapter.SetMovementPaused(false);
        _investigationOverlay?.HideScreen();
        RefreshStatus(T("The night continues.", "กะกลางคืนดำเนินต่อ"));
    }

    private void ShowOnboarding()
    {
        string body = T(
            "YOU ARE GEORGE, the night receptionist. Find who is controlled before 05:00.\n\n" +
            "1. MOVE — click a room on the floor plan.\n" +
            "2. OBSERVE — click a person to talk or follow; use Look Around for objects.\n" +
            "3. COMPARE — open the Case File and check where each clue came from.\n" +
            "4. DECIDE — make an accusation only when the pattern makes sense.\n\n" +
            "Time moves while you investigate. The menu button pauses the shift.",
            "คุณคือจอร์จ พนักงานต้อนรับกะกลางคืน ต้องหาให้พบว่าใครถูกควบคุมก่อน 05:00 น.\n\n" +
            "1. เดิน — คลิกห้องบนผังโรงแรม\n" +
            "2. สังเกต — คลิกคนเพื่อคุยหรือติดตาม และใช้ “สำรวจห้อง” เพื่อดูวัตถุ\n" +
            "3. เปรียบเทียบ — เปิดแฟ้มคดีแล้วดูที่มาของเบาะแส\n" +
            "4. ตัดสินใจ — กล่าวหาเมื่อรูปแบบของเรื่องราวสมเหตุผล\n\n" +
            "เวลาจะเดินระหว่างสืบสวน ปุ่มเมนูจะหยุดกะไว้ชั่วคราว");

        ShowInvestigationScreen(
            T("TONIGHT'S OBJECTIVE", "เป้าหมายของคืนนี้"),
            body,
            new Color("f1d18a"),
            [new InvestigationChoice(T("BEGIN THE NIGHT SHIFT", "เริ่มกะกลางคืน"), BeginInvestigation)],
            allowClose: false,
            showPortrait: false);
    }

    private void ShowClueGuide()
    {
        string body = T(
            "THE JOURNAL USES THREE SIMPLE PARTS:\n\n" +
            "[23:15] Anna entered the basement\n" +
            "George saw this himself  •  Very reliable\n\n" +
            "1. The clock tells you WHEN it happened.\n" +
            "2. The sentence tells you WHO did WHAT and WHERE.\n" +
            "3. The last line says whether George saw it or heard it from someone.\n\n" +
            "A second-hand story can be wrong. Compare clues before accusing anyone.",
            "บันทึกใช้ข้อมูลเพียงสามส่วน:\n\n" +
            "[23:15] แอนนาเข้าไปในชั้นใต้ดิน\n" +
            "จอร์จเห็นด้วยตัวเอง  •  น่าเชื่อถือมาก\n\n" +
            "1. ตัวเลขในวงเล็บคือเวลาที่เกิดเหตุ\n" +
            "2. ประโยคบอกว่าใครทำอะไรและอยู่ที่ไหน\n" +
            "3. บรรทัดล่างบอกว่าจอร์จเห็นเองหรือได้ยินจากใคร\n\n" +
            "เรื่องที่ได้ยินต่อกันมาอาจผิดได้ ควรเปรียบเทียบหลายเบาะแสก่อนกล่าวหา");

        ShowInvestigationScreen(
            T("HOW TO READ A CLUE", "วิธีอ่านเบาะแส"),
            body,
            new Color("77bdfb"),
            [new InvestigationChoice(T("START THE NIGHT SHIFT", "เริ่มกะกลางคืน"), BeginInvestigation)],
            allowClose: false,
            showPortrait: false);
    }

    private void BeginInvestigation()
    {
        _gameStarted = true;
        _simulation?.SetPaused(false);
        _worldAdapter.SetMovementPaused(false);
        _investigationOverlay?.HideScreen();
        RefreshStatus(
            T(
                "Start by clicking a connected room or a character in the current room.",
                "เริ่มจากคลิกห้องที่เชื่อมกัน หรือตัวละครที่อยู่ในห้องนี้"));
    }

    private void ToggleInsightView()
    {
        if (_gameEnded)
        {
            return;
        }

        _insightVisible = !_insightVisible;
        foreach ((EntityId actor, CharacterToken2D token) in _characterTokens)
        {
            token.SetActivity(ActivityText(actor));
            token.SetInsightVisible(_insightVisible);
        }

        UpdateInsightButton();
        RefreshStatus(
            _insightVisible
                ? T(
                    "Insight view: gold text shows inferred intentions, not guaranteed truth.",
                    "มุมมองเจตนา: ข้อความสีทองคือสิ่งที่คาดเดา ไม่ใช่ความจริงแน่นอน")
                : T("Insight view closed.", "ปิดมุมมองเจตนาแล้ว"));
    }

    private void UpdateInsightButton()
    {
        if (_insightButton is not null)
        {
            _insightButton.Text = InsightButtonText();
            _insightButton.TooltipText = T(
                "Reveal each character's inferred current intention",
                "แสดงเจตนาปัจจุบันที่จอร์จคาดเดาจากตัวละครแต่ละคน");
        }
    }

    private void OpenFinalDeduction(bool deadlineReached)
    {
        if (_simulation is null ||
            _caseDefinition is null ||
            _gameEnded ||
            _deductionOpen ||
            !_gameStarted)
        {
            return;
        }

        PlayerJournal journal = _simulation.GetPlayerJournal(_humanHost);
        if (!deadlineReached && journal.Entries.Count < 2)
        {
            ShowShiftAlert(new ShiftBeat(
                _simulation.CurrentTick,
                ShiftBeatKind.FinalWarning,
                "You need at least two journal entries before making a deduction.",
                "ต้องมีข้อมูลในบันทึกอย่างน้อยสองรายการก่อนสรุปคดี"));
            return;
        }

        var choices = _characters.Keys
            .Where(actor => actor != _humanHost)
            .OrderBy(actor => DisplayName(actor), StringComparer.CurrentCulture)
            .Select(actor =>
            {
                EntityId suspect = actor;
                return new InvestigationChoice(
                    $"{T("Accuse", "กล่าวหา")} {DisplayName(suspect)}",
                    () => ResolveFinalDeduction(suspect));
            })
            .ToList();

        string urgency = deadlineReached
            ? T(
                "Dawn has arrived. You must name the person secretly controlled by the Player.",
                "รุ่งเช้ามาถึงแล้ว คุณต้องระบุว่าใครคือคนที่ถูกผู้ควบคุมบงการอย่างลับ ๆ")
            : T(
                "End the shift now and name the person secretly controlled by the Player.",
                "จบกะตอนนี้และระบุว่าใครคือคนที่ถูกผู้ควบคุมบงการอย่างลับ ๆ");
        _deductionOpen = true;
        ShowInvestigationScreen(
            T("FINAL DEDUCTION", "สรุปคดีสุดท้าย"),
            $"{urgency}\n\n{T("Journal entries", "ข้อมูลในบันทึก")}: {journal.Entries.Count}\n" +
            T(
                "The hotel will remember a wrong accusation.",
                "โรงแรมจะจดจำหากคุณกล่าวหาคนผิด"),
            new Color(deadlineReached ? "e06c75" : "f1d18a"),
            choices,
            allowClose: !deadlineReached,
            showPortrait: false);
    }

    private void ResolveFinalDeduction(EntityId suspect)
    {
        if (_simulation is null || _caseDefinition is null || _truth is null || _gameEnded)
        {
            return;
        }

        _hasConcluded = true;
        _gameEnded = true;
        _simulation.SetPaused(true);
        _worldAdapter.SetMovementPaused(true);
        RefreshProgress();

        bool correct = _truth is not null && suspect == _truth.HiddenPlayer;
        PlayerJournal journal = _simulation.GetPlayerJournal(_humanHost);
        string title = correct
            ? T("THE NAME YOU SPOKE", "ชื่อที่คุณเอ่ยออกไป")
            : T("THE WRONG NAME", "ชื่อที่ไม่ใช่");
        string body = BuildAccusationNarrative(suspect, correct, journal);

        ShowInvestigationScreen(
            title,
            body,
            new Color(correct ? "77dd77" : "e06c75"),
            [new InvestigationChoice(
                T("SEE THE AFTERMATH", "ดูสิ่งที่เกิดขึ้นหลังจากนั้น"),
                () => ShowAftermath(suspect, correct, journal))],
            allowClose: false,
            showPortrait: false);
    }

    private string BuildAccusationNarrative(EntityId suspect, bool correct, PlayerJournal journal)
    {
        string clue = DescribeMostRelevantClue(journal);
        return correct
            ? T(
                $"At 05:00, George says {DisplayName(suspect)}'s name. The lobby goes still. {DisplayName(suspect)} does not argue; their answer arrives a beat too late, as if someone else is choosing it.\n\nThe clue that stayed with George:\n{clue}\n\nThen the basement door clicks open again — from George's side of the hotel.",
                $"เวลา 05:00 จอร์จเอ่ยชื่อ {DisplayName(suspect)} ล็อบบี้เงียบลงทันที {DisplayName(suspect)} ไม่เถียง แต่ตอบช้าราวกับมีใครอีกคนกำลังเลือกคำพูดแทน\n\nเบาะแสที่จอร์จนึกถึง:\n{clue}\n\nจากนั้นประตูชั้นใต้ดินก็ดังคลิกขึ้นอีกครั้ง จากฝั่งของจอร์จเอง")
            : T(
                $"At 05:00, George says {DisplayName(suspect)}'s name. For a moment, everyone accepts it. Then a new movement appears where no one should be.\n\nThe clue George overlooked:\n{clue}\n\nThe person you accused is frightened — but the Player is still moving somewhere in the hotel.",
                $"เวลา 05:00 จอร์จเอ่ยชื่อ {DisplayName(suspect)} ชั่วขณะหนึ่งทุกคนเชื่อว่าคดีจบแล้ว แต่มีการเคลื่อนไหวใหม่เกิดขึ้นในที่ที่ไม่มีใครควรอยู่\n\nเบาะแสที่จอร์จมองข้าม:\n{clue}\n\nคนที่คุณกล่าวหากลัวจริง แต่ผู้ควบคุมยังเคลื่อนไหวอยู่ที่ใดที่หนึ่งในโรงแรม");
    }

    private void ShowAftermath(EntityId suspect, bool correct, PlayerJournal journal)
    {
        string body = correct
            ? T(
                $"The hotel records {DisplayName(suspect)}'s movements, but they cannot explain the final door. George realizes the truth: finding the controlled person did not mean he was outside the game.\n\nWhat the case leaves behind\n• The Player can use ordinary routines as cover.\n• George's own actions can become evidence for someone else.\n• The basement is no longer only a locked room.\n\nThis night is closed. The hotel is not.",
                $"บันทึกของโรงแรมยืนยันการเคลื่อนไหวของ {DisplayName(suspect)} แต่ไม่อาจอธิบายประตูบานสุดท้ายได้ จอร์จจึงเข้าใจว่า การหาคนที่ถูกควบคุมพบไม่ได้แปลว่าเขาอยู่นอกเกม\n\nสิ่งที่คดีนี้ทิ้งไว้\n• ผู้ควบคุมใช้กิจวัตรธรรมดาเป็นฉากบังหน้าได้\n• การกระทำของจอร์จเองอาจกลายเป็นหลักฐานให้คนอื่น\n• ชั้นใต้ดินไม่ใช่เพียงห้องที่ถูกล็อกอีกต่อไป\n\nคดีคืนนี้ปิดลงแล้ว แต่โรงแรมยังไม่จบ")
            : T(
                $"Before dawn, {DisplayName(suspect)} is allowed to leave. The staff will remember George's certainty — and the damage it caused.\n\nWhat the case leaves behind\n• A convincing story is not the same as a direct observation.\n• A clue needs a source before it becomes an accusation.\n• The Player benefits whenever people stop comparing notes.\n\nThe next night begins with less trust than the last.",
                $"ก่อนรุ่งเช้า {DisplayName(suspect)} ได้รับอนุญาตให้ออกไป พนักงานทุกคนจะจดจำความมั่นใจของจอร์จ และผลเสียที่ตามมา\n\nสิ่งที่คดีนี้ทิ้งไว้\n• เรื่องที่ฟังน่าเชื่อไม่เท่ากับสิ่งที่เห็นด้วยตา\n• เบาะแสต้องมีที่มาก่อนจะกลายเป็นคำกล่าวหา\n• ผู้ควบคุมได้ประโยชน์ทุกครั้งที่คนหยุดเปรียบเทียบข้อมูล\n\nกะถัดไปเริ่มขึ้นด้วยความไว้วางใจที่น้อยกว่าเดิม");

        ShowInvestigationScreen(
            T("AFTERMATH", "หลังจากคืนนั้น"),
            body,
            new Color(correct ? "77dd77" : "e06c75"),
            [
                new InvestigationChoice(T("REPLAY THE NIGHT", "เล่นกะคืนนี้ใหม่"), RestartShift),
                new InvestigationChoice(T("BACK TO TITLE", "กลับหน้าแรก"), ReturnToTitle),
            ],
            allowClose: false,
            showPortrait: false);
    }

    private string DescribeMostRelevantClue(PlayerJournal journal)
    {
        PlayerJournalEntry? entry = journal.Entries
            .OrderByDescending(item => item.Confidence)
            .ThenByDescending(item => item.EventTime.Tick)
            .FirstOrDefault();
        return entry is null
            ? T("No single clue was enough. The pattern was the warning.", "ไม่มีเบาะแสชิ้นเดียวที่เพียงพอ รูปแบบของเรื่องต่างหากคือคำเตือน")
            : $"[{JournalPresentationFormatter.FormatClock(entry.EventTime.Tick)}] " +
                JournalPresentationFormatter.FormatHeadline(entry, DisplayName, DisplayLocation, _isThai);
    }

    // ReloadCurrentScene builds a brand new node, so the replay intent cannot ride
    // on an instance field. Without this both endings' buttons reloaded into the
    // title menu and "replay the night" was indistinguishable from "back to title".
    private static ulong? _replaySeed;

    private void RestartShift()
    {
        _replaySeed = NextReplaySeed(_truth?.Seed ?? _caseDefinition?.Seed ?? 0UL);
        GetTree().ReloadCurrentScene();
    }

    private void ReturnToTitle()
    {
        _replaySeed = null;
        GetTree().ReloadCurrentScene();
    }

    // SplitMix64's mixing step: consecutive replays land far apart in the seed
    // space, so the next night is a genuinely different case rather than a
    // neighbour of the last one.
    private static ulong NextReplaySeed(ulong seed)
    {
        ulong next = unchecked(seed + 0x9E3779B97F4A7C15UL);
        next = unchecked((next ^ (next >> 30)) * 0xBF58476D1CE4E5B9UL);
        next = unchecked((next ^ (next >> 27)) * 0x94D049BB133111EBUL);
        return next ^ (next >> 31);
    }

    private void ApplyLanguage(bool useThai, bool showStatus)
    {
        _isThai = useThai;
        if (_caseTitleLabel is not null)
        {
            _caseTitleLabel.Text = CaseTitle();
        }

        if (_instructionLabel is not null)
        {
            _instructionLabel.Text = T(
                "FLOOR PLAN  •  CLICK A ROOM TO MOVE  •  CLICK A PERSON TO ACT",
                "ผังโรงแรม  •  คลิกห้องเพื่อเดิน  •  คลิกคนเพื่อทำสิ่งต่าง ๆ");
        }

        if (_roleLabel is not null)
        {
            _roleLabel.Text = T("YOUR ROLE", "บทบาทของคุณ");
        }

        if (_roleValueLabel is not null && _characters.TryGetValue(_humanHost, out CharacterDefinition? host))
        {
            _roleValueLabel.Text = RoleText(host);
        }

        if (_objectiveHeadingLabel is not null)
        {
            _objectiveHeadingLabel.Text = T("CURRENT OBJECTIVE", "เป้าหมายปัจจุบัน");
        }

        if (_objectiveLabel is not null)
        {
            _objectiveLabel.Text = ObjectiveText();
        }

        if (_caseFeedLabel is not null)
        {
            _caseFeedLabel.Text = T("WHAT JUST HAPPENED", "สิ่งที่เพิ่งเกิด");
        }

        if (_progressHeadingLabel is not null)
        {
            _progressHeadingLabel.Text = T("NEXT STEP", "ทำอะไรต่อ");
        }

        if (_languageButton is not null)
        {
            _languageButton.Text = MenuButtonText();
            _languageButton.TooltipText = T("Open menu and settings", "เปิดเมนูและตั้งค่า");
        }

        foreach ((string id, Button button) in _roomButtons)
        {
            HotelLocationDefinition? location = _hotel?.Locations.SingleOrDefault(item => item.Id == id);
            if (location is not null)
            {
                bool isCurrent = _simulation?.PlayerController.CurrentLocation.Value == id;
                int? occupantCount = _simulation is null
                    ? null
                    : _characters.Keys.Count(actor => _simulation.GetLogicalLocation(actor).Value == id);
                SetRoomLabel(location, isCurrent, occupantCount);
                button.TooltipText = RoomTooltip(location);
            }
        }

        if (_talkButton is not null)
        {
            _talkButton.Text = T("1  TALK", "1  คุย");
            _talkButton.TooltipText = T("Ask the selected character", "ถามตัวละครที่เลือก");
        }

        if (_followButton is not null)
        {
            _followButton.TooltipText = T(
                "Follow the selected character between rooms",
                "ติดตามตัวละครที่เลือกเมื่อย้ายห้อง");
        }

        if (_inspectButton is not null)
        {
            _inspectButton.Text = T("LOOK AROUND", "สำรวจห้อง");
            _inspectButton.TooltipText = T(
                "See what George can inspect in this room",
                "ดูสิ่งที่จอร์จตรวจสอบได้ในห้องนี้");
        }

        if (_journalButton is not null)
        {
            _journalButton.Text = T("OPEN CASE FILE", "เปิดแฟ้มคดี");
            _journalButton.TooltipText = T(
                "Review the clues George remembers",
                "ทบทวนเบาะแสที่จอร์จจำได้");
        }

        if (_evidenceButton is not null)
        {
            _evidenceButton.Text = T("5  USE A CLUE", "5  ใช้เบาะแส");
            _evidenceButton.TooltipText = T(
                "Select evidence to confront someone",
                "เลือกหลักฐานเพื่อเผชิญหน้า");
        }

        if (_deduceButton is not null)
        {
            _deduceButton.Text = T("MAKE AN ACCUSATION", "กล่าวหาผู้ต้องสงสัย");
            _deduceButton.TooltipText = T(
                "Name who is secretly being controlled by the Player",
                "ระบุว่าใครกำลังถูกผู้ควบคุมบงการอย่างลับ ๆ");
        }

        if (_actorSelector is not null)
        {
            _actorSelector.TooltipText = T(
                "Characters currently in the same room",
                "ตัวละครที่อยู่ห้องเดียวกัน");
        }

        if (_objectSelector is not null)
        {
            _objectSelector.TooltipText = T(
                "Objects currently in the same room",
                "วัตถุที่อยู่ห้องเดียวกัน");
        }

        foreach ((EntityId actor, CharacterToken2D token) in _characterTokens)
        {
            token.SetDisplayName(DisplayName(actor));
            token.SetActivity(ActivityText(actor));
        }

        if (_investigationOverlay is not null)
        {
            _investigationOverlay.SetLanguage(_isThai);
        }

        if (_simulation is not null)
        {
            if (_clockLabel is not null)
            {
                _clockLabel.Text = ClockText(_simulation.CurrentTick);
            }

            RefreshContextActions();
            RefreshProgress();
        }

        UpdateInsightButton();

        RefreshEventFeed();

        if (showStatus)
        {
            RefreshStatus(_isThai ? "เปลี่ยนภาษาเป็นภาษาไทยแล้ว" : "Language switched to English.");
        }
    }

    private string LocalizeText(string text)
    {
        string localized = text;
        foreach (EntityId actor in _characters.Keys)
        {
            localized = localized.Replace(
                actor.Value,
                DisplayName(actor),
                StringComparison.OrdinalIgnoreCase);
        }

        if (!_isThai)
        {
            return localized;
        }

        foreach ((string id, string thai) in new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["lobby"] = "ล็อบบี้โรงแรม",
            ["hallway"] = "โถงทางเดินหลัก",
            ["kitchen"] = "ห้องครัว",
            ["room-201"] = "ห้อง 201",
            ["basement"] = "ชั้นใต้ดิน",
            ["garden"] = "สวนด้านนอก",
            ["security-room"] = "ห้องกล้องวงจรปิด",
            ["office"] = "ห้องผู้จัดการ",
            ["Guest Logbook"] = "สมุดทะเบียนผู้เข้าพัก",
            ["Main Electrical Fusebox"] = "ตู้ฟิวส์ไฟฟ้าหลัก",
            ["Brass Reception Bell"] = "กระดิ่งต้อนรับทองเหลือง",
            ["Kitchen Wall Safe"] = "ตู้นิรภัยติดผนังห้องครัว",
            ["Culinary Knife Block"] = "บล็อกมีดทำครัว",
            ["Locked Leather Briefcase"] = "กระเป๋าเอกสารหนังที่ล็อกไว้",
            ["Nightstand Drawer"] = "ลิ้นชักข้างเตียง",
            ["Hidden Black Ledger"] = "สมุดบัญชีดำที่ซ่อนอยู่",
            ["Hollow Marble Statue"] = "รูปปั้นหินอ่อนกลวง",
            ["CCTV Surveillance Terminal"] = "เครื่องควบคุมกล้องวงจรปิด",
            ["Manager Executive Desk"] = "โต๊ะทำงานผู้จัดการ",
            ["chef-key"] = "กุญแจของพ่อครัว",
            ["briefcase-code"] = "รหัสกระเป๋าเอกสาร",
            ["security-passcode"] = "รหัสผ่านห้องรักษาความปลอดภัย",
            ["A polished bell on the marble desk. Rings with a clear, sharp chime."] = "กระดิ่งขัดเงาบนโต๊ะหินอ่อน ส่งเสียงใสและคมเมื่อกด",
            ["The registry lists guests in rooms 101-305. A page dated yesterday has a torn entry."] = "ทะเบียนระบุผู้เข้าพักห้อง 101-305 แต่หน้าของเมื่อวานมีรายการถูกฉีกออก",
            ["Inside the safe lies an old duplicate key marked 'BASEMENT MASTER'."] = "ในตู้นิรภัยมีกุญแจสำรองเก่าที่เขียนว่า 'กุญแจหลักห้องใต้ดิน'",
            ["A heavy wooden block holding sharp knives. One slot is suspiciously vacant."] = "แท่นไม้หนักสำหรับเก็บมีดคม มีช่องหนึ่งว่างอย่างน่าสงสัย",
            ["Contains encrypted correspondence detailing clandestine midnight meetings."] = "ข้างในมีจดหมายเข้ารหัสที่บอกรายละเอียดการนัดพบลับยามเที่ยงคืน",
            ["A handwritten hotel postcard with coordinates scribbled in pencil: 'Under the Garden Statue'."] = "โปสการ์ดโรงแรมเขียนด้วยลายมือ มีพิกัดดินสอว่า 'ใต้รูปปั้นในสวน'",
            ["A black ledger documenting unauthorized surveillance on hotel residents."] = "สมุดบัญชีดำบันทึกการสอดส่องผู้พักอาศัยโดยไม่ได้รับอนุญาต",
            ["The central power breaker for the hotel corridors. Can disrupt lighting."] = "เบรกเกอร์ไฟหลักของโถงโรงแรม สามารถทำให้แสงสว่างขัดข้องได้",
            ["Hidden inside the hollow base is an ornate silver key labeled 'CHEF PRIVATE KEY'."] = "ฐานกลวงซ่อนกุญแจเงินประดับลาย เขียนว่า 'กุญแจส่วนตัวของพ่อครัว'",
            ["The surveillance feed displays timestamped camera archives of the restricted basement."] = "ภาพจากกล้องมีบันทึกเวลาและแสดงภาพย้อนหลังของชั้นใต้ดินหวงห้าม",
            ["Staff roster and incident reports indicating suspicious late-night behavior around Room 201."] = "ตารางเวรและรายงานเหตุการณ์ชี้พฤติกรรมน่าสงสัยยามดึกบริเวณห้อง 201",
        })
        {
            localized = localized.Replace(id, thai, StringComparison.OrdinalIgnoreCase);
        }

        foreach ((string english, string thai) in new[]
        {
            ("You observed: ", "คุณสังเกต: "),
            (" told you: saw ", " บอกคุณว่าเห็น "),
            (" did ", " ทำ "),
            (" at ", " ที่ "),
            (" [tick ", " [เวลา "),
            ("LOCATION:", "สถานที่:"),
            ("TIME:", "เวลา:"),
            ("TIMELINE FILTER:", "ตัวกรองไทม์ไลน์:"),
            ("KNOWN EVENTS", "เหตุการณ์ที่ทราบ"),
            ("SUSPICION NOTES", "บันทึกความน่าสงสัย"),
            ("source: direct observation", "แหล่งข้อมูล: สังเกตโดยตรง"),
            ("source: ", "แหล่งข้อมูล: "),
            ("root event:", "เหตุการณ์ต้นทาง:"),
            ("confidence:", "ความมั่นใจ:"),
            ("No reliable observations or rumors recorded yet.", "ยังไม่มีการสังเกตหรือข่าวลือที่บันทึกไว้"),
            ("No timeline entries match this filter.", "ไม่พบเหตุการณ์ตามตัวกรองนี้"),
            ("No suspicion supported by evidence yet.", "ยังไม่มีหลักฐานสนับสนุนความน่าสงสัย"),
            ("all entries", "ทั้งหมด"),
            ("subject:", "ประเด็น:"),
            ("room:", "ห้อง:"),
            ("kind:", "ประเภท:"),
            ("event:", "เหตุการณ์:"),
            ("from T", "ตั้งแต่ T"),
            ("Episodic", "เหตุการณ์ที่เห็นเอง"),
            ("Social", "ข่าวลือทางสังคม"),
            ("score", "คะแนน"),
            ("evidence", "หลักฐาน"),
            ("secrecy", "การปกปิด"),
            ("role deviation", "เบี่ยงเบนบทบาท"),
            ("meta", "พฤติกรรมเมตา"),
            ("impossible", "เป็นไปไม่ได้"),
            ("The conversation could not continue.", "ไม่สามารถสนทนาต่อได้"),
            ("Object '", "วัตถุ '"),
            (" was not found in the hotel.", " ไม่พบในโรงแรม"),
            ("You inspect ", "คุณตรวจสอบ "),
            ("Cannot inspect ", "ไม่สามารถตรวจสอบ "),
            (" because you are at ", " เพราะคุณอยู่ที่ "),
            (" but the object is at ", " แต่วัตถุอยู่ที่ "),
            (" is securely locked. (Requires: ", " ถูกล็อกแน่นหนา (ต้องใช้: "),
            ("EnterLocation", "เข้าห้อง"),
            ("LeaveLocation", "ออกจากห้อง"),
            ("Theft", "การขโมย"),
            ("SecretMeeting", "การนัดลับ"),
            ("NightActivity", "กิจกรรมกลางคืน"),
            ("Interaction", "โต้ตอบ"),
            ("RoleDutyMissed", "ละเลยหน้าที่"),
            ("BoundaryProbe", "ทดสอบเขตหวงห้าม"),
            ("BehaviorPattern", "รูปแบบพฤติกรรม"),
            ("ShareInformation", "แบ่งปันข้อมูล"),
            ("AskInformation", "ถามข้อมูล"),
            ("RealityAnomaly", "ความผิดปกติของความจริง"),
            ("The object", "วัตถุ"),
            ("was not found in the hotel.", "ไม่พบในโรงแรม"),
        })
        {
            localized = localized.Replace(english, thai, StringComparison.OrdinalIgnoreCase);
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
            RefreshStatus(
                $"{T("Moving", "กำลังย้าย")} {DisplayName(_humanHost)} " +
                $"{T("to", "ไปยัง")} {DisplayLocation(destination)}...");
        }
        else if (execution.Status == NpcMovementExecutionStatus.Failed)
        {
            RefreshStatus(
                $"{T("No accessible route to", "ไม่มีเส้นทางไปยัง")} {DisplayLocation(destination)}");
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
                "dialogue=pass inspect=pass journal=pass exposure=pass claims=pass");
            GetTree().Quit(0);
        }
        else if (_smokeElapsed >= 8.0)
        {
            GD.PushError("HOTEL_2D_SMOKE_FAIL movement did not complete within 8 seconds");
            GetTree().Quit(1);
        }
    }

    private bool _smokeExposureCompleted;

    private void RunExposureSmoke()
    {
        if (_simulation is null || _smokeExposureCompleted)
        {
            return;
        }

        _smokeExposureCompleted = true;

        ExposureReport before = _simulation.GetExposure(_humanHost);
        if (before.Level != ExposureLevel.Unnoticed)
        {
            throw new InvalidOperationException(
                $"The host started the shift already exposed ('{before.Level}').");
        }

        ObjectActionResult tamper = _simulation.PlayerController.TamperObject("lobby-guest-registry");
        if (!tamper.Succeeded)
        {
            throw new InvalidOperationException($"Smoke tamper failed: {tamper.Message}");
        }

        // Perception, memory and suspicion all run on tick advance, and the shift
        // is paused while this runs, so the act has to be given time to be seen.
        for (int index = 0; index < 4; index++)
        {
            _simulation.Step();
        }

        HandleSimulationChanges();
        RefreshExposure();

        ExposureReport after = _simulation.GetExposure(_humanHost);
        if (after.Peak <= before.Peak || after.Observers.Count == 0)
        {
            throw new InvalidOperationException(
                "Tampering in front of a witness did not raise the host's exposure " +
                $"(peak {before.Peak:F1} -> {after.Peak:F1}).");
        }

        string summary = ExposureFormatter.FormatSummary(after, DisplayName, _isThai);
        string detail = ExposureFormatter.FormatDetail(after, DisplayName, _isThai);
        if (string.IsNullOrWhiteSpace(summary) || string.IsNullOrWhiteSpace(detail))
        {
            throw new InvalidOperationException("Exposure produced no player-facing text.");
        }

        // Same rule as the rest of the player-facing pass: no raw scores, no
        // internal dimension names, no entity ids.
        foreach (string leak in new[] { "MetaBehavior", "RoleDeviation", "ImpossibleBehavior", "george" })
        {
            if (summary.Contains(leak, StringComparison.Ordinal) ||
                detail.Contains(leak, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Exposure text leaked '{leak}'.");
            }
        }

        if (_isThai && detail.Any(character => character is >= 'a' and <= 'z'))
        {
            throw new InvalidOperationException("Thai exposure detail contained English text.");
        }

        GD.Print($"HOTEL_2D_EXPOSURE level={after.Level} observers={after.Observers.Count}");
    }

    private bool _smokeClaimsCompleted;

    // Walks the second half of the loop: ask someone to account for themselves,
    // check the statement is recorded as something separate from a clue, and
    // check the page that shows it renders in the player's own language.
    private void RunContradictionSmoke()
    {
        if (_simulation is null || _smokeClaimsCompleted)
        {
            return;
        }

        _smokeClaimsCompleted = true;

        IReadOnlyList<EntityId> present = _simulation.PlayerController.GetPresentActors();
        EntityId partner = present.FirstOrDefault(actor => actor != _humanHost);
        if (partner.IsEmpty)
        {
            throw new InvalidOperationException("No one was present to give an account.");
        }

        int before = _simulation.Claims.Count;
        DialogueOutcome asked = _simulation.Talk(new DialogueRequest(
            DialogueActionKind.InquireSchedule,
            _humanHost,
            partner));
        if (!asked.Succeeded || asked.Claim is null)
        {
            throw new InvalidOperationException(
                "Asking about a shift did not put a checkable claim on the record.");
        }

        if (_simulation.Claims.Count != before + 1 ||
            _simulation.Claims[^1].Speaker != partner)
        {
            throw new InvalidOperationException("The claim ledger did not record the statement.");
        }

        string page = ClaimPresentationFormatter.FormatClaims(
            _simulation.Claims,
            _simulation.FindContradictions(_humanHost),
            DisplayName,
            DisplayLocation,
            _isThai);
        if (string.IsNullOrWhiteSpace(page))
        {
            throw new InvalidOperationException("The statements page rendered nothing.");
        }

        foreach (string leak in new[] { "AlibiClaim", "george", "charlie" })
        {
            if (page.Contains(leak, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"The statements page leaked '{leak}'.");
            }
        }

        if (_isThai && page.Any(character => character is >= 'a' and <= 'z'))
        {
            throw new InvalidOperationException("Thai statements page contained English text.");
        }

        GD.Print(
            $"HOTEL_2D_CLAIMS recorded={_simulation.Claims.Count} " +
            $"contradictions={_simulation.FindContradictions(_humanHost).Count}");
    }

    private bool RunInvestigationSmoke()
    {
        if (_simulation is null || _investigationOverlay is null)
        {
            return false;
        }

        try
        {
            if (_progressLabel is null ||
                _insightButton is null ||
                _deduceButton is null ||
                _languageButton is null ||
                _talkButton is null ||
                _inspectButton is null ||
                _journalButton is null ||
                _evidenceButton is null)
            {
                throw new InvalidOperationException("Guided investigation controls are incomplete.");
            }

            if (_progressLabel.Text.Split('\n').Length != 2 ||
                !_progressLabel.Text.Contains(T("Progress", "ความคืบหน้า"), StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Next-step investigation summary did not render.");
            }

            _simulation.PlayerController.SetPlayerEntity(_humanHost);
            IReadOnlyList<EntityId> actors = _simulation.PlayerController.GetPresentActors();
            IReadOnlyList<InteractiveObject> objects = _simulation.GetPresentObjects();
            if (actors.Count == 0 || objects.Count == 0)
            {
                throw new InvalidOperationException(
                    "Opening location must contain an actor and an interactive object.");
            }

            var scheduleRequest = new DialogueRequest(
                DialogueActionKind.InquireSchedule,
                _humanHost,
                actors[0]);
            DialogueOutcome dialogue = _simulation.Talk(scheduleRequest);
            ObjectActionResult inspection = _simulation.InspectObject(objects[0].Id);
            string dialogueText = FormatDialogue(actors[0], scheduleRequest, dialogue, null);
            PlayerJournal journal = _simulation.GetPlayerJournal(_humanHost);
            string journalText = JournalPresentationFormatter.Format(
                journal,
                DisplayName,
                DisplayLocation,
                useThai: _isThai);
            string filteredTimeline = JournalPresentationFormatter.Format(
                journal,
                DisplayName,
                DisplayLocation,
                new TimelineFilter(Kind: MemoryKind.Episodic),
                useThai: _isThai);
            if (!dialogue.Succeeded ||
                string.IsNullOrWhiteSpace(dialogueText) ||
                dialogueText.Contains("raw", StringComparison.OrdinalIgnoreCase) ||
                !inspection.Succeeded ||
                string.IsNullOrWhiteSpace(journalText))
            {
                throw new InvalidOperationException(
                    "Investigation smoke validation did not produce a valid result.");
            }

            string expectedFilterHeading = _isThai ? "กำลังแสดง:" : "Showing:";
            if (!filteredTimeline.Contains(expectedFilterHeading, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Timeline filter did not render.");
            }

            if (_isThai &&
                (dialogueText.Contains("George", StringComparison.OrdinalIgnoreCase) ||
                 journalText.Contains("Source:", StringComparison.OrdinalIgnoreCase) ||
                 filteredTimeline.Contains("Showing:", StringComparison.OrdinalIgnoreCase) ||
                 ObjectiveText().Contains("Player", StringComparison.OrdinalIgnoreCase) ||
                 LocalizeText("Kitchen Wall Safe is securely locked. (Requires: chef-key)")
                     .Contains("chef-key", StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("Thai investigation text contains untranslated UI copy.");
            }

            ToggleFollowSelected();
            if (_followTarget is null)
            {
                throw new InvalidOperationException("Follow target could not be selected.");
            }

            ToggleFollowSelected();
            OpenEvidenceSelection();
            if (!_investigationOverlay.IsOpen)
            {
                throw new InvalidOperationException("Evidence selection overlay did not open.");
            }

            _investigationOverlay.ShowScreen(
                "SMOKE JOURNAL",
                journalText,
                new Color("77bdfb"));
            if (!_investigationOverlay.IsOpen)
            {
                throw new InvalidOperationException("Investigation overlay did not open.");
            }

            RunExposureSmoke();
            RunContradictionSmoke();

            _investigationOverlay.HideScreen();
            _worldAdapter.SetMovementPaused(false);
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

    private string DisplayName(EntityId actor)
    {
        if (_isThai)
        {
            return actor.Value.ToLowerInvariant() switch
            {
                "george" => "จอร์จ",
                "anna" => "แอนนา",
                "bob" => "บ็อบ",
                "charlie" => "คลารา",
                "dana" => "เอเลียส",
                "evelyn" => "มิรา",
                _ => actor.Value,
            };
        }

        return _characters.TryGetValue(actor, out CharacterDefinition? character)
            ? character.DisplayName
            : actor.Value;
    }

    private string DisplayRole(CharacterDefinition character)
    {
        if (!_isThai)
        {
            return character.Role;
        }

        return character.Id.ToLowerInvariant() switch
        {
            "george" => "พนักงานต้อนรับ",
            "anna" => "แม่บ้าน",
            "bob" => "เจ้าหน้าที่รักษาความปลอดภัย",
            "charlie" => "แขก",
            "dana" => "พ่อครัว",
            "evelyn" => "ผู้จัดการ",
            _ => character.Role,
        };
    }

    private string ActivityText(EntityId actor)
    {
        if (actor == _humanHost)
        {
            return T("Player controlled", "ควบคุมโดยผู้เล่น");
        }

        return _npcActivities.TryGetValue(actor, out (string English, string Thai) activity)
            ? T(activity.English, activity.Thai)
            : T("Intent unknown", "ไม่ทราบเจตนา");
    }

    private string RoleText(CharacterDefinition character) =>
        $"{DisplayName(new EntityId(character.Id))}\n{DisplayRole(character)}";

    private string DisplayLocation(LocationId location) => DisplayLocation(location, _isThai);

    private string DisplayLocation(LocationId location, bool useThai)
    {
        if (useThai)
        {
            return location.Value.ToLowerInvariant() switch
            {
                "lobby" => "ล็อบบี้โรงแรม",
                "hallway" => "โถงทางเดินหลัก",
                "kitchen" => "ห้องครัว",
                "room-201" => "ห้อง 201",
                "basement" => "ชั้นใต้ดิน",
                "garden" => "สวนด้านนอก",
                "security-room" => "ห้องกล้องวงจรปิด",
                "office" => "ห้องผู้จัดการ",
                _ => location.Value,
            };
        }

        return _hotel?.Locations.SingleOrDefault(item => item.Id == location.Value)?.DisplayName ??
            location.Value;
    }

    private string DisplayObject(InteractiveObject obj)
    {
        if (!_isThai)
        {
            return obj.DisplayName;
        }

        return obj.Id.ToLowerInvariant() switch
        {
            "lobby-reception-bell" => "กระดิ่งต้อนรับทองเหลือง",
            "lobby-guest-registry" => "สมุดทะเบียนผู้เข้าพัก",
            "kitchen-pantry-safe" => "ตู้นิรภัยติดผนังห้องครัว",
            "kitchen-service-knife-block" => "บล็อกมีดทำครัว",
            "room201-briefcase" => "กระเป๋าเอกสารหนังที่ล็อกไว้",
            "room201-nightstand-drawer" => "ลิ้นชักข้างเตียง",
            "basement-incriminating-ledger" => "สมุดบัญชีดำที่ซ่อนอยู่",
            "basement-fusebox" => "ตู้ฟิวส์ไฟฟ้าหลัก",
            "garden-statue-stash" => "รูปปั้นหินอ่อนกลวง",
            "security-cctv-terminal" => "เครื่องควบคุมกล้องวงจรปิด",
            "office-manager-desk" => "โต๊ะทำงานผู้จัดการ",
            _ => obj.DisplayName,
        };
    }

    private string DisplayEventType(string eventType)
    {
        if (!_isThai)
        {
            return eventType switch
            {
                nameof(EventType.EnterLocation) => "Arrived",
                nameof(EventType.LeaveLocation) => "Left",
                nameof(EventType.ShareInformation) => "Shared info",
                nameof(EventType.AskInformation) => "Asked",
                nameof(EventType.Interaction) => "Checked something",
                _ => eventType,
            };
        }

        return eventType switch
        {
            nameof(EventType.EnterLocation) => "เข้าห้อง",
            nameof(EventType.LeaveLocation) => "ออกจากห้อง",
            nameof(EventType.Theft) => "การขโมย",
            nameof(EventType.SecretMeeting) => "การนัดลับ",
            nameof(EventType.NightActivity) => "กิจกรรมกลางคืน",
            nameof(EventType.Interaction) => "ตรวจบางอย่าง",
            nameof(EventType.RoleDutyMissed) => "ละเลยหน้าที่",
            nameof(EventType.BoundaryProbe) => "ทดสอบเขตหวงห้าม",
            nameof(EventType.BehaviorPattern) => "รูปแบบพฤติกรรม",
            nameof(EventType.ShareInformation) => "แบ่งปันข้อมูล",
            nameof(EventType.AskInformation) => "ถามข้อมูล",
            nameof(EventType.RealityAnomaly) => "ความผิดปกติของความจริง",
            _ => eventType,
        };
    }

    private string FormatEvent(WorldEvent worldEvent) =>
        $"[{JournalPresentationFormatter.FormatClock(worldEvent.Time.Tick)}]  {DisplayName(worldEvent.Actor)} • " +
        $"{DisplayEventType(worldEvent.Type.ToString())} — {DisplayLocation(worldEvent.Location)}";

    private string CaseTitle() =>
        _caseDefinition is null
            ? T("THE BASEMENT DOOR", "ประตูห้องใต้ดิน")
            : T(_caseDefinition.Title.ToUpperInvariant(), "ประตูห้องใต้ดิน");

    private string ObjectiveText() =>
        T(
            "Investigate the basement door and identify the hidden Player before dawn.",
            "สืบสวนประตูห้องใต้ดินและหาผู้ควบคุมที่ซ่อนอยู่ให้พบก่อนรุ่งเช้า");

    private void SetRoomLabel(
        HotelLocationDefinition location,
        bool isCurrent,
        int? occupantCount)
    {
        if (_roomLabels.TryGetValue(new LocationId(location.Id), out Label? label))
        {
            label.Text = RoomButtonText(location, isCurrent, occupantCount);
            label.AddThemeColorOverride(
                "font_color",
                isCurrent ? new Color("f1d18a") : Colors.White);
        }
    }

    private string RoomButtonText(
        HotelLocationDefinition location,
        bool isCurrent = false,
        int? occupantCount = null)
    {
        string prefix = isCurrent ? "●  " : string.Empty;
        string name = DisplayLocation(new LocationId(location.Id));
        string icon = RoomIcon(location.Id);
        // Seven rooms all announcing "0 people" was noise that buried the one
        // room where anybody actually was.
        string people = occupantCount is null or 0
            ? string.Empty
            : $"  •  {occupantCount} {T("people", "คน")}";
        return location.Restricted
            ? $"{prefix}{icon}  {name}{people}\n{T("[RESTRICTED]", "[พื้นที่หวงห้าม]")}"
            : $"{prefix}{icon}  {name}{people}";
    }

    private static string RoomIcon(string locationId) => locationId switch
    {
        "lobby" => "◆",
        "hallway" => "↔",
        "kitchen" => "▤",
        "room-201" => "□",
        "basement" => "▼",
        "garden" => "✦",
        "security-room" => "◉",
        "office" => "▣",
        _ => "◇",
    };

    private string RoomTooltip(HotelLocationDefinition location) =>
        $"{T("Move George to", "ย้ายจอร์จไปที่")} {DisplayLocation(new LocationId(location.Id))}";

    private string MenuButtonText() => T("MENU", "เมนู");

    private string InsightButtonText() =>
        _insightVisible
            ? T("CLOSE LENS", "ปิดมุมมอง")
            : T("INSIGHT", "ดูเจตนา");

    private string ClockText(long tick)
    {
        int clampedTick = (int)Math.Clamp(tick, 0, NightShiftDirector.DeadlineTick);
        int minuteOfDay = ((23 * 60) + clampedTick) % (24 * 60);
        int hour = minuteOfDay / 60;
        int minute = minuteOfDay % 60;
        int remaining = NightShiftDirector.DeadlineTick - clampedTick;
        return $"{hour:00}:{minute:00}  •  {remaining} {T("MIN LEFT", "นาทีที่เหลือ")}";
    }

    private string T(string english, string thai) => _isThai ? thai : english;

    private void RefreshStatus(string message)
    {
        if (_statusLabel is not null)
        {
            _statusLabel.Text = LocalizeText(message);
        }
    }

    private const float PanelLeft = 895.0f;
    private const float PanelWidth = 340.0f;
    private const float PanelTop = 96.0f;

    private float _panelCursor = PanelTop;

    private Label PanelHeading(string text)
    {
        Label label = AddLabel(
            text,
            new Vector2(PanelLeft, _panelCursor),
            new Vector2(PanelWidth, 16.0f),
            11,
            new Color("8091b3"),
            clipText: true);
        _panelCursor += 18.0f;
        return label;
    }

    private Label PanelText(string text, float height, int fontSize, Color color)
    {
        Label label = AddLabel(
            text,
            new Vector2(PanelLeft, _panelCursor),
            new Vector2(PanelWidth, height),
            fontSize,
            color,
            autowrap: true,
            clipText: true);
        _panelCursor += height + 2.0f;
        return label;
    }

    private void PanelGap() => _panelCursor += 6.0f;

    private Button PanelButton(string text, Action onPressed, string tooltip)
    {
        Button button = AddActionButton(
            text,
            new Vector2(PanelLeft, _panelCursor),
            new Vector2(PanelWidth, 34.0f),
            onPressed);
        button.TooltipText = tooltip;
        _panelCursor += 37.0f;
        return button;
    }

    private Label AddLabel(
        string text,
        Vector2 position,
        Vector2 size,
        int fontSize,
        Color color,
        bool autowrap = false,
        bool clipText = false)
    {
        var label = new Label
        {
            Text = text,
            Position = position,
            Size = size,
            AutowrapMode = autowrap
                ? TextServer.AutowrapMode.WordSmart
                : TextServer.AutowrapMode.Off,
            ClipText = clipText,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        AddChild(label);

        // Wrapping needs a width the Control will actually keep. Setting only Size
        // let long objective and status lines run off the edge of the screen.
        label.CustomMinimumSize = size;
        label.Size = size;
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
            ClipText = true,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand,
        };
        button.AddThemeFontSizeOverride("font_size", 13);
        var normal = new StyleBoxFlat
        {
            BgColor = new Color("293247"),
            BorderColor = new Color("44516d"),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 5,
            CornerRadiusTopRight = 5,
            CornerRadiusBottomLeft = 5,
            CornerRadiusBottomRight = 5,
        };
        var hover = (StyleBoxFlat)normal.Duplicate();
        hover.BgColor = new Color("35415a");
        hover.BorderColor = new Color("f1d18a");
        var pressedStyle = (StyleBoxFlat)normal.Duplicate();
        pressedStyle.BgColor = new Color("20283a");
        var disabled = (StyleBoxFlat)normal.Duplicate();
        disabled.BgColor = new Color("171b25");
        disabled.BorderColor = new Color("2a3140");
        button.AddThemeStyleboxOverride("normal", normal);
        button.AddThemeStyleboxOverride("hover", hover);
        button.AddThemeStyleboxOverride("pressed", pressedStyle);
        button.AddThemeStyleboxOverride("disabled", disabled);
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

    private Rect2 ToFloorRect(HotelLocationDefinition location)
    {
        if (_hotel is null)
        {
            throw new InvalidOperationException("Hotel definition is not loaded.");
        }

        NavigationSurfaceDefinition bounds = _hotel.Navigation;
        Vector2 center = ToScreenPosition(location.FloorPosition);
        float width = (location.FloorSize.X / (bounds.MaximumX - bounds.MinimumX)) * MapWidth;
        float height = (location.FloorSize.Z / (bounds.MaximumZ - bounds.MinimumZ)) * MapHeight;
        return new Rect2(center - new Vector2(width, height) / 2.0f, new Vector2(width, height));
    }

    private static BasementRealtimeAdapter CreateSimulation(ulong seed, SessionTruth truth)
    {
        InMemorySuspicionRuleRepository rules = JsonSuspicionRuleParser.Parse(
            File.ReadAllText(ResolveContentPath("SuspicionRules", "mvp.json")));
        BasementScenarioSession session = new BasementScenario(rules).CreateSession(
            new BasementScenarioOptions(seed, ticks: NightShiftDirector.DeadlineTick, truth));
        return new BasementRealtimeAdapter(session);
    }

    private static EntityId? ToEntityId(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : new EntityId(value);

    private static HotelWorldDefinition LoadHotelDefinition() =>
        HotelWorldDefinitionParser.Parse(
            File.ReadAllText(ResolveContentPath("Hotel", "hotel-world.json")));

    private static CharacterCatalogDefinition LoadCharacterCatalog() =>
        CharacterCatalogDefinitionParser.Parse(
            File.ReadAllText(ResolveContentPath("Characters", "characters.json")));

    private static DialogueCatalogDefinition LoadDialogueCatalog() =>
        DialogueCatalogDefinitionParser.Parse(
            File.ReadAllText(ResolveContentPath("Dialogue", "dialogue-lines.json")));

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
