# Criterios de aceptación
Registrar cada caso como aprobado, fallido o pendiente, con entorno y evidencia. No afirmar resultados no ejecutados.

## Manuales
- [ ] (Pendiente) Inicia oculta; Windows+B permite localizar y activar icono con NVDA; menú contextual accesible.
- [ ] Clic izquierdo y hotkey muestran ventana; foco en resumen y lectura desde inicio.
- [ ] NVDA lee línea a línea y continuamente; no anuncia valores repetidamente.
- [ ] Tab y Shift+Tab recorren controles; F5 actualiza; copiar funciona.
- [ ] Escape, X, botón Cerrar y Alt+F4 ocultan sin terminar; Salir elimina icono y proceso.
- [ ] Reabrir 20 veces sin iconos duplicados ni crecimiento sostenido de handles/memoria.
- [ ] Segunda instancia abre la existente; conflicto de hotkey no provoca cierre.
- [ ] Alto contraste, 100/150/200% DPI, varios monitores y barra de tareas en diferentes posiciones.
- [ ] Sin administrador ni Internet; inicio automático desactivado por defecto y reversible.
- [ ] Detecta ambas GPU en equipo híbrido AMD Radeon 780M / NVIDIA RTX 5060 Laptop 8 GB; no confunde VRAM y RAM compartida.
- [ ] Comparar CPU, RAM, discos y GPU con Administrador de tareas en reposo y bajo carga normal; documentar diferencias de intervalo y definición. No exigir igualdad instantánea.
- [ ] Fallo de un contador, disco inaccesible, GPU ausente o configuración corrupta no bloquean la aplicación.
- [ ] Oculto no realiza muestreo periódico; medir CPU y memoria en reposo durante 60 segundos. Objetivo orientativo: CPU promedio menor a 0.5%, memoria privada menor a 100 MB; documentar valores reales, no falsearlos.

## Automatizadas
- [x] Cálculos de RAM, CPU por deltas y porcentajes; delta cero, datos ausentes.
- [x] Formateador español, valores desconocidos y cero real.
- [x] Agrupación de motores GPU por adaptador y motor; evitar doble conteo; adaptadores distintos y contadores ausentes.
- [ ] Cancelación, errores parciales, resultados obsoletos, concurrencia de actualización.
- [x] Captura real de CPU/RAM/discos y representación explícita de métricas no disponibles (prueba en Windows).
- [ ] Configuración por defecto y recuperación de JSON inválido.

## Entrega
README actualizado, comandos reproducibles, código y pruebas, binario portable si entorno Windows disponible, reporte de resultados y limitaciones. Si el agente trabaja fuera de Windows, indicar que ejecución, GPU y NVDA quedan pendientes de validar en Windows.
