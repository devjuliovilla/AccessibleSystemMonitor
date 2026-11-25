using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using ASM.Core;

namespace ASM;

internal enum ProcessTerminationResult
{
    Closed,
    AlreadyExited,
    NoMainWindow,
    StillRunning,
    AccessDenied,
    IdentityChanged,
    ElevationCancelled,
    Failed
}

internal static class ProcessTerminationService
{
    private const string ElevatedVerb = "--elevated-terminate";
    private const int AccessDeniedError = 5;
    private const int ElevationCancelledError = 1223;

    public static bool IsElevatedHelper(string[] args) => args.Length > 0 && args[0].Equals(ElevatedVerb, StringComparison.OrdinalIgnoreCase);

    public static int RunElevatedHelper(string[] args)
    {
        if (args.Length != 3 || !int.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out var processId) || processId <= 0 ||
            !long.TryParse(args[2], NumberStyles.None, CultureInfo.InvariantCulture, out var expectedStartTime)) return 2;

        Process process;
        try { process = Process.GetProcessById(processId); }
        catch (ArgumentException) { return 1; }
        using (process)
        {
            try
            {
                if (!ProcessIdentity.Matches(expectedStartTime, process.StartTime.ToUniversalTime().Ticks)) return 3;
                if (process.HasExited) return 1;
                process.Kill(entireProcessTree: false);
                return process.WaitForExit(5000) ? 0 : 5;
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == AccessDeniedError) { return 4; }
            catch (UnauthorizedAccessException) { return 4; }
            catch (InvalidOperationException) { return 1; }
            catch (Exception ex) when (ex is Win32Exception or NotSupportedException) { return 5; }
        }
    }

    public static async Task<ProcessTerminationResult> TryCloseNormallyAsync(int processId, long expectedStartTime)
    {
        var opened = TryOpenVerified(processId, expectedStartTime, out var process);
        if (opened != ProcessTerminationResult.Closed) return opened;
        using (process!)
        {
            try
            {
                if (process!.HasExited) return ProcessTerminationResult.AlreadyExited;
                if (!process.CloseMainWindow()) return ProcessTerminationResult.NoMainWindow;
                var exited = await Task.Run(() => process.WaitForExit(2500)).ConfigureAwait(false);
                return exited ? ProcessTerminationResult.Closed : ProcessTerminationResult.StillRunning;
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == AccessDeniedError) { return ProcessTerminationResult.AccessDenied; }
            catch (UnauthorizedAccessException) { return ProcessTerminationResult.AccessDenied; }
            catch (InvalidOperationException) { return ProcessTerminationResult.AlreadyExited; }
            catch (NotSupportedException) { return ProcessTerminationResult.Failed; }
        }
    }

    public static async Task<ProcessTerminationResult> TryForceNormallyAsync(int processId, long expectedStartTime)
    {
        var opened = TryOpenVerified(processId, expectedStartTime, out var process);
        if (opened != ProcessTerminationResult.Closed) return opened;
        using (process!)
        {
            try
            {
                if (process!.HasExited) return ProcessTerminationResult.AlreadyExited;
                process.Kill(entireProcessTree: false);
                var exited = await Task.Run(() => process.WaitForExit(5000)).ConfigureAwait(false);
                return exited ? ProcessTerminationResult.Closed : ProcessTerminationResult.Failed;
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == AccessDeniedError) { return ProcessTerminationResult.AccessDenied; }
            catch (UnauthorizedAccessException) { return ProcessTerminationResult.AccessDenied; }
            catch (InvalidOperationException) { return ProcessTerminationResult.AlreadyExited; }
            catch (NotSupportedException) { return ProcessTerminationResult.Failed; }
        }
    }

    public static async Task<ProcessTerminationResult> TryForceElevatedAsync(int processId, long expectedStartTime)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = Application.ExecutablePath,
            Arguments = string.Create(CultureInfo.InvariantCulture, $"{ElevatedVerb} {processId} {expectedStartTime}"),
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden
        };

        try
        {
            using var helper = Process.Start(startInfo);
            if (helper is null) return ProcessTerminationResult.Failed;
            await helper.WaitForExitAsync().ConfigureAwait(false);
            return helper.ExitCode switch
            {
                0 => ProcessTerminationResult.Closed,
                1 => ProcessTerminationResult.AlreadyExited,
                3 => ProcessTerminationResult.IdentityChanged,
                4 => ProcessTerminationResult.AccessDenied,
                _ => ProcessTerminationResult.Failed
            };
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ElevationCancelledError) { return ProcessTerminationResult.ElevationCancelled; }
        catch (Win32Exception ex) when (ex.NativeErrorCode == AccessDeniedError) { return ProcessTerminationResult.AccessDenied; }
        catch (InvalidOperationException) { return ProcessTerminationResult.Failed; }
    }

    private static ProcessTerminationResult TryOpenVerified(int processId, long expectedStartTime, out Process? process)
    {
        process = null;
        try
        {
            process = Process.GetProcessById(processId);
            if (ProcessIdentity.Matches(expectedStartTime, process.StartTime.ToUniversalTime().Ticks)) return ProcessTerminationResult.Closed;
            process.Dispose();
            process = null;
            return ProcessTerminationResult.IdentityChanged;
        }
        catch (ArgumentException) { process?.Dispose(); process = null; return ProcessTerminationResult.AlreadyExited; }
        catch (Win32Exception ex) when (ex.NativeErrorCode == AccessDeniedError) { process?.Dispose(); process = null; return ProcessTerminationResult.AccessDenied; }
        catch (UnauthorizedAccessException) { process?.Dispose(); process = null; return ProcessTerminationResult.AccessDenied; }
        catch (InvalidOperationException) { process?.Dispose(); process = null; return ProcessTerminationResult.AlreadyExited; }
        catch (NotSupportedException) { process?.Dispose(); process = null; return ProcessTerminationResult.Failed; }
    }
}
