using Godot;

namespace Game.Client.Godot.World;

public sealed partial class RestrictedDoorNode : Node3D
{
    private const float OpenHeight = 3.0f;

    private CollisionShape3D? _collision;
    private Tween? _animation;
    private Vector3 _closedPosition;

    public bool IsOpen { get; private set; }

    public void Initialize(Vector3 position, Vector3 size, Color color)
    {
        Name = "RestrictedBasementDoor";
        Position = position;
        _closedPosition = position;

        var material = new StandardMaterial3D
        {
            AlbedoColor = color,
            Roughness = 0.78f,
        };
        AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh
            {
                Size = size,
                Material = material,
            },
        });

        var body = new StaticBody3D { Name = "DoorCollision" };
        _collision = new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = size },
        };
        body.AddChild(_collision);
        AddChild(body);
    }

    public void Open(bool immediate = false)
    {
        if (IsOpen)
        {
            return;
        }

        IsOpen = true;
        _collision?.SetDeferred(CollisionShape3D.PropertyName.Disabled, true);
        AnimateTo(_closedPosition + Vector3.Up * OpenHeight, immediate);
    }

    public void ResetClosed()
    {
        IsOpen = false;
        _animation?.Kill();
        Position = _closedPosition;
        _collision?.SetDeferred(CollisionShape3D.PropertyName.Disabled, false);
    }

    private void AnimateTo(Vector3 destination, bool immediate)
    {
        _animation?.Kill();
        if (immediate)
        {
            Position = destination;
            return;
        }

        _animation = CreateTween();
        _animation
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.Out)
            .TweenProperty(this, new NodePath("position"), destination, 0.45);
    }
}
