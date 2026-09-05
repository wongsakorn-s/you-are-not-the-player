using Godot;

namespace Game.Client.Godot.Presentation;

public sealed partial class NoirPortraitPanel : Control
{
    private Color _accent = new("77bdfb");

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

        DrawCircle(new Vector2(115.0f, 142.0f), 55.0f, new Color("090c13"));
        DrawColoredPolygon(
            [
                new Vector2(30.0f, 355.0f),
                new Vector2(52.0f, 240.0f),
                new Vector2(92.0f, 205.0f),
                new Vector2(138.0f, 205.0f),
                new Vector2(178.0f, 240.0f),
                new Vector2(200.0f, 355.0f),
            ],
            new Color("090c13"));
        DrawLine(
            new Vector2(79.0f, 145.0f),
            new Vector2(151.0f, 145.0f),
            new Color(_accent, 0.65f),
            2.0f);
        DrawCircle(new Vector2(96.0f, 145.0f), 3.0f, _accent);
        DrawCircle(new Vector2(134.0f, 145.0f), 3.0f, _accent);
        DrawLine(
            new Vector2(22.0f, 385.0f),
            new Vector2(208.0f, 385.0f),
            new Color(_accent, 0.5f),
            2.0f);
    }
}
