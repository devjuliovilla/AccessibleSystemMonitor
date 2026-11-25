# Instalación y uso de ASM Portable

ASM es una aplicación portable: no requiere instalador ni conexión a Internet.

## Requisitos

- Windows 11 de 64 bits.
- No es necesario instalar .NET; el ejecutable portable incluye el runtime.

## Abrir ASM

1. Copia `ASM.exe` desde la carpeta `artifacts\ASM-portable` a una ubicación permanente, por ejemplo `Documentos\ASM`.
2. Haz doble clic en `ASM.exe`.

La ventana del monitor se abrirá. ASM también aparecerá en el área de notificación. Si ya está abierto, ejecutar el archivo otra vez mostrará la instancia existente.

## Iniciar ASM automáticamente con Windows

Esto configura el inicio automático solo para tu cuenta de Windows y se puede deshacer eliminando el acceso directo.

1. Deja `ASM.exe` en su ubicación permanente. No crees el acceso directo dentro de una carpeta temporal.
2. Pulsa **Windows+R**, escribe `shell:startup` y presiona **Enter**. Se abrirá la carpeta de inicio de tu usuario.
3. En una ventana del Explorador, busca `ASM.exe`.
4. Haz clic derecho en `ASM.exe` y selecciona **Mostrar más opciones** si aparece el menú compacto. Elige **Crear acceso directo**. Si Windows ofrece crear el acceso directo en el escritorio porque no puede hacerlo en esa carpeta, acepta y luego mueve ese acceso directo a la carpeta de inicio abierta en el paso 2.
5. En la carpeta de inicio, abre las propiedades del acceso directo creado. En el campo **Destino**, conserva la ruta entre comillas y agrega un espacio seguido de `--background` después de las comillas. Por ejemplo:

   ```text
   "C:\Users\Ana\Documents\ASM\ASM.exe" --background
   ```

6. Confirma con **Aplicar** y **Aceptar**.

Al iniciar sesión, ASM se iniciará oculto en la bandeja. Puedes abrir la ventana desde el icono de ASM o con **Ctrl+Alt+M**. Si Windows avisa que el atajo ya está en uso, el icono de bandeja sigue disponible.

## Quitar ASM del inicio automático

1. Pulsa **Windows+R**, escribe `shell:startup` y presiona **Enter**.
2. Elimina el acceso directo de ASM de esa carpeta. Esto no elimina el ejecutable ni tus otros archivos.

## Cerrar ASM

Cerrar la ventana la oculta y deja ASM en la bandeja. Para terminar la aplicación, abre el menú contextual del icono de bandeja y selecciona **Salir**.

## Publicar de nuevo la versión portable

Desde la carpeta del proyecto, ejecuta `tools\Publish-Portable.ps1`. El ejecutable actualizado se generará en `artifacts\ASM-portable\ASM.exe`. Si ya tienes un acceso directo de inicio automático, comprueba que siga apuntando a la ubicación donde copiaste el ejecutable.
