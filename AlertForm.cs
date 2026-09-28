namespace Firaw.WorkAssistant;

internal sealed class AlertForm : Form
{
    private bool _acted;
    public event Action<string>? ActionChosen;

    public AlertForm(WorkItem item, int stackIndex = 0)
    {
        Text = "Firaw • Aviso";
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Size = new Size(400, 235);
        BackColor = Theme.Panel;
        ForeColor = Theme.Text;
        var screen = Screen.FromPoint(Cursor.Position).WorkingArea;
        Location = new Point(screen.Right - Width - 20, screen.Bottom - Height - 20 - (stackIndex * (Height + 12)));
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), RowCount = 4 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.Controls.Add(new Label { Text = "HORA DO LEMBRETE", Dock = DockStyle.Fill, ForeColor = Theme.Cyan, Font = new Font("Segoe UI Semibold", 10) }, 0, 0);
        root.Controls.Add(new Label { Text = item.Title, Dock = DockStyle.Fill, Font = new Font("Segoe UI Semibold", 16), AutoEllipsis = true }, 0, 1);
        root.Controls.Add(new Label { Text = $"{(string.IsNullOrWhiteSpace(item.Project) ? "Geral" : item.Project)}  •  {item.EffectiveDueAt:dd/MM/yyyy HH:mm}\n{item.Notes}", Dock = DockStyle.Fill, ForeColor = Theme.Muted, AutoEllipsis = true }, 0, 2);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        actions.Controls.Add(ActionButton("Concluir", "complete", true));
        actions.Controls.Add(ActionButton("Adiar 10 min", "snooze"));
        actions.Controls.Add(ActionButton("Abrir", "open"));
        root.Controls.Add(actions, 0, 3);
        Controls.Add(root);
        Theme.Style(this);
        foreach (Control control in actions.Controls) if (control is Button button && button.Tag?.ToString() == "complete") Theme.Primary(button);
        FormClosing += (_, _) => { if (!_acted) ActionChosen?.Invoke("close"); };
    }

    private Button ActionButton(string title, string action, bool primary = false)
    {
        var button = new Button { Text = title, Tag = action, AutoSize = true, Height = 32, FlatStyle = FlatStyle.Flat, BackColor = primary ? Theme.Cyan : Theme.Field, ForeColor = primary ? Theme.Background : Theme.Text };
        button.Click += (_, _) => { _acted = true; ActionChosen?.Invoke(action); Close(); };
        return button;
    }
}
