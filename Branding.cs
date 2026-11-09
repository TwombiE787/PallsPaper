using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace PallsPaper;

internal enum GlyphKind
{
    Play,
    Stop,
    Folder,
    Link,
    Monitor
}

internal static class Branding
{
    public static Icon CreateIcon()
    {
        using var bitmap = new Bitmap(64, 64);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            DrawLogo(graphics, new Rectangle(0, 0, 64, 64));
        }

        var handle = bitmap.GetHicon();
        try
        {
            using var icon = Icon.FromHandle(handle);
            return (Icon)icon.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    public static void DrawLogo(Graphics graphics, Rectangle bounds)
    {
        var scale = bounds.Width / 64f;
        var background = new RectangleF(
            bounds.X + 2 * scale,
            bounds.Y + 2 * scale,
            bounds.Width - 4 * scale,
            bounds.Height - 4 * scale);

        using var backgroundPath = RoundedRectangle(background, 15 * scale);
        using var backgroundBrush = new LinearGradientBrush(
            background,
            Color.FromArgb(111, 130, 255),
            Color.FromArgb(62, 82, 217),
            45f);
        graphics.FillPath(backgroundBrush, backgroundPath);

        var paper = new RectangleF(
            bounds.X + 18 * scale,
            bounds.Y + 10 * scale,
            29 * scale,
            41 * scale);
        using var paperPath = RoundedRectangle(paper, 5 * scale);
        using var paperBrush = new SolidBrush(Color.FromArgb(244, 247, 255));
        graphics.FillPath(paperBrush, paperPath);

        using var linePen = new Pen(Color.FromArgb(175, 188, 219), 2.2f * scale)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        graphics.DrawLine(linePen, bounds.X + 24 * scale, bounds.Y + 21 * scale,
            bounds.X + 39 * scale, bounds.Y + 21 * scale);
        graphics.DrawLine(linePen, bounds.X + 24 * scale, bounds.Y + 28 * scale,
            bounds.X + 34 * scale, bounds.Y + 28 * scale);

        using var playBrush = new SolidBrush(Color.FromArgb(56, 187, 190));
        var play = new[]
        {
            new PointF(bounds.X + 27 * scale, bounds.Y + 34 * scale),
            new PointF(bounds.X + 39 * scale, bounds.Y + 41 * scale),
            new PointF(bounds.X + 27 * scale, bounds.Y + 48 * scale)
        };
        graphics.FillPolygon(playBrush, play);
    }

    private static GraphicsPath RoundedRectangle(RectangleF rectangle, float radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(rectangle.X, rectangle.Y, diameter, diameter, 180, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Y, diameter, diameter, 270, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rectangle.X, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr icon);
}

internal sealed class GlyphIcon : Control
{
    public GlyphIcon(GlyphKind glyph, Color color, int size = 20)
    {
        Glyph = glyph;
        IconColor = color;
        Size = new Size(size, size);
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.SupportsTransparentBackColor
            | ControlStyles.UserPaint,
            true);
        BackColor = Color.Transparent;
    }

    public GlyphKind Glyph { get; set; }
    public Color IconColor { get; set; }

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        base.OnPaint(eventArgs);
        eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        DrawGlyph(eventArgs.Graphics, ClientRectangle, Glyph, IconColor);
    }

    internal static void DrawGlyph(Graphics graphics, Rectangle bounds, GlyphKind glyph, Color color)
    {
        var scale = Math.Min(bounds.Width, bounds.Height) / 24f;
        var offsetX = bounds.X + (bounds.Width - 24 * scale) / 2f;
        var offsetY = bounds.Y + (bounds.Height - 24 * scale) / 2f;
        var state = graphics.Save();
        graphics.TranslateTransform(offsetX, offsetY);
        graphics.ScaleTransform(scale, scale);

        using var pen = new Pen(color, 1.8f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };
        using var fill = new SolidBrush(color);

        switch (glyph)
        {
            case GlyphKind.Play:
                graphics.FillPolygon(fill, [new PointF(7, 4), new PointF(19, 12), new PointF(7, 20)]);
                break;
            case GlyphKind.Stop:
                graphics.FillRectangle(fill, 6, 6, 12, 12);
                break;
            case GlyphKind.Folder:
                graphics.DrawLines(pen, [new PointF(3, 7), new PointF(10, 7), new PointF(12, 9), new PointF(21, 9), new PointF(20, 19), new PointF(4, 19), new PointF(3, 7)]);
                break;
            case GlyphKind.Link:
                graphics.DrawArc(pen, 3, 7, 12, 11, 45, 220);
                graphics.DrawArc(pen, 9, 6, 12, 11, 225, 220);
                graphics.DrawLine(pen, 8, 15, 16, 9);
                break;
            case GlyphKind.Monitor:
                graphics.DrawRectangle(pen, 3, 4, 18, 13);
                graphics.DrawLine(pen, 12, 17, 12, 20);
                graphics.DrawLine(pen, 8, 20, 16, 20);
                break;
        }

        graphics.Restore(state);
    }
}
