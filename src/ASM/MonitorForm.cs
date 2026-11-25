using System.Runtime.InteropServices;
using System.ComponentModel;
using ASM.Core;

namespace ASM;

internal sealed class MonitorForm : Form
{
    private const int HotKeyId = 0x41534D;
    private const int WmHotKey = 0x0312;
    private const int WmSysCommand = 0x0112;
    private const int ScClose = 0xF060;
    private readonly TextBox _summary;
    private readonly Button _refresh;
    private readonly ISystemMetricsProvider _metrics = new SystemMetricsProvider();
    private SystemSnapshot? _lastSnapshot;
    private bool _refreshing;
    private bool _hotKeyRegistered;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool AllowExit { get; set; }

    public MonitorForm(Action showWindow)
    {
        Text = "Monitor del sistema";
        AccessibleName = "Monitor del sistema";
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(1000, 360);
        Size = new Size(1260, 510);
        KeyPreview = true;

        _summary = new TextBox
        {
            Name = "summary",
            AccessibleName = "Resumen de recursos del sistema",
            AccessibleDescription = "Resumen de recursos. Pulsa Enter sobre un renglón de CPU, RAM o GPU, o haz clic en él, para consultar procesos relacionados.",
            Multiline = true,
            ReadOnly = true,
            WordWrap = false,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill,
            TabIndex = 0,
            Font = SystemFonts.MessageBoxFont,
            Text = "Capturando métricas del sistema…"
        };
        _summary.MouseClick += async (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
                await OpenProcessDetailsAtAsync(_summary.GetCharIndexFromPosition(e.Location));
        };
        _summary.MouseMove += (_, e) =>
        {
            var line = GetLineAt(_summary.GetCharIndexFromPosition(e.Location));
            _summary.Cursor = IsInteractiveLine(line) ? Cursors.Hand : Cursors.IBeam;
        };
        _summary.KeyDown += async (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            e.Handled = true;
            e.SuppressKeyPress = true;
            await OpenProcessDetailsAtAsync(_summary.SelectionStart);
        };

        _refresh = new Button { Text = "Actualizar", AccessibleName = "Actualizar métricas", AutoSize = true, TabIndex = 1 };
        _refresh.Click += async (_, _) => await RefreshAsync();
        var copy = new Button { Text = "Copiar", AccessibleName = "Copiar resumen", AutoSize = true, TabIndex = 2 };
        copy.Click += (_, _) => { try { Clipboard.SetText(_summary.Text); } catch (ExternalException) { } };
        var close = new Button { Text = "Cerrar", AccessibleName = "Cerrar y ocultar", AutoSize = true, TabIndex = 3 };
        close.Click += (_, _) => Hide();

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(8), WrapContents = true };
        buttons.Controls.AddRange([_refresh, copy, close]);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(8) };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(_summary, 0, 0);
        layout.Controls.Add(buttons, 0, 1);
        Controls.Add(layout);

        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) { Hide(); e.Handled = true; } };
        FormClosing += (_, e) => { if (!AllowExit && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); } };
        Shown += async (_, _) => { PositionNearTaskbar(); await RefreshAsync(); FocusSummary(); };
    }

    private async Task RefreshAsync()
    {
        if (_refreshing || IsDisposed) return;
        _refreshing = true;
        _refresh.Enabled = false;
        _summary.Text = "Actualizando métricas…";
        try
        {
            var snapshot = await _metrics.CaptureAsync();
            if (!IsDisposed)
            {
                _lastSnapshot = snapshot;
                _summary.Text = SnapshotFormatter.Format(snapshot);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            if (!IsDisposed) _summary.Text = $"No se pudieron actualizar las métricas. {ex.Message}";
        }
        finally
        {
            _refreshing = false;
            if (!IsDisposed) _refresh.Enabled = true;
        }
    }

    private Task OpenProcessDetailsAtAsync(int characterIndex)
    {
        try
        {
            var line = GetLineAt(characterIndex);
            if (line.StartsWith("Procesador:", StringComparison.Ordinal))
            {
                using var details = new ProcessDetailForm(ProcessResource.Cpu);
                details.ShowDialog(this);
            }
            else if (line.StartsWith("Memoria RAM:", StringComparison.Ordinal))
            {
                using var details = new ProcessDetailForm(ProcessResource.Memory, systemMemory: _lastSnapshot?.Memory.Value);
                details.ShowDialog(this);
            }
            else
            {
                var gpu = _lastSnapshot?.Gpus.FirstOrDefault(item => line.StartsWith($"GPU {item.Name}:", StringComparison.Ordinal));
                if (gpu is null && !line.StartsWith("GPU:", StringComparison.Ordinal)) return Task.CompletedTask;
                using var details = new ProcessDetailForm(ProcessResource.Gpu, gpu?.Luid, gpu?.Name);
                details.ShowDialog(this);
            }
            FocusSummary();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            if (!IsDisposed) _summary.Text = $"No se pudo abrir la lista de procesos. {ex.Message}";
        }
        return Task.CompletedTask;
    }

    private string GetLineAt(int characterIndex)
    {
        if (_summary.Lines.Length == 0) return string.Empty;
        var index = Math.Clamp(characterIndex, 0, Math.Max(0, _summary.TextLength));
        var lineNumber = _summary.GetLineFromCharIndex(index);
        return lineNumber >= 0 && lineNumber < _summary.Lines.Length ? _summary.Lines[lineNumber] : string.Empty;
    }

    private static bool IsInteractiveLine(string line) =>
        line.StartsWith("Procesador:", StringComparison.Ordinal) ||
        line.StartsWith("Memoria RAM:", StringComparison.Ordinal) ||
        line.StartsWith("GPU ", StringComparison.Ordinal) ||
        line.StartsWith("GPU:", StringComparison.Ordinal);

    public void FocusSummary()
    {
        if (!Visible) return;
        _summary.Focus();
        _summary.SelectionStart = 0;
        _summary.SelectionLength = 0;
    }

    public bool RegisterMonitorHotKey()
    {
        _hotKeyRegistered = RegisterHotKey(Handle, HotKeyId, 0x0002 | 0x0001, (uint)Keys.M);
        return _hotKeyRegistered;
    }

    public void UnregisterMonitorHotKey()
    {
        if (_hotKeyRegistered) { UnregisterHotKey(Handle, HotKeyId); _hotKeyRegistered = false; }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmHotKey && m.WParam.ToInt32() == HotKeyId) { Show(); Activate(); FocusSummary(); return; }
        if (m.Msg == WmSysCommand && (m.WParam.ToInt32() & 0xFFF0) == ScClose && !AllowExit) { Hide(); return; }
        base.WndProc(ref m);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape) { Hide(); return true; }
        if (keyData == Keys.F5)
        {
            _ = RefreshAndRestoreFocusAsync();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private async Task RefreshAndRestoreFocusAsync()
    {
        await RefreshAsync();
        FocusSummary();
    }

    private void PositionNearTaskbar()
    {
        var area = Screen.FromPoint(Cursor.Position).WorkingArea;
        Location = new Point(area.Right - Width, area.Bottom - Height);
    }

    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint virtualKey);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
