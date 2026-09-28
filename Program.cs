namespace Firaw.WorkAssistant;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var isolatedData = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FIRAW_ASSISTANT_DATA_PATH"));
        using var mutex = new Mutex(true, isolatedData ? @"Local\FirawWorkAssistantPreview" : @"Local\FirawWorkAssistant", out var firstInstance);
        using var activate = new EventWaitHandle(false, EventResetMode.AutoReset,
            isolatedData ? @"Local\FirawWorkAssistantPreviewActivate" : @"Local\FirawWorkAssistantActivate");
        if (!firstInstance)
        {
            activate.Set();
            return;
        }
        ApplicationConfiguration.Initialize();
        using var form = new MainForm();
        var registration = ThreadPool.RegisterWaitForSingleObject(activate, (_, _) =>
        {
            if (form.IsHandleCreated && !form.IsDisposed)
                form.BeginInvoke(form.BringBack);
        }, null, -1, false);
        try { Application.Run(form); }
        finally { registration.Unregister(null); }
    }
}
