using Godot;

namespace Game.Client.Godot.Presentation;

public sealed record InvestigationChoice(string Label, Action Selected);

public sealed partial class InvestigationOverlay : Control
{
    private Label? _title;
    private RichTextLabel? _body;
    private GridContainer? _choices;
    private StyleBoxFlat? _panelStyle;

    public event Action? Closed;

    public bool IsOpen => Visible;

    public override void _Ready()
    {
        Position = Vector2.Zero;
        Size = new Vector2(1280.0f, 720.0f);
        MouseFilter = MouseFilterEnum.Stop;
        ZIndex = 100;

        var veil = new ColorRect
        {
            Color = new Color("05070bcc"),
            Position = Vector2.Zero,
            Size = Size,
            MouseFilter = MouseFilterEnum.Stop,
        };
        AddChild(veil);

        var panel = new Panel
        {
            Position = new Vector2(100.0f, 245.0f),
            Size = new Vector2(1080.0f, 410.0f),
        };
        _panelStyle = new StyleBoxFlat
        {
            BgColor = new Color("171d2a"),
            BorderColor = new Color("d2b36f"),
            BorderWidthLeft = 3,
            BorderWidthTop = 3,
            BorderWidthRight = 3,
            BorderWidthBottom = 3,
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10,
            CornerRadiusBottomRight = 10,
        };
        panel.AddThemeStyleboxOverride("panel", _panelStyle);
        AddChild(panel);

        var portrait = new ColorRect
        {
            Name = "PortraitPlaceholder",
            Color = new Color("252e41"),
            Position = new Vector2(28.0f, 34.0f),
            Size = new Vector2(210.0f, 338.0f),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        panel.AddChild(portrait);

        var portraitText = new Label
        {
            Text = "PORTRAIT\nPLACEHOLDER",
            Position = new Vector2(18.0f, 125.0f),
            Size = new Vector2(174.0f, 80.0f),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        portraitText.AddThemeColorOverride("font_color", new Color("75829e"));
        portraitText.AddThemeFontSizeOverride("font_size", 16);
        portrait.AddChild(portraitText);

        _title = new Label
        {
            Position = new Vector2(270.0f, 25.0f),
            Size = new Vector2(680.0f, 40.0f),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _title.AddThemeFontSizeOverride("font_size", 24);
        panel.AddChild(_title);

        _body = new RichTextLabel
        {
            Position = new Vector2(270.0f, 76.0f),
            Size = new Vector2(770.0f, 130.0f),
            Text = string.Empty,
            ScrollActive = true,
            SelectionEnabled = true,
            MouseFilter = MouseFilterEnum.Stop,
        };
        _body.AddThemeFontSizeOverride("normal_font_size", 17);
        _body.AddThemeColorOverride("default_color", new Color("e6eaf2"));
        panel.AddChild(_body);

        _choices = new GridContainer
        {
            Columns = 2,
            Position = new Vector2(270.0f, 220.0f),
            Size = new Vector2(770.0f, 165.0f),
        };
        _choices.AddThemeConstantOverride("h_separation", 10);
        _choices.AddThemeConstantOverride("v_separation", 8);
        panel.AddChild(_choices);

        var close = new Button
        {
            Text = "CLOSE",
            Position = new Vector2(948.0f, 22.0f),
            Size = new Vector2(92.0f, 36.0f),
        };
        close.Pressed += HideScreen;
        panel.AddChild(close);

        Visible = false;
    }

    public void ShowScreen(
        string title,
        string body,
        Color accent,
        IReadOnlyList<InvestigationChoice>? choices = null)
    {
        if (_title is null || _body is null || _choices is null || _panelStyle is null)
        {
            throw new InvalidOperationException("Investigation overlay is not ready.");
        }

        _title.Text = title;
        _title.AddThemeColorOverride("font_color", accent);
        _panelStyle.BorderColor = accent.Darkened(0.15f);
        _body.Text = body;
        ClearChoices();

        foreach (InvestigationChoice choice in choices ?? [])
        {
            var button = new Button
            {
                Text = choice.Label,
                CustomMinimumSize = new Vector2(375.0f, 50.0f),
            };
            Action selected = choice.Selected;
            button.Pressed += selected;
            _choices.AddChild(button);
        }

        Visible = true;
    }

    public void HideScreen()
    {
        if (!Visible)
        {
            return;
        }

        Visible = false;
        Closed?.Invoke();
    }

    private void ClearChoices()
    {
        if (_choices is null)
        {
            return;
        }

        foreach (Node child in _choices.GetChildren())
        {
            _choices.RemoveChild(child);
            child.QueueFree();
        }
    }
}
