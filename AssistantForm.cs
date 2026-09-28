using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Firaw.WorkAssistant;

internal sealed class AssistantForm : Form
{
    private sealed record ProjectGroup(string Name, List<WorkItem> Tasks);

    private const int ProjectWidth = 248;
    private const int CompactProjectWidth = 212;
    private static readonly float[] ProjectScales = [0.86f, 0.93f, 1.0f, 1.13f, 1.27f];
    public static readonly string[] MascotSizeLabels = ["Compacto", "Pequeno", "Médio", "Grande", "Extra grande"];
    public static readonly string[] ProjectSizeLabels = ["Compacto", "Pequeno", "Médio", "Grande", "Extra grande"];
    public static readonly (int Seconds, string Label)[] MotionOptions =
        [(0, "Sem movimento imediato"), (1, "1 segundo"), (3, "3 segundos"), (5, "5 segundos")];
    private static readonly int[] MascotHeights = [105, 135, 180, 240, 300];
    private readonly System.Windows.Forms.Timer _animation = new() { Interval = 90 };
    private readonly System.Windows.Forms.Timer _motion = new() { Interval = 35 };
    private readonly System.Windows.Forms.Timer _automaticMotion = new() { Interval = 1000 };
    private readonly Image?[] _mascots = new Image?[4];
    private readonly Dictionary<(int Mood, int Width, int Height), Region> _mascotRegions = [];
    private readonly List<ProjectGroup> _projects = [];
    private readonly List<string> _allProjectNames = [];
    private readonly List<string> _selectedProjectNames = [];
    private string _visibilityMenuSignature = "";
    private readonly List<(Rectangle Bounds, WorkItem Item)> _taskTargets = [];
    private readonly List<(Rectangle Bounds, string Project)> _moreTaskTargets = [];
    private readonly Dictionary<string, int> _taskOffsets = new(StringComparer.CurrentCultureIgnoreCase);
    private int _frame;
    private int _projectStart;
    private int _columns = 1;
    private int _projectWidth = ProjectWidth;
    private int _projectGap = 8;
    private int _projectTop = 60;
    private int _headerHeight = 48;
    private int _projectSizeIndex;
    private int _maxTasksPerProject = 2;
    private int _projectHeight;
    private readonly bool _initialPositionProvided;
    private bool _hasLayout;
    private Guid _selectedId;
    private WorkItem? _pressedTask;
    private string? _pressedMoreProject;
    private bool _pressedHeaderMore;
    private Point _mouseStart;
    private Point _windowStart;
    private bool _dragged;
    private int _mascotSizeIndex;
    private int _mascotHeight;
    private int _mascotWidth;
    private readonly ToolStripMenuItem _sizeMenu = new("Tamanho do bonequinho");
    private readonly ToolStripMenuItem _projectSizeMenu = new("Tamanho dos projetos");
    private readonly ToolStripMenuItem _visibilityMenu = new("Projetos visíveis");
    private readonly ToolStripMenuItem _motionMenu = new("Movimento imediato ao ativar");
    private readonly ToolStripMenuItem _motionModeMenu = new("Ativar movimento automático");
    private int _motionSeconds;
    private bool _automaticMotionEnabled;
    private int _automaticIntervalMinutes;
    private int _automaticMinSeconds;
    private int _automaticMaxSeconds;
    private DateTime _nextAutomaticMotionAt;
    private DateTime _motionUntil;
    private Point _motionOrigin;
    private Point _motionLast;
    private int _motionFrame;
    private bool _pressedMotionGesture;

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    public event Action<WorkItem>? TaskClicked;
    public event Action? HideRequested;
    public event Action? TemporarilyHideRequested;
    public event Action<int>? MascotSizeRequested;
    public event Action<int>? ProjectSizeRequested;
    public event Action<int>? MotionDurationRequested;
    public event Action? MotionModeRequested;
    public event Action? MotionScheduleRequested;
    public event Action<string?>? ProjectVisibilityRequested;
    public event Action<Point>? PositionChanged;

