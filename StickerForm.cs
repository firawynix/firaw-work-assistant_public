using System.Drawing.Drawing2D;

namespace Firaw.WorkAssistant;

internal sealed class StickerForm : Form
{
    private readonly WorkItem _item;
    private readonly Action _onChanged;
    private readonly CheckedListBox _steps = new() { BorderStyle = BorderStyle.None, CheckOnClick = true, Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly ProgressView _progress = new() { Dock = DockStyle.Fill };
    private readonly Label _title = new() { Dock = DockStyle.Fill, AutoEllipsis = true, Font = new Font("Segoe UI Semibold", 18), TextAlign = ContentAlignment.MiddleLeft };
    private readonly Label _dates = new() { Dock = DockStyle.Fill, ForeColor = Theme.Muted,
        Font = new Font("Segoe UI", 9), TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
    private readonly TextBox _noteText = new() { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.None, BorderStyle = BorderStyle.None, PlaceholderText = "Escreva uma anotação para esta tarefa..." };
    private readonly NeonButton _checkButton = new() { Text = "☑ Checklist", Dock = DockStyle.Fill };
    private readonly NeonButton _textButton = new() { Text = "Aa", Dock = DockStyle.Fill };
    private readonly NeonButton _opacityButton = new() { Text = "◐", Dock = DockStyle.Fill };
    private readonly NeonButton _completeButton = new() { Text = "✓ Concluir tarefa", Dock = DockStyle.Fill, PrimaryStyle = true };
    private readonly Label _stepsStatus = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Theme.Muted, Font = new Font("Segoe UI", 9) };
    private readonly TableLayoutPanel _layout = new() { Dock = DockStyle.Fill, RowCount = 7, Padding = new Padding(16, 0, 16, 12), BackColor = Theme.Panel };
    private readonly FieldShell _checklistWrap = new() { Dock = DockStyle.Fill, Margin = new Padding(0, 9, 0, 7) };
    private readonly FieldShell _noteWrap = new() { Dock = DockStyle.Fill, Padding = new Padding(13, 9, 12, 9), Margin = new Padding(0, 2, 0, 4) };
    private readonly ResizeGrip _resizeGrip = new();
    private bool _expanded;
    private bool _textCollapsed;
    private bool _internalClose;
    private bool _loading;
    private bool _resizing;
    private int _hiddenTextHeight = 127;
    private Point _dragOrigin;
    private Point _windowOrigin;
    private Point _resizeOrigin;
    private Size _resizeStart;
    public event Action? CompletionRequested;

    public StickerForm(WorkItem item, Action onChanged)
    {
        _item = item;
        _onChanged = onChanged;
        Text = "Sticker • " + item.Title;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        _expanded = item.StickerChecklistExpanded;
        _textCollapsed = item.StickerTextCollapsed;
        MinimumSize = new Size(360, MinimumHeight());
        MaximumSize = new Size(900, 900);
        Size = new Size(Math.Clamp(item.StickerWidth, 360, 900), Math.Clamp(item.StickerHeight, MinimumHeight(), 900));
        BackColor = Theme.Panel;
        ForeColor = Theme.Text;
        Opacity = item.StickerOpacity;
        var bounds = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 720);
        Location = item.StickerX is int x && item.StickerY is int y
            ? new Point(Math.Clamp(x, bounds.Left, Math.Max(bounds.Left, bounds.Right - Width)), Math.Clamp(y, bounds.Top, Math.Max(bounds.Top, bounds.Bottom - Height)))
            : new Point(bounds.Right - Width - 34, bounds.Top + 80);
        BuildLayout();
        Theme.Style(this);
        ApplySections();
        _steps.ItemCheck += StepChecked;
        _completeButton.Click += (_, _) => CompletionRequested?.Invoke();
        _noteText.TextChanged += (_, _) =>
        {
            if (_loading) return;
            _item.Notes = _noteText.Text;
            _onChanged();
        };
        _noteText.Font = new Font("Segoe UI", 12);
        FormClosing += (_, _) => { if (!_internalClose) _item.ShowSticker = false; };
        FormClosed += (_, _) => { if (!_internalClose) _onChanged(); };
        Resize += (_, _) => RoundCorners();
        _resizeGrip.MouseDown += StartResize;
        _resizeGrip.MouseMove += ResizeSticker;
        _resizeGrip.MouseUp += EndResize;
        RefreshItem();
    }

