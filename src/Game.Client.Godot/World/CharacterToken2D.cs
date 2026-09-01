using Godot;

namespace Game.Client.Godot.World;

public sealed partial class CharacterToken2D : Control
{
    private const float ArrivalDistance = 2.0f;
    private const float BaseMovementSpeed = 220.0f;

    private Color _color = Colors.White;
    private Vector2 _destination;

    public event Action<CharacterToken2D, Vector2>? DestinationReached;

    public string ActorId { get; private set; } = string.Empty;

    public bool IsNavigating { get; private set; }

    public Vector2 Destination => _destination;

    public bool IsMovementPaused { get; private set; }

    public float SpeedMultiplier { get; private set; } = 1.0f;

    public void Initialize(string actorId, string displayName, Color color)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        ActorId = actorId;
        Name = $"Token-{actorId}";
        _color = color;
        MouseFilter = MouseFilterEnum.Ignore;
        Size = new Vector2(118.0f, 30.0f);

        var label = new Label
        {
            Text = displayName,
            Position = new Vector2(30.0f, 2.0f),
            Size = new Vector2(88.0f, 26.0f),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        label.AddThemeColorOverride("font_color", Colors.White);
        label.AddThemeColorOverride("font_shadow_color", new Color("000000aa"));
        label.AddThemeConstantOverride("shadow_offset_x", 1);
        label.AddThemeConstantOverride("shadow_offset_y", 1);
        AddChild(label);
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawCircle(new Vector2(15.0f, 15.0f), 12.0f, new Color("111827"));
        DrawCircle(new Vector2(15.0f, 15.0f), 9.0f, _color);
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
