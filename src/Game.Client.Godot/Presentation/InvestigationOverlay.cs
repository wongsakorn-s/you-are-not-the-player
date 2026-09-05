using Godot;

namespace Game.Client.Godot.Presentation;

public sealed record InvestigationChoice(string Label, Action Selected);

/// <summary>Who the screen is about: the portrait and the card beneath it.</summary>
/// <summary>One line on the rail when a screen is not about a person.</summary>
public sealed record StatusLine(string Label, string Value, string? Accent = null);

public sealed record PortraitSubject(
    string CharacterId,
    Color Accent,
    string Name,
    string Role,
    string? Station);

public sealed partial class InvestigationOverlay : Control
{
    private Label? _title;
    private NoirPortraitPanel? _portrait;
    private Label? _portraitText;
    private Label? _time;
    private RichTextLabel? _body;
    private GridContainer? _choices;
    private Button? _close;
    private LineEdit? _nameEntry;
    private VBoxContainer? _status;
    private Panel? _panel;
    private StyleBoxFlat? _panelStyle;
    private Tween? _transition;
    private bool _comfortableText;

    public event Action? Closed;

    public bool IsOpen => Visible;

    public override void _Ready()
    {
        Position = Vector2.Zero;
        Size = new Vector2(1280.0f, 720.0f);
        PivotOffset = Size / 2.0f;
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
            Position = new Vector2(70.0f, 115.0f),
            Size = new Vector2(1140.0f, 560.0f),
        };
        _panel = panel;
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

        _portrait = new NoirPortraitPanel
        {
            Name = "PortraitPlaceholder",
            Position = new Vector2(24.0f, 46.0f),
            Size = new Vector2(230.0f, 430.0f),
            MouseFilter = MouseFilterEnum.Ignore,
            // The lighting streaks are drawn past the edge on purpose; without
            // this they spill over the rounded corner of the panel behind.
            ClipContents = true,
        };
        panel.AddChild(_portrait);

