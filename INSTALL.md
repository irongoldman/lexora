# Instalación en Windows

1. Abre `dist\Instalar-Lexora.exe`. Lee las pestañas **Licencia MIT** y **Términos y descargo**. Marca **He leído y acepto...** para habilitar **Instalar**. Si no aceptas, pulsa **No acepto y salir**: se cerrará sin modificar la instalación existente ni instalar archivos. También puedes cerrar con la X.
   Antes de instalar, puedes marcar **Iniciar Léxora con Windows (en la bandeja)**. En una instalación nueva está desmarcado; al actualizar conserva la elección existente.
2. Abre **Léxora** desde el escritorio o el menú Inicio.
3. Selecciona el idioma de destino en la aplicación. La configuración se conserva entre sesiones.
4. Selecciona texto en una página o documento. Aparecerá un pequeño icono azul junto a la selección: púlsalo para traducir. También puedes mantener Ctrl pulsado y presionar C dos veces seguidas (Ctrl + C + C).
5. Para sustituirlo en un documento editable compatible, pulsa **Reemplazar en documento**. Debe mantenerse la selección original. Si no puede comprobarse, el botón se deshabilita o cancela la operación. Puedes deshacer en el documento con **Ctrl+Z**; la traducción queda copiada.

El instalador copia la aplicación en `%LOCALAPPDATA%\SeleccionTraductor`, solo para el usuario actual. No requiere permisos de administrador ni instala servicios. Está destinada a Windows 10/11 con .NET Framework 4.8 y conexión a Internet para traducir.

Solo necesitas distribuir `Instalar-Lexora.exe`. Contiene todo lo necesario para instalar `SeleccionTraductor.exe`, `Lexora.ico` y `Desinstalar.exe` en esa carpeta y crear los accesos directos. Los nombres internos se mantienen para conservar tus preferencias al actualizar.

También incluye `LICENSE` (MIT), `TERMS.txt` y `SourceCode.zip`, que se guardan en la carpeta de instalación. El ZIP contiene el código fuente y los scripts de compilación correspondientes. Cada instalación o actualización requiere aceptación explícita; la desinstalación no la requiere. Al desinstalar se retiran también estos tres archivos conocidos, conservando la preferencia de idioma.

## Inicio con Windows

La opción es por usuario. Cuando está activada, Windows ejecuta Léxora con `--tray` al iniciar sesión: se mantiene en la bandeja sin abrir la ventana, con el atajo y el icono de selección activos. Puedes cambiar la opción después desde **Iniciar con Windows (en la bandeja)** en el menú de la bandeja.

## Actualizar

En la X elige **Cerrar aplicación**, o pulsa **Salir** en el menú de la bandeja. Ejecuta el nuevo instalador. Conserva la configuración y sustituye los accesos directos de la versión anterior que apuntan a esta misma instalación. Si Windows bloquea archivos en uso, cierra la aplicación y repite la instalación.

## Desinstalar

Abre **Configuración > Aplicaciones > Aplicaciones instaladas > Léxora > Desinstalar**. También puedes hacer doble clic en `%LOCALAPPDATA%\SeleccionTraductor\Desinstalar.exe`. Si tienes un desinstalador antiguo, abre el instalador nuevo y pulsa **Desinstalar**: no necesitas actualizar primero. La desinstalación retira también la entrada de inicio automático si pertenece a esta instalación.

Si Léxora sigue abierta, se ofrece **finalizar sus procesos y continuar**. Elegir **No** cancela sin eliminar archivos. Si aceptas, se cierran únicamente los procesos de esa instalación en tu sesión; los textos no guardados en Léxora se pierden. Tus documentos externos no se cierran. Si Windows sigue bloqueando archivos, la eliminación no comienza y se muestra un mensaje explicativo.

Solo se eliminan los ejecutables conocidos, el icono, los accesos directos que apuntan a esta aplicación y su entrada de desinstalación. La configuración se conserva. El desinstalador se ejecuta desde una copia temporal para poder eliminarse; Windows puede conservar esa copia en la carpeta temporal hasta su limpieza habitual.

El instalador incluye `Lexora.ico` y lo asigna a los accesos directos y a Aplicaciones instaladas. Al actualizar se solicita a Windows refrescar los iconos. El desinstalador elimina también este archivo de icono.

## Compilar desde el código

En PowerShell, desde la carpeta del proyecto:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

El script utiliza el compilador de .NET Framework incluido en Windows (`C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`). Compila la aplicación e incrusta el ejecutable y el icono en el instalador. No instala ni ejecuta la aplicación. En `dist` deja únicamente `Instalar-Lexora.exe` y elimina los archivos intermedios de su carpeta temporal.

Los ejecutables no están firmados digitalmente; Windows puede mostrar un aviso de editor desconocido.
