using System.Runtime.InteropServices;

namespace ASM;

internal sealed class MonitorContext : ApplicationContext
{
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _showSignal;
    private readonly MonitorForm _form;
    private readonly NotifyIcon _trayIcon;
    private readonly Icon? _executableIcon;
    private readonly Thread _signalThread;
    private bool _exiting;

    public MonitorContext(Mutex mutex, EventWaitHandle showSignal, bool startVisible)
    {
        _mutex = mutex;
        _showSignal = showSignal;
        _form = new MonitorForm(ShowWindow);
        var menu = new ContextMenuStrip();
        menu.Items.Add("Abrir monitor", null, (_, _) => ShowWindow());
        menu.Items.Add("Iniciar con Windows", null, (_, _) => MessageBox.Show("La configuración de inicio estará disponible en una etapa posterior.", "ASM", MessageBoxButtons.OK, MessageBoxIcon.Information));
        menu.Items.Add("Configuración", null, (_, _) => MessageBox.Show("La configuración estará disponible en una etapa posterior.", "ASM", MessageBoxButtons.OK, MessageBoxIcon.Information));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Salir", null, (_, _) => ExitApplication());

        _executableIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        _trayIcon = new NotifyIcon
        {
            Text = "Monitor del sistema",
            Icon = _executableIcon ?? SystemIcons.Application,
            ContextMenuStrip = menu,
            Visible = true
        };
        _trayIcon.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) ShowWindow(); };
        _trayIcon.DoubleClick += (_, _) => ShowWindow();
        _form.FormClosed += (_, _) => { if (!_exiting) _form.Hide(); };

        if (!_form.RegisterMonitorHotKey())
        {
            _trayIcon.BalloonTipText = "Ctrl+Alt+M está ocupado; puedes abrir ASM desde el icono de bandeja.";
            _trayIcon.ShowBalloonTip(5000);
        }

        _signalThread = new Thread(() =>
        {
            while (!_exiting)
            {
                try
                {
                    if (_showSignal.WaitOne()) _form.BeginInvoke((Action)ShowWindow);
                }
                catch (ObjectDisposedException) { break; }
                catch (InvalidOperationException) { break; }
            }
        }) { IsBackground = true, Name = "ASM second-instance listener" };
        _signalThread.Start();
        if (startVisible) ShowWindow();
    }

    private void ShowWindow()
    {
        if (_exiting || _form.IsDisposed) return;
        _form.Show();
        _form.WindowState = FormWindowState.Normal;
        _form.Activate();
        _form.FocusSummary();
    }

    private void ExitApplication()
    {
        if (_exiting) return;
        _exiting = true;
        _showSignal.Set();
        _form.UnregisterMonitorHotKey();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _executableIcon?.Dispose();
        _form.AllowExit = true;
        _form.Close();
        _showSignal.Dispose();
        _mutex.ReleaseMutex();
        _mutex.Dispose();
        ExitThread();
    }
}