        _portraitText = new Label
        {
            Text = "SUBJECT\nPROFILE",
            Position = new Vector2(14.0f, 350.0f),
            Size = new Vector2(202.0f, 74.0f),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _portraitText.AddThemeColorOverride("font_color", new Color("75829e"));
        _portraitText.AddThemeFontSizeOverride("font_size", 16);
        _portrait.AddChild(_portraitText);

        // The rail is the same width whatever a screen is about, so the text
        // column never moves. When there is nobody to show, it carries the state
        // of the night rather than an empty silhouette captioned SUBJECT PROFILE.
        _status = new VBoxContainer
        {
            Position = new Vector2(24.0f, 46.0f),
            Size = new Vector2(230.0f, 430.0f),
            Visible = false,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _status.AddThemeConstantOverride("separation", 14);
        panel.AddChild(_status);

        _title = new Label
        {
            Position = new Vector2(285.0f, 24.0f),
            Size = new Vector2(700.0f, 44.0f),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _title.AddThemeFontSizeOverride("font_size", 24);
        panel.AddChild(_title);

        _time = new Label
        {
            Position = new Vector2(760.0f, 27.0f),
            Size = new Vector2(215.0f, 34.0f),
            HorizontalAlignment = HorizontalAlignment.Right,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _time.AddThemeColorOverride("font_color", new Color("e06c75"));
        _time.AddThemeFontSizeOverride("font_size", 13);
        panel.AddChild(_time);

        _body = new RichTextLabel
        {
            Position = new Vector2(285.0f, 82.0f),
            Size = new Vector2(810.0f, 210.0f),
            Text = string.Empty,
            ScrollActive = false,
            SelectionEnabled = false,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _body.AddThemeFontSizeOverride("normal_font_size", 17);
        _body.AddThemeColorOverride("default_color", new Color("e6eaf2"));
        panel.AddChild(_body);

        _choices = new GridContainer
        {
            Columns = 2,
            Position = new Vector2(285.0f, 310.0f),
            Size = new Vector2(810.0f, 220.0f),
        };
        _choices.AddThemeConstantOverride("h_separation", 10);
        _choices.AddThemeConstantOverride("v_separation", 8);
        panel.AddChild(_choices);

        _nameEntry = new LineEdit
        {
            Position = new Vector2(285.0f, 268.0f),
            Size = new Vector2(420.0f, 44.0f),
            MaxLength = 24,
            Visible = false,
            CaretBlink = true,
        };
        _nameEntry.AddThemeFontSizeOverride("font_size", 18);
        panel.AddChild(_nameEntry);

        _close = new Button
        {
            Text = "CLOSE",
            Position = new Vector2(1015.0f, 22.0f),
            Size = new Vector2(92.0f, 36.0f),
        };
        _close.Pressed += HideScreen;
        panel.AddChild(_close);

        Visible = false;
    }

    public void SetLanguage(bool useThai)
    {
        if (_portraitText is not null)
        {
            _portraitText.Text = useThai
                ? "แฟ้มบุคคล\nการรับรู้ไม่สมบูรณ์"
                : "SUBJECT PROFILE\nUNRELIABLE SIGNAL";
        }

        if (_close is not null)
        {
            _close.Text = useThai ? "ปิด" : "CLOSE";
        }
    }

    private void BuildStatusRail(IReadOnlyList<StatusLine>? status, Color accent)
    {
        if (_status is null || _portrait is null)
        {
            return;
        }

        foreach (Node child in _status.GetChildren())
        {
            child.QueueFree();
        }

        _status.Visible = status is { Count: > 0 };
        _portrait.Visible = !_status.Visible;
        if (!_status.Visible)
        {
            return;
        }

        foreach (StatusLine line in status!)
        {
            var caption = new Label
            {
                Text = line.Label,
                MouseFilter = MouseFilterEnum.Ignore,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(214.0f, 0.0f),
            };
            caption.AddThemeFontSizeOverride("font_size", 12);
            caption.AddThemeColorOverride("font_color", new Color("75829e"));
            _status.AddChild(caption);

            var value = new Label
            {
                Text = line.Value,
                MouseFilter = MouseFilterEnum.Ignore,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(214.0f, 0.0f),
            };
            value.AddThemeFontSizeOverride("font_size", 17);
            value.AddThemeColorOverride(
                "font_color",
                line.Accent is null ? accent : new Color(line.Accent));
            _status.AddChild(value);
        }
    }

    /// <summary>What the player typed, trimmed, or null if they typed nothing.</summary>
    public string? NameEntryText =>
        string.IsNullOrWhiteSpace(_nameEntry?.Text) ? null : _nameEntry!.Text.Trim();

    private string _subjectlessCaption = "SUBJECT\nPROFILE";

    /// <summary>The caption under an empty portrait, in the player's language.</summary>
    public void SetSubjectlessCaption(string caption)
    {
        _subjectlessCaption = caption;
    }

    public void SetTimeText(string text)
    {
        if (_time is not null)
        {
            _time.Text = text;
        }
    }

    public void SetComfortableText(bool enabled)
    {
        _comfortableText = enabled;
        _body?.AddThemeFontSizeOverride("normal_font_size", enabled ? 20 : 17);
    }

    public void ShowScreen(
        string title,
        string body,
        Color accent,
        IReadOnlyList<InvestigationChoice>? choices = null,
        bool allowClose = true,
        PortraitSubject? subject = null,
        string? nameEntryPlaceholder = null,
        string? nameEntryValue = null,
        IReadOnlyList<StatusLine>? status = null)
    {
        if (_title is null || _body is null || _choices is null || _panelStyle is null)
        {
            throw new InvalidOperationException("Investigation overlay is not ready.");
        }

        _title.Text = title;
        _title.AddThemeColorOverride("font_color", accent);
        _panelStyle.BorderColor = accent.Darkened(0.15f);
        if (subject is null)
        {
            _portrait?.ClearSubject();
            _portrait?.SetAccent(accent);
            if (_portraitText is not null)
            {
                _portraitText.Text = status is { Count: > 0 } ? string.Empty : _subjectlessCaption;
                _portraitText.AddThemeColorOverride("font_color", new Color("75829e"));
            }
        }
        else
        {
            _portrait?.SetSubject(subject.CharacterId, subject.Accent);
            if (_portraitText is not null)
            {
                // Name, job, and where they are normally found. A player who has
                // met six people in one night needs the second and third lines.
                _portraitText.Text = string.IsNullOrWhiteSpace(subject.Station)
                    ? $"{subject.Name}\n{subject.Role}"
                    : $"{subject.Name}\n{subject.Role}\n{subject.Station}";
                _portraitText.AddThemeColorOverride("font_color", subject.Accent);
            }
        }

        if (_nameEntry is not null)
        {
            _nameEntry.Visible = nameEntryPlaceholder is not null;
            if (nameEntryPlaceholder is not null)
            {
                _nameEntry.PlaceholderText = nameEntryPlaceholder;
                _nameEntry.Text = nameEntryValue ?? string.Empty;
                _nameEntry.GrabFocus();
            }
        }

        BuildStatusRail(status, accent);
        _body.Text = body;
        ApplyLayout();
        if (_close is not null)
        {
            _close.Visible = allowClose;
        }

        ClearChoices();

        // Two columns leave a stranded button whenever the count is odd, and a
        // single wide button reads better than half a row anyway.
        int choiceCount = choices?.Count ?? 0;
        _choices.Columns = choiceCount <= 3 ? 1 : 2;
        float choiceWidth = _choices.Columns == 1 ? ContentWidth : 395.0f;

        foreach (InvestigationChoice choice in choices ?? [])
        {
            var button = new Button
            {
                Text = choice.Label,
                CustomMinimumSize = new Vector2(choiceWidth, 50.0f),
                MouseDefaultCursorShape = CursorShape.PointingHand,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
            };
            button.AddThemeFontSizeOverride("font_size", _comfortableText ? 17 : 15);
            var normal = new StyleBoxFlat
            {
                BgColor = new Color("111722"),
                BorderColor = new Color(accent, 0.22f),
                BorderWidthLeft = 1,
                BorderWidthTop = 1,
                BorderWidthRight = 1,
                BorderWidthBottom = 1,
                CornerRadiusTopLeft = 6,
                CornerRadiusTopRight = 6,
                CornerRadiusBottomLeft = 6,
                CornerRadiusBottomRight = 6,
            };
            var hover = (StyleBoxFlat)normal.Duplicate();
            hover.BgColor = new Color("263044");
            hover.BorderColor = accent;
            hover.BorderWidthLeft = 2;
            hover.BorderWidthTop = 2;
            hover.BorderWidthRight = 2;
            hover.BorderWidthBottom = 2;
            button.AddThemeStyleboxOverride("normal", normal);
            button.AddThemeStyleboxOverride("hover", hover);
            button.AddThemeStyleboxOverride("focus", hover);
            Action selected = choice.Selected;
            button.Pressed += selected;
            _choices.AddChild(button);
        }

        bool wasVisible = Visible;
        Visible = true;
        if (!wasVisible)
        {
            PlayEntranceAnimation();
        }
    }

    private const float ContentLeft = 285.0f;
    private const float ContentWidth = 810.0f;
    private const float BodyTop = 82.0f;

    // The portrait used to be hidden on some screens, which slid the text column
    // from x=285 to x=40 and back as the player moved between pages of the same
    // case file, and left a hole where it had been. One geometry for every screen
    // costs some width and buys a layout that does not jump.
    private void ApplyLayout()
    {
        if (_portrait is null || _title is null || _body is null || _choices is null)
        {
            return;
        }

        _portrait.Visible = _status is not { Visible: true };
        _title.Position = new Vector2(ContentLeft, 24.0f);
        _title.Size = new Vector2(500.0f, 44.0f);
        _body.Position = new Vector2(ContentLeft, BodyTop);
        _body.Size = new Vector2(ContentWidth, 210.0f);
        _choices.Position = new Vector2(ContentLeft, 310.0f);
        _choices.Size = new Vector2(ContentWidth, 220.0f);
    }

    // Body text ranged from one line to a dozen, but the choices were pinned at a
    // fixed y, so most screens opened with a paragraph, a few hundred pixels of
    // nothing, and then the buttons. Measuring has to happen after a layout pass,
    // which is why it runs here rather than in ShowScreen.
    public override void _Process(double delta)
    {
        if (!Visible || _body is null || _choices is null)
        {
            return;
        }

        float measured = Mathf.Clamp(_body.GetContentHeight() + 6.0f, 28.0f, 330.0f);
        if (Mathf.Abs(_body.Size.Y - measured) > 1.0f)
        {
            _body.Size = new Vector2(ContentWidth, measured);
        }

        // The field belongs between what was asked and the buttons that answer
        // it. Pinning it to a fixed y put it underneath them as soon as the body
        // text was short.
        float cursor = BodyTop + measured + 22.0f;
        if (_nameEntry is { Visible: true })
        {
            if (Mathf.Abs(_nameEntry.Position.Y - cursor) > 1.0f)
            {
                _nameEntry.Position = new Vector2(ContentLeft, cursor);
            }

            cursor += _nameEntry.Size.Y + 22.0f;
        }

        if (Mathf.Abs(_choices.Position.Y - cursor) > 1.0f)
        {
            _choices.Position = new Vector2(ContentLeft, cursor);
        }

        // The panel used to be 560 tall whatever it held, so a four-button screen
        // sat in a box with two hundred empty pixels under it.
        if (_panel is not null)
        {
            float needed = Mathf.Max(
                cursor + _choices.Size.Y + 28.0f,
                _status is { Visible: true }
                    ? _status.GetCombinedMinimumSize().Y + 92.0f
                    : 500.0f);
            float height = Mathf.Clamp(needed, 320.0f, 566.0f);
            if (Mathf.Abs(_panel.Size.Y - height) > 1.0f)
            {
                _panel.Size = new Vector2(_panel.Size.X, height);
                _panel.Position = new Vector2(_panel.Position.X, (720.0f - height) / 2.0f);
            }
        }
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

    private void PlayEntranceAnimation()
    {
        _transition?.Kill();
        Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);
        Scale = new Vector2(0.985f, 0.985f);
        _transition = CreateTween().SetParallel();
        _transition.TweenProperty(this, "modulate", Colors.White, 0.14f);
        _transition.TweenProperty(this, "scale", Vector2.One, 0.14f)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.Out);
    }
}
