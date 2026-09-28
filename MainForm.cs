using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Firaw.WorkAssistant;

internal sealed class MainForm : Form
{
    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValue = "FirawWorkAssistant";
    private readonly WorkStore _store;
    private readonly HashSet<Guid> _openAlerts = [];
    private readonly ListBox _items = new() { Dock = DockStyle.Fill, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 92, IntegralHeight = false, BorderStyle = BorderStyle.None };
    private Panel? _listHost;
    private SurfaceCard? _rail;
    private TableLayoutPanel? _body;
    private readonly ComboBox _filter = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
    private readonly List<NeonButton> _filterButtons = [];
    private readonly TextBox _title = new() { Dock = DockStyle.Fill, PlaceholderText = "O que precisa ser feito?" };
    private readonly NeonComboField _project = new(true) { Dock = DockStyle.Fill };
    private readonly DateTimeField _start = new() { Dock = DockStyle.Fill };
    private readonly DateTimeField _due = new() { Dock = DockStyle.Fill };
    private readonly DateTimeField _adjustedDue = new() { Dock = DockStyle.Fill };
    private readonly NeonComboField _adjustment = new() { Dock = DockStyle.Fill };
    private readonly Label _adjustmentHint = new() { Dock = DockStyle.Fill, ForeColor = Theme.Muted,
        Font = new Font("Segoe UI", 8.5f), TextAlign = ContentAlignment.MiddleLeft };
    private readonly NeonComboField _repeat = new() { Dock = DockStyle.Fill };
    private readonly GlowCheckBox _stickerToggle = new() { Text = "Fixar sticker na tela" };
    private readonly GlowCheckBox _assistantToggle = new() { Text = "Assistente na tela" };
    private readonly ProgressView _progress = new() { Dock = DockStyle.Fill };
    private readonly TextBox _notes = new() { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.None };
    private readonly CheckedListBox _checklist = new() { Dock = DockStyle.Fill, CheckOnClick = true, BorderStyle = BorderStyle.None };
    private readonly TextBox _newCheck = new() { Dock = DockStyle.Fill, PlaceholderText = "Novo passo da checklist" };
    private readonly TextBox _noteTitle = new() { Dock = DockStyle.Fill, PlaceholderText = "Título da anotação" };
    private readonly TextBox _noteText = new() { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.None, PlaceholderText = "Escreva aqui. O texto ficará no sticker." };
    private readonly Label _noteHelp = new() { Dock = DockStyle.Fill, ForeColor = Theme.Muted, Font = new Font("Segoe UI", 9), TextAlign = ContentAlignment.MiddleLeft };
    private readonly GlowCheckBox _noteVisible = new() { Text = "Manter anotação na tela" };
    private readonly NeonComboField _noteColor = new() { Dock = DockStyle.Fill };
    private readonly NeonComboField _noteOpacity = new() { Dock = DockStyle.Fill };
    private readonly List<NeonButton> _noteActionButtons = [];
    private Panel _taskPanel = null!;
    private Panel _notePanel = null!;
    private readonly Label _detailHeadline = new() { Dock = DockStyle.Fill, AutoEllipsis = true, Font = new Font("Segoe UI Semibold", 19), ForeColor = Theme.Text };
    private readonly Label _detailBadge = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI Semibold", 8.5f), ForeColor = Theme.Cyan, Margin = Padding.Empty };
    private readonly Label _status = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
    private readonly GlowCheckBox _startup = new() { Text = "Iniciar com o Windows" };
    private readonly NotifyIcon _tray;
    private readonly ToolStripMenuItem _trayMascotSizeMenu = new("Tamanho do bonequinho");
    private readonly ToolStripMenuItem _trayProjectSizeMenu = new("Tamanho dos projetos");
    private readonly ToolStripMenuItem _trayProjectsMenu = new("Projetos visíveis");
    private string _trayProjectsSignature = "";
    private readonly ToolStripMenuItem _trayMotionMenu = new("Movimento imediato ao ativar");
    private readonly ToolStripMenuItem _trayMotionModeMenu = new("Ativar movimento automático");
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 15_000 };
    private readonly Dictionary<Guid, StickerForm> _stickers = [];
    private readonly Dictionary<Guid, NoteStickerForm> _noteStickers = [];
    private AssistantForm? _assistant;
    private NeonButton? _folderButton;
    private WorkItem? _selected;
    private StickyNote? _selectedNote;
    private bool _loading;
    private bool _exitRequested;
    private bool _settingStartup;

    public MainForm()
    {
        _store = new WorkStore(Environment.GetEnvironmentVariable("FIRAW_ASSISTANT_DATA_PATH"));
        Text = "Firaw • Assistente de trabalho";
        Icon = Theme.MakeIcon();
        FormBorderStyle = FormBorderStyle.None;
        MinimumSize = new Size(1120, 760);
        Size = new Size(1400, 920);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        BuildLayout();
        Theme.Style(this);
        _filter.Items.AddRange(["Abertos", "Concluídos", "Anotações", "Todos"]);
        _filter.SelectedIndex = 0;
        _repeat.Items.AddRange(["Nenhuma", "Diariamente", "Semanalmente"]);
        _repeat.SelectedIndex = 0;
        _adjustment.Items.AddRange(["Nenhum", "Postergado", "Reduzido"]);
        _adjustment.SelectedIndex = 0;
        _adjustment.SelectedIndexChanged += (_, _) => UpdateAdjustmentEditor();
        _noteColor.Items.AddRange(["Ciano", "Verde", "Violeta"]);
        _noteOpacity.Items.AddRange(["92%", "72%", "56%"]);
        Theme.Primary((Button)Controls.Find("newButton", true)[0]);
        Theme.Primary((Button)Controls.Find("saveButton", true)[0]);
        Theme.Primary((Button)Controls.Find("saveNoteButton", true)[0]);
        _noteText.Font = new Font("Segoe UI", 13);
        _items.DrawItem += DrawItem;
        _items.SelectedIndexChanged += (_, _) => ChangeSelection();
        _title.TextChanged += (_, _) => { if (!_loading && _selected is not null) _detailHeadline.Text = _title.Text; };
        _filter.SelectedIndexChanged += (_, _) => { StyleFilterButtons(); RefreshList(); };
        StyleFilterButtons();
        _checklist.ItemCheck += ChecklistChecked;
        _newCheck.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; AddChecklistItem(); } };
        _stickerToggle.CheckedChanged += (_, _) =>
        {
            if (_loading || _selected is null) return;
            if (!PersistEditor())
            {
                _loading = true;
                _stickerToggle.Checked = _selected.ShowSticker;
                _loading = false;
                return;
            }
            _selected.ShowSticker = _stickerToggle.Checked;
            _store.Save();
            SyncStickers();
        };
        _noteVisible.CheckedChanged += (_, _) =>
        {
            if (_loading || _selectedNote is null) return;
            PersistEditor();
            _selectedNote.Visible = _noteVisible.Checked;
            _store.Save();
            SyncNoteStickers();
        };
        _startup.Checked = IsStartupEnabled();
        _startup.CheckedChanged += (_, _) => { if (!_settingStartup) SetStartup(_startup.Checked); };

        _tray = new NotifyIcon
        {
            Icon = Icon,
            Text = "Firaw • rodinha: mostrar/ocultar bonequinho",
            Visible = true,
            ContextMenuStrip = new ContextMenuStrip()
        };
        _tray.ContextMenuStrip.Items.Add("Abrir assistente", null, (_, _) => BringBack());
        _tray.ContextMenuStrip.Items.Add("Mostrar bonequinho", null, (_, _) => ShowAssistantFromTray());
        for (var index = 0; index < AssistantForm.MascotSizeLabels.Length; index++)
        {
            var size = index;
            _trayMascotSizeMenu.DropDownItems.Add(AssistantForm.MascotSizeLabels[index], null,
                (_, _) => SetMascotSize(size));
        }
        _tray.ContextMenuStrip.Items.Add(_trayMascotSizeMenu);
        for (var index = 0; index < AssistantForm.ProjectSizeLabels.Length; index++)
        {
            var size = index;
            _trayProjectSizeMenu.DropDownItems.Add(AssistantForm.ProjectSizeLabels[index], null,
                (_, _) => SetProjectSize(size));
        }
        _tray.ContextMenuStrip.Items.Add(_trayProjectSizeMenu);
        _tray.ContextMenuStrip.Items.Add(_trayProjectsMenu);
        foreach (var (seconds, label) in AssistantForm.MotionOptions)
            _trayMotionMenu.DropDownItems.Add(label, null, (_, _) => SetMotionDuration(seconds));
        _tray.ContextMenuStrip.Items.Add(_trayMotionMenu);
        _trayMotionModeMenu.Click += (_, _) => ToggleMotionMode();
        _tray.ContextMenuStrip.Items.Add(_trayMotionModeMenu);
        _tray.ContextMenuStrip.Items.Add("Programar movimento automático…", null,
            (_, _) => OpenMotionSchedule());
        _tray.ContextMenuStrip.Items.Add("Novo lembrete", null, (_, _) => { BringBack(); AddNew(); });
        _tray.ContextMenuStrip.Items.Add("Nova anotação", null, (_, _) => { BringBack(); AddNote(); });
        _tray.ContextMenuStrip.Items.Add("Sair", null, (_, _) => ExitApp());
        Theme.StyleMenu(_tray.ContextMenuStrip);
        _tray.DoubleClick += (_, _) => BringBack();
        _tray.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Middle) ToggleAssistantVisibility();
        };
        try { _store.Load(); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Dados recuperados", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        RefreshTrayMascotSize();
        RefreshTrayProjectSize();
        RefreshTrayMotionDuration();
        RefreshTrayMotionMode();
        _assistantToggle.Checked = _store.Settings.AssistantEnabled;
        _assistantToggle.CheckedChanged += (_, _) =>
        {
            _store.Settings.AssistantEnabled = _assistantToggle.Checked;
            if (_assistantToggle.Checked) _store.Settings.AssistantHidden = false;
            _store.SaveSettings();
            UpdateAssistant();
        };
        LoadProjectNames();
        RefreshList();
        _timer.Tick += (_, _) => CheckDue();
        _timer.Start();
        Shown += (_, _) => { UpdateRailHeight(); UpdateListHeight(); CheckDue(); };
        FormClosing += OnClosing;
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(24, 0, 24, 14), BackColor = Theme.Background };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 140));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        Controls.Add(root);
        root.Controls.Add(BuildWindowBar(), 0, 0);

        var hero = new SurfaceCard { Dock = DockStyle.Fill, Highlight = true, Margin = new Padding(0, 0, 0, 17), Padding = new Padding(25, 16, 22, 16) };
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, BackColor = Theme.Panel };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 185));
        var heroText = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, BackColor = Theme.Panel };
        heroText.RowStyles.Add(new RowStyle(SizeType.Absolute, 19));
        heroText.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        heroText.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        heroText.Controls.Add(new Label { Text = "FIRAW  /  WORKSPACE  •  01", Dock = DockStyle.Fill, Font = new Font("Segoe UI Semibold", 9), ForeColor = Theme.Cyan, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        heroText.Controls.Add(new Label { Text = "Tudo em foco, sem perder o prazo.", Dock = DockStyle.Fill, Font = new Font("Segoe UI Semibold", 20), ForeColor = Theme.Text, TextAlign = ContentAlignment.MiddleLeft }, 0, 1);
        heroText.Controls.Add(new Label { Text = "Tarefas, anotações e seu assistente diretamente na área de trabalho.", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 10), ForeColor = Theme.Muted, TextAlign = ContentAlignment.MiddleLeft }, 0, 2);
        header.Controls.Add(heroText, 0, 0);
        var newButton = Button("+ Nova tarefa", (_, _) => AddNew());
        newButton.Name = "newButton";
        newButton.Dock = DockStyle.None;
        newButton.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        newButton.Height = 44;
        newButton.Margin = new Padding(8, 0, 4, 0);
        header.Controls.Add(newButton, 1, 0);
        var newNoteButton = Button("+ Nova anotação", (_, _) => AddNote());
        newNoteButton.Name = "newNoteButton";
        newNoteButton.Dock = DockStyle.None;
        newNoteButton.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        newNoteButton.Height = 44;
        newNoteButton.Margin = new Padding(4, 0, 8, 0);
        header.Controls.Add(newNoteButton, 2, 0);
        hero.Controls.Add(header);
        root.Controls.Add(hero, 0, 1);

        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = Theme.Background };
        _body = body;
        body.Resize += (_, _) => UpdateRailHeight();
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 354));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.Controls.Add(body, 0, 2);

        var rail = new SurfaceCard { Dock = DockStyle.Top, Height = 280,
            Margin = new Padding(0, 0, 16, 0), Padding = new Padding(16) };
        _rail = rail;
        var left = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, BackColor = Theme.Panel };
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 65));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 47));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        var railHeader = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, BackColor = Theme.Panel };
        railHeader.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        railHeader.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        railHeader.Controls.Add(new Label { Text = "SEU PAINEL", Dock = DockStyle.Fill, ForeColor = Theme.Cyan, Font = new Font("Segoe UI Semibold", 10), TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        railHeader.Controls.Add(new Label { Text = "Organize o que merece atenção", Dock = DockStyle.Fill, ForeColor = Theme.Muted, Font = new Font("Segoe UI", 9.5f), TextAlign = ContentAlignment.MiddleLeft }, 0, 1);
        left.Controls.Add(railHeader, 0, 0);
        var filters = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, BackColor = Theme.Field };
        for (var index = 0; index < 4; index++) filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        filters.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var names = new[] { "Abertas", "Feitas", "Notas", "Todas" };
        for (var index = 0; index < names.Length; index++)
        {
            var selectedIndex = index;
            var filterButton = (NeonButton)Button(names[index], (_, _) => _filter.SelectedIndex = selectedIndex);
            filterButton.Dock = DockStyle.Fill;
            filterButton.SegmentStyle = true;
            filterButton.Font = new Font("Segoe UI Semibold", 9);
            filterButton.Margin = new Padding(1);
            _filterButtons.Add(filterButton);
            filters.Controls.Add(filterButton, index, 0);
        }
        var filterFrame = new FieldShell { Dock = DockStyle.Fill, Padding = new Padding(3),
            Margin = new Padding(0, 0, 0, 5) };
        filterFrame.Controls.Add(filters);
        left.Controls.Add(filterFrame, 0, 1);
        _items.BackColor = Theme.Panel;
        _items.Dock = DockStyle.Top;
        _listHost = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Panel };
        _listHost.Controls.Add(_items);
        ScrollChrome.Attach(_listHost, _items);
        _listHost.Resize += (_, _) => UpdateListHeight();
        _listHost.Layout += (_, _) => UpdateListHeight();
        left.Controls.Add(_listHost, 0, 2);
        var railFooter = new Label { Text = "DADOS LOCAIS  /  VOCÊ NO CONTROLE", Dock = DockStyle.Fill, ForeColor = Theme.Muted, Font = new Font("Segoe UI Semibold", 8), TextAlign = ContentAlignment.MiddleLeft };
        left.Controls.Add(railFooter, 0, 3);
        rail.Controls.Add(left);
        body.Controls.Add(rail, 0, 0);

        _taskPanel = BuildTaskPanel();
        _notePanel = BuildNotePanel();
        var rightHost = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background };
        rightHost.Controls.Add(_taskPanel);
        rightHost.Controls.Add(_notePanel);
        body.Controls.Add(rightHost, 1, 0);

        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 188));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 196));
        footer.Controls.Add(_status, 0, 0);
        footer.Controls.Add(_assistantToggle, 1, 0);
        footer.Controls.Add(_startup, 2, 0);
        var footerFrame = new SurfaceCard { Dock = DockStyle.Fill, Padding = new Padding(12, 4, 12, 4) };
        footerFrame.Controls.Add(footer);
        root.Controls.Add(footerFrame, 0, 3);
    }

    private Panel BuildTaskPanel()
    {
        var host = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, BackColor = Theme.Background };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 63));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 72));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 28));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 59));
        host.Controls.Add(layout);
        var titles = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, BackColor = Theme.Background };
        titles.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        titles.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        titles.Controls.Add(new Label { Text = "TAREFA  /  PLANEJAMENTO", Dock = DockStyle.Fill, ForeColor = Theme.Cyan, Font = new Font("Segoe UI Semibold", 9), TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        titles.Controls.Add(_detailHeadline, 0, 1);
        layout.Controls.Add(titles, 0, 0);

        var cards = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = Theme.Background, Margin = new Padding(0, 0, 0, 13) };
        cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 53));
        cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 47));
        cards.Controls.Add(BuildPlanCard(), 0, 0);
        cards.Controls.Add(BuildChecklistCard(), 1, 0);
        layout.Controls.Add(cards, 0, 1);

        var noteCard = new SurfaceCard { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 12), Padding = new Padding(18, 13, 18, 15) };
        var noteLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, BackColor = Theme.Panel };
        noteLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 31));
        noteLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        noteLayout.Controls.Add(SectionHeading("03  /  NOTAS DA TAREFA"), 0, 0);
        noteLayout.Controls.Add(WrapField(_notes, 10), 0, 1);
        noteCard.Controls.Add(noteLayout);
        layout.Controls.Add(noteCard, 0, 2);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, BackColor = Theme.Panel };
        var save = Button("Salvar alterações", (_, _) => SaveCurrent());
        save.Name = "saveButton";
        save.Width = 160;
        actions.Controls.Add(save);
        var done = Button("✓ Concluir", (_, _) => CompleteCurrent());
        done.Width = 120;
        actions.Controls.Add(done);
        var snooze = Button("Adiar 10 min", (_, _) => SnoozeCurrent());
        snooze.Width = 125;
        actions.Controls.Add(snooze);
        var delete = (NeonButton)Button("Excluir", (_, _) => DeleteCurrent());
        delete.DangerStyle = true;
        delete.Width = 90;
        actions.Controls.Add(delete);
        layout.Controls.Add(ActionBar("AÇÕES DA TAREFA", actions), 0, 3);
        return host;
    }

    private SurfaceCard BuildPlanCard()
    {
        var card = new SurfaceCard { Dock = DockStyle.Fill, Highlight = true, Margin = new Padding(0, 0, 8, 0), Padding = new Padding(17, 11, 17, 15) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 6, BackColor = Theme.Panel };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 37));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 57));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 61));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 61));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 61));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(SectionHeading("01  /  PLANEJAMENTO"), 0, 0);
        layout.Controls.Add(FieldBlock("O QUE VAMOS FAZER", _title), 0, 1);
        var row2 = Pair(FieldBlock("PROJETO / AMBIENTE", _project), FieldBlock("REPETIÇÃO", _repeat));
        layout.Controls.Add(row2, 0, 2);
        var row3 = Pair(FieldBlock("DATA INICIAL", _start), FieldBlock("PRAZO ORIGINAL", _due));
        layout.Controls.Add(row3, 0, 3);
        layout.Controls.Add(Pair(FieldBlock("ALTERAÇÃO DO PRAZO", _adjustment),
            FieldBlock("NOVA DATA FINAL", _adjustedDue)), 0, 4);
        var bottom = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, BackColor = Theme.Panel };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 158));
        bottom.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        bottom.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        bottom.Controls.Add(_adjustmentHint, 0, 0);
        bottom.SetColumnSpan(_adjustmentHint, 2);
        bottom.Controls.Add(_stickerToggle, 0, 1);
        _folderButton = (NeonButton)Button("Vincular pasta", (_, _) => OpenProjectFolder());
        _folderButton.Dock = DockStyle.Fill;
        var folderMenu = new ContextMenuStrip();
        folderMenu.Items.Add("Escolher outra pasta", null, (_, _) => ChooseProjectFolder());
        folderMenu.Items.Add("Desvincular pasta", null, (_, _) => UnlinkProjectFolder());
        Theme.StyleMenu(folderMenu);
        _folderButton.ContextMenuStrip = folderMenu;
        bottom.Controls.Add(_folderButton, 1, 1);
        layout.Controls.Add(bottom, 0, 5);
        card.Controls.Add(layout);
        return card;
    }

    private SurfaceCard BuildChecklistCard()
    {
        var card = new SurfaceCard { Dock = DockStyle.Fill, Margin = new Padding(8, 0, 0, 0), Padding = new Padding(17, 11, 17, 15) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, BackColor = Theme.Panel };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 37));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 51));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        var heading = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Panel, Margin = Padding.Empty };
        var headingText = SectionHeading("02  /  ETAPAS E PROGRESSO");
        headingText.Padding = new Padding(0, 0, 156, 0);
        heading.Controls.Add(headingText);
        var badge = new FieldShell { Padding = new Padding(5, 2, 5, 2), Margin = Padding.Empty };
        badge.Controls.Add(_detailBadge);
        heading.Controls.Add(badge);
        badge.BringToFront();
        void PositionBadge() => badge.Bounds = new Rectangle(Math.Max(0, heading.ClientSize.Width - 156), 4, 156, 28);
        heading.Resize += (_, _) => PositionBadge();
        PositionBadge();
        layout.Controls.Add(heading, 0, 0);
        layout.Controls.Add(_progress, 0, 1);
        layout.Controls.Add(WrapField(_checklist, 7), 0, 2);
        var add = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, BackColor = Theme.Panel, Margin = new Padding(0, 6, 0, 0) };
        add.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        add.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 98));
        add.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86));
        add.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        add.Controls.Add(WrapField(_newCheck, 5), 0, 0);
        var plus = Button("+ Etapa", (_, _) => AddChecklistItem());
        plus.Dock = DockStyle.None;
        plus.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        plus.Height = 35;
        add.Controls.Add(plus, 1, 0);
        var remove = (NeonButton)Button("Remover", (_, _) => RemoveChecklistItem());
        remove.Dock = DockStyle.None;
        remove.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        remove.Height = 35;
        remove.SubtleStyle = true;
        add.Controls.Add(remove, 2, 0);
        layout.Controls.Add(add, 0, 3);
        card.Controls.Add(layout);
        return card;
    }

    private static TableLayoutPanel Pair(Control first, Control second)
    {
        var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2,
            RowCount = 1, BackColor = Theme.Panel };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        row.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        first.Margin = new Padding(0, 0, 6, 0);
        second.Margin = new Padding(6, 0, 0, 0);
        row.Controls.Add(first, 0, 0);
        row.Controls.Add(second, 1, 0);
        return row;
    }

    private static Control FieldBlock(string caption, Control control)
    {
        var block = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, BackColor = Theme.Panel };
        block.RowStyles.Add(new RowStyle(SizeType.Absolute, 21));
        block.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        block.Controls.Add(new Label { Text = caption, Dock = DockStyle.Fill, Margin = Padding.Empty, ForeColor = Theme.Muted, Font = new Font("Segoe UI Semibold", 8), TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        block.Controls.Add(WrapField(control, 7), 0, 1);
        return block;
    }

    private static FieldShell WrapField(Control control, int pad)
    {
        var shell = new FieldShell { Dock = DockStyle.Fill, Margin = Padding.Empty, Padding = new Padding(pad, 5, pad, 5) };
        if (control is TextBox text) text.BorderStyle = BorderStyle.None;
        control.Dock = DockStyle.Fill;
        shell.Controls.Add(control);
        if (control is ListBox or TextBox { Multiline: true }) ScrollChrome.Attach(shell, control);
        return shell;
    }

    private static Label SectionHeading(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        ForeColor = Theme.Cyan,
        Font = new Font("Segoe UI Semibold", 9),
        TextAlign = ContentAlignment.MiddleLeft
    };

    private void StyleFilterButtons()
    {
        for (var index = 0; index < _filterButtons.Count; index++)
        {
            _filterButtons[index].SelectedStyle = index == _filter.SelectedIndex;
            _filterButtons[index].Invalidate();
        }
    }

    private void UpdateAdjustmentEditor()
    {
        var kind = _adjustment.SelectedItem?.ToString() ?? "Nenhum";
        _adjustedDue.Enabled = kind != "Nenhum";
        _adjustmentHint.Text = kind switch
        {
            "Postergado" => "Prazo original preservado • nova data deve ser posterior.",
            "Reduzido" => "Prazo original preservado • nova data deve ser anterior.",
            _ => "Sem alteração • o prazo original será usado nos avisos."
        };
        if (_loading || kind == "Nenhum") return;
        var original = _due.Value;
        var start = _start.Value;
        _adjustedDue.Value = kind == "Postergado"
            ? original.AddHours(1)
            : original > start.AddMinutes(2)
                ? start.AddTicks((original - start).Ticks / 2)
                : original.AddMinutes(-1);
    }

    private Control BuildWindowBar()
    {
        var bar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, BackColor = Theme.Background, Margin = new Padding(-20, 0, -20, 0) };
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 43));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 43));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 43));
        var caption = new Label { Text = "FIRAW  /  ASSISTENTE DE TRABALHO", Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft, ForeColor = Theme.Cyan,
            Font = new Font("Segoe UI Semibold", 10), Padding = new Padding(61, 0, 0, 0) };
        var brandIcon = new PictureBox { Image = Icon!.ToBitmap(), SizeMode = PictureBoxSizeMode.Zoom,
            Bounds = new Rectangle(20, 7, 30, 30), BackColor = Theme.Background };
        brandIcon.Disposed += (_, _) => brandIcon.Image?.Dispose();
        caption.Controls.Add(brandIcon);
        caption.MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) DragWindow(); };
        caption.DoubleClick += (_, _) => ToggleMaximize();
        brandIcon.MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) DragWindow(); };
        brandIcon.DoubleClick += (_, _) => ToggleMaximize();
        bar.Controls.Add(caption, 0, 0);
        var minimize = WindowButton("−", (_, _) => WindowState = FormWindowState.Minimized);
        var maximize = WindowButton("□", (_, _) => ToggleMaximize());
        var close = WindowButton("×", (_, _) => Close());
        close.MouseEnter += (_, _) => close.BackColor = Color.FromArgb(169, 39, 58);
        close.MouseLeave += (_, _) => close.BackColor = Theme.Background;
        bar.Controls.Add(minimize, 1, 0);
        bar.Controls.Add(maximize, 2, 0);
        bar.Controls.Add(close, 3, 0);
        return bar;
    }

    private static Button WindowButton(string text, EventHandler action)
    {
        var button = new Button { Text = text, Tag = "window-control", Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 13), ForeColor = Theme.Muted, BackColor = Theme.Background, Margin = Padding.Empty, TabStop = false };
        button.FlatAppearance.BorderSize = 0;
        button.Click += action;
        return button;
    }

    private void DragWindow()
    {
        if (WindowState == FormWindowState.Maximized) return;
        ReleaseCapture();
        SendMessage(Handle, 0x00A1, new IntPtr(2), IntPtr.Zero);
    }

    private void ToggleMaximize() => WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;

    protected override void WndProc(ref Message message)
    {
        const int wmNcHitTest = 0x0084;
        if (message.Msg == wmNcHitTest && WindowState == FormWindowState.Normal)
        {
            var screen = new Point((short)((long)message.LParam & 0xffff), (short)(((long)message.LParam >> 16) & 0xffff));
            var client = PointToClient(screen);
            const int grip = 8;
            var left = client.X < grip;
            var right = client.X >= Width - grip;
            var top = client.Y < grip;
            var bottom = client.Y >= Height - grip;
            var hit = top && left ? 13 : top && right ? 14 : bottom && left ? 16 : bottom && right ? 17 : left ? 10 : right ? 11 : top ? 12 : bottom ? 15 : 0;
            if (hit != 0) { message.Result = new IntPtr(hit); return; }
        }
        base.WndProc(ref message);
    }

    private Panel BuildNotePanel()
    {
        var host = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, BackColor = Theme.Background };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 63));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 59));
        host.Controls.Add(layout);
        var top = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, BackColor = Theme.Background };
        top.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        top.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        top.Controls.Add(new Label { Text = "NOTA  /  EDITOR", Dock = DockStyle.Fill, ForeColor = Theme.Cyan, Font = new Font("Segoe UI Semibold", 9), TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        top.Controls.Add(new Label { Text = "Uma ideia sempre à vista.", Dock = DockStyle.Fill, ForeColor = Theme.Text, Font = new Font("Segoe UI Semibold", 19), TextAlign = ContentAlignment.MiddleLeft }, 0, 1);
        layout.Controls.Add(top, 0, 0);

        var cards = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = Theme.Background, Margin = new Padding(0, 0, 0, 13) };
        cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));
        var settings = new SurfaceCard { Dock = DockStyle.Fill, Highlight = true, Margin = new Padding(0, 0, 8, 0), Padding = new Padding(18, 13, 18, 18) };
        var settingsLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 6, BackColor = Theme.Panel };
        settingsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        settingsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
        settingsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
        settingsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
        settingsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        settingsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        settingsLayout.Controls.Add(SectionHeading("01  /  IDENTIDADE"), 0, 0);
        settingsLayout.Controls.Add(FieldBlock("TÍTULO DA NOTA", _noteTitle), 0, 1);
        settingsLayout.Controls.Add(FieldBlock("COR DO BRILHO", _noteColor), 0, 2);
        settingsLayout.Controls.Add(FieldBlock("TRANSPARÊNCIA", _noteOpacity), 0, 3);
        settingsLayout.Controls.Add(_noteVisible, 0, 4);
        settingsLayout.Controls.Add(new Label { Text = "Arraste o sticker pela barra superior para escolher onde ele fica na tela.", Dock = DockStyle.Fill, ForeColor = Theme.Muted, Font = new Font("Segoe UI", 9), TextAlign = ContentAlignment.TopLeft }, 0, 5);
        settings.Controls.Add(settingsLayout);
        cards.Controls.Add(settings, 0, 0);

        var editor = new SurfaceCard { Dock = DockStyle.Fill, Margin = new Padding(8, 0, 0, 0), Padding = new Padding(18, 13, 18, 18) };
        var editorLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, BackColor = Theme.Panel };
        editorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        editorLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        editorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 31));
        editorLayout.Controls.Add(SectionHeading("02  /  TEXTO LIVRE"), 0, 0);
        _noteText.Font = new Font("Segoe UI", 13);
        editorLayout.Controls.Add(WrapField(_noteText, 15), 0, 1);
        editorLayout.Controls.Add(_noteHelp, 0, 2);
        editor.Controls.Add(editorLayout);
        cards.Controls.Add(editor, 1, 0);
        layout.Controls.Add(cards, 0, 1);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, BackColor = Theme.Panel };
        var save = Button("Salvar anotação", (_, _) => SaveCurrent());
        save.Name = "saveNoteButton";
        save.Width = 162;
        actions.Controls.Add(save);
        var show = Button("Mostrar na tela", (_, _) => { if (_selectedNote is not null) _noteVisible.Checked = true; });
        show.Width = 154;
        actions.Controls.Add(show);
        var delete = (NeonButton)Button("Excluir", (_, _) => DeleteCurrent());
        delete.DangerStyle = true;
        delete.Width = 90;
        actions.Controls.Add(delete);
        _noteActionButtons.AddRange([(NeonButton)save, (NeonButton)show, delete]);
        layout.Controls.Add(ActionBar("AÇÕES DA ANOTAÇÃO", actions), 0, 2);
        return host;
    }

    private static SurfaceCard ActionBar(string caption, FlowLayoutPanel actions)
    {
        var card = new SurfaceCard { Dock = DockStyle.Fill, Padding = new Padding(14, 6, 8, 6) };
        actions.Padding = new Padding(0, 3, 0, 0);
        foreach (Control button in actions.Controls)
        {
            button.MinimumSize = Size.Empty;
            button.Height = 30;
            button.Margin = new Padding(4, 0, 4, 0);
        }
        var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Theme.Panel };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        row.Controls.Add(new Label { Text = caption, Dock = DockStyle.Fill, ForeColor = Theme.Muted,
            Font = new Font("Segoe UI Semibold", 8), TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        row.Controls.Add(actions, 1, 0);
        card.Controls.Add(row);
        return card;
    }

    private static Button Button(string label, EventHandler click)
    {
        var button = new NeonButton { Text = label, AutoSize = false, Height = 38, MinimumSize = new Size(0, 32), Margin = new Padding(3) };
        button.Click += click;
        return button;
    }

    private void LoadProjectNames()
    {
        foreach (var name in _store.Items.Select(item => item.Project.Trim())
            .Where(name => name.Length > 0).Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase))
            _project.Items.Add(name);
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        try
        {
            foreach (var folder in Directory.EnumerateDirectories(home)
                .Where(path => Directory.Exists(Path.Combine(path, ".git")))
                .OrderBy(Path.GetFileName))
            {
                var name = Path.GetFileName(folder);
                if (!_project.Items.Cast<string>().Contains(name, StringComparer.CurrentCultureIgnoreCase))
                    _project.Items.Add(name);
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private void RefreshList(Guid? selectId = null)
    {
        var current = selectId ?? _selected?.Id ?? _selectedNote?.Id;
        _loading = true;
        _items.Items.Clear();
        var visibleTasks = _store.Items.Where(item => _filter.SelectedIndex switch
        {
            0 => !item.Done,
            1 => item.Done,
            2 => false,
            _ => true
        }).OrderBy(item => item.Done).ThenBy(item => item.EffectiveDueAt);
        foreach (var item in visibleTasks) _items.Items.Add(item);
        if (_filter.SelectedIndex is 2 or 3)
            foreach (var note in _store.Notes.OrderBy(note => note.Title)) _items.Items.Add(note);
        UpdateRailHeight();
        UpdateListHeight();
        var match = _items.Items.Cast<object>().FirstOrDefault(item => item switch
        {
            WorkItem work => work.Id == current,
            StickyNote note => note.Id == current,
            _ => false
        });
        if (match is not null) _items.SelectedItem = match;
        else if (_items.Items.Count > 0) _items.SelectedIndex = 0;
        _loading = false;
        ShowSelection(_items.SelectedItem);
        SyncStickers();
        SyncNoteStickers();
        UpdateAssistant();
        _status.Text = $"{_store.Items.Count(item => !item.Done)} tarefas abertas  •  {_store.Notes.Count} anotações  •  Salvo neste computador";
    }

    private void UpdateListHeight()
    {
        if (_listHost is null || _listHost.IsDisposed) return;
        var hasItems = _items.Items.Count > 0;
        if (hasItems)
            _items.Height = Math.Min(_listHost.ClientSize.Height,
                Math.Max(_items.ItemHeight, _items.Items.Count * _items.ItemHeight));
        _items.Visible = hasItems;
    }

    private void UpdateRailHeight()
    {
        if (_rail is null || _body is null || _rail.IsDisposed) return;
        var available = _body.ClientSize.Height;
        if (available < 1) return;
        var desired = 38 + 65 + 47 + 38 + _items.Items.Count * _items.ItemHeight;
        _rail.Height = Math.Min(available, Math.Max(240, desired));
    }

    private void DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        var entry = _items.Items[e.Index];
        var item = entry as WorkItem;
        var note = entry as StickyNote;
        var selected = (e.State & DrawItemState.Selected) != 0;
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var clear = new SolidBrush(Theme.Panel);
        e.Graphics.FillRectangle(clear, e.Bounds);
        var cardRect = new Rectangle(e.Bounds.X + 2, e.Bounds.Y + 4, e.Bounds.Width - 8, e.Bounds.Height - 10);
        using var card = SurfaceCard.Rounded(cardRect, 10);
        using var bg = new SolidBrush(selected ? Color.FromArgb(18, 49, 63) : Color.FromArgb(15, 29, 43));
        using var border = new Pen(selected ? Theme.CyanDark : Theme.Border, selected ? 1.4f : 1);
        e.Graphics.FillPath(bg, card);
        e.Graphics.DrawPath(border, card);
        var accentColor = note is not null ? Theme.Cyan : item!.Done ? Color.FromArgb(52, 211, 153) : Theme.ProgressAccent(item.Progress);
        using var accent = new SolidBrush(accentColor);
        e.Graphics.FillEllipse(accent, cardRect.X + 13, cardRect.Y + 14, 7, 7);
        var title = note?.Title ?? item!.Title;
        using var titleFont = new Font("Segoe UI Semibold", 10.5f);
        using var metaFont = new Font("Segoe UI", 9);
        using var tinyFont = new Font("Segoe UI Semibold", 8.5f);
        TextRenderer.DrawText(e.Graphics, title, titleFont,
            new Rectangle(cardRect.X + 29, cardRect.Y + 9, cardRect.Width - 98, 25), Theme.Text,
            TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);
        if (note is not null)
        {
            var snippet = note.Text.Replace("\r", " ").Replace("\n", " ");
            TextRenderer.DrawText(e.Graphics, string.IsNullOrWhiteSpace(snippet) ? "Anotação vazia" : snippet, metaFont,
                new Rectangle(cardRect.X + 15, cardRect.Y + 37, cardRect.Width - 30, 20), Theme.Muted, TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(e.Graphics, note.Visible ? "NA TELA" : "OCULTA", tinyFont,
                new Rectangle(cardRect.X + 15, cardRect.Y + 61, cardRect.Width - 30, 17), Theme.Cyan);
        }
        else
        {
            var meta = $"{item!.EffectiveDueAt:dd/MM HH:mm}   •   {(string.IsNullOrWhiteSpace(item.Project) ? "Geral" : item.Project)}";
            TextRenderer.DrawText(e.Graphics, meta, metaFont,
                new Rectangle(cardRect.X + 15, cardRect.Y + 38, cardRect.Width - 30, 19), Theme.Muted, TextFormatFlags.EndEllipsis);
            using var track = new SolidBrush(Theme.Field);
            var progressRect = new Rectangle(cardRect.X + 15, cardRect.Y + 69, cardRect.Width - 72, 4);
            e.Graphics.FillRectangle(track, progressRect);
            e.Graphics.FillRectangle(accent, progressRect.X, progressRect.Y, progressRect.Width * item.Progress / 100, progressRect.Height);
            TextRenderer.DrawText(e.Graphics, $"{item.Progress}%", tinyFont,
                new Rectangle(cardRect.Right - 48, cardRect.Y + 60, 38, 20), accentColor,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
        }
    }

    private void ChangeSelection()
    {
        if (_loading) return;
        if (!PersistEditor())
        {
            _loading = true;
            _items.SelectedItem = (object?)_selectedNote ?? _selected;
            _loading = false;
            return;
        }
        ShowSelection(_items.SelectedItem);
    }

    private void ShowSelection(object? entry)
    {
        _loading = true;
        _selected = entry as WorkItem;
        _selectedNote = entry as StickyNote;
        var showingNotes = _selectedNote is not null || _filter.SelectedIndex == 2;
        _taskPanel.Visible = !showingNotes;
        _notePanel.Visible = showingNotes;
        _noteTitle.Enabled = _selectedNote is not null;
        _noteText.Enabled = _selectedNote is not null;
        _noteColor.Enabled = _selectedNote is not null;
        _noteOpacity.Enabled = _selectedNote is not null;
        _noteVisible.Enabled = _selectedNote is not null;
        foreach (var button in _noteActionButtons) button.Enabled = _selectedNote is not null;
        _noteHelp.Text = _selectedNote is null
            ? "Nenhuma anotação ainda. Clique em + Nova anotação para começar."
            : "Escreva aqui ou diretamente na anotação sobre a tela. Tudo é salvo neste computador.";
        if (showingNotes)
        {
            _noteTitle.Text = _selectedNote?.Title ?? "";
            _noteText.Text = _selectedNote is null ? "" : TextLines.ForEditor(_selectedNote.Text);
            _noteColor.SelectedItem = _selectedNote?.Color ?? "Ciano";
            _noteOpacity.SelectedItem = _selectedNote is null || _selectedNote.Opacity > 0.85 ? "92%" : _selectedNote.Opacity > 0.65 ? "72%" : "56%";
            _noteVisible.Checked = _selectedNote?.Visible ?? false;
            _loading = false;
            return;
        }
        var item = _selected;
        _detailHeadline.Text = item?.Title ?? "Selecione ou crie uma tarefa";
        var now = DateTime.Now;
        _detailBadge.Text = item is null ? "PRONTO PARA COMEÇAR" : item.Done ? "✓ CONCLUÍDA" : item.EffectiveDueAt < now ? "● ATRASADA" : item.IsActive(now) ? "● EM ANDAMENTO" : "○ PROGRAMADA";
        _detailBadge.ForeColor = item is not null && item.EffectiveDueAt < now && !item.Done ? Color.FromArgb(251, 191, 36) : Theme.Cyan;
        _title.Text = item?.Title ?? "";
        _project.Text = item?.Project ?? "";
        if (_folderButton is not null)
        {
            _folderButton.Enabled = item is not null;
            _folderButton.Text = item is not null && !string.IsNullOrWhiteSpace(item.ProjectFolderPath)
                ? "↗ Abrir pasta" : "Vincular pasta";
        }
        _start.Value = item?.StartAt is DateTime start && start >= _start.MinDate && start <= _start.MaxDate ? start : DateTime.Now;
        _due.Value = item?.DueAt is DateTime due && due >= _due.MinDate && due <= _due.MaxDate ? due : DateTime.Now;
        _adjustment.SelectedItem = item?.AdjustmentKind is "Postergado" or "Reduzido" ? item.AdjustmentKind : "Nenhum";
        _adjustedDue.Value = item?.AdjustedDueAt ?? _due.Value;
        UpdateAdjustmentEditor();
        _repeat.SelectedItem = item?.Repeat is "Diariamente" or "Semanalmente" ? item.Repeat : "Nenhuma";
        _stickerToggle.Checked = item?.ShowSticker ?? false;
        _notes.Text = TextLines.ForEditor(item?.Notes);
        _progress.Percent = item?.Progress ?? 0;
        _checklist.Items.Clear();
        if (item is not null)
            foreach (var step in item.Checklist) _checklist.Items.Add(step.Text, step.Done);
        _newCheck.Clear();
        _loading = false;
    }

    private bool PersistEditor()
    {
        if (_loading) return true;
        if (_selectedNote is not null)
        {
            _selectedNote.Title = string.IsNullOrWhiteSpace(_noteTitle.Text) ? "Nova anotação" : _noteTitle.Text.Trim();
            _selectedNote.Text = _noteText.Text;
            _selectedNote.Color = _noteColor.SelectedItem?.ToString() ?? "Ciano";
            _selectedNote.Opacity = _noteOpacity.SelectedItem?.ToString() switch { "72%" => 0.72, "56%" => 0.56, _ => 0.92 };
            _store.Save();
            return true;
        }
        if (_selected is null) return true;
        var start = _start.Value;
        var originalDue = _due.Value;
        var adjustment = _adjustment.SelectedItem?.ToString() ?? "Nenhum";
        var adjustedDue = adjustment == "Nenhum" ? (DateTime?)null : _adjustedDue.Value;
        if (originalDue <= start || adjustedDue is not null &&
            (adjustedDue <= start || adjustment == "Postergado" && adjustedDue <= originalDue ||
             adjustment == "Reduzido" && adjustedDue >= originalDue))
        {
            MessageBox.Show("Confira as datas: o prazo original deve vir após o início; o novo prazo deve ficar depois do original ao postergar ou antes dele ao reduzir, sempre após o início.",
                "Ajuste de prazo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        _selected.Title = string.IsNullOrWhiteSpace(_title.Text) ? "Novo lembrete" : _title.Text.Trim();
        _selected.Project = _project.Text.Trim();
        if (_selected.Project.Length > 0 && !_project.Items.Cast<string>()
            .Contains(_selected.Project, StringComparer.CurrentCultureIgnoreCase))
            _project.Items.Add(_selected.Project);
        _selected.StartAt = start;
        _selected.DueAt = originalDue;
        _selected.TrySetAdjustment(adjustment, adjustedDue);
        _selected.Repeat = _repeat.SelectedItem?.ToString() ?? "Nenhuma";
        _selected.Notes = _notes.Text.Trim();
        _store.Save();
        return true;
    }

    private void AddNew()
    {
        if (!PersistEditor()) return;
        var item = new WorkItem { Title = "Novo lembrete", StartAt = DateTime.Now, DueAt = DateTime.Now.AddHours(1) };
        _store.Items.Add(item);
        _store.Save();
        _filter.SelectedIndex = 0;
        RefreshList(item.Id);
        _title.Focus();
        _title.SelectAll();
    }

    private void AddNote()
    {
        if (!PersistEditor()) return;
        var note = new StickyNote();
        _store.Notes.Add(note);
        _store.Save();
        _filter.SelectedIndex = 2;
        RefreshList(note.Id);
        _noteText.Focus();
    }

    private void SaveCurrent()
    {
        if (!PersistEditor()) return;
        RefreshList();
    }

    private void AddChecklistItem()
    {
        if (_selected is null || string.IsNullOrWhiteSpace(_newCheck.Text)) return;
        _selected.Checklist.Add(new ChecklistItem { Text = _newCheck.Text.Trim() });
        _store.Save();
        _checklist.Items.Add(_newCheck.Text.Trim(), false);
        _progress.Percent = _selected.Progress;
        SyncStickers();
        _items.Invalidate();
        UpdateAssistant();
        _newCheck.Clear();
        _newCheck.Focus();
    }

    private void ChecklistChecked(object? sender, ItemCheckEventArgs e)
    {
        if (_loading || _selected is null || e.Index >= _selected.Checklist.Count) return;
        var item = _selected;
        item.Checklist[e.Index].Done = e.NewValue == CheckState.Checked;
        BeginInvoke(() =>
        {
            _store.Save();
            if (_selected?.Id == item.Id) _progress.Percent = item.Progress;
            SyncStickers();
            UpdateAssistant();
        });
    }

    private void RemoveChecklistItem()
    {
        if (_selected is null || _checklist.SelectedIndex < 0) return;
        var index = _checklist.SelectedIndex;
        _selected.Checklist.RemoveAt(index);
        _checklist.Items.RemoveAt(index);
        _store.Save();
        _progress.Percent = _selected.Progress;
        SyncStickers();
        _items.Invalidate();
        UpdateAssistant();
    }

    private void CompleteCurrent()
    {
        if (_selected is null) return;
        if (!PersistEditor()) return;
        Complete(_selected);
    }

    private void Complete(WorkItem item)
    {
        if (item.Repeat == "Nenhuma") item.Done = true;
        else
        {
            var duration = item.EffectiveDueAt - item.StartAt;
            if (duration <= TimeSpan.Zero) duration = TimeSpan.FromHours(1);
            item.DueAt = WorkStore.NextDue(item.EffectiveDueAt, item.Repeat, DateTime.Now);
            item.StartAt = item.DueAt - duration;
            item.TrySetAdjustment("Nenhum", null);
            foreach (var step in item.Checklist) step.Done = false;
        }
        _store.Save();
        RefreshList(item.Id);
    }

    private void SnoozeCurrent()
    {
        if (_selected is null) return;
        if (!PersistEditor()) return;
        Snooze(_selected, 10);
    }

    private void Snooze(WorkItem item, int minutes)
    {
        var newDue = new[] { item.EffectiveDueAt, item.DueAt, DateTime.Now }.Max().AddMinutes(minutes);
        item.TrySetAdjustment("Postergado", newDue);
        _store.Save();
        RefreshList(item.Id);
    }

    private void DeleteCurrent()
    {
        if (_selectedNote is not null)
        {
            if (MessageBox.Show($"Excluir a anotação '{_selectedNote.Title}'?", "Confirmar exclusão", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            _store.Notes.Remove(_selectedNote);
            _selectedNote = null;
            _store.Save();
            RefreshList();
            return;
        }
        if (_selected is null) return;
        if (MessageBox.Show($"Excluir '{_selected.Title}'?", "Confirmar exclusão", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        _store.Items.Remove(_selected);
        _selected = null;
        _store.Save();
        RefreshList();
    }

    private void OpenProjectFolder()
    {
        if (_selected is null) return;
        var path = _selected.ProjectFolderPath;
        if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
        else ChooseProjectFolder();
    }

    private void ChooseProjectFolder()
    {
        if (_selected is null || !PersistEditor()) return;
        using var picker = new FolderBrowserDialog
        {
            Description = "Escolha uma pasta para este lembrete (opcional)",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };
        if (Directory.Exists(_selected.ProjectFolderPath))
            picker.SelectedPath = _selected.ProjectFolderPath;
        if (picker.ShowDialog(this) != DialogResult.OK) return;
        _selected.ProjectFolderPath = picker.SelectedPath;
        _store.Save();
        if (_folderButton is not null) _folderButton.Text = "↗ Abrir pasta";
    }

    private void UnlinkProjectFolder()
    {
        if (_selected is null) return;
        _selected.ProjectFolderPath = "";
        _store.Save();
        if (_folderButton is not null) _folderButton.Text = "Vincular pasta";
    }

    private void CheckDue()
    {
        UpdateAssistant();
        var room = Math.Max(0, 3 - _openAlerts.Count);
        foreach (var item in _store.Items.Where(x => !x.Done && x.EffectiveDueAt <= DateTime.Now && !_openAlerts.Contains(x.Id)).OrderBy(x => x.EffectiveDueAt).Take(room).ToList())
        {
            _openAlerts.Add(item.Id);
            var alert = new AlertForm(item, _openAlerts.Count - 1);
            alert.ActionChosen += action =>
            {
                PersistEditor();
                if (action == "complete") Complete(item);
                else if (action == "open") { BringBack(); RefreshList(item.Id); Snooze(item, 5); }
                else Snooze(item, action == "snooze" ? 10 : 5);
            };
            alert.FormClosed += (_, _) => _openAlerts.Remove(item.Id);
            alert.Show();
        }
    }

    private void SyncStickers()
    {
        foreach (var pair in _stickers.ToList())
        {
            var item = _store.Items.FirstOrDefault(x => x.Id == pair.Key);
            if (item is null || item.Done || !item.ShowSticker || pair.Value.IsDisposed)
            {
                if (!pair.Value.IsDisposed) pair.Value.Stop();
                _stickers.Remove(pair.Key);
            }
        }
        foreach (var item in _store.Items.Where(x => x.ShowSticker && !x.Done))
        {
            if (_stickers.TryGetValue(item.Id, out var existing)) { existing.RefreshItem(); continue; }
            var sticker = new StickerForm(item, () =>
            {
                if (IsDisposed || !IsHandleCreated) return;
                BeginInvoke(() =>
                {
                    if (IsDisposed) return;
                    _store.Save();
                    _items.Invalidate();
                    if (_selected?.Id == item.Id)
                    {
                        _loading = true;
                        if (!_notes.Focused) _notes.Text = TextLines.ForEditor(item.Notes);
                        _stickerToggle.Checked = item.ShowSticker;
                        _progress.Percent = item.Progress;
                        for (var index = 0; index < Math.Min(_checklist.Items.Count, item.Checklist.Count); index++)
                            _checklist.SetItemChecked(index, item.Checklist[index].Done);
                        _loading = false;
                    }
                    UpdateAssistant();
                });
            });
            sticker.FormClosed += (_, _) => _stickers.Remove(item.Id);
            sticker.CompletionRequested += () =>
            {
                if (!PersistEditor()) return;
                Complete(item);
            };
            _stickers[item.Id] = sticker;
            sticker.Show();
        }
    }

    private void SyncNoteStickers()
    {
        foreach (var pair in _noteStickers.ToList())
        {
            var note = _store.Notes.FirstOrDefault(x => x.Id == pair.Key);
            if (note is null || !note.Visible || pair.Value.IsDisposed)
            {
                if (!pair.Value.IsDisposed) pair.Value.Stop();
                _noteStickers.Remove(pair.Key);
            }
        }
        foreach (var note in _store.Notes.Where(x => x.Visible))
        {
            if (_noteStickers.TryGetValue(note.Id, out var existing)) { existing.RefreshNote(); continue; }
            var sticker = new NoteStickerForm(note, () =>
            {
                if (IsDisposed || !IsHandleCreated) return;
                BeginInvoke(() =>
                {
                    if (IsDisposed) return;
                    _store.Save();
                    _items.Invalidate();
                    if (_selectedNote?.Id == note.Id)
                    {
                        _loading = true;
                        if (!_noteTitle.Focused) _noteTitle.Text = note.Title;
                        if (!_noteText.Focused) _noteText.Text = TextLines.ForEditor(note.Text);
                        _noteVisible.Checked = note.Visible;
                        _noteColor.SelectedItem = note.Color;
                        _noteOpacity.SelectedItem = note.Opacity > 0.85 ? "92%" : note.Opacity > 0.65 ? "72%" : "56%";
                        _loading = false;
                    }
                });
            });
            sticker.FormClosed += (_, _) => _noteStickers.Remove(note.Id);
            _noteStickers[note.Id] = sticker;
            sticker.Show();
        }
    }

    private void UpdateAssistant()
    {
        var active = _store.Items.Where(item => item.IsOngoing(DateTime.Now)).ToList();
        var projectNames = active.Select(item => string.IsNullOrWhiteSpace(item.Project) ? "Geral" : item.Project.Trim())
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase).ToList();
        var selected = _store.Settings.AssistantVisibleProjects;
        RefreshTrayProjects(projectNames);
        var visibleCount = selected.Count == 0 ? active.Count : active.Count(item =>
            selected.Contains(string.IsNullOrWhiteSpace(item.Project) ? "Geral" : item.Project.Trim(),
                StringComparer.CurrentCultureIgnoreCase));
        if (!_store.Settings.AssistantEnabled || _store.Settings.AssistantHidden || visibleCount == 0)
        {
            _assistant?.Hide();
            return;
        }
        if (_assistant is null || _assistant.IsDisposed)
        {
            _assistant = new AssistantForm(_store.Settings);
            _assistant.TaskClicked += OpenTaskSticker;
            _assistant.HideRequested += () => _assistantToggle.Checked = false;
            _assistant.TemporarilyHideRequested += () =>
            {
                _store.Settings.AssistantHidden = true;
                _store.SaveSettings();
                UpdateAssistant();
            };
            _assistant.MascotSizeRequested += SetMascotSize;
            _assistant.ProjectSizeRequested += SetProjectSize;
            _assistant.MotionDurationRequested += SetMotionDuration;
            _assistant.MotionModeRequested += ToggleMotionMode;
            _assistant.MotionScheduleRequested += OpenMotionSchedule;
            _assistant.ProjectVisibilityRequested += ToggleProjectVisibility;
            _assistant.PositionChanged += point =>
            {
                _store.Settings.AssistantX = point.X;
                _store.Settings.AssistantY = point.Y;
                _store.SaveSettings();
            };
        }
        _assistant.SetMascotSize(_store.Settings.AssistantMascotSize);
        _assistant.SetProjectSize(_store.Settings.AssistantProjectSize);
        _assistant.SetMotionDuration(_store.Settings.AssistantMotionSeconds);
        _assistant.SetAutomaticMotion(_store.Settings.AssistantAutoMotionIntervalMinutes,
            _store.Settings.AssistantAutoMotionMinSeconds,
            _store.Settings.AssistantAutoMotionMaxSeconds);
        _assistant.SetAutomaticMotionEnabled(_store.Settings.AssistantAutoMotionEnabled);
        _assistant.SetTasks(active, selected);
        if (!_assistant.Visible) _assistant.Show();
    }

    private void RefreshTrayProjects(IReadOnlyCollection<string> projectNames)
    {
        var selected = _store.Settings.AssistantVisibleProjects;
        var choices = projectNames.Concat(selected).Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase).ToList();
        var signature = string.Join("\u001f", projectNames) + "\u001e" + string.Join("\u001f", selected);
        if (signature == _trayProjectsSignature) return;
        _trayProjectsSignature = signature;
        _trayProjectsMenu.DropDownItems.Clear();
        _trayProjectsMenu.DropDownItems.Add(new ToolStripMenuItem("Todos os projetos", null,
            (_, _) => ToggleProjectVisibility(null)) { Checked = selected.Count == 0 });
        if (choices.Count == 0)
        {
            Theme.StyleMenu(_tray.ContextMenuStrip!);
            return;
        }
        _trayProjectsMenu.DropDownItems.Add(new ToolStripSeparator());
        foreach (var name in choices)
        {
            var projectName = name;
            var inactive = !projectNames.Contains(name, StringComparer.CurrentCultureIgnoreCase);
            var label = selected.Count == 0 ? $"Somente {name}" : name;
            if (inactive) label += " (sem tarefas ativas)";
            _trayProjectsMenu.DropDownItems.Add(new ToolStripMenuItem(label, null,
                (_, _) => ToggleProjectVisibility(projectName))
            {
                Checked = selected.Contains(name, StringComparer.CurrentCultureIgnoreCase)
            });
        }
        Theme.StyleMenu(_tray.ContextMenuStrip!);
    }

    private void ToggleProjectVisibility(string? projectName)
    {
        var selected = _store.Settings.AssistantVisibleProjects;
        if (projectName is null) selected.Clear();
        else if (selected.Count == 0) selected.Add(projectName);
        else if (selected.Contains(projectName, StringComparer.CurrentCultureIgnoreCase))
        {
            selected.RemoveAll(name => string.Equals(name, projectName, StringComparison.CurrentCultureIgnoreCase));
        }
        else selected.Add(projectName);
        _store.SaveSettings();
        if (IsHandleCreated) BeginInvoke(UpdateAssistant);
        else UpdateAssistant();
    }

    private void SetMascotSize(int index)
    {
        _store.Settings.AssistantMascotSize = Math.Clamp(index, 0, AssistantForm.MascotSizeLabels.Length - 1);
        _store.SaveSettings();
        RefreshTrayMascotSize();
        _assistant?.SetMascotSize(_store.Settings.AssistantMascotSize);
    }

    private void SetProjectSize(int index)
    {
        _store.Settings.AssistantProjectSize = Math.Clamp(index, 0,
            AssistantForm.ProjectSizeLabels.Length - 1);
        _store.SaveSettings();
        RefreshTrayProjectSize();
        _assistant?.SetProjectSize(_store.Settings.AssistantProjectSize);
    }

    private void RefreshTrayProjectSize()
    {
        for (var index = 0; index < _trayProjectSizeMenu.DropDownItems.Count; index++)
            if (_trayProjectSizeMenu.DropDownItems[index] is ToolStripMenuItem option)
                option.Checked = index == _store.Settings.AssistantProjectSize;
    }

    private void RefreshTrayMascotSize()
    {
        for (var index = 0; index < _trayMascotSizeMenu.DropDownItems.Count; index++)
            if (_trayMascotSizeMenu.DropDownItems[index] is ToolStripMenuItem option)
                option.Checked = index == _store.Settings.AssistantMascotSize;
    }

    private void SetMotionDuration(int seconds)
    {
        _store.Settings.AssistantMotionSeconds = AssistantForm.MotionOptions.Any(option => option.Seconds == seconds)
            ? seconds : 3;
        _store.SaveSettings();
        RefreshTrayMotionDuration();
        _assistant?.SetMotionDuration(_store.Settings.AssistantMotionSeconds);
    }

    private void ToggleMotionMode()
    {
        if (_store.Settings.AssistantAutoMotionEnabled)
        {
            SetMotionMode(false);
            return;
        }
        if (_store.Settings.AssistantAutoMotionIntervalMinutes == 0)
        {
            OpenMotionSchedule();
            if (_store.Settings.AssistantAutoMotionIntervalMinutes == 0) return;
        }
        SetMotionMode(true);
    }

    private void SetMotionMode(bool enabled)
    {
        _store.Settings.AssistantAutoMotionEnabled = enabled;
        _store.SaveSettings();
        RefreshTrayMotionMode();
        _assistant?.SetAutomaticMotionEnabled(enabled);
    }

    private void RefreshTrayMotionMode()
    {
        _trayMotionModeMenu.Checked = _store.Settings.AssistantAutoMotionEnabled;
        _trayMotionModeMenu.Text = _store.Settings.AssistantAutoMotionEnabled
            ? "Desativar movimento automático" : "Ativar movimento automático";
    }

    private void OpenMotionSchedule()
    {
        using var dialog = new MotionScheduleDialog(_store.Settings);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _store.Settings.AssistantAutoMotionIntervalMinutes = dialog.IntervalMinutes;
        _store.Settings.AssistantAutoMotionMinSeconds = dialog.MinimumSeconds;
        _store.Settings.AssistantAutoMotionMaxSeconds = dialog.MaximumSeconds;
        if (dialog.IntervalMinutes == 0) _store.Settings.AssistantAutoMotionEnabled = false;
        _store.SaveSettings();
        _assistant?.SetAutomaticMotion(dialog.IntervalMinutes, dialog.MinimumSeconds, dialog.MaximumSeconds);
        _assistant?.SetAutomaticMotionEnabled(_store.Settings.AssistantAutoMotionEnabled);
        RefreshTrayMotionMode();
    }

    private void RefreshTrayMotionDuration()
    {
        for (var index = 0; index < _trayMotionMenu.DropDownItems.Count; index++)
            if (_trayMotionMenu.DropDownItems[index] is ToolStripMenuItem option)
                option.Checked = AssistantForm.MotionOptions[index].Seconds == _store.Settings.AssistantMotionSeconds;
    }

    private void ShowAssistantFromTray()
    {
        _store.Settings.AssistantEnabled = true;
        _store.Settings.AssistantHidden = false;
        _assistantToggle.Checked = true;
        _store.SaveSettings();
        UpdateAssistant();
    }

    private void ToggleAssistantVisibility()
    {
        if (_assistant?.Visible == true)
        {
            _store.Settings.AssistantHidden = true;
            _store.SaveSettings();
            UpdateAssistant();
        }
        else ShowAssistantFromTray();
    }

    private void OpenTaskSticker(WorkItem item)
    {
        if (!PersistEditor()) return;
        item.ShowSticker = true;
        _store.Save();
        SyncStickers();
        if (_stickers.TryGetValue(item.Id, out var sticker))
        {
            sticker.OpenChecklist();
            sticker.BringToFront();
        }
        RefreshList(item.Id);
    }

    internal void BringBack()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private void OnClosing(object? sender, FormClosingEventArgs e)
    {
        if (_exitRequested || e.CloseReason == CloseReason.WindowsShutDown)
        {
            PersistEditor();
            foreach (var sticker in _stickers.Values.ToList()) sticker.Stop();
            foreach (var sticker in _noteStickers.Values.ToList()) sticker.Stop();
            _assistant?.Close();
            _tray.Visible = false;
            _tray.Dispose();
            return;
        }
        e.Cancel = true;
        PersistEditor();
        Hide();
    }

    private void ExitApp()
    {
        _exitRequested = true;
        Close();
    }

    private static bool IsStartupEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(RunValue) is string;
    }

    private void SetStartup(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (enabled) key.SetValue(RunValue, $"\"{Environment.ProcessPath}\"");
            else key.DeleteValue(RunValue, false);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Não foi possível alterar a inicialização: {ex.Message}", "Inicialização", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _settingStartup = true;
            _startup.Checked = !enabled;
            _settingStartup = false;
        }
    }
}
