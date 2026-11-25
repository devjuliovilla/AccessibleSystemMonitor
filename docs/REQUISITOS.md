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
- Activar con clic o Enter los renglones de CPU, RAM y GPU para consultar procesos ordenados por consumo. La vista de procesos se recorre con teclado/NVDA, se cierra con Escape y permite actualizar con F5.
- Por proceso: CPU normalizada al máximo de 100% de la capacidad total; RAM residente (working set); GPU por PID/adaptador/motor; memoria dedicada y compartida por proceso cuando PDH las exponga.
- La tabla RAM ordena columnas como Proceso, RAM usada por proceso e ID del proceso (PID). CPU: Proceso, CPU total, PID. GPU: Proceso, Uso GPU, Memoria dedicada, Memoria compartida, PID, Adaptador y Motor más ocupado. No mostrar tabla por proceso de disco en esta versión.
- Finalizar proceso desde botón o menú contextual de la fila, siempre con confirmación. Intentar cierre normal primero; si no responde, solicitar confirmación de terminación forzada. Solicitar UAC solo si Windows deniega la operación; ASM no se ejecuta elevado durante el uso normal. Validar identidad por PID y hora de inicio antes de terminar; no finalizar árbol de procesos.
- No exigir que la suma por proceso coincida con la memoria global. Las páginas compartidas, caché, memoria de kernel, contadores no disponibles y procesos protegidos/inaccesibles pueden causar diferencias. No elevar privilegios.

## Fuera del MVP
Historial, gráficas, alertas, temperaturas, instalador, soporte remoto y control de hardware. Barras visuales opcionales después de validar el resumen accesible.
