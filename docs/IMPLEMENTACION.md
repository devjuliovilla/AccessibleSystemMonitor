# Implementación

## Estructura sugerida
- ASM.sln (o .slnx)
- src/ASM: net10.0-windows, UseWindowsForms=true, WinExe, x64.
- src/ASM.Core: modelos de snapshot, contratos de proveedores, formateador puro y lógica comprobable.
- tests/ASM.Tests: xUnit.
- docs: documentación y resultados.

ApplicationContext propietario de NotifyIcon y ventana; controlar cierre explícito con bandera de salida. Mutex por sesión e IPC local para segunda instancia. IDisposable para contadores, handles, icono, hotkey y cancelación. Evitar async void salvo handlers con manejo de excepciones. Configuración JSON local con valores seguros si corrupta.

## Proveedores de métricas
- RAM: GlobalMemoryStatusEx, utilizada = total físico menos disponible físico. Explicar que puede diferir del Administrador de tareas. Usar ulong y comprobar retorno.
- CPU: GetSystemTimes; delta de kernel+user, restar idle (kernel incluye idle); validar delta y primera muestra. En sistemas con múltiples grupos de procesadores considerar contador Processor Information (_Total) para cobertura completa.
- Discos: DriveInfo para unidades fijas listas; capturar excepciones por unidad. TotalFreeSpace para espacio libre del volumen, documentar semántica. Actividad mediante PDH o PerformanceCounter; no confundir porcentaje de tiempo activo con porcentaje de espacio. No atribuir actividad física a una letra sin mapeo válido. Etiquetar disco físico por separado si necesario.
- GPU: enumerar adaptadores mediante DXGI, excluir adaptadores software, asociar contadores GPU Engine y GPU Adapter Memory por LUID. Usar nombres de contadores ingleses vía PdhAddEnglishCounterW o resolver nombres localizados. Contemplar instancias dinámicas y contadores ausentes. Agrupar por motor físico, sumar cargas de procesos del mismo motor cuando corresponda y tomar motor más ocupado como utilización global aproximada (no sumar porcentajes de motores distintos). Documentar método y límites. Obtener capacidad dedicada de DXGI; no presentar QueryVideoMemoryInfo CurrentUsage como consumo global, pues corresponde al proceso. Usar contadores globales de memoria de adaptador cuando existan. Para iGPU distinguir memoria dedicada/reservada de compartida. NVML opcional solo si necesario, no obligatorio.
- Usar dos muestras separadas aproximadamente 500–1000 ms para tasas; ejecutar fuera del hilo UI, cancelar al ocultar, impedir muestreos simultáneos y descartar resultados obsoletos. No activar GPU dedicada deliberadamente para medirla. Registrar limitaciones si una API despierta el adaptador.
- Usar bytes internamente; presentar GiB o GB con conversión explícita y consistente. Redondear solo al formatear. Valores desconocidos nullable, no cero. Snapshot inmutable con timestamp y estado por métrica.

## Etapas
1. Crear solución, UI nativa, bandeja, menú, hotkey, instancia única y cierre/ocultamiento. Datos ficticios solo en pruebas.
2. Proveedores CPU, RAM, discos; formateador y pruebas unitarias.
3. Proveedor GPU y validación en equipo híbrido AMD/NVIDIA. Investigar APIs si contadores no disponibles; no sustituir con números inventados.
4. Integración de muestreo asíncrono, cancelación, configuración e inicio automático opt-in.
5. Validación teclado/NVDA, DPI, errores, recursos y comparación con herramientas Windows.
6. Publicación portátil self-contained win-x64, sin trimming inicialmente. Intentar single-file si funciona; si no, entregar carpeta portable. Documentar tamaño y dependencias.

## Comandos orientativos
 dotnet restore
 dotnet build -c Release
 dotnet test -c Release
 dotnet publish src/ASM/ASM.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false

## Referencias técnicas para consultar durante implementación
- https://learn.microsoft.com/dotnet/api/system.windows.forms.notifyicon
- https://learn.microsoft.com/windows/win32/api/sysinfoapi/nf-sysinfoapi-globalmemorystatusex
- https://learn.microsoft.com/windows/win32/api/processthreadsapi/nf-processthreadsapi-getsystemtimes
- https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-registerhotkey
- https://learn.microsoft.com/windows/win32/api/pdh/nf-pdh-pdhaddenglishcounterw
- https://learn.microsoft.com/windows/win32/api/dxgi1_4/nf-dxgi1_4-idxgiadapter3-queryvideomemoryinfo
- https://learn.microsoft.com/dotnet/api/system.io.driveinfo
