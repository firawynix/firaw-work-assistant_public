namespace Firaw.WorkAssistant;

internal sealed class MotionScheduleDialog : Form
{
    private readonly TextBox _interval = new() { Dock = DockStyle.Fill, MaxLength = 3, TextAlign = HorizontalAlignment.Center };
    private readonly TextBox _minimum = new() { Dock = DockStyle.Fill, MaxLength = 2, TextAlign = HorizontalAlignment.Center };
    private readonly TextBox _maximum = new() { Dock = DockStyle.Fill, MaxLength = 2, TextAlign = HorizontalAlignment.Center };

    public int IntervalMinutes { get; private set; }
    public int MinimumSeconds { get; private set; }
    public int MaximumSeconds { get; private set; }

    public MotionScheduleDialog(WorkSettings settings)
    {
        Text = "Firaw • Movimento do cursor";
        Icon = Theme.MakeIcon();
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        Font = new Font("Segoe UI", 10);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = false;
        MaximizeBox = false;
        MinimizeBox = false;
        TopMost = true;
        ClientSize = new Size(452, 362);
        _interval.Text = settings.AssistantAutoMotionIntervalMinutes.ToString();
        _minimum.Text = settings.AssistantAutoMotionMinSeconds.ToString();
        _maximum.Text = settings.AssistantAutoMotionMaxSeconds.ToString();

        var card = new SurfaceCard
        {
            Dock = DockStyle.Fill, Margin = new Padding(14), Padding = new Padding(18, 14, 18, 15)
        };
        Controls.Add(card);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 5, BackColor = Theme.Panel };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 37));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 57));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 65));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 65));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        card.Controls.Add(layout);
        var heading = new Label
        {
            Text = "MOVIMENTO AUTOMÁTICO", Dock = DockStyle.Fill,
            ForeColor = Theme.Cyan, Font = new Font("Segoe UI Semibold", 11),
            TextAlign = ContentAlignment.MiddleLeft
        };
        layout.Controls.Add(heading, 0, 0);
        layout.SetColumnSpan(heading, 2);
        var help = new Label
        {
            Text = "Salvar só programa. Ctrl esquerdo + clique ativa ou desativa. Para testar no Teams, use 2–3 min; tela bloqueada ou em suspensão não conta.",
            Dock = DockStyle.Fill, ForeColor = Theme.Muted,
            Font = new Font("Segoe UI", 8.8f), TextAlign = ContentAlignment.MiddleLeft
        };
        layout.Controls.Add(help, 0, 1);
        layout.SetColumnSpan(help, 2);
        var intervalField = Field("A CADA QUANTOS MINUTOS", _interval);
        layout.Controls.Add(intervalField, 0, 2);
        layout.SetColumnSpan(intervalField, 2);
        layout.Controls.Add(Field("DURAÇÃO MÍNIMA · SEGUNDOS", _minimum), 0, 3);
        layout.Controls.Add(Field("DURAÇÃO MÁXIMA · SEGUNDOS", _maximum), 1, 3);
        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false, BackColor = Theme.Panel,
            Padding = new Padding(0, 6, 0, 0)
        };
        var save = new NeonButton { Text = "Salvar programação", Width = 167, Height = 36, PrimaryStyle = true };
        save.Click += (_, _) => SaveSchedule();
        var cancel = new NeonButton { Text = "Cancelar", Width = 92, Height = 36 };
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        actions.Controls.Add(save);
        actions.Controls.Add(cancel);
        layout.Controls.Add(actions, 0, 4);
        layout.SetColumnSpan(actions, 2);
        AcceptButton = save;
        CancelButton = cancel;
        Theme.Style(this);
    }

    private static Control Field(string label, TextBox input)
    {
        var host = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, RowCount = 2, Margin = new Padding(3, 2, 3, 4),
            BackColor = Theme.Panel
        };
        host.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        host.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        host.Controls.Add(new Label
        {
            Text = label, Dock = DockStyle.Fill, ForeColor = Theme.Muted,
            Font = new Font("Segoe UI Semibold", 8), TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        var shell = new FieldShell { Dock = DockStyle.Fill, Padding = new Padding(8, 4, 8, 4) };
        shell.Controls.Add(input);
        host.Controls.Add(shell, 0, 1);
        return host;
    }

    private void SaveSchedule()
    {
        if (!int.TryParse(_interval.Text, out var interval) || interval is < 0 or > 240 ||
            !int.TryParse(_minimum.Text, out var minimum) || minimum is < 1 or > 60 ||
            !int.TryParse(_maximum.Text, out var maximum) || maximum < minimum || maximum > 60)
        {
            MessageBox.Show(this, "Informe 0 a 240 minutos e uma duração de 1 a 60 segundos, com o máximo igual ou maior que o mínimo.",
                "Confira os valores", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        IntervalMinutes = interval;
        MinimumSeconds = minimum;
        MaximumSeconds = maximum;
        DialogResult = DialogResult.OK;
        Close();
    }
}
