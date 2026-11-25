# Instrucciones para el agente
Implementa Accessible System Monitor (ASM), aplicación Windows 11 x64, C# .NET 10 Windows Forms. Lee README.md y todos los documentos de docs antes de escribir código. El objetivo es entregar una aplicación funcional, no únicamente un prototipo visual.

- Prioriza accesibilidad, exactitud de métricas y simplicidad. No uses Electron, MAUI, web, drivers ni servicios externos.
- Usa controles nativos, sin privilegios administrativos, telemetría ni red. No instales dependencias globales ni modifiques el registro sin autorización; el inicio automático será opt-in del usuario.
- Mantén separación sencilla entre UI, ciclo de vida y proveedores de métricas. Evita frameworks de arquitectura innecesarios.
- Implementa por etapas según docs/IMPLEMENTACION.md. Ejecuta build y pruebas en cada etapa; documenta fallos y limitaciones. No declares verificada la accesibilidad sin prueba real con NVDA.
- No inventes datos: distingue cero, no disponible y error. No ocultes errores de GPU ni sumes motores indiscriminadamente.
- Entrega código, pruebas, instrucciones de compilación/publicación, lista de dependencias y reporte de validación. No publiques en GitHub ni realices acciones externas sin permiso.
