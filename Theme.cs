using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace Firaw.WorkAssistant;

internal static class Theme
{
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr handle, string? subAppName, string? subIdList);

    public static readonly Color Background = Color.FromArgb(8, 13, 22);
    public static readonly Color Panel = Color.FromArgb(13, 23, 35);
    public static readonly Color Field = Color.FromArgb(20, 37, 53);
    public static readonly Color FieldHover = Color.FromArgb(28, 59, 73);
    public static readonly Color Cyan = Color.FromArgb(34, 211, 238);
    public static readonly Color CyanBright = Color.FromArgb(103, 232, 249);
    public static readonly Color CyanDark = Color.FromArgb(42, 121, 137);
    public static readonly Color Text = Color.FromArgb(220, 233, 243);
    public static readonly Color Muted = Color.FromArgb(128, 148, 169);
    public static readonly Color Border = Color.FromArgb(32, 50, 70);
    public static readonly Color Danger = Color.FromArgb(251, 113, 133);

    public static Color ProgressAccent(int percent) => percent >= 100 ? Cyan
        : percent >= 67 ? Color.FromArgb(77, 233, 164)
        : percent >= 34 ? Color.FromArgb(255, 194, 78)
        : Color.FromArgb(255, 107, 118);

    public static Color ColorForLevel(int level) => level switch
    {
        0 => Color.FromArgb(255, 107, 118),
        1 => Color.FromArgb(255, 194, 78),
        2 => Color.FromArgb(77, 233, 164),
        _ => Cyan
    };

    public static Color TaskAccent(WorkItem item, DateTime now) => ColorForLevel(item.ColorLevel(now));

    public static void Style(Control control)
    {
        if (control is NeonButton or GlowCheckBox) return;
        if (control is TextBox or ComboBox or DateTimePicker or CheckedListBox or ListBox)
        {
            control.Font = new Font("Segoe UI", 10.5f);
            control.ForeColor = Text;
            control.BackColor = Field;
        }
        if (control is ComboBox combo)
        {
            combo.FlatStyle = FlatStyle.Flat;
            combo.HandleCreated += (_, _) => SetWindowTheme(combo.Handle, "DarkMode_Explorer", null);
            if (combo.DropDownStyle == ComboBoxStyle.DropDownList)
            {
                combo.DrawMode = DrawMode.OwnerDrawFixed;
                combo.ItemHeight = 25;
                combo.DrawItem += (_, e) =>
                {
                    if (e.Index < 0) return;
                    using var brush = new SolidBrush((e.State & DrawItemState.Selected) != 0 ? Cyan : Field);
                    e.Graphics.FillRectangle(brush, e.Bounds);
                    var color = (e.State & DrawItemState.Selected) != 0 ? Background : Text;
                    TextRenderer.DrawText(e.Graphics, combo.Items[e.Index]?.ToString() ?? "", combo.Font,
                        new Rectangle(e.Bounds.Left + 8, e.Bounds.Top, e.Bounds.Width - 8, e.Bounds.Height), color,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                };
            }
        }
        if (control is TextBox { Multiline: true } textBox)
            textBox.HandleCreated += (_, _) => SetWindowTheme(textBox.Handle, "DarkMode_Explorer", null);
        if (control is ListBox listBox)
            listBox.HandleCreated += (_, _) => SetWindowTheme(listBox.Handle, "DarkMode_Explorer", null);
        if (control is DateTimePicker picker)
        {
            picker.CalendarForeColor = Text;
            picker.CalendarMonthBackground = Field;
            picker.HandleCreated += (_, _) => SetWindowTheme(picker.Handle, "DarkMode_Explorer", null);
        }
        if (control is Button button)
        {
            if (button.Tag?.ToString() == "window-control") return;
            button.Font = new Font("Segoe UI", 10);
            button.ForeColor = Text;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = Border;
            button.BackColor = Panel;
            button.Cursor = Cursors.Hand;
            button.Padding = new Padding(8, 3, 8, 3);
            button.Resize += (_, _) => RoundButton(button);
            RoundButton(button);
        }
        foreach (Control child in control.Controls) Style(child);
    }

    public static void Primary(Button button)
    {
        if (button is NeonButton neon)
        {
            neon.PrimaryStyle = true;
            neon.Invalidate();
            return;
        }
        button.BackColor = Cyan;
        button.ForeColor = Background;
        button.FlatAppearance.BorderColor = Cyan;
        button.Font = new Font("Segoe UI Semibold", 10);
    }

    public static void StyleMenu(ContextMenuStrip menu)
    {
        var renderer = new NeonMenuRenderer();
        void StyleItems(ToolStripDropDown dropdown)
        {
            dropdown.BackColor = Panel;
            dropdown.ForeColor = Text;
            dropdown.Renderer = renderer;
            foreach (ToolStripItem item in dropdown.Items)
            {
                item.BackColor = Panel;
                item.ForeColor = Text;
                if (item is ToolStripDropDownItem child) StyleItems(child.DropDown);
            }
        }
        StyleItems(menu);
    }

    private static void RoundButton(Button button)
    {
        if (button.Width < 12 || button.Height < 12) return;
        using var path = new GraphicsPath();
        var rect = new Rectangle(0, 0, button.Width, button.Height);
        var radius = 8;
        path.AddArc(rect.Left, rect.Top, radius * 2, radius * 2, 180, 90);
        path.AddArc(rect.Right - radius * 2, rect.Top, radius * 2, radius * 2, 270, 90);
        path.AddArc(rect.Right - radius * 2, rect.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
        path.AddArc(rect.Left, rect.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
        path.CloseFigure();
        button.Region?.Dispose();
        button.Region = new Region(path);
    }

    public static Icon MakeIcon()
    {
        using var stream = typeof(Theme).Assembly.GetManifestResourceStream(
            "Firaw.WorkAssistant.assets.huginn-muninn.ico")
            ?? throw new InvalidOperationException("O ícone Huginn e Muninn não foi encontrado.");
        using var icon = new Icon(stream);
        return (Icon)icon.Clone();
    }
}

internal sealed class NeonMenuRenderer : ToolStripProfessionalRenderer
{
    public NeonMenuRenderer() : base(new NeonMenuColors()) { }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = Theme.Cyan;
        base.OnRenderArrow(e);
    }
}

internal sealed class NeonMenuColors : ProfessionalColorTable
{
    public override Color ToolStripDropDownBackground => Theme.Panel;
    public override Color ImageMarginGradientBegin => Theme.Panel;
    public override Color ImageMarginGradientMiddle => Theme.Panel;
    public override Color ImageMarginGradientEnd => Theme.Panel;
    public override Color MenuBorder => Theme.CyanDark;
    public override Color MenuItemBorder => Theme.CyanDark;
    public override Color MenuItemSelected => Theme.FieldHover;
    public override Color MenuItemSelectedGradientBegin => Theme.FieldHover;
    public override Color MenuItemSelectedGradientEnd => Theme.FieldHover;
    public override Color MenuItemPressedGradientBegin => Theme.FieldHover;
    public override Color MenuItemPressedGradientMiddle => Theme.FieldHover;
    public override Color MenuItemPressedGradientEnd => Theme.FieldHover;
    public override Color CheckBackground => Theme.CyanDark;
    public override Color CheckSelectedBackground => Theme.CyanDark;
    public override Color CheckPressedBackground => Theme.CyanDark;
}
