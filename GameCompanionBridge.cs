using System.ComponentModel;
using System.Diagnostics;

namespace Firaw.WorkAssistant;

internal static class GameCompanionBridge
{
    internal static string ExecutablePath => Path.Combine(AppContext.BaseDirectory, "GameCompanion", "Strigoi.Companion.exe");
    internal static bool IsAvailable => File.Exists(ExecutablePath);

    internal static void Open()
    {
        try
        {
            if (!IsAvailable) throw new FileNotFoundException("O módulo de jogos não está incluído nesta instalação.");
            Process.Start(new ProcessStartInfo(ExecutablePath) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(ExecutablePath)! });
        }
        catch (Exception ex) when (ex is IOException or Win32Exception or UnauthorizedAccessException)
        {
            MessageBox.Show(ex.Message, "Firaw Assistente de Trabalho", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
