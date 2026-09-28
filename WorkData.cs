using System.Text.Json;
using System.Text.Json.Serialization;

namespace Firaw.WorkAssistant;

public sealed class ChecklistItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Text { get; set; } = "";
    public bool Done { get; set; }
}

public static class TextLines
{
    public static string ForEditor(string? text) => (text ?? "").Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");
}

public sealed class WorkItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string Project { get; set; } = "";
    public string ProjectFolderPath { get; set; } = "";
    public string Notes { get; set; } = "";
    public DateTime StartAt { get; set; } = DateTime.Now;
    public DateTime DueAt { get; set; } = DateTime.Now.AddHours(1);
    public DateTime? AdjustedDueAt { get; set; }
    public string AdjustmentKind { get; set; } = "Nenhum";
    public string Repeat { get; set; } = "Nenhuma";
    public bool Done { get; set; }
    public bool ShowSticker { get; set; }
    public double StickerOpacity { get; set; } = 0.88;
    public int? StickerX { get; set; }
    public int? StickerY { get; set; }
    public int StickerWidth { get; set; } = 460;
    public int StickerHeight { get; set; } = 370;
    public bool StickerTextCollapsed { get; set; }
    public bool StickerChecklistExpanded { get; set; }
    public List<ChecklistItem> Checklist { get; set; } = [];

    [JsonIgnore]
    public int Progress => Done ? 100 : Checklist.Count == 0
        ? 0 : (int)Math.Round(100.0 * Checklist.Count(step => step.Done) / Checklist.Count);

    [JsonIgnore]
    public DateTime EffectiveDueAt => AdjustedDueAt ?? DueAt;

    public bool IsActive(DateTime now) => !Done && StartAt <= now && now <= EffectiveDueAt;

    public bool IsOngoing(DateTime now) => !Done && StartAt <= now;

    public bool TrySetAdjustment(string kind, DateTime? adjustedDueAt)
    {
        if (kind == "Nenhum")
        {
            AdjustmentKind = kind;
            AdjustedDueAt = null;
            return true;
        }
        if (adjustedDueAt is null || adjustedDueAt <= StartAt ||
            kind == "Postergado" && adjustedDueAt <= DueAt ||
            kind == "Reduzido" && adjustedDueAt >= DueAt ||
            kind is not ("Postergado" or "Reduzido")) return false;
        AdjustmentKind = kind;
        AdjustedDueAt = adjustedDueAt;
        return true;
    }
}

public sealed class WorkSettings
{
    public bool AssistantEnabled { get; set; } = true;
    public bool AssistantHidden { get; set; }
    public int AssistantMascotSize { get; set; } = 2;
    public int AssistantProjectSize { get; set; } = 2;
    public int AssistantSizeVersion { get; set; } = 1;
    public int AssistantMotionSeconds { get; set; } = 3;
    public int AssistantAutoMotionIntervalMinutes { get; set; }
    public bool AssistantAutoMotionEnabled { get; set; }
    public int AssistantAutoMotionMinSeconds { get; set; } = 3;
    public int AssistantAutoMotionMaxSeconds { get; set; } = 3;
    public List<string> AssistantVisibleProjects { get; set; } = [];
    public int? AssistantX { get; set; }
    public int? AssistantY { get; set; }
}

public sealed class StickyNote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "Nova anotação";
    public string Text { get; set; } = "";
    public string Color { get; set; } = "Ciano";
    public bool Visible { get; set; } = true;
    public double Opacity { get; set; } = 0.9;
    public int? X { get; set; }
    public int? Y { get; set; }
    public int Width { get; set; } = 455;
    public int Height { get; set; } = 385;
}

