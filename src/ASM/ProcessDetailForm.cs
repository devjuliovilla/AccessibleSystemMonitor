using System.Globalization;
using ASM.Core;

namespace ASM;

internal enum ProcessResource
{
    Cpu,
    Memory,
    Gpu
}

internal sealed class ProcessDetailForm : Form
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("es-MX");
    private const double GiB = 1024d * 1024 * 1024;
    private readonly ProcessResource _resource;
    private readonly string? _adapterLuid;
    private readonly MemoryMetric? _systemMemory;
    private readonly ProcessMetricsProvider _provider = new();
    private readonly DataGridView _grid;
    private readonly Label _status;
    private readonly Button _refresh;
    private readonly Button _terminate;
    private readonly ToolStripMenuItem _terminateMenuItem;
    private bool _busy;
    private bool _terminating;

    public ProcessDetailForm(ProcessResource resource, string? adapterLuid = null, string? adapterName = null, MemoryMetric? systemMemory = null)
    {
        _resource = resource;
        _adapterLuid = adapterLuid;
        _systemMemory = systemMemory;
        Text = resource switch
        {
            ProcessResource.Cpu => "Procesos: uso de CPU",
            ProcessResource.Memory => "Procesos: uso de memoria RAM",
            _ => string.IsNullOrWhiteSpace(adapterName) ? "Procesos: uso de GPU" : $"Procesos: {adapterName}"
        };
        AccessibleName = Text;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(760, 420);
        Size = new Size(1000, 600);
        KeyPreview = true;

        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AccessibleName = "Procesos ordenados por consumo",
            AccessibleDescription = "Tabla de procesos. Usa las flechas para recorrer filas y columnas. Pulsa Shift+F10 para abrir el menú contextual de la fila.",
            AccessibleRole = AccessibleRole.Table,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            AutoGenerateColumns = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowHeadersVisible = false,
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            TabIndex = 0
        };
        AddColumns();
        var contextMenu = new ContextMenuStrip();
        _terminateMenuItem = new ToolStripMenuItem("Finalizar proceso") { AccessibleName = "Finalizar proceso seleccionado" };
        _terminateMenuItem.Click += async (_, _) => await FinalizeSelectedProcessAsync();
        contextMenu.Items.Add(_terminateMenuItem);
        contextMenu.Opening += (_, _) => _terminateMenuItem.Enabled = CanFinalizeSelectedProcess();
        _grid.ContextMenuStrip = contextMenu;
        _grid.CellMouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Right || e.RowIndex < 0 || e.ColumnIndex < 0) return;
            _grid.CurrentCell = _grid.Rows[e.RowIndex].Cells[e.ColumnIndex];
            _grid.Rows[e.RowIndex].Selected = true;
        };

        _status = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            AccessibleName = "Estado de captura de procesos",
            Text = "Preparado para consultar procesos.",
            Padding = new Padding(8)
        };
        _refresh = new Button { Text = "Actualizar", AccessibleName = "Actualizar procesos", AutoSize = true, TabIndex = 1 };
        _refresh.Click += async (_, _) => await RefreshAsync();
        _terminate = new Button { Text = "Finalizar proceso", AccessibleName = "Finalizar proceso seleccionado", AutoSize = true, TabIndex = 2, Enabled = false };
        _terminate.Click += async (_, _) => await FinalizeSelectedProcessAsync();
        var close = new Button { Text = "Cerrar", AccessibleName = "Cerrar lista de procesos", AutoSize = true, TabIndex = 3 };
        close.Click += (_, _) => Close();

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(8) };
        buttons.Controls.AddRange([_refresh, _terminate, close]);
        _grid.SelectionChanged += (_, _) => UpdateTerminationAvailability();
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(8) };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(_grid, 0, 0);
        layout.Controls.Add(_status, 0, 1);
        layout.Controls.Add(buttons, 0, 2);
        Controls.Add(layout);

        Shown += async (_, _) => { await RefreshAsync(); if (_grid.Rows.Count > 0) _grid.Focus(); };
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape) { Close(); return true; }
        if (keyData == Keys.F5) { _ = RefreshAsync(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void AddColumns()
    {
        AddColumn("ProcessName", "Proceso", 1.6f);
        switch (_resource)
        {
            case ProcessResource.Memory:
                AddColumn("WorkingSet", "RAM usada por proceso", 1.0f);
                AddColumn("ProcessId", "ID del proceso (PID)", 0.9f);
                break;
            case ProcessResource.Cpu:
                AddColumn("Cpu", "CPU total", 0.75f);
                AddColumn("ProcessId", "ID del proceso (PID)", 0.9f);
                break;
            case ProcessResource.Gpu:
                AddColumn("GpuUsage", "Uso GPU", 0.65f);
                AddColumn("Dedicated", "Memoria dedicada", 1.0f);
                AddColumn("Shared", "Memoria compartida", 1.0f);
                AddColumn("ProcessId", "ID del proceso (PID)", 0.9f);
                AddColumn("Adapter", "Adaptador", 1.4f);
                AddColumn("Engine", "Motor más ocupado", 1.0f);
                break;
        }
    }

    private void AddColumn(string name, string title, float weight)
    {
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = name,
            HeaderText = title,
            FillWeight = weight,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
    }

    private async Task RefreshAsync()
    {
        if (_busy || IsDisposed) return;
        _busy = true;
        _refresh.Enabled = false;
        _terminate.Enabled = false;
        _status.Text = "Consultando procesos; la primera captura tarda aproximadamente un segundo…";
        try
        {
            var snapshot = await _provider.CaptureAsync(includeGpu: _resource == ProcessResource.Gpu);
            if (IsDisposed) return;
            Populate(snapshot);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            if (!IsDisposed) _status.Text = $"No se pudo actualizar la lista: {ex.Message}";
        }
        finally
        {
            _busy = false;
            if (!IsDisposed)
            {
                _refresh.Enabled = true;
                UpdateTerminationAvailability();
            }
        }
    }

    private void Populate(ProcessSnapshot snapshot)
    {
        _grid.Rows.Clear();
        var rows = snapshot.Processes.SelectMany(process =>
        {
            if (_resource != ProcessResource.Gpu)
                return new[] { (Process: process, Gpu: (ProcessGpuMetric?)null) };
            var gpus = process.Gpus.Where(gpu => _adapterLuid is null || gpu.Luid.Equals(_adapterLuid, StringComparison.OrdinalIgnoreCase)).ToArray();
            return gpus.Select(gpu => (Process: process, Gpu: (ProcessGpuMetric?)gpu));
        });

        rows = _resource switch
        {
            ProcessResource.Cpu => rows.OrderByDescending(row => row.Process.CpuPercent ?? -1),
            ProcessResource.Memory => rows.OrderByDescending(row => row.Process.WorkingSetBytes ?? 0),
            _ => rows.OrderByDescending(row => row.Gpu?.UtilizationPercent ?? -1).ThenByDescending(row => row.Gpu?.DedicatedUsedBytes ?? 0)
        };

        foreach (var row in rows)
        {
            var process = row.Process;
            switch (_resource)
            {
                case ProcessResource.Memory:
                    AddProcessRow(process, process.Name, Bytes(process.WorkingSetBytes), process.ProcessId);
                    break;
                case ProcessResource.Cpu:
                    AddProcessRow(process, process.Name, Percent(process.CpuPercent), process.ProcessId);
                    break;
                case ProcessResource.Gpu:
                    var gpu = row.Gpu!;
                    AddProcessRow(process, process.Name, Percent(gpu.UtilizationPercent), Bytes(gpu.DedicatedUsedBytes),
                        Bytes(gpu.SharedUsedBytes), process.ProcessId, gpu.AdapterName, EngineName(gpu.Engine));
                    break;
            }
        }
        UpdateTerminationAvailability();

        var summary = _resource switch
        {
            ProcessResource.Cpu => $"{_grid.Rows.Count} procesos. CPU se normaliza contra la capacidad total de los procesadores lógicos; el máximo mostrado es 100 por ciento.",
            ProcessResource.Memory => MemorySummary(_grid.Rows.Count),
            _ => $"{_grid.Rows.Count} filas de proceso y adaptador. Uso GPU es el motor más ocupado, sin sumar motores; memoria dedicada y compartida se muestran por separado."
        };
        if (string.IsNullOrWhiteSpace(snapshot.Error)) _status.Text = summary;
        else _status.Text = $"{summary} {snapshot.Error}. Los valores no disponibles no se sustituyen por cero.";
        if (_grid.Rows.Count == 0)
            _status.Text = $"No hay métricas por proceso disponibles para este recurso. {snapshot.Error ?? "Windows no expuso contadores atribuibles a procesos."}";
    }

    private static string Bytes(ulong? bytes) => bytes is ulong value ? $"{(value / GiB).ToString("0.00", Culture)} GiB" : "No disponible";
    private static string Percent(double? value) => value is double percent ? $"{percent.ToString("0.0", Culture)} por ciento" : "No disponible";
    private string MemorySummary(int rowCount)
    {
        var total = _systemMemory is MemoryMetric memory
            ? $"En todo el sistema: {Bytes(memory.UsedBytes)} usados de {Bytes(memory.TotalBytes)}. "
            : string.Empty;
        return $"{total}{rowCount} procesos. RAM usada por proceso es memoria residente e incluye páginas compartidas; por eso la suma por proceso puede diferir del total.";
    }

    private void AddProcessRow(ProcessMetric process, params object[] values)
    {
        var rowIndex = _grid.Rows.Add(values);
        _grid.Rows[rowIndex].Tag = process;
    }

    private ProcessMetric? SelectedProcess() => _grid.CurrentRow?.Tag as ProcessMetric;

    private bool CanFinalizeSelectedProcess() => SelectedProcess()?.StartTimeUtcTicks is not null && !_busy && !_terminating;

    private void UpdateTerminationAvailability()
    {
        if (IsDisposed) return;
        var process = SelectedProcess();
        _terminate.Enabled = process?.StartTimeUtcTicks is not null && !_busy && !_terminating;
        if (process is not null && process.StartTimeUtcTicks is null && !_busy && !_terminating)
            _status.Text = $"No se puede finalizar {process.Name} (ID {process.ProcessId}) porque Windows no permitió verificar su identidad.";
    }

    private async Task FinalizeSelectedProcessAsync()
    {
        if (_terminating || _busy) return;
        var process = SelectedProcess();
        if (process is null) return;
        if (process.StartTimeUtcTicks is not long startTime)
        {
            _status.Text = "Windows no permitió verificar cuándo inició el proceso; no se finalizará por seguridad.";
            return;
        }

        var answer = MessageBox.Show(this,
            $"¿Intentar cerrar {process.Name} (ID {process.ProcessId})? ASM primero solicitará un cierre normal. Si no responde, preguntará antes de forzar la finalización.",
            "Confirmar cierre de proceso", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
        if (answer != DialogResult.Yes) return;

        _terminating = true;
        UpdateTerminationAvailability();
        try
        {
            await FinalizeConfirmedProcessAsync(process, startTime);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            _status.Text = $"No se pudo completar la acción sobre {process.Name} (ID {process.ProcessId}): {ex.Message}";
        }
        finally
        {
            _terminating = false;
            UpdateTerminationAvailability();
        }
    }

    private async Task FinalizeConfirmedProcessAsync(ProcessMetric process, long startTime)
    {
        _status.Text = $"Intentando cerrar {process.Name} (ID {process.ProcessId})…";
        var closeResult = await ProcessTerminationService.TryCloseNormallyAsync(process.ProcessId, startTime);
        if (closeResult == ProcessTerminationResult.Closed || closeResult == ProcessTerminationResult.AlreadyExited)
        {
            _status.Text = closeResult == ProcessTerminationResult.Closed ? "El proceso se cerró normalmente." : "El proceso ya había terminado.";
            await RefreshAsync();
            _status.Text = $"{_status.Text} La lista ya está actualizada.";
            return;
        }
        if (closeResult == ProcessTerminationResult.IdentityChanged)
        {
            _status.Text = "El proceso terminó o el ID ahora pertenece a otro proceso. Actualiza la lista.";
            return;
        }
        if (closeResult == ProcessTerminationResult.AccessDenied)
        {
            await TryElevatedTerminationAsync(process, startTime);
            return;
        }
        if (closeResult == ProcessTerminationResult.Failed)
        {
            _status.Text = "Windows no permitió solicitar el cierre normal. Actualiza la lista e inténtalo de nuevo.";
            return;
        }

        var force = MessageBox.Show(this,
            $"{process.Name} (ID {process.ProcessId}) no se cerró normalmente. Forzarlo puede hacer que se pierdan datos sin guardar. ¿Deseas finalizarlo?",
            "Confirmar finalización forzada", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
        if (force != DialogResult.Yes)
        {
            _status.Text = "No se forzó la finalización del proceso.";
            return;
        }

        var forceResult = await ProcessTerminationService.TryForceNormallyAsync(process.ProcessId, startTime);
        if (forceResult == ProcessTerminationResult.Closed || forceResult == ProcessTerminationResult.AlreadyExited)
        {
            _status.Text = "El proceso finalizó.";
            await RefreshAsync();
            _status.Text = $"{_status.Text} La lista ya está actualizada.";
        }
        else if (forceResult == ProcessTerminationResult.AccessDenied)
            await TryElevatedTerminationAsync(process, startTime);
        else if (forceResult == ProcessTerminationResult.IdentityChanged)
            _status.Text = "El proceso terminó o el ID ahora pertenece a otro proceso; no se finalizó el nuevo proceso.";
        else
            _status.Text = "No se pudo finalizar el proceso. Puede estar protegido por Windows.";
    }

    private async Task TryElevatedTerminationAsync(ProcessMetric process, long startTime)
    {
        var answer = MessageBox.Show(this,
            $"Windows denegó el acceso a {process.Name} (ID {process.ProcessId}). ¿Deseas solicitar autorización de administrador para finalizar únicamente este proceso? ASM no se ejecutará elevado después.",
            "Se requiere autorización de Windows", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
        if (answer != DialogResult.Yes)
        {
            _status.Text = "No se solicitaron permisos elevados.";
            return;
        }

        _status.Text = "Esperando la autorización de Windows…";
        var result = await ProcessTerminationService.TryForceElevatedAsync(process.ProcessId, startTime);
        if (result == ProcessTerminationResult.Closed || result == ProcessTerminationResult.AlreadyExited)
        {
            _status.Text = "El proceso finalizó con autorización elevada.";
            await RefreshAsync();
            _status.Text = $"{_status.Text} La lista ya está actualizada.";
        }
        else if (result == ProcessTerminationResult.ElevationCancelled)
            _status.Text = "Cancelaste la solicitud de permisos de Windows; el proceso sigue activo.";
        else if (result == ProcessTerminationResult.IdentityChanged)
            _status.Text = "El proceso terminó o el ID cambió; el proceso nuevo no se tocó.";
        else
            _status.Text = "No se pudo finalizar el proceso, incluso con la autorización concedida. Windows puede protegerlo.";
    }

    private static string EngineName(string? engine)
    {
        if (engine is null) return "No disponible";
        var marker = "_engtype_";
        var typeIndex = engine.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        var engineStart = engine.IndexOf("_eng_", StringComparison.OrdinalIgnoreCase);
        if (typeIndex < 0 || engineStart < 0) return engine;
        var numberStart = engineStart + "_eng_".Length;
        var numberEnd = engine.IndexOf('_', numberStart);
        var number = numberEnd < 0 ? engine[numberStart..] : engine[numberStart..numberEnd];
        return $"{engine[(typeIndex + marker.Length)..]} (motor {number})";
    }
}
