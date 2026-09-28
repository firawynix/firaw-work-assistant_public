using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Globalization;

namespace Firaw.WorkAssistant;

internal sealed class DatePickerPopup : Form
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private readonly CalendarGrid _calendar = new();
    private readonly Label _month = new() { Dock = DockStyle.Fill, ForeColor = Theme.Text,
        Font = new Font("Segoe UI Semibold", 11), TextAlign = ContentAlignment.MiddleCenter };
    private readonly TextBox _hour = TimeInput();
    private readonly TextBox _minute = TimeInput();
    private DateTime _selected;
    private DateTime _shownMonth;

    public event Action<DateTime>? Accepted;

    public DatePickerPopup(DateTime value)
    {
        _selected = value;
        _shownMonth = new DateTime(value.Year, value.Month, 1);
        _hour.Text = value.Hour.ToString("00");
        _minute.Text = value.Minute.ToString("00");
        Text = "Escolher data e horário";
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        KeyPreview = true;
        Size = new Size(326, 411);
        BackColor = Theme.Panel;
        ForeColor = Theme.Text;
        DoubleBuffered = true;
        BuildLayout();
        ChangeMonth(0);
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) Close(); };
    }

    private static TextBox TimeInput() => new()
    {
        Width = 43, MaxLength = 2, BorderStyle = BorderStyle.None,
        BackColor = Theme.Field, ForeColor = Theme.Text,
        Font = new Font("Segoe UI Semibold", 11), TextAlign = HorizontalAlignment.Center,
        Margin = new Padding(2, 3, 2, 0)
    };

    private void BuildLayout()
    {
        var frame = new SurfaceCard { Dock = DockStyle.Fill, Highlight = true,
            Padding = new Padding(14, 10, 14, 15) };
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, BackColor = Theme.Panel };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        root.Controls.Add(new Label { Text = "FIRAW  /  DEFINIR DATA E HORA",
            Dock = DockStyle.Fill, ForeColor = Theme.Cyan, Font = new Font("Segoe UI Semibold", 9),
            TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        var navigation = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3,
            RowCount = 1, BackColor = Theme.Panel };
        navigation.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42));
        navigation.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        navigation.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42));
        navigation.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var previous = new NeonButton { Text = "‹", Dock = DockStyle.Fill,
            Font = new Font("Segoe UI Semibold", 16), Margin = new Padding(2) };
        var next = new NeonButton { Text = "›", Dock = DockStyle.Fill,
            Font = new Font("Segoe UI Semibold", 16), Margin = new Padding(2) };
        previous.Click += (_, _) => ChangeMonth(-1);
        next.Click += (_, _) => ChangeMonth(1);
        navigation.Controls.Add(previous, 0, 0);
        navigation.Controls.Add(_month, 1, 0);
        navigation.Controls.Add(next, 2, 0);
        root.Controls.Add(navigation, 0, 1);
        _calendar.Dock = DockStyle.Fill;
        _calendar.Month = _shownMonth;
        _calendar.Selected = _selected.Date;
        _calendar.DateClicked += date =>
        {
            _selected = date + _selected.TimeOfDay;
            _calendar.Selected = date;
            _calendar.Invalidate();
        };
        root.Controls.Add(_calendar, 0, 2);

        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2,
            BackColor = Theme.Panel };
        footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 31));
        footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var time = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false,
            BackColor = Theme.Panel, FlowDirection = FlowDirection.LeftToRight };
        time.Controls.Add(new Label { Text = "HORÁRIO", Width = 73, Height = 27,
            ForeColor = Theme.Muted, Font = new Font("Segoe UI Semibold", 8),
            TextAlign = ContentAlignment.MiddleLeft });
        time.Controls.Add(_hour);
        time.Controls.Add(new Label { Text = ":", Width = 12, Height = 27,
            ForeColor = Theme.Cyan, TextAlign = ContentAlignment.MiddleCenter });
        time.Controls.Add(_minute);
        _hour.Enter += (_, _) => _hour.SelectAll();
        _minute.Enter += (_, _) => _minute.SelectAll();
        footer.Controls.Add(time, 0, 0);
        var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3,
            RowCount = 1, BackColor = Theme.Panel };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 91));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 91));
        actions.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var today = new NeonButton { Text = "Hoje", Dock = DockStyle.Fill, Margin = new Padding(2, 2, 4, 0) };
        var cancel = new NeonButton { Text = "Cancelar", Dock = DockStyle.Fill, Margin = new Padding(2, 2, 4, 0) };
        var apply = new NeonButton { Text = "Aplicar", Dock = DockStyle.Fill, PrimaryStyle = true,
            Margin = new Padding(2, 2, 0, 0) };
        today.Click += (_, _) =>
        {
            _selected = DateTime.Today + _selected.TimeOfDay;
            _shownMonth = new DateTime(_selected.Year, _selected.Month, 1);
            ChangeMonth(0);
        };
        cancel.Click += (_, _) => Close();
        apply.Click += (_, _) =>
        {
            if (!int.TryParse(_hour.Text, out var hour) || hour is < 0 or > 23 ||
                !int.TryParse(_minute.Text, out var minute) || minute is < 0 or > 59)
            {
                MessageBox.Show(this, "Informe um horário entre 00:00 e 23:59.",
                    "Horário inválido", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            Accepted?.Invoke(_selected.Date.AddHours(hour).AddMinutes(minute));
            Close();
        };
        actions.Controls.Add(today, 0, 0);
        actions.Controls.Add(cancel, 1, 0);
        actions.Controls.Add(apply, 2, 0);
        footer.Controls.Add(actions, 0, 1);
        root.Controls.Add(footer, 0, 3);
        frame.Controls.Add(root);
        Controls.Add(frame);
    }

    private void ChangeMonth(int delta)
    {
        _shownMonth = _shownMonth.AddMonths(delta);
        _month.Text = PtBr.TextInfo.ToTitleCase(_shownMonth.ToString("MMMM yyyy", PtBr));
        _calendar.Month = _shownMonth;
        _calendar.Selected = _selected.Date;
        _calendar.Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (Width < 20 || Height < 20) return;
        using var path = SurfaceCard.Rounded(new Rectangle(0, 0, Width, Height), 14);
        Region?.Dispose();
        Region = new Region(path);
    }
}

