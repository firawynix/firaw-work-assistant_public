using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace Firaw.WorkAssistant;

internal sealed class ProgressView : Control
{
    private int _percent;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Percent
    {
        get => _percent;
        set { _percent = Math.Clamp(value, 0, 100); Invalidate(); }
    }

    public ProgressView()
    {
        DoubleBuffered = true;
        Height = 30;
        BackColor = Theme.Panel;
        ForeColor = Theme.Text;
        Font = new Font("Segoe UI Semibold", 9);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var bar = new Rectangle(0, Height - 10, Math.Max(1, Width - 52), 8);
        using var track = new SolidBrush(Theme.Field);
        var accent = Theme.ProgressAccent(_percent);
        using var fill = new LinearGradientBrush(bar, accent, ControlPaint.Light(accent, 0.25f), LinearGradientMode.Horizontal);
        e.Graphics.FillRoundedRectangle(track, bar, 4);
        if (_percent > 0)
        {
            var progress = new Rectangle(bar.X, bar.Y, Math.Max(8, bar.Width * _percent / 100), bar.Height);
            e.Graphics.FillRoundedRectangle(fill, progress, 4);
        }
        TextRenderer.DrawText(e.Graphics, $"{_percent}%", Font,
            new Rectangle(Width - 50, 0, 50, Height), accent,
            TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
    }
}

internal static class GraphicsExtensions
{
    public static void FillRoundedRectangle(this Graphics graphics, Brush brush, Rectangle rect, int radius)
    {
        using var path = new GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(rect.Left, rect.Top, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Top, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.Left, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        graphics.FillPath(brush, path);
    }
}
