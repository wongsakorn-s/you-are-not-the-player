using Godot;

namespace Game.Client.Godot.World;

public sealed partial class NpcActorNode : Node3D
{
    private const float ArrivalDistance = 0.16f;
    private const float BaseMovementSpeed = 3.6f;

    private NavigationAgent3D? _navigationAgent;
    private Vector3 _destination;

    public event Action<NpcActorNode, Vector3>? DestinationReached;

    public event Action<NpcActorNode, Vector3>? NavigationFailed;

    public string ActorId { get; private set; } = string.Empty;

    public bool IsNavigating { get; private set; }

    public Vector3 Destination => _destination;

    public bool IsMovementPaused { get; private set; }

    public float SpeedMultiplier { get; private set; } = 1.0f;

    public void Initialize(string actorId, Color color)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        ActorId = actorId;
        Name = actorId;

        var mesh = new CapsuleMesh
        {
            Radius = 0.35f,
            Height = 1.6f,
        };
        var material = new StandardMaterial3D
        {
            AlbedoColor = color,
            Metallic = 0.05f,
            Roughness = 0.65f,
        };
        mesh.Material = material;
        AddChild(new MeshInstance3D { Mesh = mesh });

        AddChild(new Label3D
        {
            Text = actorId,
            Position = new Vector3(0.0f, 1.25f, 0.0f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 28,
            OutlineSize = 8,
            Modulate = Colors.White,
        });

        _navigationAgent = new NavigationAgent3D
        {
            Name = "NavigationAgent3D",
            PathDesiredDistance = 0.12f,
            TargetDesiredDistance = ArrivalDistance,
            Radius = 0.35f,
            Height = 1.6f,
            AvoidanceEnabled = false,
        };
        AddChild(_navigationAgent);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsNavigating || IsMovementPaused || _navigationAgent is null)
        {
            return;
        }

        Vector3 remaining = _destination - GlobalPosition;
        remaining.Y = 0.0f;
        if (remaining.Length() <= ArrivalDistance)
        {
            CompleteNavigation();
            return;
        }

        Rid navigationMap = _navigationAgent.GetNavigationMap();
        if (NavigationServer3D.MapGetIterationId(navigationMap) == 0)
        {
            return;
        }

        Vector3 nextPosition = _navigationAgent.GetNextPathPosition();
        if (_navigationAgent.IsNavigationFinished())
        {
            FailNavigation();
            return;
        }

        Vector3 direction = nextPosition - GlobalPosition;
        direction.Y = 0.0f;
        if (direction.IsZeroApprox())
        {
            return;
        }

        float step = MathF.Min(
            BaseMovementSpeed * SpeedMultiplier * (float)delta,
            remaining.Length());
        GlobalPosition += direction.Normalized() * step;
    }

    public void MoveTo(Vector3 destination, bool immediate)
    {
        _destination = destination;
        if (immediate)
        {
            IsNavigating = false;
            GlobalPosition = destination;
            _navigationAgent?.SetVelocityForced(Vector3.Zero);
            return;
        }

        if (_navigationAgent is null)
        {
            throw new InvalidOperationException("Actor must be initialized before navigation starts.");
        }

        IsNavigating = true;
        _navigationAgent.TargetPosition = destination;
    }

    public void Stop()
    {
        IsNavigating = false;
        _destination = GlobalPosition;
        if (_navigationAgent is not null)
        {
            _navigationAgent.TargetPosition = GlobalPosition;
            _navigationAgent.SetVelocityForced(Vector3.Zero);
        }
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
        GlobalPosition = _destination;
        DestinationReached?.Invoke(this, _destination);
    }

    private void FailNavigation()
    {
        IsNavigating = false;
        NavigationFailed?.Invoke(this, _destination);
    }
}
