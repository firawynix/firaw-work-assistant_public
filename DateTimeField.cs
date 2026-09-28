using System.ComponentModel;
using System.Globalization;

namespace Firaw.WorkAssistant;

internal sealed class DateTimeField : UserControl
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private readonly TextBox _input = new() { BorderStyle = BorderStyle.None, TextAlign = HorizontalAlignment.Left };
    private readonly NeonButton _calendarButton = new() { Text = "▦",
        Font = new Font("Segoe UI Symbol", 12), MinimumSize = Size.Empty, Margin = Padding.Empty };
    private readonly ToolTip _tip = new();
    private DateTime _value = DateTime.Now;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public DateTime Value
    {
        get
        {
            if (TryParse(out var parsed)) _value = parsed;
            return _value;
        }
        set
        {
            _value = value;
            _input.Text = value.ToString("dd/MM/yyyy HH:mm", PtBr);
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public DateTime MinDate { get; } = new(1753, 1, 1);

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public DateTime MaxDate { get; } = new(9998, 12, 31);

    public DateTimeField()
    {
        Height = 38;
        BackColor = Theme.Field;
        _input.Font = new Font("Segoe UI Semibold", 10.5f);
        _input.BackColor = Theme.Field;
        _input.ForeColor = Theme.Text;
        _calendarButton.SubtleStyle = true;
        Controls.Add(_input);
        Controls.Add(_calendarButton);
        Resize += (_, _) => LayoutParts();
        LayoutParts();
        Value = DateTime.Now;
        _input.Leave += (_, _) => ValidateInput();
        _input.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; ValidateInput(); } };
        _calendarButton.Click += (_, _) => OpenCalendar();
    }

    private void LayoutParts()
    {
        var buttonHeight = Math.Max(24, Math.Min(31, Height - 4));
        _calendarButton.Bounds = new Rectangle(Math.Max(0, Width - 32),
            Math.Max(0, (Height - buttonHeight) / 2), 30, buttonHeight);
        _input.Bounds = new Rectangle(11, Math.Max(0, (Height - _input.PreferredHeight) / 2),
            Math.Max(20, Width - 50), _input.PreferredHeight);
    }

    private bool TryParse(out DateTime parsed)
    {
        var valid = DateTime.TryParseExact(_input.Text, "dd/MM/yyyy HH:mm", PtBr,
            DateTimeStyles.None, out parsed);
        return valid && parsed >= MinDate && parsed <= MaxDate;
    }

    private void ValidateInput()
    {
        if (TryParse(out var parsed)) { _value = parsed; return; }
        _tip.Show("Use dia/mês/ano hora:minuto, por exemplo 30/09/2026 15:00.", _input, 0, _input.Height + 4, 4000);
        _input.Text = _value.ToString("dd/MM/yyyy HH:mm", PtBr);
    }

    private void OpenCalendar()
    {
        var popup = new DatePickerPopup(Value);
        popup.Accepted += date => Value = date;
        var area = Screen.FromControl(this).WorkingArea;
        var desired = PointToScreen(new Point(0, Height + 5));
        popup.Location = new Point(
            Math.Clamp(desired.X, area.Left, Math.Max(area.Left, area.Right - popup.Width)),
            desired.Y + popup.Height <= area.Bottom ? desired.Y : Math.Max(area.Top, desired.Y - Height - popup.Height - 10));
        popup.Show(FindForm());
    }
}
