using Game.Client.Godot.Audio;
using Game.Sim.Objects;
using Godot;

namespace Game.Client.Godot.World;

public sealed partial class InteractiveObjectNode : Node3D
{
    private static readonly AudioStreamWav LockClickStream = ProceduralAudioSynthesizer.CreateLockClick();
    private static readonly AudioStreamWav PaperStream = ProceduralAudioSynthesizer.CreatePaperFlutter();
    private static readonly AudioStreamWav BuzzStream = ProceduralAudioSynthesizer.CreateFuseboxBuzz();
    private static readonly AudioStreamWav BeepStream = ProceduralAudioSynthesizer.CreateTerminalBeep();

    private Label3D? _label;
    private MeshInstance3D? _meshInstance;
    private StandardMaterial3D? _material;
    private AudioStreamPlayer3D? _audioPlayer;

    public string ObjectId { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public void Initialize(InteractiveObject obj, Vector3 worldPosition)
    {
        ArgumentNullException.ThrowIfNull(obj);
        ObjectId = obj.Id;
        DisplayName = obj.DisplayName;
        Name = $"Obj_{obj.Id}";
        Position = worldPosition;

        var mesh = new BoxMesh
        {
            Size = new Vector3(0.55f, 0.55f, 0.55f),
        };

        Color objectColor = GetObjectColor(obj.Kind);
        _material = new StandardMaterial3D
        {
            AlbedoColor = objectColor,
            Metallic = 0.4f,
            Roughness = 0.5f,
        };
        mesh.Material = _material;

        _meshInstance = new MeshInstance3D { Mesh = mesh };
        AddChild(_meshInstance);

        _label = new Label3D
        {
            Position = new Vector3(0.0f, 0.65f, 0.0f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 24,
            OutlineSize = 8,
            Modulate = Colors.White,
        };
        AddChild(_label);

        _audioPlayer = new AudioStreamPlayer3D
        {
            UnitSize = 12.0f,
            MaxDistance = 40.0f,
            VolumeDb = 2.0f,
        };
        AddChild(_audioPlayer);

        UpdateState(obj);
    }

    public void PlayInteractionSound(InteractiveObjectKind kind)
    {
        if (_audioPlayer is null) return;
        _audioPlayer.Stream = kind switch
        {
            InteractiveObjectKind.Safe => LockClickStream,
            InteractiveObjectKind.Registry => PaperStream,
            InteractiveObjectKind.Switch => BuzzStream,
            InteractiveObjectKind.Terminal => BeepStream,
            InteractiveObjectKind.Contraband => LockClickStream,
            _ => LockClickStream,
        };
        _audioPlayer.PitchScale = 0.95f + Random.Shared.NextSingle() * 0.1f;
        _audioPlayer.Play();
    }

    public void UpdateState(InteractiveObject obj)
    {
        if (_label is null || _material is null)
        {
            return;
        }

        string lockIcon = obj.IsLocked ? " 🔒" : string.Empty;
        string tamperNotice = obj.IsTampered ? " ⚠️" : string.Empty;
        _label.Text = $"[O] {obj.DisplayName}{lockIcon}{tamperNotice}";

        if (obj.IsTampered)
        {
            _label.Modulate = new Color(1.0f, 0.4f, 0.3f);
            _material.AlbedoColor = new Color(0.85f, 0.25f, 0.2f);
        }
        else if (obj.IsLocked)
        {
            _label.Modulate = new Color(1.0f, 0.85f, 0.4f);
        }
        else
        {
            _label.Modulate = new Color(0.6f, 1.0f, 0.8f);
        }
    }

    private static Color GetObjectColor(InteractiveObjectKind kind) => kind switch
    {
        InteractiveObjectKind.Safe => new Color(0.75f, 0.65f, 0.25f), // Gold / Brass
        InteractiveObjectKind.Registry => new Color(0.45f, 0.3f, 0.15f), // Leather / Wood
        InteractiveObjectKind.Terminal => new Color(0.15f, 0.35f, 0.65f), // Slate Blue
        InteractiveObjectKind.Switch => new Color(0.85f, 0.45f, 0.15f), // Amber / Orange
        InteractiveObjectKind.Contraband => new Color(0.65f, 0.15f, 0.25f), // Crimson
        _ => new Color(0.4f, 0.45f, 0.5f),
    };
}