internal sealed class CalendarGrid : Control
{
    private static readonly string[] Days = ["SEG", "TER", "QUA", "QUI", "SEX", "SÁB", "DOM"];
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public DateTime Month { get; set; } = DateTime.Today;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public DateTime Selected { get; set; } = DateTime.Today;
    public event Action<DateTime>? DateClicked;

    public CalendarGrid()
    {
        DoubleBuffered = true;
        BackColor = Theme.Panel;
        ForeColor = Theme.Text;
        Cursor = Cursors.Hand;
    }

    private (int Left, int Top, int CellWidth, int CellHeight) Grid()
    {
        var left = 1;
        var top = 30;
        return (left, top, Math.Max(1, (Width - 2) / 7), Math.Max(1, (Height - top - 3) / 6));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(Theme.Panel);
        using var weekday = new Font("Segoe UI Semibold", 8);
        using var number = new Font("Segoe UI Semibold", 9.5f);
        var (left, top, cw, ch) = Grid();
        for (var col = 0; col < 7; col++)
            TextRenderer.DrawText(e.Graphics, Days[col], weekday,
                new Rectangle(left + col * cw, 3, cw, 22), Theme.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        var first = new DateTime(Month.Year, Month.Month, 1);
        var offset = ((int)first.DayOfWeek + 6) % 7;
        var count = DateTime.DaysInMonth(Month.Year, Month.Month);
        for (var day = 1; day <= count; day++)
        {
            var cell = offset + day - 1;
            var rectangle = new Rectangle(left + cell % 7 * cw + 2,
                top + cell / 7 * ch + 2, cw - 4, ch - 4);
            var date = new DateTime(Month.Year, Month.Month, day);
            var chosen = date == Selected;
            if (chosen)
            {
                using var fill = new SolidBrush(Theme.Cyan);
                using var path = SurfaceCard.Rounded(rectangle, 7);
                e.Graphics.FillPath(fill, path);
            }
            else if (date == DateTime.Today)
            {
                using var border = new Pen(Theme.CyanDark, 1.4f);
                using var path = SurfaceCard.Rounded(rectangle, 7);
                e.Graphics.DrawPath(border, path);
            }
            TextRenderer.DrawText(e.Graphics, day.ToString(), number, rectangle,
                chosen ? Theme.Background : date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday
                    ? Theme.Muted : Theme.Text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        var (left, top, cw, ch) = Grid();
        if (e.X < left || e.Y < top) return;
        var col = (e.X - left) / cw;
        var row = (e.Y - top) / ch;
        if (col < 0 || col >= 7 || row < 0 || row >= 6) return;
        var first = new DateTime(Month.Year, Month.Month, 1);
        var offset = ((int)first.DayOfWeek + 6) % 7;
        var day = row * 7 + col - offset + 1;
        if (day >= 1 && day <= DateTime.DaysInMonth(Month.Year, Month.Month))
            DateClicked?.Invoke(new DateTime(Month.Year, Month.Month, day));
    }
}