    protected override CreateParams CreateParams
    {
        get { var parameters = base.CreateParams; parameters.ClassStyle |= 0x00020000; return parameters; }
    }

    private void BuildLayout()
    {
        _layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        _layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 61));
        _layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 43));
        _layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 49));
        _layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 127));
        _layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));
        var frame = new SurfaceCard { Dock = DockStyle.Fill, Padding = new Padding(2), Highlight = true };
        frame.Controls.Add(_layout);
        Controls.Add(frame);
        _resizeGrip.Location = new Point(Width - 27, Height - 27);
        Controls.Add(_resizeGrip);
        _resizeGrip.BringToFront();

        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 1,
            BackColor = Theme.Panel, Margin = new Padding(0, 0, 0, 5) };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 44));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42));
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var brand = new Label { Text = "FIRAW  /  TAREFA", Dock = DockStyle.Fill, ForeColor = Theme.Cyan, Font = new Font("Segoe UI Semibold", 9), TextAlign = ContentAlignment.MiddleLeft };
        header.Controls.Add(brand, 0, 0);
        header.Controls.Add(_checkButton, 1, 0);
        header.Controls.Add(_textButton, 2, 0);
        header.Controls.Add(_opacityButton, 3, 0);
        var close = new NeonButton { Text = "×", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 14), DangerStyle = true };
        close.Click += (_, _) => Close();
        header.Controls.Add(close, 4, 0);
        _checkButton.Click += (_, _) => ToggleChecklist();
        _textButton.Click += (_, _) => ToggleText();
        new ToolTip().SetToolTip(_textButton, "Ocultar ou mostrar a anotação da tarefa");
        _opacityButton.Click += (_, _) => CycleOpacity();
        header.MouseDown += StartDrag;
        header.MouseMove += Drag;
        header.MouseUp += EndDrag;
        brand.MouseDown += StartDrag;
        brand.MouseMove += Drag;
        brand.MouseUp += EndDrag;
        _layout.Controls.Add(header, 0, 0);
        _layout.Controls.Add(_title, 0, 1);
        _layout.Controls.Add(_progress, 0, 2);
        _layout.Controls.Add(_dates, 0, 3);
        var noteLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, BackColor = Theme.Field };
        noteLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        noteLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        noteLayout.Controls.Add(new Label { Text = "ANOTAÇÃO DA TAREFA", Dock = DockStyle.Fill, ForeColor = Theme.Cyan, Font = new Font("Segoe UI Semibold", 8) }, 0, 0);
        noteLayout.Controls.Add(_noteText, 0, 1);
        ScrollChrome.Attach(_noteWrap, _noteText);
        _noteWrap.Controls.Add(noteLayout);
        _layout.Controls.Add(_noteWrap, 0, 4);
        _checklistWrap.Controls.Add(_steps);
        ScrollChrome.Attach(_checklistWrap, _steps);
        _layout.Controls.Add(_checklistWrap, 0, 5);
        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2,
            Padding = new Padding(0, 0, 28, 0) };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        footer.Controls.Add(_stepsStatus, 0, 0);
        footer.Controls.Add(_completeButton, 1, 0);
        _layout.Controls.Add(footer, 0, 6);
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

    private void ToggleChecklist()
    {
        _expanded = !_expanded;
        ApplySections(_expanded ? 225 : -225);
    }

    private void ToggleText()
    {
        if (!_textCollapsed) _hiddenTextHeight = Math.Max(127, _noteWrap.Height);
        _textCollapsed = !_textCollapsed;
        ApplySections(_textCollapsed ? -_hiddenTextHeight : _hiddenTextHeight);
    }

    private int MinimumHeight() => 225 + (_textCollapsed ? 0 : 135) + (_expanded ? 145 : 0);

    private void ApplySections(int heightDelta = 0)
    {
        _layout.RowStyles[4].SizeType = !_textCollapsed && !_expanded ? SizeType.Percent : SizeType.Absolute;
        _layout.RowStyles[4].Height = _textCollapsed ? 0 : _expanded ? 127 : 100;
        _layout.RowStyles[5].SizeType = _expanded ? SizeType.Percent : SizeType.Absolute;
        _layout.RowStyles[5].Height = _expanded ? 100 : 0;
        _layout.RowStyles[6].Height = _expanded ? 45 : 0;
        _noteWrap.Visible = !_textCollapsed;
        _steps.Visible = _expanded;
        _checklistWrap.Visible = _expanded;
        _stepsStatus.Visible = _expanded;
        _completeButton.Visible = _expanded;
        _checkButton.Text = _expanded ? "☑ Fechar" : "☑ Checklist";
        _textButton.Text = _textCollapsed ? "Aa+" : "Aa−";
        MinimumSize = new Size(360, MinimumHeight());
        Height = Math.Clamp(Height + heightDelta, MinimumHeight(), MaximumSize.Height);
        _item.StickerChecklistExpanded = _expanded;
        _item.StickerTextCollapsed = _textCollapsed;
        _item.StickerWidth = Width;
        _item.StickerHeight = Height;
        if (heightDelta != 0) _onChanged();
    }

    public void OpenChecklist()
    {
        if (!_expanded) ToggleChecklist();
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
        _item.StickerWidth = Width;
        _item.StickerHeight = Height;
        _onChanged();
    }

    private void CycleOpacity()
    {
        _item.StickerOpacity = _item.StickerOpacity > 0.85 ? 0.72 : _item.StickerOpacity > 0.65 ? 0.56 : 0.92;
        Opacity = _item.StickerOpacity;
        _onChanged();
    }

    private void StepChecked(object? sender, ItemCheckEventArgs e)
    {
        if (_loading || e.Index >= _item.Checklist.Count) return;
        _item.Checklist[e.Index].Done = e.NewValue == CheckState.Checked;
        BeginInvoke(() => { _progress.Percent = _item.Progress; UpdateStepsStatus(); _onChanged(); });
    }

    public void RefreshItem()
    {
        _loading = true;
        _title.Text = _item.Title;
        _dates.Text = _item.AdjustedDueAt is DateTime adjusted
            ? $"Início {_item.StartAt:dd/MM HH:mm}  •  original {_item.DueAt:dd/MM HH:mm}\n{_item.AdjustmentKind}: {adjusted:dd/MM HH:mm}  •  {_item.Project}"
            : $"Início {_item.StartAt:dd/MM HH:mm}  →  final {_item.DueAt:dd/MM HH:mm}  •  {_item.Project}";
        _progress.Percent = _item.Progress;
        if (!_noteText.Focused) _noteText.Text = TextLines.ForEditor(_item.Notes);
        _steps.Items.Clear();
        foreach (var step in _item.Checklist) _steps.Items.Add(step.Text, step.Done);
        UpdateStepsStatus();
        _steps.Visible = _expanded;
        _checklistWrap.Visible = _expanded;
        _stepsStatus.Visible = _expanded;
        _completeButton.Visible = _expanded;
        _loading = false;
    }

    private void UpdateStepsStatus() => _stepsStatus.Text = $"{_item.Checklist.Count(step => step.Done)} de {_item.Checklist.Count} etapas";

    private void StartDrag(object? sender, MouseEventArgs e)
    {
        _dragOrigin = Cursor.Position;
        _windowOrigin = Location;
    }

    private void Drag(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        var screen = Cursor.Position;
        Location = new Point(_windowOrigin.X + screen.X - _dragOrigin.X, _windowOrigin.Y + screen.Y - _dragOrigin.Y);
    }

    private void EndDrag(object? sender, MouseEventArgs e)
    {
        _item.StickerX = Left;
        _item.StickerY = Top;
        _onChanged();
    }

    public void Stop()
    {
        _internalClose = true;
        Close();
    }
}
