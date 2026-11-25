# Accessible System Monitor (ASM)
Aplicación nativa de Windows para consultar CPU, RAM, almacenamiento y GPU mediante teclado, NVDA o mouse.

## Objetivo
Consultar CPU, RAM, almacenamiento y GPU rápidamente mediante teclado, NVDA o mouse. Aplicación local residente en la bandeja del sistema, con ventana pequeña sobre la barra de tareas.

## Estado
Implementadas las etapas 1 a 3: ventana accesible base, bandeja, atajo global Ctrl+Alt+M, instancia única y métricas de CPU, RAM, volúmenes fijos y GPU. Al abrir `ASM.exe` manualmente, la ventana aparece; con `--background` inicia oculta para ejecución automática. La GPU requiere validación manual en equipos híbridos.

## Compilar
Requisitos: Windows 11 x64 y SDK .NET 10.
La aplicación no añade dependencias externas de ejecución; usa Windows Forms y APIs nativas de Windows. El proyecto de pruebas usa xUnit y Microsoft.NET.Test.Sdk.

```powershell
dotnet build ASM.sln -c Release
dotnet test ASM.sln -c Release
```

La aplicación se inicia con el resumen visible al hacer doble clic. Usa `--background` para iniciarla oculta en la bandeja.

## Publicación portable

Ejecuta `tools\Publish-Portable.ps1`. Genera un único `ASM.exe` autocontenido para Windows 11 x64 en `artifacts\ASM-portable\`; no requiere instalar .NET. Copia ese ejecutable a la computadora de destino y haz doble clic.

El icono de la aplicación se guarda en `assets\ASM.ico`; se puede regenerar con `tools\Generate-Icon.ps1`.

## Documentación
- install-instructions.md: uso portable e inicio automático con Windows.
- docs/REQUISITOS.md: comportamiento y alcance.
- docs/ACCESIBILIDAD.md: interacción y criterios NVDA.
- docs/IMPLEMENTACION.md: arquitectura, métricas y etapas.
- docs/PRUEBAS.md: criterios de aceptación.
- docs/RESULTADOS.md: etapa implementada, compilación y validaciones pendientes.

Nombre provisional: ASM. Idioma inicial: español de México. No se requiere instalador para el MVP.
