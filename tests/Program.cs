using Firaw.WorkAssistant;
using System.Text.Json;

var path = Path.Combine(Path.GetTempPath(), "firaw-assistant-tests-" + Guid.NewGuid(), "tasks.json");
try
{
    var store = new WorkStore(path);
    var item = new WorkItem
    {
        Title = "Revisar projeto",
        Project = "FirawMerge",
        ProjectFolderPath = "",
        StartAt = new DateTime(2026, 9, 26, 9, 0, 0),
        DueAt = new DateTime(2026, 9, 26, 10, 30, 0),
        ShowSticker = true,
        Checklist = [new ChecklistItem { Text = "Verificar arquivos", Done = true }]
    };
    store.Items.Add(item);
    store.Notes.Add(new StickyNote { Title = "Telefone", Text = "Ligar às 14h", Visible = true, Opacity = 0.72 });
    store.Save();
    var loaded = new WorkStore(path);
    loaded.Load();
    Check(loaded.Items.Count == 1, "contagem após reabrir");
    Check(loaded.Items[0].Title == item.Title && loaded.Items[0].Project == item.Project, "dados após reabrir");
    Check(loaded.Items[0].ProjectFolderPath == "", "nome de projeto sem pasta vinculada");
    Check(loaded.Items[0].Checklist.Single().Done, "checklist após reabrir");
    Check(loaded.Items[0].Progress == 100, "progresso da checklist");
    Check(loaded.Items[0].IsActive(new DateTime(2026, 9, 26, 9, 30, 0)), "tarefa no intervalo ativo");
    Check(!loaded.Items[0].IsActive(new DateTime(2026, 9, 26, 10, 31, 0)), "tarefa após o prazo");
    Check(loaded.Items[0].IsOngoing(new DateTime(2026, 9, 26, 10, 31, 0)), "assistente mantém tarefa atrasada aberta");
    Check(!loaded.Items[0].IsOngoing(new DateTime(2026, 9, 26, 8, 59, 0)), "assistente aguarda início da tarefa");
    Check(loaded.Items[0].ShowSticker, "preferência do sticker");
    var adjusted = loaded.Items[0];
    Check(adjusted.TrySetAdjustment("Postergado", new DateTime(2026, 9, 26, 11, 30, 0)), "postergar prazo");
    Check(adjusted.EffectiveDueAt == new DateTime(2026, 9, 26, 11, 30, 0), "prazo ajustado efetivo");
    Check(adjusted.IsActive(new DateTime(2026, 9, 26, 10, 45, 0)), "ativo após prazo original postergado");
    Check(!adjusted.TrySetAdjustment("Postergado", new DateTime(2026, 9, 26, 10, 0, 0)), "rejeita postergação anterior");
    Check(adjusted.TrySetAdjustment("Reduzido", new DateTime(2026, 9, 26, 10, 0, 0)), "reduzir prazo");
    Check(!adjusted.IsActive(new DateTime(2026, 9, 26, 10, 15, 0)), "inativo após prazo reduzido");
    adjusted.StickerWidth = 540;
    adjusted.StickerHeight = 440;
    adjusted.StickerTextCollapsed = true;
    loaded.Save();
    var adjustedAgain = new WorkStore(path);
    adjustedAgain.Load();
    Check(adjustedAgain.Items[0].DueAt == item.DueAt && adjustedAgain.Items[0].EffectiveDueAt < item.DueAt,
        "prazo original preservado após reabrir");
    Check(adjustedAgain.Items[0].StickerWidth == 540 && adjustedAgain.Items[0].StickerHeight == 440 &&
        adjustedAgain.Items[0].StickerTextCollapsed, "tamanho e texto do sticker após reabrir");
    var legacy = JsonSerializer.Deserialize<WorkItem>("{\"StartAt\":\"2026-09-26T09:00:00\",\"DueAt\":\"2026-09-26T10:30:00\"}")!;
    Check(legacy.EffectiveDueAt == legacy.DueAt && legacy.AdjustmentKind == "Nenhum", "tarefa antiga sem ajuste");
    Check(loaded.Notes.Count == 1 && loaded.Notes[0].Text == "Ligar às 14h", "texto da anotação independente");
    Check(loaded.Notes[0].Opacity == 0.72, "transparência da anotação");
    Check(TextLines.ForEditor("linha 1\nlinha 2") == "linha 1\r\nlinha 2", "quebras de linha no sticker");
    loaded.Settings.AssistantEnabled = false;
    loaded.Settings.AssistantHidden = true;
    loaded.Settings.AssistantMascotSize = 4;
    loaded.Settings.AssistantProjectSize = 0;
    loaded.Settings.AssistantMotionSeconds = 5;
    loaded.Settings.AssistantAutoMotionIntervalMinutes = 12;
    loaded.Settings.AssistantAutoMotionEnabled = true;
    loaded.Settings.AssistantAutoMotionMinSeconds = 2;
    loaded.Settings.AssistantAutoMotionMaxSeconds = 7;
    loaded.Settings.AssistantVisibleProjects = ["FirawMerge"];
    loaded.SaveSettings();
    var loadedAgain = new WorkStore(path);
    loadedAgain.Load();
    Check(!loadedAgain.Settings.AssistantEnabled, "preferência do assistente");
    Check(loadedAgain.Settings.AssistantHidden && loadedAgain.Settings.AssistantMascotSize == 4 &&
        loadedAgain.Settings.AssistantMotionSeconds == 5, "tamanho e gestos do assistente persistidos");
    Check(loadedAgain.Settings.AssistantProjectSize == 0, "tamanho dos projetos persistido");
    Check(loadedAgain.Settings.AssistantVisibleProjects.SequenceEqual(["FirawMerge"]),
        "projetos visíveis persistidos");
    Check(loadedAgain.Settings.AssistantAutoMotionEnabled &&
        loadedAgain.Settings.AssistantAutoMotionIntervalMinutes == 12 &&
        loadedAgain.Settings.AssistantAutoMotionMinSeconds == 2 &&
        loadedAgain.Settings.AssistantAutoMotionMaxSeconds == 7,
        "programação do cursor persistida");
    loadedAgain.Settings.AssistantAutoMotionIntervalMinutes = 0;
    loadedAgain.SaveSettings();
    var disabledAgain = new WorkStore(path);
    disabledAgain.Load();
    Check(!disabledAgain.Settings.AssistantAutoMotionEnabled,
        "programação desligada quando intervalo é zero");
    var oldSettings = "{\"AssistantMascotSize\":3,\"AssistantEnabled\":true}";
    File.WriteAllText(Path.Combine(Path.GetDirectoryName(path)!, "settings.json"), oldSettings);
    var migrated = new WorkStore(path);
    migrated.Load();
    Check(migrated.Settings.AssistantMascotSize == 4, "migração do tamanho antigo");
    var now = new DateTime(2026, 9, 29, 9, 0, 0);
    Check(WorkStore.NextDue(item.DueAt, "Diariamente", now) == new DateTime(2026, 9, 29, 10, 30, 0), "próxima repetição diária");
    Check(WorkStore.NextDue(item.DueAt, "Semanalmente", now) == new DateTime(2026, 10, 3, 10, 30, 0), "próxima repetição semanal");
    Console.WriteLine("Persistência, prazos ajustados, sticker, progresso, anotações e repetições: OK");
}
finally
{
    var directory = Path.GetDirectoryName(path)!;
    if (Directory.Exists(directory)) Directory.Delete(directory, true);
}

static void Check(bool condition, string name)
{
    if (!condition) throw new Exception($"Falhou: {name}");
}