public sealed class WorkStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public static string DataPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Firawynix", "WorkAssistant", "tasks.json");
    public static string SettingsPath => Path.Combine(Path.GetDirectoryName(DataPath)!, "settings.json");
    private readonly string _path;
    private readonly string _settingsPath;
    private readonly string _notesPath;

    public WorkStore(string? path = null)
    {
        _path = path ?? DataPath;
        _settingsPath = Path.Combine(Path.GetDirectoryName(_path)!, "settings.json");
        _notesPath = Path.Combine(Path.GetDirectoryName(_path)!, "notes.json");
    }

    public List<WorkItem> Items { get; private set; } = [];
    public List<StickyNote> Notes { get; private set; } = [];
    public WorkSettings Settings { get; private set; } = new();

    public void Load()
    {
        if (File.Exists(_settingsPath))
        {
            try
            {
                var json = File.ReadAllText(_settingsPath);
                Settings = JsonSerializer.Deserialize<WorkSettings>(json) ?? new();
                using var document = JsonDocument.Parse(json);
                if (!document.RootElement.TryGetProperty(nameof(WorkSettings.AssistantSizeVersion), out _))
                    Settings.AssistantMascotSize = Settings.AssistantMascotSize == 0 ? 0 : Settings.AssistantMascotSize + 1;
            }
            catch (JsonException) { Settings = new(); }
        }
        Settings.AssistantSizeVersion = 1;
        Settings.AssistantMascotSize = Math.Clamp(Settings.AssistantMascotSize, 0, 4);
        Settings.AssistantProjectSize = Math.Clamp(Settings.AssistantProjectSize, 0, 4);
        Settings.AssistantVisibleProjects ??= [];
        Settings.AssistantVisibleProjects = Settings.AssistantVisibleProjects
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.CurrentCultureIgnoreCase).ToList();
        if (Settings.AssistantMotionSeconds is not (0 or 1 or 3 or 5))
            Settings.AssistantMotionSeconds = 3;
        Settings.AssistantAutoMotionIntervalMinutes = Math.Clamp(Settings.AssistantAutoMotionIntervalMinutes, 0, 240);
        if (Settings.AssistantAutoMotionIntervalMinutes == 0)
            Settings.AssistantAutoMotionEnabled = false;
        Settings.AssistantAutoMotionMinSeconds = Math.Clamp(Settings.AssistantAutoMotionMinSeconds, 1, 60);
        Settings.AssistantAutoMotionMaxSeconds = Math.Clamp(Settings.AssistantAutoMotionMaxSeconds,
            Settings.AssistantAutoMotionMinSeconds, 60);
        if (File.Exists(_notesPath))
        {
            try
            {
                Notes = JsonSerializer.Deserialize<List<StickyNote>>(File.ReadAllText(_notesPath)) ?? [];
                foreach (var note in Notes)
                {
                    note.Opacity = Math.Clamp(note.Opacity, 0.55, 1);
                    note.Width = Math.Clamp(note.Width, 340, 900);
                    note.Height = Math.Clamp(note.Height, 240, 900);
                }
            }
            catch (Exception ex)
            {
                var backup = _notesPath + ".corrupt-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
                File.Copy(_notesPath, backup, true);
                throw new InvalidDataException($"Não foi possível ler as anotações. Uma cópia foi salva em {backup}.", ex);
            }
        }
        if (!File.Exists(_path)) return;
        try
        {
            Items = JsonSerializer.Deserialize<List<WorkItem>>(File.ReadAllText(_path)) ?? [];
            foreach (var item in Items)
            {
                item.Checklist ??= [];
                item.StickerOpacity = Math.Clamp(item.StickerOpacity, 0.55, 1.0);
                if (item.StartAt > item.DueAt) item.StartAt = item.DueAt.AddHours(-1);
                if (!item.TrySetAdjustment(item.AdjustmentKind, item.AdjustedDueAt))
                    item.TrySetAdjustment("Nenhum", null);
                item.StickerWidth = Math.Clamp(item.StickerWidth, 360, 900);
                item.StickerHeight = Math.Clamp(item.StickerHeight, 235, 900);
            }
        }
        catch (Exception ex)
        {
            var backup = _path + ".corrupt-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            File.Copy(_path, backup, true);
            throw new InvalidDataException($"Não foi possível ler os lembretes. Uma cópia foi salva em {backup}.", ex);
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temp = _path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(Items, JsonOptions));
        File.Move(temp, _path, true);
        var notesTemp = _notesPath + ".tmp";
        File.WriteAllText(notesTemp, JsonSerializer.Serialize(Notes, JsonOptions));
        File.Move(notesTemp, _notesPath, true);
    }

    public void SaveSettings()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
        var temp = _settingsPath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(Settings, JsonOptions));
        File.Move(temp, _settingsPath, true);
    }

    public static DateTime NextDue(DateTime previous, string repeat, DateTime now)
    {
        var interval = repeat == "Diariamente" ? TimeSpan.FromDays(1) : TimeSpan.FromDays(7);
        var candidate = previous;
        do candidate = candidate.Add(interval);
        while (candidate <= now);
        return candidate;
    }
}
