# Accesibilidad
- Usar TextBox multilínea nativo de solo lectura, seleccionable, con scroll, AccessibleName «Resumen de recursos del sistema». Un párrafo por recurso, texto completo, sin depender de colores ni barras. Usar nombres y roles nativos de UI Automation/MSAA.
- Al mostrar ventana, enfocar resumen y posicionar cursor al inicio; no seleccionar todo. Permitir flechas, Ctrl+Home/End, navegación por palabras, selección y lectura continua NVDA+Flecha abajo (distribución escritorio).
- Orden Tab: resumen, Actualizar, Copiar, Cerrar. Shift+Tab inverso. Escape desde cualquier control oculta. Menús y configuración navegables por teclado, nombres inequívocos, foco visible.
- Evitar anuncios repetitivos, cambios de foco y reemplazo de texto mientras se lee. F5 explícito puede reiniciar el cursor al inicio; comunicar estado de actualización sin diálogos intrusivos. No integrar API privada de NVDA.
- Los renglones Procesador, Memoria RAM y GPU aceptan clic y Enter. La descripción accesible del resumen anuncia la interacción; al cerrar la tabla de procesos, devolver el foco al resumen. La tabla usa encabezados nativos y orden inicial por consumo descendente.
- La vista de procesos permite F5 para una captura nueva y Escape para cerrarla. No actualizar mientras se leen filas; cada refresco es explícito.
- El botón y el menú contextual «Finalizar proceso» actúan sobre la fila seleccionada, presentan confirmación y comunican el resultado. El menú se puede abrir con Shift+F10; la elevación se solicita solo ante acceso denegado.
- Respetar alto contraste, escala de texto, DPI y tema del sistema. No dibujar controles personalizados. No depender de hover. Permitir desplazamiento con muchas unidades.
- Copiar resumen completo al portapapeles con botón; Ctrl+C copia selección. Confirmación accesible breve, no modal.

## Ejemplo de texto (datos ficticios)
Actualizado a las 10:41.
Procesador: 12 por ciento de utilización.
Memoria RAM: 18.4 GB utilizados de 32 GB. 13.6 GB disponibles. 57.5 por ciento en uso.
Disco C: 519 GB libres de 931 GB. Actividad: 3 por ciento.
GPU NVIDIA RTX 5060: utilización 24 por ciento. Memoria dedicada: 3.2 GB utilizados de 8 GB.
GPU AMD Radeon 780M: utilización 8 por ciento. Memoria compartida: no disponible.

Validar con NVDA real; inspección UI Automation no sustituye prueba auditiva. Windows+B debe permitir localizar el icono, incluso si Windows lo coloca en iconos ocultos. No modificar preferencias de bandeja del usuario.
