using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace Firaw.WorkAssistant;

internal sealed class NeonScrollBar : Control
{
    private const int EmGetFirstVisibleLine = 0x00CE;
    private const int EmGetLineCount = 0x00BA;
    private const int EmLineScroll = 0x00B6;
    private readonly Control _target;
    private bool _dragging;
    private int _dragOffset;

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr handle, int message, IntPtr wParam, IntPtr lParam);

    public NeonScrollBar(Control target)
    {
        _target = target;
        Width = 17;
        BackColor = target.BackColor;
        target.BackColorChanged += (_, _) => { BackColor = target.BackColor; Invalidate(); };
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer, true);
        target.MouseWheel += (_, _) => BeginRefresh();
        target.KeyUp += (_, _) => BeginRefresh();
        target.Resize += (_, _) => BeginRefresh();
        target.Invalidated += (_, _) => RefreshScrollState();
        target.VisibleChanged += (_, _) => RefreshScrollState();
        if (target is ListBox list)
            list.SelectedIndexChanged += (_, _) => BeginRefresh();
        if (target is TextBox text)
            text.TextChanged += (_, _) => BeginRefresh();
    }

    private void BeginRefresh()
    {
        if (IsHandleCreated && !IsDisposed) BeginInvoke(RefreshScrollState);
    }

    public void RefreshScrollState()
    {
        if (IsDisposed) return;
        Visible = _target.Visible && ScrollState().Maximum > 0;
        Invalidate();
    }

    private (int Position, int Maximum, int Visible) ScrollState()
    {
        if (_target is ListBox list)
        {
            var visible = Math.Max(1, list.ClientSize.Height / Math.Max(1, list.ItemHeight));
            return (list.TopIndex, Math.Max(0, list.Items.Count - visible), visible);
        }
        if (_target is TextBox text && text.IsHandleCreated)
        {
            var visible = Math.Max(1, text.ClientSize.Height / Math.Max(1, text.Font.Height));
            var count = (int)SendMessage(text.Handle, EmGetLineCount, IntPtr.Zero, IntPtr.Zero);
            var position = (int)SendMessage(text.Handle, EmGetFirstVisibleLine, IntPtr.Zero, IntPtr.Zero);
            return (position, Math.Max(0, count - visible), visible);
        }
        return (0, 0, 1);
    }

    private Rectangle Thumb(int position, int maximum, int visible)
    {
        var trackHeight = Math.Max(1, Height - 8);
        var thumbHeight = Math.Clamp(trackHeight * visible / Math.Max(visible + maximum, 1),
            Math.Min(26, trackHeight), trackHeight);
        var travel = Math.Max(0, trackHeight - thumbHeight);
        var y = 4 + (maximum == 0 ? 0 : travel * Math.Clamp(position, 0, maximum) / maximum);
        return new Rectangle(5, y, 7, thumbHeight);
    }

    private void ScrollTo(int position)
    {
        var (current, maximum, _) = ScrollState();
        position = Math.Clamp(position, 0, maximum);
        if (_target is ListBox list) list.TopIndex = position;
        else if (_target is TextBox text && text.IsHandleCreated)
            SendMessage(text.Handle, EmLineScroll, IntPtr.Zero, (IntPtr)(position - current));
        Invalidate();
    }

    private void ScrollAt(int y)
    {
        var (_, maximum, visible) = ScrollState();
        if (maximum == 0) return;
        var thumb = Thumb(0, maximum, visible);
        var travel = Math.Max(1, Height - 8 - thumb.Height);
        ScrollTo((int)Math.Round((double)Math.Clamp(y - 4 - _dragOffset, 0, travel) * maximum / travel));
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        var (position, maximum, visible) = ScrollState();
        if (maximum == 0) return;
        var thumb = Thumb(position, maximum, visible);
        _dragOffset = thumb.Contains(e.Location) ? e.Y - thumb.Top : thumb.Height / 2;
        _dragging = true;
        Capture = true;
        ScrollAt(e.Y);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragging && e.Button == MouseButtons.Left) ScrollAt(e.Y);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _dragging = false;
        Capture = false;
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        var (position, _, _) = ScrollState();
        ScrollTo(position + (e.Delta < 0 ? 3 : -3));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(BackColor);
        var (position, maximum, visible) = ScrollState();
        if (maximum == 0) return;
        using var track = new SolidBrush(Theme.Border);
        e.Graphics.FillRoundedRectangle(track, new Rectangle(7, 4, 3, Math.Max(4, Height - 8)), 1);
        using var thumbBrush = new LinearGradientBrush(
            new Rectangle(5, 0, 7, Math.Max(1, Height)),
            Theme.CyanDark, Theme.Cyan, LinearGradientMode.Vertical);
        e.Graphics.FillRoundedRectangle(thumbBrush, Thumb(position, maximum, visible), 3);
    }
}

internal static class ScrollChrome
{
    public static void Attach(Control host, Control target)
    {
        var bar = new NeonScrollBar(target);
        host.Controls.Add(bar);
        void PositionBar()
        {
            var origin = target.Location;
            for (var parent = target.Parent; parent is not null && parent != host; parent = parent.Parent)
                origin.Offset(parent.Location);
            var bounds = new Rectangle(origin.X + target.Width - bar.Width,
                origin.Y, bar.Width, target.Height);
            if (bar.Bounds != bounds) bar.Bounds = bounds;
            bar.BringToFront();
            bar.RefreshScrollState();
        }
        target.Resize += (_, _) => PositionBar();
        target.Move += (_, _) => PositionBar();
        host.Resize += (_, _) => PositionBar();
        PositionBar();
    }
}
