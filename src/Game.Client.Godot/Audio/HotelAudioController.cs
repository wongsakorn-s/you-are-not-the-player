using Godot;

namespace Game.Client.Godot.Audio;

public sealed partial class HotelAudioController : Node
{
    private static readonly AudioStreamWav AnomalyWarpStream = ProceduralAudioSynthesizer.CreateAnomalyWarp();
    private static readonly AudioStreamWav ClimaxAlertStream = ProceduralAudioSynthesizer.CreateClimaxAlert();
    private static readonly AudioStreamWav DialogueChimeStream = ProceduralAudioSynthesizer.CreateDialogueChime();

    private AudioStreamPlayer? _globalPlayer;

    public override void _Ready()
    {
        _globalPlayer = new AudioStreamPlayer();
        AddChild(_globalPlayer);
    }

    public void PlayAnomalyWarp()
    {
        if (_globalPlayer is null) return;
        _globalPlayer.Stream = AnomalyWarpStream;
        _globalPlayer.VolumeDb = 2.0f;
        _globalPlayer.Play();
    }

    public void PlayClimaxAlert()
    {
        if (_globalPlayer is null) return;
        _globalPlayer.Stream = ClimaxAlertStream;
        _globalPlayer.VolumeDb = 3.0f;
        _globalPlayer.Play();
    }

    public void PlayDialogueChime()
    {
        if (_globalPlayer is null) return;
        _globalPlayer.Stream = DialogueChimeStream;
        _globalPlayer.VolumeDb = 0.0f;
        _globalPlayer.Play();
    }
}
