using Godot;

namespace Game.Client.Godot.Debug;

public sealed partial class DebugHud : CanvasLayer
{
    private readonly Label _status = new();
    private readonly Label _events = new();
    private readonly Label _objective = new();
    private readonly Label _feedback = new();
    private readonly Label _help = new();

    public override void _Ready()
    {
        var statusBackground = CreateBackground(new Vector2(16, 16), new Vector2(430, 330));
        AddChild(statusBackground);

        ConfigureLabel(_status, new Vector2(28, 26), new Vector2(405, 300), fontSize: 16);
        AddChild(_status);

        var eventBackground = CreateBackground(new Vector2(824, 16), new Vector2(440, 360));
        AddChild(eventBackground);
        ConfigureLabel(_events, new Vector2(836, 26), new Vector2(415, 335), fontSize: 14);
        AddChild(_events);

        var helpBackground = CreateBackground(new Vector2(16, 604), new Vector2(1248, 100));
        AddChild(helpBackground);

        ConfigureLabel(_objective, new Vector2(28, 612), new Vector2(1220, 24), fontSize: 15);
        _objective.AddThemeColorOverride("font_color", new Color("ffe08a"));
        AddChild(_objective);

        ConfigureLabel(_feedback, new Vector2(28, 640), new Vector2(1220, 24), fontSize: 14);
        AddChild(_feedback);

        ConfigureLabel(_help, new Vector2(28, 670), new Vector2(1220, 24), fontSize: 13);
        _help.Text = "P Possess | 1-8 Move | T Talk | Y Inquire | O Object | E Door | J Journal | K Conspiracy | Z/X/C Climax | F6/F7 Save/Load | Space Step | R Reset";
        AddChild(_help);
    }

    public void SetStatus(string text) => _status.Text = text;

    public void SetEvents(string text) => _events.Text = text;

    public void SetObjective(string text) => _objective.Text = text;

    public void SetFeedback(string text, bool isError = false)
    {
        _feedback.Text = text;
        _feedback.AddThemeColorOverride(
            "font_color",
            isError ? new Color("ff8f8f") : new Color("9ff2bd"));
    }

    private static ColorRect CreateBackground(Vector2 position, Vector2 size) =>
        new()
        {
            Position = position,
            Size = size,
            Color = new Color(0.025f, 0.035f, 0.055f, 0.88f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

    private static void ConfigureLabel(
        Label label,
        Vector2 position,
        Vector2 size,
        int fontSize)
    {
        label.Position = position;
        label.Size = size;
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", new Color(0.88f, 0.93f, 1.0f));
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.MouseFilter = Control.MouseFilterEnum.Ignore;
    }
}
