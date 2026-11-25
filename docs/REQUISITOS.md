# Requisitos funcionales

## Ciclo de vida
- Una instancia por sesión de usuario; iniciar oculto con icono y tooltip «Monitor del sistema» en bandeja. Segunda ejecución solicita mostrar la instancia existente (IPC local), sin duplicarla.
- Clic izquierdo o activación por teclado del icono muestra ventana. Menú contextual nativo: Abrir monitor, Iniciar con Windows (marcado si activo), Configuración, Salir.
- Atajo global propuesto Ctrl+Alt+M mediante RegisterHotKey; configurable, reportar conflicto sin impedir el uso por bandeja. Liberarlo al salir.
- Ventana no modal, tamaño compacto inicial aproximado 420 × 500 DIP, ajustable según DPI y contenido; esquina inferior derecha del área de trabajo del monitor activo, sin cubrir barra de tareas. No aparecer en barra de tareas. No ocultar automáticamente al perder foco.
- Escape, botón Cerrar, X y Alt+F4 ocultan; solo Salir del menú termina normalmente el proceso. Permitir cierre por apagado de Windows. Restaurar foco previo cuando sea posible sin forzarlo indebidamente.
- Inicio automático opcional, desactivado por defecto, por usuario. No elevar permisos. Configuración local en LocalAppData.

## Datos
- CPU: porcentaje total.
- RAM: total, utilizada, disponible, porcentaje.
- Almacenamiento: todas las unidades locales fijas accesibles; letra, capacidad, utilizado, libre y porcentaje; actividad de disco por disco físico o volumen si hay contador fiable, indicando alcance.
- GPU: detectar adaptadores físicos integrados y dedicados; nombre, utilización, memoria dedicada utilizada y capacidad si conocida, memoria compartida utilizada si disponible. No confundir memoria compartida con VRAM.
- Resumen de texto en español, fecha/hora de captura; botones Actualizar, Copiar y Cerrar. F5 actualiza. Ctrl+A/C funciona en resumen.
- Capturar al abrir, mantener texto estable mientras se lee. Actualización manual por defecto; opción automática solo si no altera la lectura (si no puede garantizarse, posponerla). No muestrear continuamente cuando oculto.
- Si una métrica falla, mostrar «No disponible» y conservar las demás. No bloquear UI durante muestreo.

## Fuera del MVP
Historial, gráficas, alertas, temperaturas, procesos individuales, instalador, soporte remoto y control de hardware. Barras visuales opcionales después de validar el resumen accesible.