    public AssistantForm(WorkSettings settings)
    {
        Text = "Firaw • Assistente na tela";
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Size = new Size(264, 360);
        BackColor = Color.FromArgb(1, 2, 3);
        TransparencyKey = BackColor;
        DoubleBuffered = true;
        Cursor = Cursors.Hand;
        _mascots[0] = LoadMascot("mascot-red.png");
        _mascots[1] = LoadMascot("mascot-amber.png");
        _mascots[2] = LoadMascot("mascot-green.png");
        _mascots[3] = LoadMascot("mascot.png");
        _mascotSizeIndex = Math.Clamp(settings.AssistantMascotSize, 0, MascotHeights.Length - 1);
        _projectSizeIndex = Math.Clamp(settings.AssistantProjectSize, 0, ProjectScales.Length - 1);
        _motionSeconds = NormalizeMotionSeconds(settings.AssistantMotionSeconds);
        _automaticMotionEnabled = settings.AssistantAutoMotionEnabled;
        _automaticIntervalMinutes = settings.AssistantAutoMotionIntervalMinutes;
        _automaticMinSeconds = settings.AssistantAutoMotionMinSeconds;
        _automaticMaxSeconds = settings.AssistantAutoMotionMaxSeconds;
        _initialPositionProvided = settings.AssistantX is not null && settings.AssistantY is not null;
        var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 720);
        Location = settings.AssistantX is int x && settings.AssistantY is int y
            ? new Point(Math.Clamp(x, area.Left, Math.Max(area.Left, area.Right - Width)),
                Math.Clamp(y, area.Top, Math.Max(area.Top, area.Bottom - Height)))
            : new Point(area.Right - Width - 28, area.Bottom - Height - 35);
        var menu = new ContextMenuStrip();
        menu.Items.Add("Abrir tarefa selecionada", null, (_, _) => OpenCurrent());
        menu.Items.Add("Ver próximos projetos", null, (_, _) => ShowNext());
        menu.Items.Add(_visibilityMenu);
        for (var index = 0; index < ProjectSizeLabels.Length; index++)
        {
            var size = index;
            _projectSizeMenu.DropDownItems.Add(ProjectSizeLabels[index], null,
                (_, _) => ProjectSizeRequested?.Invoke(size));
        }
        menu.Items.Add(_projectSizeMenu);
        for (var index = 0; index < MascotSizeLabels.Length; index++)
        {
            var size = index;
            _sizeMenu.DropDownItems.Add(MascotSizeLabels[index], null, (_, _) => MascotSizeRequested?.Invoke(size));
        }
        menu.Items.Add(_sizeMenu);
        foreach (var (seconds, label) in MotionOptions)
            _motionMenu.DropDownItems.Add(label, null, (_, _) => MotionDurationRequested?.Invoke(seconds));
        menu.Items.Add(_motionMenu);
        _motionModeMenu.Click += (_, _) => MotionModeRequested?.Invoke();
        menu.Items.Add(_motionModeMenu);
        menu.Items.Add("Programar movimento automático…", null, (_, _) => MotionScheduleRequested?.Invoke());
        menu.Items.Add("Ocultar até clicar no ícone da bandeja", null, (_, _) => TemporarilyHideRequested?.Invoke());
        menu.Items.Add("Desativar assistente", null, (_, _) => HideRequested?.Invoke());
        Theme.StyleMenu(menu);
        ContextMenuStrip = menu;
        RefreshSizeMenu();
        RefreshProjectSizeMenu();
        RefreshMotionMenu();
        RefreshMotionModeMenu();
        _animation.Tick += (_, _) => { _frame++; UpdateInteractiveRegion(); Invalidate(); };
        _animation.Start();
        _motion.Tick += (_, _) => MoveCursorStep();
        _automaticMotion.Tick += (_, _) => CheckAutomaticMotion();
        _automaticMotion.Start();
    }

    protected override bool ShowWithoutActivation => true;

    protected override void OnVisibleChanged(EventArgs e)
    {
        if (!Visible) StopMotion(false);
        else ResetAutomaticMotionClock();
        base.OnVisibleChanged(e);
    }

    public void SetMascotSize(int index)
    {
        var next = Math.Clamp(index, 0, MascotHeights.Length - 1);
        if (_mascotSizeIndex == next) return;
        _mascotSizeIndex = next;
        RefreshSizeMenu();
        Reflow();
    }

    public void SetProjectSize(int index)
    {
        var next = Math.Clamp(index, 0, ProjectScales.Length - 1);
        if (_projectSizeIndex == next) return;
        _projectSizeIndex = next;
        RefreshProjectSizeMenu();
        Reflow();
    }

    private void RefreshProjectSizeMenu()
    {
        for (var index = 0; index < _projectSizeMenu.DropDownItems.Count; index++)
            if (_projectSizeMenu.DropDownItems[index] is ToolStripMenuItem option)
                option.Checked = index == _projectSizeIndex;
    }

    public void SetMotionDuration(int seconds)
    {
        _motionSeconds = NormalizeMotionSeconds(seconds);
        RefreshMotionMenu();
    }

    public void SetAutomaticMotionEnabled(bool enabled)
    {
        if (_automaticMotionEnabled == enabled) return;
        _automaticMotionEnabled = enabled;
        RefreshMotionModeMenu();
        ResetAutomaticMotionClock();
        if (!enabled) StopMotion(false);
        else if (Visible && _motionSeconds > 0) StartMotion(_motionSeconds);
    }

    private void RefreshMotionModeMenu()
    {
        _motionModeMenu.Checked = _automaticMotionEnabled;
        _motionModeMenu.Text = _automaticMotionEnabled
            ? "Desativar movimento automático" : "Ativar movimento automático";
    }

    public void SetAutomaticMotion(int intervalMinutes, int minSeconds, int maxSeconds)
    {
        if (_automaticIntervalMinutes == intervalMinutes && _automaticMinSeconds == minSeconds &&
            _automaticMaxSeconds == maxSeconds) return;
        _automaticIntervalMinutes = intervalMinutes;
        _automaticMinSeconds = minSeconds;
        _automaticMaxSeconds = maxSeconds;
        ResetAutomaticMotionClock();
    }

    private void ResetAutomaticMotionClock() => _nextAutomaticMotionAt =
        _automaticMotionEnabled && _automaticIntervalMinutes > 0
            ? DateTime.UtcNow.AddMinutes(_automaticIntervalMinutes) : DateTime.MaxValue;

    private void CheckAutomaticMotion()
    {
        if (!Visible || !_automaticMotionEnabled || _automaticIntervalMinutes <= 0 ||
            DateTime.UtcNow < _nextAutomaticMotionAt) return;
        ResetAutomaticMotionClock();
        if (_motion.Enabled) return;
        var seconds = Random.Shared.Next(_automaticMinSeconds, _automaticMaxSeconds + 1);
        StartMotion(seconds);
    }

    private static int NormalizeMotionSeconds(int seconds) =>
        MotionOptions.Any(option => option.Seconds == seconds) ? seconds : 3;

    private void RefreshMotionMenu()
    {
        for (var index = 0; index < _motionMenu.DropDownItems.Count; index++)
            if (_motionMenu.DropDownItems[index] is ToolStripMenuItem option)
                option.Checked = MotionOptions[index].Seconds == _motionSeconds;
    }

    private void StartMotion(int seconds)
    {
        _motionOrigin = Cursor.Position;
        _motionLast = _motionOrigin;
        _motionFrame = 0;
        _motionUntil = DateTime.UtcNow.AddSeconds(seconds);
        _motion.Start();
    }

    private void MoveCursorStep()
    {
        if (DateTime.UtcNow >= _motionUntil) { StopMotion(true); return; }
        var current = Cursor.Position;
        if (Math.Abs(current.X - _motionLast.X) + Math.Abs(current.Y - _motionLast.Y) > 32)
        {
            StopMotion(false);
            return;
        }
        _motionFrame++;
        var bounds = Screen.FromPoint(_motionOrigin).Bounds;
        var x = _motionOrigin.X + (int)Math.Round(Math.Sin(_motionFrame * 1.35) * 19);
        var y = _motionOrigin.Y + (int)Math.Round(Math.Cos(_motionFrame * 1.1) * 9);
        _motionLast = new Point(Math.Clamp(x, bounds.Left, bounds.Right - 1),
            Math.Clamp(y, bounds.Top, bounds.Bottom - 1));
        Cursor.Position = _motionLast;
    }

    private void StopMotion(bool restore)
    {
        if (!_motion.Enabled) return;
        _motion.Stop();
        if (restore && Math.Abs(Cursor.Position.X - _motionLast.X) +
            Math.Abs(Cursor.Position.Y - _motionLast.Y) <= 32)
            Cursor.Position = _motionOrigin;
    }

    private void RefreshSizeMenu()
    {
        for (var index = 0; index < _sizeMenu.DropDownItems.Count; index++)
            if (_sizeMenu.DropDownItems[index] is ToolStripMenuItem option)
                option.Checked = index == _mascotSizeIndex;
    }

    private static Image? LoadMascot(string name)
    {
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("Firaw.WorkAssistant.assets." + name);
        return stream is null ? null : new Bitmap(stream);
    }

    private static string ProjectName(WorkItem item) =>
        string.IsNullOrWhiteSpace(item.Project) ? "Geral" : item.Project.Trim();

    private static int MoodIndex(int percent) => percent >= 100 ? 3 : percent >= 67 ? 2 : percent >= 34 ? 1 : 0;
    private static Color MoodColor(int percent) => Theme.ProgressAccent(percent);

    public void SetTasks(IEnumerable<WorkItem> tasks, IReadOnlyCollection<string> selectedProjectNames)
    {
        var all = tasks.ToList();
        var grouped = all.GroupBy(ProjectName, StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.CurrentCultureIgnoreCase)
            .Select(group => new ProjectGroup(group.Key, group.OrderBy(item => item.EffectiveDueAt).ToList()))
            .ToList();
        _allProjectNames.Clear();
        _allProjectNames.AddRange(grouped.Select(group => group.Name));
        _selectedProjectNames.Clear();
        _selectedProjectNames.AddRange(selectedProjectNames);
        RefreshVisibilityMenu();
        _projects.Clear();
        _projects.AddRange(selectedProjectNames.Count == 0 ? grouped : grouped.Where(group =>
            selectedProjectNames.Contains(group.Name, StringComparer.CurrentCultureIgnoreCase)));
        if (_projects.SelectMany(group => group.Tasks).All(item => item.Id != _selectedId))
            _selectedId = _projects.FirstOrDefault()?.Tasks.FirstOrDefault()?.Id ?? Guid.Empty;
        _projectStart = _projects.Count == 0 ? 0 : _projectStart % _projects.Count;
        Reflow();
    }

    private void RefreshVisibilityMenu()
    {
        var signature = string.Join("\u001f", _allProjectNames) + "\u001e" +
            string.Join("\u001f", _selectedProjectNames);
        if (signature == _visibilityMenuSignature) return;
        _visibilityMenuSignature = signature;
        _visibilityMenu.DropDownItems.Clear();
        _visibilityMenu.DropDownItems.Add(new ToolStripMenuItem("Todos os projetos", null,
            (_, _) => ProjectVisibilityRequested?.Invoke(null)) { Checked = _selectedProjectNames.Count == 0 });
        if (_allProjectNames.Count == 0)
        {
            Theme.StyleMenu(ContextMenuStrip!);
            return;
        }
        _visibilityMenu.DropDownItems.Add(new ToolStripSeparator());
        foreach (var name in _allProjectNames)
        {
            var projectName = name;
            _visibilityMenu.DropDownItems.Add(new ToolStripMenuItem(_selectedProjectNames.Count == 0
                ? $"Somente {name}" : name, null,
                (_, _) => ProjectVisibilityRequested?.Invoke(projectName))
            {
                Checked = _selectedProjectNames.Contains(name, StringComparer.CurrentCultureIgnoreCase)
            });
        }
        Theme.StyleMenu(ContextMenuStrip!);
    }

    private List<ProjectGroup> VisibleProjects() =>
        Enumerable.Range(0, Math.Min(_columns, _projects.Count))
            .Select(index => _projects[(_projectStart + index) % _projects.Count]).ToList();

    private List<WorkItem> VisibleTasks(ProjectGroup project)
    {
        var offset = _taskOffsets.GetValueOrDefault(project.Name) % project.Tasks.Count;
        return project.Tasks.Skip(offset).Take(_maxTasksPerProject).ToList();
    }

    private int Scale(int value) => Math.Max(1, (int)Math.Round(value * ProjectScales[_projectSizeIndex]));

    private int TaskHeight(WorkItem item) =>
        Scale(60) + Math.Min(3, Math.Max(1, item.Checklist.Count)) * Scale(17) +
        (item.Checklist.Count > 3 ? Scale(16) : 0);

    private int HeightFor(ProjectGroup project)
    {
        var tasks = VisibleTasks(project);
        return Scale(36) + tasks.Sum(TaskHeight) + Math.Max(0, tasks.Count - 1) * Scale(6) +
            (project.Tasks.Count > _maxTasksPerProject ? Scale(25) : 0) + Scale(8);
    }

    private void Reflow()
    {
        if (_projects.Count == 0) return;
        var area = Screen.FromPoint(Location).WorkingArea;
        var rightGap = Math.Max(0, area.Right - Right);
        var bottomGap = Math.Max(0, area.Bottom - Bottom);
        _headerHeight = Scale(48);
        _projectTop = Scale(60);
        _projectGap = Scale(8);
        var compactWidth = Scale(CompactProjectWidth);
        var regularWidth = Scale(ProjectWidth);
        _columns = _projects.Count >= 3 && 16 + 3 * compactWidth + 2 * _projectGap <= area.Width ? 3
            : _projects.Count >= 2 && 16 + 2 * regularWidth + _projectGap <= area.Width ? 2 : 1;
        _projectWidth = Math.Min(_columns == 3 ? compactWidth : regularWidth, area.Width - 16);
        _maxTasksPerProject = area.Height < 720 ? 1 : 2;
        _projectHeight = VisibleProjects().Max(HeightFor);
        if (_maxTasksPerProject == 2 && _projectTop + _projectHeight + 106 > area.Height)
        {
            _maxTasksPerProject = 1;
            _projectHeight = VisibleProjects().Max(HeightFor);
        }
        _mascotHeight = Math.Min(MascotHeights[_mascotSizeIndex],
            Math.Max(90, area.Height - _projectTop - _projectHeight - 16));
        _mascotWidth = _mascotHeight * 2 / 3;
        Size = new Size(16 + _columns * _projectWidth + (_columns - 1) * _projectGap,
            _projectTop + _projectHeight + Scale(2) + _mascotHeight + Scale(8));
        var nextX = !_hasLayout && _initialPositionProvided ? Left : area.Right - Width - rightGap;
        var nextY = !_hasLayout && _initialPositionProvided ? Top : area.Bottom - Height - bottomGap;
        Location = new Point(Math.Clamp(nextX, area.Left, Math.Max(area.Left, area.Right - Width)),
            Math.Clamp(nextY, area.Top, Math.Max(area.Top, area.Bottom - Height)));
        _hasLayout = true;
        UpdateInteractiveRegion();
        Invalidate();
    }

    private WorkItem? SelectedTask() => _projects.SelectMany(group => group.Tasks)
        .FirstOrDefault(item => item.Id == _selectedId) ?? _projects.FirstOrDefault()?.Tasks.FirstOrDefault();

    private void UpdateInteractiveRegion()
    {
        if (_projects.Count == 0 || Width < 20 || Height < 20) return;
        var interactive = new Region();
        interactive.MakeEmpty();
        using (var header = SurfaceCard.Rounded(new Rectangle(8, 5, Width - 16, _headerHeight), 10))
            interactive.Union(header);
        foreach (var column in Enumerable.Range(0, Math.Min(_columns, _projects.Count)))
        {
            using var card = SurfaceCard.Rounded(new Rectangle(8 + column * (_projectWidth + _projectGap),
                _projectTop, _projectWidth, _projectHeight), 10);
            interactive.Union(card);
        }
        var selected = SelectedTask();
        if (selected is not null && _mascotHeight > 0)
        {
            var mood = MoodIndex(selected.Progress);
            var image = _mascots[mood] ?? _mascots[3];
            if (image is not null)
            {
                var key = (mood, _mascotWidth, _mascotHeight);
                if (!_mascotRegions.TryGetValue(key, out var cached))
                {
                    cached = CreateMascotRegion(image, _mascotWidth, _mascotHeight);
                    _mascotRegions[key] = cached;
                }
                using var mascot = cached.Clone();
                var bob = (int)Math.Round(Math.Sin(_frame * 0.16) * 3);
                mascot.Translate((Width - _mascotWidth) / 2, _projectTop + _projectHeight + Scale(2) + bob);
                interactive.Union(mascot);
            }
            else interactive.Union(new Rectangle((Width - 80) / 2,
                _projectTop + _projectHeight + Scale(17), 80, 80));
        }
        var old = Region;
        Region = interactive;
        old?.Dispose();
    }

    private static Region CreateMascotRegion(Image image, int width, int height)
    {
        using var scaled = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(scaled))
        {
            graphics.Clear(Color.Transparent);
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.DrawImage(image, 0, 0, width, height);
        }
        var data = scaled.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb);
        using var path = new GraphicsPath();
        try
        {
            var stride = Math.Abs(data.Stride);
            var pixels = new byte[stride * height];
            Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
            for (var y = 0; y < height; y++)
            {
                var row = (data.Stride > 0 ? y : height - 1 - y) * stride;
                var x = 0;
                while (x < width)
                {
                    while (x < width && pixels[row + x * 4 + 3] < 40) x++;
                    var start = x;
                    while (x < width && pixels[row + x * 4 + 3] >= 40) x++;
                    if (x > start) path.AddRectangle(new Rectangle(start, y, x - start, 1));
                }
            }
        }
        finally { scaled.UnlockBits(data); }
        return new Region(path);
    }

    private void ShowNext()
    {
        if (_projects.Count > _columns)
        {
            _projectStart = (_projectStart + 1) % _projects.Count;
            _selectedId = VisibleProjects()[0].Tasks[0].Id;
            Reflow();
        }
        else
        {
            var project = _projects.FirstOrDefault(group => group.Tasks.Count > _maxTasksPerProject);
            if (project is not null) CycleTasks(project.Name);
        }
    }

    private void CycleTasks(string projectName)
    {
        var project = _projects.FirstOrDefault(group =>
            string.Equals(group.Name, projectName, StringComparison.CurrentCultureIgnoreCase));
        if (project is null || project.Tasks.Count <= _maxTasksPerProject) return;
        _taskOffsets[project.Name] =
            (_taskOffsets.GetValueOrDefault(project.Name) + _maxTasksPerProject) % project.Tasks.Count;
        _selectedId = VisibleTasks(project)[0].Id;
        Reflow();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (_projects.Count <= _columns) return;
        _projectStart = (_projectStart + (e.Delta < 0 ? 1 : _projects.Count - 1)) % _projects.Count;
        _selectedId = VisibleProjects()[0].Tasks[0].Id;
        Reflow();
    }

    private WorkItem? HitTask(Point point) =>
        _taskTargets.FirstOrDefault(target => target.Bounds.Contains(point)).Item;
    private string? HitMoreTasks(Point point) =>
        _moreTaskTargets.FirstOrDefault(target => target.Bounds.Contains(point)).Project;
    private bool HitMoreProjects(Point point) =>
        _projects.Count > _columns && new Rectangle(Width - Scale(69), 10, Scale(58), Scale(33)).Contains(point);

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Middle)
        {
            StopMotion(false);
            TemporarilyHideRequested?.Invoke();
            return;
        }
        if (e.Button != MouseButtons.Left) return;
        if (GetAsyncKeyState(0xA2) < 0)
        {
            _pressedMotionGesture = true;
            return;
        }
        _pressedTask = HitTask(e.Location);
        _pressedMoreProject = HitMoreTasks(e.Location);
        _pressedHeaderMore = HitMoreProjects(e.Location);
        _mouseStart = Cursor.Position;
        _windowStart = Location;
        _dragged = false;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_pressedMotionGesture) return;
        if (e.Button == MouseButtons.None)
        {
            var hovered = HitTask(e.Location);
            if (hovered is not null && hovered.Id != _selectedId)
            {
                _selectedId = hovered.Id;
                UpdateInteractiveRegion();
                Invalidate();
            }
            return;
        }
        if (e.Button != MouseButtons.Left || _pressedTask is not null ||
            _pressedMoreProject is not null || _pressedHeaderMore) return;
        var cursor = Cursor.Position;
        var dx = cursor.X - _mouseStart.X;
        var dy = cursor.Y - _mouseStart.Y;
        if (Math.Abs(dx) + Math.Abs(dy) > 6) _dragged = true;
        if (_dragged) Location = new Point(_windowStart.X + dx, _windowStart.Y + dy);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left) return;
        if (_pressedMotionGesture)
        {
            _pressedMotionGesture = false;
            MotionModeRequested?.Invoke();
            return;
        }
        if (_dragged) PositionChanged?.Invoke(Location);
        else if (_pressedTask is not null && HitTask(e.Location)?.Id == _pressedTask.Id)
        {
            _selectedId = _pressedTask.Id;
            TaskClicked?.Invoke(_pressedTask);
        }
        else if (_pressedMoreProject is not null &&
            HitMoreTasks(e.Location) == _pressedMoreProject)
            CycleTasks(_pressedMoreProject);
        else if (_pressedHeaderMore && HitMoreProjects(e.Location)) ShowNext();
        else if (e.Y > _projectTop + _projectHeight) OpenCurrent();
        _pressedTask = null;
        _pressedMoreProject = null;
        _pressedHeaderMore = false;
        Invalidate();
    }

    private void OpenCurrent()
    {
        var item = _projects.SelectMany(group => group.Tasks).FirstOrDefault(task => task.Id == _selectedId);
        if (item is not null) TaskClicked?.Invoke(item);
    }

    private static string Remaining(DateTime dueAt)
    {
        var remaining = dueAt - DateTime.Now;
        var duration = remaining.Duration();
        var amount = duration.TotalHours >= 24 ? $"{Math.Ceiling(duration.TotalDays)} dias"
            : duration.TotalHours >= 1 ? $"{Math.Ceiling(duration.TotalHours)} h"
            : $"{Math.Ceiling(duration.TotalMinutes)} min";
        return remaining < TimeSpan.Zero ? $"atraso {amount}" : $"faltam {amount}";
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (_projects.Count == 0) return;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        _taskTargets.Clear();
        _moreTaskTargets.Clear();
        var scale = ProjectScales[_projectSizeIndex];
        using var heading = new Font("Segoe UI Semibold", Math.Max(8, 9 * scale));
        using var small = new Font("Segoe UI", Math.Max(7.3f, 8 * scale));
        using var tiny = new Font("Segoe UI Semibold", Math.Max(7, 7.5f * scale));
        using var dark = new SolidBrush(Color.FromArgb(15, 28, 40));
        using (var headerPath = SurfaceCard.Rounded(new Rectangle(8, 5, Width - 16, _headerHeight), 10))
        using (var border = new Pen(Theme.Cyan, 1.5f))
        {
            g.FillPath(dark, headerPath);
            g.DrawPath(border, headerPath);
        }
        var count = _projects.Sum(group => group.Tasks.Count);
        var headerTitle = _projects.Count == _allProjectNames.Count
            ? $"FIRAW  /  {_projects.Count} {(_projects.Count == 1 ? "PROJETO ATIVO" : "PROJETOS ATIVOS")}"
            : $"FIRAW  /  {_projects.Count} DE {_allProjectNames.Count} VISÍVEIS";
        TextRenderer.DrawText(g, headerTitle,
            heading, new Rectangle(8 + Scale(13), 5 + Scale(5),
                Width - (_projects.Count > _columns ? Scale(92) : Scale(42)), Scale(20)), Theme.Cyan,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(g, $"{count} {(count == 1 ? "lembrete" : "lembretes")}  •  toque para abrir",
            small, new Rectangle(8 + Scale(13), 5 + Scale(26),
                Width - (_projects.Count > _columns ? Scale(99) : Scale(42)), Scale(17)),
            Theme.Muted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        if (_projects.Count > _columns)
        {
            using var nextPath = SurfaceCard.Rounded(new Rectangle(Width - Scale(65), 5 + Scale(6),
                Scale(52), Scale(28)), 8);
            using var nextFill = new SolidBrush(Theme.Field);
            g.FillPath(nextFill, nextPath);
            TextRenderer.DrawText(g, "MAIS ›", tiny, new Rectangle(Width - Scale(64),
                5 + Scale(7), Scale(50), Scale(26)),
                Theme.Cyan, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
        var visible = VisibleProjects();
        for (var column = 0; column < visible.Count; column++)
            DrawProject(g, visible[column], new Rectangle(8 + column * (_projectWidth + _projectGap),
                _projectTop, _projectWidth, _projectHeight), heading, small, tiny);
        var selected = SelectedTask() ?? visible[0].Tasks[0];
        var image = _mascots[MoodIndex(selected.Progress)] ?? _mascots[3];
        var mascotTop = _projectTop + _projectHeight + Scale(2);
        var bob = (int)Math.Round(Math.Sin(_frame * 0.16) * 3);
        if (image is not null)
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(image, new Rectangle((Width - _mascotWidth) / 2, mascotTop + bob, _mascotWidth, _mascotHeight));
        }
        else
        {
            using var fallback = new SolidBrush(MoodColor(selected.Progress));
            g.FillEllipse(fallback, (Width - 80) / 2, mascotTop + 15 + bob, 80, 80);
        }
    }

    private void DrawProject(Graphics g, ProjectGroup project, Rectangle bounds,
        Font heading, Font small, Font tiny)
    {
        using var panel = SurfaceCard.Rounded(bounds, Scale(10));
        using var fill = new SolidBrush(Color.FromArgb(15, 28, 40));
        using var border = new Pen(Theme.CyanDark, 1.1f);
        g.FillPath(fill, panel);
        g.DrawPath(border, panel);
        var progress = (int)Math.Round(project.Tasks.Average(item => item.Progress));
        TextRenderer.DrawText(g, project.Name.ToUpperInvariant(), heading,
            new Rectangle(bounds.X + Scale(11), bounds.Y + Scale(6),
                bounds.Width - Scale(66), Scale(20)), Theme.Cyan,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(g, $"{progress}%", tiny,
            new Rectangle(bounds.Right - Scale(50), bounds.Y + Scale(6), Scale(36), Scale(20)), MoodColor(progress),
            TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
        using (var separator = new Pen(Theme.Border))
            g.DrawLine(separator, bounds.X + Scale(10), bounds.Y + Scale(30),
                bounds.Right - Scale(10), bounds.Y + Scale(30));
        var y = bounds.Y + Scale(36);
        foreach (var item in VisibleTasks(project))
        {
            var taskBounds = new Rectangle(bounds.X + Scale(7), y,
                bounds.Width - Scale(14), TaskHeight(item));
            DrawTask(g, item, taskBounds, heading, small, tiny);
            _taskTargets.Add((taskBounds, item));
            y += taskBounds.Height + Scale(6);
        }
        if (project.Tasks.Count > _maxTasksPerProject)
        {
            var more = new Rectangle(bounds.X + Scale(8), bounds.Bottom - Scale(28),
                bounds.Width - Scale(16), Scale(21));
            _moreTaskTargets.Add((more, project.Name));
            using var surface = SurfaceCard.Rounded(more, Scale(8));
            using var brush = new SolidBrush(Theme.Field);
            g.FillPath(brush, surface);
            TextRenderer.DrawText(g, "VER OUTRAS TAREFAS ›", tiny, more, Theme.Cyan,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    private void DrawTask(Graphics g, WorkItem item, Rectangle bounds,
        Font heading, Font small, Font tiny)
    {
        var selected = item.Id == _selectedId;
        var color = MoodColor(item.Progress);
        using var card = SurfaceCard.Rounded(bounds, Scale(8));
        using var fill = new SolidBrush(selected ? Color.FromArgb(24, 47, 62) : Theme.Field);
        using var border = new Pen(selected ? color : Theme.Border, selected ? 1.3f : 1f);
        g.FillPath(fill, card);
        g.DrawPath(border, card);
        using var marker = new SolidBrush(color);
        g.FillEllipse(marker, bounds.X + Scale(9), bounds.Y + Scale(10), Scale(6), Scale(6));
        TextRenderer.DrawText(g, item.Title, heading,
            new Rectangle(bounds.X + Scale(20), bounds.Y + Scale(3),
                bounds.Width - Scale(70), Scale(20)), Theme.Text,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(g, $"{item.Progress}%", heading,
            new Rectangle(bounds.Right - Scale(46), bounds.Y + Scale(3), Scale(36), Scale(20)), color,
            TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
        TextRenderer.DrawText(g, $"{item.EffectiveDueAt:dd/MM HH:mm}  •  {Remaining(item.EffectiveDueAt)}",
            small, new Rectangle(bounds.X + Scale(10), bounds.Y + Scale(22),
                bounds.Width - Scale(20), Scale(17)), Theme.Muted,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        using var track = new SolidBrush(Theme.Border);
        g.FillRoundedRectangle(track, new Rectangle(bounds.X + Scale(10), bounds.Y + Scale(43),
            bounds.Width - Scale(20), Scale(3)), Scale(2));
        if (item.Progress > 0)
            g.FillRoundedRectangle(marker, new Rectangle(bounds.X + Scale(10), bounds.Y + Scale(43),
                Math.Max(Scale(3), (bounds.Width - Scale(20)) * item.Progress / 100), Scale(3)), Scale(2));
        var steps = item.Checklist.Take(3).ToList();
        if (steps.Count == 0)
            TextRenderer.DrawText(g, "Sem etapas cadastradas", small,
                new Rectangle(bounds.X + Scale(10), bounds.Y + Scale(54),
                    bounds.Width - Scale(20), Scale(17)), Theme.Muted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        for (var index = 0; index < steps.Count; index++)
        {
            var step = steps[index];
            var lineY = bounds.Y + Scale(54) + index * Scale(17);
            var box = new Rectangle(bounds.X + Scale(11), lineY + Scale(3), Scale(10), Scale(10));
            using var boxPath = SurfaceCard.Rounded(box, Scale(3));
            using var boxFill = new SolidBrush(step.Done ? Theme.Cyan : Theme.Field);
            using var boxBorder = new Pen(step.Done ? Theme.Cyan : Theme.Muted);
            g.FillPath(boxFill, boxPath);
            g.DrawPath(boxBorder, boxPath);
            if (step.Done)
                TextRenderer.DrawText(g, "✓", tiny, new Rectangle(box.X - Scale(1), box.Y - Scale(3),
                    Scale(13), Scale(14)),
                    Theme.Background, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, step.Text, small,
                new Rectangle(bounds.X + Scale(26), lineY, bounds.Width - Scale(36), Scale(17)),
                step.Done ? Theme.Muted : Theme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
        if (item.Checklist.Count > 3)
            TextRenderer.DrawText(g, $"+{item.Checklist.Count - 3} etapas • abrir tarefa para ver",
                tiny, new Rectangle(bounds.X + Scale(26), bounds.Y + Scale(105),
                    bounds.Width - Scale(36), Scale(16)),
                Theme.Cyan, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            StopMotion(false);
            _motion.Dispose();
            _automaticMotion.Dispose();
            _animation.Dispose();
            foreach (var image in _mascots) image?.Dispose();
            foreach (var region in _mascotRegions.Values) region.Dispose();
        }
        base.Dispose(disposing);
    }
}
