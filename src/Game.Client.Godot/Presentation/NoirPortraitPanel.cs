using Godot;

namespace Game.Client.Godot.Presentation;

/// <summary>
/// A portrait for one character, drawn rather than loaded.
/// </summary>
/// <remarks>
/// The cast had a colour each and one shared silhouette, so the profile panel
/// showed the same anonymous shape whoever was being talked to. There are no art
/// assets in this project and inventing six of them is not the job; deriving a
/// face from the character id is, and it gives every person a fixed appearance
/// that a player can learn to recognise across a night.
/// </remarks>
public sealed partial class NoirPortraitPanel : Control
{
    private Color _accent = new("77bdfb");
    private uint _seed;
    private bool _hasSubject;

    /// <summary>
    /// Fixes this portrait to one character. The same id always draws the same
    /// face, which is the whole point of a portrait.
    /// </summary>
    public void SetSubject(string characterId, Color accent)
    {
        _accent = accent;
        _seed = Hash(characterId);
        _hasSubject = true;
        QueueRedraw();
    }

    public void ClearSubject()
    {
        _hasSubject = false;
        QueueRedraw();
    }

    public void SetAccent(Color accent)
    {
        _accent = accent;
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color("101522"));
        for (int index = 0; index < 6; index++)
        {
            float x = 18.0f + (index * 39.0f);
            DrawLine(
                new Vector2(x, 0.0f),
                new Vector2(x - 70.0f, Size.Y),
                new Color(_accent, 0.08f),
                14.0f);
        }

        if (!_hasSubject)
        {
            DrawSilhouette(1.0f, 0.0f);
            return;
        }

        // Every dimension is a small deviation from the same silhouette, so the
        // cast still reads as one set of people rather than six clip-art styles.
        // Two bits each: four jaw widths and four builds. Five bits multiplied by
        // these steps produced heads two and a half times the size of the panel.
        float jaw = 0.86f + (Bits(0, 2) * 0.06f);
        float shoulders = 0.9f + (Bits(2, 2) * 0.05f);
        DrawSilhouette(jaw, shoulders - 1.0f);

        var head = new Vector2(115.0f, 142.0f);
        float radius = 55.0f * jaw;
        DrawHair(head, radius, (int)Bits(4, 3));
        DrawFace(head, radius, (int)Bits(7, 3));
        DrawCollar(head, radius, (int)Bits(10, 3));
    }

    private void DrawSilhouette(float jaw, float shoulderBias)
    {
        DrawCircle(new Vector2(115.0f, 142.0f), 55.0f * jaw, new Color("090c13"));
        float spread = 1.0f + shoulderBias;
        DrawColoredPolygon(
            [
                new Vector2(115.0f - (85.0f * spread), 355.0f),
                new Vector2(115.0f - (63.0f * spread), 240.0f),
                new Vector2(115.0f - (23.0f * spread), 205.0f),
                new Vector2(115.0f + (23.0f * spread), 205.0f),
                new Vector2(115.0f + (63.0f * spread), 240.0f),
                new Vector2(115.0f + (85.0f * spread), 355.0f),
            ],
            new Color("090c13"));
    }

    private void DrawHair(Vector2 head, float radius, int style)
    {
        var ink = new Color(_accent, 0.55f);
        switch (style % 4)
        {
            case 0:
                // Cropped: a line following the top of the skull.
                DrawArc(head, radius - 4.0f, Mathf.Pi, Mathf.Tau, 24, ink, 5.0f);
                break;
            case 1:
                // Tied back, with the shape of it showing behind the jaw.
                DrawArc(head, radius - 4.0f, Mathf.Pi, Mathf.Tau, 24, ink, 4.0f);
                DrawCircle(head + new Vector2(0.0f, -radius + 6.0f), 11.0f, ink);
                break;
            case 2:
                // A cap, which is a job as much as a haircut.
                DrawColoredPolygon(
                    [
                        head + new Vector2(-radius - 4.0f, -12.0f),
                        head + new Vector2(-radius + 6.0f, -radius - 6.0f),
                        head + new Vector2(radius - 6.0f, -radius - 6.0f),
                        head + new Vector2(radius + 4.0f, -12.0f),
                    ],
                    ink);
                break;
            default:
                // Loose, falling past the jaw on both sides.
                DrawArc(head, radius - 2.0f, Mathf.Pi * 0.85f, Mathf.Tau * 1.08f, 28, ink, 7.0f);
                break;
        }
    }

    private void DrawFace(Vector2 head, float radius, int style)
    {
        float eyeY = head.Y + 3.0f;
        float eyeSpan = radius * 0.34f;
        DrawLine(
            new Vector2(head.X - (radius * 0.65f), eyeY),
            new Vector2(head.X + (radius * 0.65f), eyeY),
            new Color(_accent, 0.65f),
            2.0f);
        DrawCircle(new Vector2(head.X - eyeSpan, eyeY), 3.0f, _accent);
        DrawCircle(new Vector2(head.X + eyeSpan, eyeY), 3.0f, _accent);

        if (style % 3 == 0)
        {
            // Glasses.
            var rim = new Color(_accent, 0.7f);
            DrawArc(new Vector2(head.X - eyeSpan, eyeY), 10.0f, 0.0f, Mathf.Tau, 18, rim, 2.0f);
            DrawArc(new Vector2(head.X + eyeSpan, eyeY), 10.0f, 0.0f, Mathf.Tau, 18, rim, 2.0f);
            DrawLine(
                new Vector2(head.X - eyeSpan + 10.0f, eyeY),
                new Vector2(head.X + eyeSpan - 10.0f, eyeY),
                rim,
                2.0f);
        }

        if (style % 3 == 2)
        {
            // A set mouth. Enough to change the read of a face.
            DrawLine(
                new Vector2(head.X - 13.0f, head.Y + (radius * 0.52f)),
                new Vector2(head.X + 13.0f, head.Y + (radius * 0.52f)),
                new Color(_accent, 0.45f),
                2.0f);
        }
    }

    private void DrawCollar(Vector2 head, float radius, int style)
    {
        float neckY = head.Y + radius + 24.0f;
        var ink = new Color(_accent, 0.5f);
        switch (style % 3)
        {
            case 0:
                DrawLine(
                    new Vector2(head.X - 34.0f, neckY),
                    new Vector2(head.X, neckY + 30.0f),
                    ink,
                    3.0f);
                DrawLine(
                    new Vector2(head.X + 34.0f, neckY),
                    new Vector2(head.X, neckY + 30.0f),
                    ink,
                    3.0f);
                break;
            case 1:
                DrawLine(
                    new Vector2(head.X - 30.0f, neckY + 6.0f),
                    new Vector2(head.X + 30.0f, neckY + 6.0f),
                    ink,
                    3.0f);
                break;
            default:
                DrawColoredPolygon(
                    [
                        new Vector2(head.X - 7.0f, neckY + 4.0f),
                        new Vector2(head.X + 7.0f, neckY + 4.0f),
                        new Vector2(head.X + 4.0f, neckY + 48.0f),
                        new Vector2(head.X - 4.0f, neckY + 48.0f),
                    ],
                    ink);
                break;
        }
    }

    private uint Bits(int offset, int count) => (_seed >> offset) & ((1u << count) - 1u);

    private static uint Hash(string value)
    {
        // FNV-1a. Stable across runs and platforms, which a portrait has to be.
        uint hash = 2166136261u;
        foreach (char character in value)
        {
            hash = (hash ^ character) * 16777619u;
        }

        return hash;
    }
}
