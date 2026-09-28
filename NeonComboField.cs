using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing.Drawing2D;

namespace Firaw.WorkAssistant;

internal sealed class NeonComboField : UserControl
{
    private readonly TextBox _input = new()
    {
        BorderStyle = BorderStyle.None, BackColor = Theme.Field,
        ForeColor = Theme.Text, Font = new Font("Segoe UI", 10.5f)
    };
    private readonly ComboArrow _arrow = new();
    private readonly bool _editable;
    private int _selectedIndex = -1;

    public List<string> Items { get; } = [];
    public event EventHandler? SelectedIndexChanged;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            var next = value >= 0 && value < Items.Count ? value : -1;
            if (_selectedIndex == next) return;
            _selectedIndex = next;
            _input.Text = next >= 0 ? Items[next] : "";
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public object? SelectedItem
    {
        get => SelectedIndex >= 0 ? Items[SelectedIndex] : null;
        set => SelectedIndex = value is null ? -1 :
            Items.FindIndex(item => string.Equals(item, value.ToString(), StringComparison.CurrentCultureIgnoreCase));
    }

    [AllowNull]
    public override string Text
    {
        get => _input?.Text ?? "";
        set
        {
            if (_input is null) return;
            _input.Text = value ?? "";
            if (!_editable)
                _selectedIndex = Items.FindIndex(item =>
                    string.Equals(item, _input.Text, StringComparison.CurrentCultureIgnoreCase));
        }
    }

    public NeonComboField(bool editable = false)
    {
        _editable = editable;
        Height = 32;
        BackColor = Theme.Field;
        _input.ReadOnly = !editable;
        _input.Cursor = editable ? Cursors.IBeam : Cursors.Hand;
        _input.AutoCompleteMode = editable ? AutoCompleteMode.SuggestAppend : AutoCompleteMode.None;
        _input.AutoCompleteSource = editable ? AutoCompleteSource.CustomSource : AutoCompleteSource.None;
        _arrow.Click += (_, _) => ShowChoices();
        _input.MouseClick += (_, _) => { if (!_editable) ShowChoices(); };
        _input.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Down && e.Alt || e.KeyCode == Keys.F4)
            {
                e.SuppressKeyPress = true;
                ShowChoices();
            }
        };
        _input.Enter += (_, _) => RefreshSuggestions();
        Controls.Add(_input);
        Controls.Add(_arrow);
        Resize += (_, _) => LayoutParts();
        LayoutParts();
    }

    private void LayoutParts()
    {
        if (Width < 10 || Height < 10) return;
        _arrow.Bounds = new Rectangle(Math.Max(0, Width - 32), 2, 30, Math.Max(20, Height - 4));
        _input.Bounds = new Rectangle(11, Math.Max(0, (Height - _input.PreferredHeight) / 2),
            Math.Max(15, Width - 50), _input.PreferredHeight);
    }

    private void RefreshSuggestions()
    {
        if (!_editable) return;
        var source = new AutoCompleteStringCollection();
        source.AddRange(Items.ToArray());
        _input.AutoCompleteCustomSource = source;
    }

    private void ShowChoices()
    {
        if (!Enabled || Items.Count == 0) return;
        RefreshSuggestions();
        var rows = Math.Min(8, Items.Count);
        var list = new ListBox
        {
            BorderStyle = BorderStyle.None, DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = 30, BackColor = Theme.Panel, ForeColor = Theme.Text,
            Font = new Font("Segoe UI", 10), IntegralHeight = false,
            Size = new Size(Math.Max(Width, 150), rows * 30)
        };
        list.Items.AddRange(Items.Cast<object>().ToArray());
        list.SelectedIndex = _selectedIndex;
        list.DrawItem += (_, e) =>
        {
            if (e.Index < 0) return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var selected = (e.State & DrawItemState.Selected) != 0;
            using var fill = new SolidBrush(selected ? Color.FromArgb(32, 83, 100) : Theme.Panel);
            e.Graphics.FillRectangle(fill, e.Bounds);
            TextRenderer.DrawText(e.Graphics, Items[e.Index], list.Font,
                new Rectangle(e.Bounds.X + 11, e.Bounds.Y, e.Bounds.Width - 18, e.Bounds.Height),
                selected ? Theme.CyanBright : Theme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        };
        var host = new ToolStripControlHost(list)
        {
            AutoSize = false, Margin = Padding.Empty, Padding = Padding.Empty,
            Size = list.Size
        };
        var menu = new ToolStripDropDown
        {
            AutoSize = false, Padding = new Padding(2), BackColor = Theme.Panel,
            Size = new Size(list.Width + 4, list.Height + 4)
        };
        menu.Items.Add(host);
        void Accept()
        {
            if (list.SelectedIndex >= 0) SelectedIndex = list.SelectedIndex;
            menu.Close();
        }
        list.MouseClick += (_, e) =>
        {
            var index = list.IndexFromPoint(e.Location);
            if (index >= 0) { list.SelectedIndex = index; Accept(); }
        };
        list.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Accept(); }
            else if (e.KeyCode == Keys.Escape) menu.Close();
        };
        var area = Screen.FromControl(this).WorkingArea;
        var location = PointToScreen(new Point(0, Height + 2));
        location.X = Math.Clamp(location.X, area.Left, Math.Max(area.Left, area.Right - menu.Width));
        if (location.Y + menu.Height > area.Bottom)
            location.Y = Math.Max(area.Top, location.Y - Height - menu.Height - 4);
        menu.Show(location);
        list.Focus();
    }
}

internal sealed class ComboArrow : Control
{
    private bool _hover;

    public ComboArrow()
    {
        Cursor = Cursors.Hand;
        AccessibleName = "Abrir opções";
        AccessibleRole = AccessibleRole.PushButton;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(Parent?.BackColor ?? Theme.Field);
        using var shape = SurfaceCard.Rounded(new Rectangle(1, 1, Width - 3, Height - 3), 11);
        using var fill = new SolidBrush(_hover ? Theme.FieldHover : Theme.Panel);
        using var border = new Pen(_hover ? Theme.CyanDark : Theme.Border, 1);
        using var arrow = new Pen(Enabled ? Theme.Cyan : Theme.Muted, 1.8f)
        {
            StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round
        };
        e.Graphics.FillPath(fill, shape);
        e.Graphics.DrawPath(border, shape);
        var centerX = Width / 2f;
        var centerY = Height / 2f;
        e.Graphics.DrawLines(arrow, [new PointF(centerX - 4, centerY - 2),
            new PointF(centerX, centerY + 2), new PointF(centerX + 4, centerY - 2)]);
    }
}
