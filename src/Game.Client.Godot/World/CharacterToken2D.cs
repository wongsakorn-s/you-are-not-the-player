using Godot;

namespace Game.Client.Godot.World;

public sealed partial class CharacterToken2D : Control
{
    private const float ArrivalDistance = 2.0f;
    private const float BaseMovementSpeed = 220.0f;

    private Color _color = Colors.White;
    private Vector2 _destination;
    private bool _isHumanHost;
    private bool _insightVisible;
    private bool _isSelected;
    private string _displayName = string.Empty;
    private string _activity = string.Empty;
    private Label? _activityLabel;
    private Label? _label;

    public event Action<CharacterToken2D, Vector2>? DestinationReached;
    public event Action<CharacterToken2D>? Selected;

    public string ActorId { get; private set; } = string.Empty;

    public bool IsNavigating { get; private set; }

    public Vector2 Destination => _destination;

    public bool IsMovementPaused { get; private set; }

    public float SpeedMultiplier { get; private set; } = 1.0f;

    public void Initialize(string actorId, string displayName, Color color, bool isHumanHost = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        ActorId = actorId;
        Name = $"Token-{actorId}";
        _displayName = displayName;
        _color = color;
        _isHumanHost = isHumanHost;
        MouseFilter = isHumanHost ? MouseFilterEnum.Ignore : MouseFilterEnum.Stop;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        Size = new Vector2(30.0f, 30.0f);

        _label = new Label
        {
            Text = displayName,
            Position = new Vector2(18.0f, -18.0f),
            Size = new Vector2(108.0f, 24.0f),
            VerticalAlignment = VerticalAlignment.Center,
            ClipText = true,
            MouseFilter = MouseFilterEnum.Ignore,
            Visible = false,
        };
        _label.AddThemeColorOverride("font_color", Colors.White);
        _label.AddThemeColorOverride("font_shadow_color", new Color("000000aa"));
        _label.AddThemeConstantOverride("shadow_offset_x", 1);
        _label.AddThemeConstantOverride("shadow_offset_y", 1);
        _label.AddThemeConstantOverride("outline_size", 2);
        _label.AddThemeColorOverride("font_outline_color", new Color("05070bcc"));
        _label.AddThemeFontSizeOverride("font_size", 13);
        AddChild(_label);

        _activityLabel = new Label
        {
            Position = new Vector2(18.0f, 7.0f),
            Size = new Vector2(122.0f, 18.0f),
            ClipText = true,
            MouseFilter = MouseFilterEnum.Ignore,
            Visible = false,
        };
        _activityLabel.AddThemeColorOverride("font_color", new Color("f1d18a"));
        _activityLabel.AddThemeFontSizeOverride("font_size", 10);
        AddChild(_activityLabel);
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_isSelected)
        {
            DrawCircle(new Vector2(15.0f, 15.0f), 15.0f, new Color("f1d18a"));
        }

        if (_isHumanHost)
        {
            DrawCircle(new Vector2(15.0f, 15.0f), 13.0f, new Color("f1d18a"));
        }

        DrawCircle(new Vector2(15.0f, 15.0f), 11.0f, new Color("111827"));
        DrawCircle(new Vector2(15.0f, 15.0f), 8.0f, _color);
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
        {
            return;
        }

        Selected?.Invoke(this);
        AcceptEvent();
    }

    public override void _Process(double delta)
    {
        if (!IsNavigating || IsMovementPaused)
        {
            return;
        }

        float distance = Position.DistanceTo(_destination);
        if (distance <= ArrivalDistance)
        {
            CompleteNavigation();
            return;
        }

        Position = Position.MoveToward(
            _destination,
            BaseMovementSpeed * SpeedMultiplier * (float)delta);
    }

    public void MoveTo(Vector2 destination, bool immediate)
    {
        _destination = destination;
        if (immediate)
        {
            IsNavigating = false;
            Position = destination;
            return;
        }

        IsNavigating = true;
    }

    public void Stop()
    {
        IsNavigating = false;
        _destination = Position;
    }

    public void SetMovementPaused(bool isPaused) => IsMovementPaused = isPaused;

    public void SetDisplayName(string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        if (_label is not null)
        {
            _displayName = displayName;
            _label.Text = displayName;
            TooltipText = string.IsNullOrWhiteSpace(_activity)
                ? displayName
                : $"{displayName}\n{_activity}";
        }
    }

    public void SetActivity(string activity)
    {
        _activity = activity;
        TooltipText = string.IsNullOrWhiteSpace(activity)
            ? _displayName
            : $"{_displayName}\n{activity}";
        if (_activityLabel is not null)
        {
            _activityLabel.Text = activity;
        }
    }

    public void SetInsightVisible(bool visible)
    {
        _insightVisible = visible && !_isHumanHost;
        Size = new Vector2(30.0f, 30.0f);
        if (_activityLabel is not null)
        {
            _activityLabel.Visible = _insightVisible && _isSelected;
        }

        QueueRedraw();
    }

    public void SetSelected(bool isSelected)
    {
        if (_isSelected == isSelected)
        {
            return;
        }

        _isSelected = isSelected;
        if (_label is not null)
        {
            _label.Visible = isSelected;
        }

        if (_activityLabel is not null)
        {
            _activityLabel.Visible = _insightVisible && isSelected;
        }

        QueueRedraw();
    }

    public void SetSpeedMultiplier(float multiplier)
    {
        if (!float.IsFinite(multiplier) || multiplier <= 0.0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(multiplier),
                multiplier,
                "Movement speed multiplier must be positive.");
        }

        SpeedMultiplier = multiplier;
    }

    private void CompleteNavigation()
    {
        IsNavigating = false;
        Position = _destination;
        DestinationReached?.Invoke(this, _destination);
    }
}
