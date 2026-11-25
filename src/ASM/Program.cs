using System.Threading;

namespace ASM;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        using var mutex = new Mutex(true, $"Local\\ASM-{Environment.UserName}", out bool createdNew);
        if (!createdNew)
        {
            try
            {
                using var signal = EventWaitHandle.OpenExisting($"Local\\ASM-Show-{Environment.UserName}");
                signal.Set();
            }
            catch (WaitHandleCannotBeOpenedException) { }
            return;
        }

        using var showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, $"Local\\ASM-Show-{Environment.UserName}");
        var startInBackground = args.Contains("--background", StringComparer.OrdinalIgnoreCase);
        Application.Run(new MonitorContext(mutex, showSignal, startVisible: !startInBackground));
    }
}
