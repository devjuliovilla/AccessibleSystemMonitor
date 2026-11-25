# Reporte de implementación y validación

La versión de trabajo 0.2.0 está implementada en `feature/process-usage`; no se ha publicado. Build y pruebas automatizadas aprobados: `dotnet build ASM.sln -c Release` (0 advertencias, 0 errores), `dotnet test ASM.sln -c Release` (13/13). Pruebas manuales con NVDA, menú contextual, UAC y validación de atribución VRAM en equipo híbrido siguen pendientes.

## Etapa 1 — estructura y ciclo de vida

- **Estado:** implementada como base funcional.
- **Entorno de compilación:** Windows x64, SDK .NET 11.0.100 con runtime .NET 10 instalado. El proyecto apunta a `net10.0-windows`.
- **Comando ejecutado:** `dotnet build ASM.sln -c Release`.
- **Resultado:** aprobado; 0 advertencias y 0 errores.
- **Incluye:** solución x64, aplicación WinForms, ventana de resumen nativa, menú en bandeja, ocultar con Escape/X/Alt+F4, combinación Ctrl+Alt+M, mutex por usuario e indicador local para solicitar mostrar la instancia existente.
- **Datos en esa etapa:** todavía no había métricas; las etapas 2 y 3 añadieron proveedores reales de CPU, RAM, discos y GPU.

## Validación pendiente

- Prueba interactiva de bandeja, hotkey, segunda instancia, teclado, DPI y cierre en Windows.
- Prueba auditiva real con NVDA; no se declara accesibilidad verificada.
- Validación GPU en equipo híbrido AMD/NVIDIA, muestreo automático/configuración y publicación portable, siguiendo `docs/IMPLEMENTACION.md`.

## Limitaciones actuales

- Los elementos «Iniciar con Windows» y «Configuración» son marcadores informativos; todavía no cambian configuración.
- La actividad de disco no se reporta: `DriveInfo` informa capacidad/libre del volumen, no actividad.
- No se ha validado en equipos híbridos AMD/NVIDIA ni comparado con el Administrador de tareas.

## Etapa 2 — CPU, memoria y volúmenes

- **Estado:** implementada.
- **CPU:** dos llamadas a `GetSystemTimes` separadas por 650 ms; el delta de kernel+usuario menos idle determina utilización. Primera muestra/delta inválido queda no disponible.
- **RAM:** `GlobalMemoryStatusEx`; usada = total físico - disponible físico. Presentación en GiB; puede diferir de Administrador de tareas.
- **Almacenamiento:** unidades fijas accesibles mediante `DriveInfo`; los errores y unidades no listas se registran por separado. Se muestran capacidad, usado, libre y porcentaje del volumen.
- **Interfaz:** captura al mostrar, botón Actualizar y tecla F5, sin bloquear el hilo de interfaz; mantiene una sola captura a la vez. Copiar conserva el resumen completo.
- **Pruebas en esa etapa:** `dotnet test ASM.sln -c Release` aprobado: 7/7 en ese momento. Incluye cálculos, cero vs. no disponible, formato y captura nativa en Windows.
- **Build completo:** `dotnet build ASM.sln -c Release` aprobado: 0 advertencias y 0 errores.

## Etapa 3 — GPU

- **Estado:** proveedor implementado; validación en el equipo híbrido de aceptación pendiente.
- **Adaptadores:** enumeración DXGI, descripción y capacidad de memoria dedicada; se excluyen adaptadores software.
- **Utilización:** contadores `GPU Engine\Utilization Percentage` obtenidos mediante PDH con ruta inglesa y expansión localizada de instancias. Suma cargas de procesos por motor físico y reporta el motor más ocupado por adaptador; no suma motores distintos.
- **Memoria:** contadores `GPU Adapter Memory\Dedicated Usage` y `Shared Usage`, asociados por LUID. La memoria compartida se etiqueta separada de VRAM dedicada.
- **Fallos:** contador ausente, métrica parcialmente disponible y adaptador sin medición quedan señalados, sin convertirlos en ceros. Los errores PDH se muestran en el resumen.
- **Pruebas:** pruebas unitarias de agrupación/anti-doble-conteo y disponibilidad faltante. La prueba de captura del proveedor también pasa en Windows; eso no sustituye la comparación requerida en GPU AMD/NVIDIA.
- **Límites:** los contadores pueden no existir en drivers o versiones de Windows y el uso es aproximado, basado en los motores expuestos por Windows. La capacidad DXGI indica memoria dedicada del adaptador; en una iGPU puede ser desconocida/cero y el uso compartido no representa VRAM.

## Publicación portable

- `tools/Publish-Portable.ps1` genera `artifacts/ASM-portable-0.2.0-preview2/ASM.exe` para Windows x64, autocontenido, de un solo archivo, sin trimming y con icono ASM incrustado.
- La salida fue generada y comprobada como un único archivo ejecutable en la carpeta portable. No se ha realizado todavía una prueba manual de apertura en el escritorio.
- Al iniciar manualmente sin argumentos, abre la ventana. El argumento `--background` inicia oculto en la bandeja.

## Versión 0.2.0 — procesos por recurso

- **Interacción:** clic en un renglón o foco sobre CPU/RAM/GPU y Enter abre la tabla correspondiente. Escape cierra el diálogo y devuelve foco; F5 actualiza explícitamente.
- **CPU:** `Process.TotalProcessorTime` entre muestras; porcentaje normalizado por número de procesadores lógicos y limitado a 100% de capacidad total.
- **RAM:** muestra el total físico usado del sistema y `WorkingSet64` por proceso (residente; incluye páginas compartidas). Orden inicial por working set.
- **GPU:** los contadores GPU Engine se vinculan por PID/LUID; agrega procesos del mismo motor y toma el motor más ocupado. Consulta adicional `GPU Process Memory` para memoria dedicada/compartida por PID si el sistema la proporciona. Vista filtrada por adaptador al activar una línea GPU.
- **Límites:** procesos que terminan durante la captura o no son accesibles pueden faltar o tener celdas «No disponible». Memoria residente por proceso puede contar páginas compartidas y no sumará igual que RAM total. Los contadores VRAM por proceso dependen del driver/Windows; no se sustituyen por `QueryVideoMemoryInfo`, que refleja el proceso que consulta.
- **Accesibilidad:** tabla WinForms nativa, encabezados accesibles, teclado, captura bajo demanda; debe probarse con NVDA real.
- **Finalización:** botón y menú contextual actúan sobre la fila seleccionada; ASM intenta `CloseMainWindow`, solicita confirmación adicional antes de forzar, y usa un modo auxiliar del ejecutable con UAC solo si Windows deniega la operación. El modo elevado valida PID y hora de inicio, no carga la UI ni el mutex y solo termina el PID seleccionado, nunca el árbol de procesos. Pruebas manuales de permisos/procesos protegidos pendientes.
