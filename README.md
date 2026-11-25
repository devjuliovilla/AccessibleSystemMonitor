# Accessible System Monitor (ASM)
Aplicación nativa de Windows para consultar CPU, RAM, almacenamiento y GPU mediante teclado, NVDA o mouse.

## Objetivo
Consultar CPU, RAM, almacenamiento y GPU rápidamente mediante teclado, NVDA o mouse. Aplicación local residente en la bandeja del sistema, con ventana pequeña sobre la barra de tareas.

## Estado
Versión de trabajo 0.2.0: incluye resumen de CPU/RAM/discos/GPU y tablas de procesos al activar las líneas de CPU, RAM o GPU con clic o Enter. Las tablas permiten finalizar el proceso seleccionado mediante botón o menú contextual, con confirmación; UAC solo se solicita si Windows deniega el acceso. Al abrir `ASM.exe` manualmente, la ventana aparece; con `--background` inicia oculta. La atribución VRAM por proceso y la validación manual con NVDA/equipo híbrido siguen pendientes.

## Compilar
Requisitos: Windows 11 x64 y SDK .NET 10.
La aplicación no añade dependencias externas de ejecución; usa Windows Forms y APIs nativas de Windows. El proyecto de pruebas usa xUnit y Microsoft.NET.Test.Sdk.

```powershell
dotnet build ASM.sln -c Release
dotnet test ASM.sln -c Release
```

La aplicación se inicia con el resumen visible al hacer doble clic. Usa `--background` para iniciarla oculta en la bandeja.

## Publicación portable

Ejecuta `tools\Publish-Portable.ps1`. Genera un único `ASM.exe` autocontenido para Windows 11 x64 en `artifacts\ASM-portable-0.2.0-preview2\`; no requiere instalar .NET. Copia ese ejecutable a la computadora de destino y haz doble clic.

El icono de la aplicación se guarda en `assets\ASM.ico`; se puede regenerar con `tools\Generate-Icon.ps1`.

## Documentación
- install-instructions.md: uso portable e inicio automático con Windows.
- docs/REQUISITOS.md: comportamiento y alcance.
- docs/ACCESIBILIDAD.md: interacción y criterios NVDA.
- docs/IMPLEMENTACION.md: arquitectura, métricas y etapas.
- docs/PRUEBAS.md: criterios de aceptación.
- docs/RESULTADOS.md: etapa implementada, compilación y validaciones pendientes.

Nombre provisional: ASM. Idioma inicial: español de México. No se requiere instalador para el MVP.
