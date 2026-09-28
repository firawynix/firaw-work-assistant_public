using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace Firaw.WorkAssistant;

internal sealed class SurfaceCard : Panel
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color Stroke { get; set; } = Theme.Border;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Highlight { get; set; }

    public SurfaceCard()
    {
        DoubleBuffered = true;
        BackColor = Theme.Panel;
        Padding = new Padding(18);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (Width < 30 || Height < 30) return;
        using var outline = Rounded(new Rectangle(0, 0, Width, Height), 14);
        Region?.Dispose();
        Region = new Region(outline);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (Width < 8 || Height < 8) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var border = Rounded(new Rectangle(1, 1, Width - 3, Height - 3), 14);
        using var pen = new Pen(Highlight ? Theme.CyanDark : Stroke, 1.2f);
        e.Graphics.DrawPath(pen, border);
        if (Highlight)
        {
            using var accent = new Pen(Theme.Cyan, 2);
            e.Graphics.DrawLine(accent, 20, 1, Math.Min(100, Width - 20), 1);
        }
    }

    public static GraphicsPath Rounded(Rectangle rectangle, int radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(rectangle.Left, rectangle.Top, diameter, diameter, 180, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Top, diameter, diameter, 270, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rectangle.Left, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class FieldShell : Panel
{
    public FieldShell()
    {
        DoubleBuffered = true;
        BackColor = Theme.Field;
        Padding = new Padding(11, 5, 8, 5);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (Width < 8 || Height < 8) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = SurfaceCard.Rounded(new Rectangle(1, 1, Width - 3, Height - 3), 7);
        using var pen = new Pen(ContainsFocus ? Theme.CyanDark : Theme.Border, 1);
        e.Graphics.DrawPath(pen, path);
    }

    protected override void OnEnter(EventArgs e) { Invalidate(); base.OnEnter(e); }
    protected override void OnLeave(EventArgs e) { Invalidate(); base.OnLeave(e); }
}

internal sealed class NeonButton : Button
{
    private bool _hover;
    private bool _pressed;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool PrimaryStyle { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool DangerStyle { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool SubtleStyle { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool SegmentStyle { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool SelectedStyle { get; set; }

    public NeonButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        Font = new Font("Segoe UI Semibold", 10);
        ForeColor = Theme.Text;
        BackColor = Theme.Panel;
        Cursor = Cursors.Hand;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        MinimumSize = new Size(28, 35);
        Margin = new Padding(3);
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _pressed = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(Parent?.BackColor ?? Theme.Background);
        if (SegmentStyle)
        {
            var segment = new Rectangle(1, 1, Math.Max(1, Width - 2), Math.Max(1, Height - 2));
            using var shape = SurfaceCard.Rounded(segment, 7);
            using var surface = new SolidBrush(SelectedStyle ? Color.FromArgb(35, 93, 108) : Theme.Field);
            e.Graphics.FillPath(surface, shape);
            if (SelectedStyle)
            {
                using var edge = new Pen(Theme.Cyan, 1.3f);
                using var accent = new Pen(Theme.CyanBright, 3f);
                e.Graphics.DrawPath(edge, shape);
                e.Graphics.DrawLine(accent, 12, Height - 2, Width - 12, Height - 2);
            }
            TextRenderer.DrawText(e.Graphics, Text, Font, segment,
                !Enabled ? Theme.Muted : SelectedStyle ? Theme.CyanBright : Theme.Text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            return;
        }
        var rect = new Rectangle(1, 1, Math.Max(1, Width - 3), Math.Max(1, Height - 3));
        using var path = SurfaceCard.Rounded(rect, 11);
        var fill = !Enabled ? Theme.Panel : PrimaryStyle
            ? (_pressed ? Theme.CyanDark : _hover ? Theme.CyanBright : Theme.Cyan)
            : DangerStyle
                ? (_hover ? Color.FromArgb(67, 32, 48) : Theme.Panel)
                : _hover ? Theme.FieldHover : SubtleStyle ? Theme.Panel : Theme.Field;
        var border = !Enabled ? Theme.Border : PrimaryStyle ? Theme.Cyan : DangerStyle ? Theme.Danger : _hover ? Theme.CyanDark : Theme.Border;
        using var brush = new SolidBrush(fill);
        using var pen = new Pen(border, 1.1f);
        e.Graphics.FillPath(brush, path);
        e.Graphics.DrawPath(pen, path);
        var color = !Enabled ? Theme.Muted : PrimaryStyle ? Theme.Background : DangerStyle ? Theme.Danger : _hover ? Theme.CyanBright : Theme.Text;
        TextRenderer.DrawText(e.Graphics, Text, Font, rect, color,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
}

internal sealed class GlowCheckBox : CheckBox
{
    public GlowCheckBox()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint, true);
        AutoSize = true;
        MinimumSize = new Size(0, 32);
        Font = new Font("Segoe UI", 10);
        Cursor = Cursors.Hand;
        ForeColor = Theme.Text;
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var measured = TextRenderer.MeasureText(Text, Font);
        return new Size(measured.Width + 37, Math.Max(32, measured.Height + 10));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? Theme.Panel);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var y = (Height - 19) / 2;
        var box = new Rectangle(2, y, 18, 18);
        using var path = SurfaceCard.Rounded(box, 5);
        using var fill = new SolidBrush(Checked ? Theme.Cyan : Theme.Field);
        using var border = new Pen(Checked ? Theme.Cyan : Theme.CyanDark, 1.2f);
        e.Graphics.FillPath(fill, path);
        e.Graphics.DrawPath(border, path);
        if (Checked)
        {
            using var tick = new Pen(Theme.Background, 2.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            e.Graphics.DrawLines(tick, [new Point(6, y + 9), new Point(10, y + 13), new Point(16, y + 5)]);
        }
        TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(29, 0, Width - 29, Height),
            ForeColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
}

internal sealed class ResizeGrip : Control
{
    public ResizeGrip()
    {
        Size = new Size(25, 25);
        Cursor = Cursors.SizeNWSE;
        BackColor = Theme.Panel;
        Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(BackColor);
        using var pen = new Pen(Theme.CyanDark, 1.7f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        e.Graphics.DrawLine(pen, 9, 20, 20, 9);
        e.Graphics.DrawLine(pen, 15, 20, 20, 15);
    }
}
