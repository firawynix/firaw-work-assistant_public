using System.Drawing.Drawing2D;

namespace Firaw.WorkAssistant;

internal sealed class NoteStickerForm : Form
{
    private readonly StickyNote _note;
    private readonly Action _onChanged;
    private readonly TextBox _title = new() { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, Font = new Font("Segoe UI Semibold", 17) };
    private readonly TextBox _text = new() { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, Multiline = true, ScrollBars = ScrollBars.None, AcceptsReturn = true, Font = new Font("Segoe UI", 14) };
    private readonly Panel _header = new() { Dock = DockStyle.Fill };
    private readonly SurfaceCard _frame = new() { Dock = DockStyle.Fill, Padding = new Padding(2), Highlight = true };
    private readonly Label _brand = new() { Text = "FIRAW  /  ANOTAÇÃO", Dock = DockStyle.Fill, Font = new Font("Segoe UI Semibold", 9), TextAlign = ContentAlignment.MiddleLeft };
    private readonly Label _hint = new() { Text = "ANOTAÇÃO EM TELA  •  SALVA AUTOMATICAMENTE", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 8), ForeColor = Theme.Muted, TextAlign = ContentAlignment.MiddleLeft };
    private readonly NeonButton _colorButton = new() { Text = "●", Dock = DockStyle.Fill };
    private readonly NeonButton _opacityButton = new() { Text = "◐", Dock = DockStyle.Fill };
    private readonly ResizeGrip _resizeGrip = new();
    private bool _loading;
    private bool _internalClose;
    private bool _resizing;
    private Point _dragOrigin;
    private Point _windowOrigin;
    private Point _resizeOrigin;
    private Size _resizeStart;

    public NoteStickerForm(StickyNote note, Action onChanged)
    {
        _note = note;
        _onChanged = onChanged;
        Text = "Anotação • " + note.Title;
        MinimumSize = new Size(340, 240);
        MaximumSize = new Size(900, 900);
        Size = new Size(Math.Clamp(note.Width, 340, 900), Math.Clamp(note.Height, 240, 900));
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Theme.Panel;
        ForeColor = Theme.Text;
        Opacity = note.Opacity;
        var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 720);
        Location = note.X is int x && note.Y is int y
            ? new Point(Math.Clamp(x, area.Left, Math.Max(area.Left, area.Right - Width)), Math.Clamp(y, area.Top, Math.Max(area.Top, area.Bottom - Height)))
            : new Point(area.Right - Width - 45, area.Top + 310);
        BuildLayout();
        Theme.Style(this);
        _title.Font = new Font("Segoe UI Semibold", 17);
        _text.Font = new Font("Segoe UI", 14);
        _colorButton.Click += (_, _) => CycleColor();
        _opacityButton.Click += (_, _) => CycleOpacity();
        _title.TextChanged += (_, _) =>
        {
            if (_loading) return;
            _note.Title = _title.Text;
            Text = "Anotação • " + _note.Title;
            _onChanged();
        };
        _text.TextChanged += (_, _) =>
        {
            if (_loading) return;
            _note.Text = _text.Text;
            _onChanged();
        };
        FormClosing += (_, _) => { if (!_internalClose) _note.Visible = false; };
        FormClosed += (_, _) => { if (!_internalClose) _onChanged(); };
        Resize += (_, _) => RoundCorners();
        _resizeGrip.MouseDown += StartResize;
        _resizeGrip.MouseMove += ResizeSticker;
        _resizeGrip.MouseUp += EndResize;
        RefreshNote();
    }

    protected override CreateParams CreateParams
    {
        get { var p = base.CreateParams; p.ClassStyle |= 0x00020000; return p; }
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, Padding = new Padding(16, 0, 16, 12), BackColor = Theme.Panel };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 69));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        _frame.Controls.Add(root);
        Controls.Add(_frame);
        _resizeGrip.Location = new Point(Width - 27, Height - 27);
        Controls.Add(_resizeGrip);
        _resizeGrip.BringToFront();
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, Margin = new Padding(0, 0, 0, 5), BackColor = Theme.Panel };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 43));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 43));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 43));
        header.Controls.Add(_brand, 0, 0);
        header.Controls.Add(_colorButton, 1, 0);
        header.Controls.Add(_opacityButton, 2, 0);
        var close = new NeonButton { Text = "×", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 14), DangerStyle = true };
        close.Click += (_, _) => Close();
        header.Controls.Add(close, 3, 0);
        _header.Controls.Add(header);
        root.Controls.Add(_header, 0, 0);
        var titleWrap = new FieldShell { Dock = DockStyle.Fill, Margin = new Padding(0, 2, 0, 10), Padding = new Padding(13, 9, 12, 9) };
        titleWrap.Controls.Add(_title);
        root.Controls.Add(titleWrap, 0, 1);
        var textWrap = new FieldShell { Dock = DockStyle.Fill, Padding = new Padding(14, 12, 12, 12) };
        textWrap.Controls.Add(_text);
        ScrollChrome.Attach(textWrap, _text);
        root.Controls.Add(textWrap, 0, 2);
        root.Controls.Add(_hint, 0, 3);
        _header.MouseDown += StartDrag;
        _header.MouseMove += Drag;
        _header.MouseUp += EndDrag;
        header.MouseDown += StartDrag;
        header.MouseMove += Drag;
        header.MouseUp += EndDrag;
        _brand.MouseDown += StartDrag;
        _brand.MouseMove += Drag;
        _brand.MouseUp += EndDrag;
    }

    private void RoundCorners()
    {
        using var path = new GraphicsPath();
        var r = 18;
        path.AddArc(0, 0, r, r, 180, 90);
        path.AddArc(Width - r, 0, r, r, 270, 90);
        path.AddArc(Width - r, Height - r, r, r, 0, 90);
        path.AddArc(0, Height - r, r, r, 90, 90);
        path.CloseFigure();
        Region?.Dispose();
        Region = new Region(path);
    }

    private Color Accent => _note.Color switch
    {
        "Verde" => Color.FromArgb(61, 236, 172),
        "Violeta" => Color.FromArgb(175, 130, 255),
        _ => Theme.Cyan
    };

    public void RefreshNote()
    {
        _loading = true;
        if (!_title.Focused) _title.Text = _note.Title;
        if (!_text.Focused) _text.Text = TextLines.ForEditor(_note.Text);
        _header.BackColor = Theme.Panel;
        _brand.ForeColor = Accent;
        _frame.Stroke = Accent;
        _frame.Invalidate();
        _hint.Text = $"ANOTAÇÃO EM TELA  •  {Math.Round(_note.Opacity * 100)}% OPACO  •  SALVA AUTOMATICAMENTE";
        Opacity = _note.Opacity;
        _loading = false;
    }

    private void CycleColor()
    {
        _note.Color = _note.Color switch { "Ciano" => "Verde", "Verde" => "Violeta", _ => "Ciano" };
        RefreshNote();
        _onChanged();
    }

    private void CycleOpacity()
    {
        _note.Opacity = _note.Opacity > 0.85 ? 0.72 : _note.Opacity > 0.65 ? 0.56 : 0.92;
        RefreshNote();
        _onChanged();
    }

    private void StartDrag(object? sender, MouseEventArgs e)
    {
        _dragOrigin = Cursor.Position;
        _windowOrigin = Location;
    }

    private void Drag(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        Location = new Point(_windowOrigin.X + Cursor.Position.X - _dragOrigin.X,
            _windowOrigin.Y + Cursor.Position.Y - _dragOrigin.Y);
    }

    private void EndDrag(object? sender, MouseEventArgs e)
    {
        _note.X = Left;
        _note.Y = Top;
        _onChanged();
    }

    private void StartResize(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        _resizing = true;
        _resizeOrigin = Cursor.Position;
        _resizeStart = Size;
        _resizeGrip.Capture = true;
    }

    private void ResizeSticker(object? sender, MouseEventArgs e)
    {
        if (!_resizing || e.Button != MouseButtons.Left) return;
        var cursor = Cursor.Position;
        var area = Screen.FromPoint(Location).WorkingArea;
        var maxWidth = Math.Max(MinimumSize.Width, Math.Min(900, area.Right - Left));
        var maxHeight = Math.Max(MinimumSize.Height, Math.Min(900, area.Bottom - Top));
        Size = new Size(Math.Clamp(_resizeStart.Width + cursor.X - _resizeOrigin.X, MinimumSize.Width, maxWidth),
            Math.Clamp(_resizeStart.Height + cursor.Y - _resizeOrigin.Y, MinimumSize.Height, maxHeight));
    }

    private void EndResize(object? sender, MouseEventArgs e)
    {
        if (!_resizing) return;
        _resizing = false;
        _resizeGrip.Capture = false;
        _note.Width = Width;
        _note.Height = Height;
        _onChanged();
    }

    public void Stop()
    {
        _internalClose = true;
        Close();
    }
}
