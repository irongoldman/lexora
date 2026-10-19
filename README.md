# Léxora para Windows

Aplicación nativa para Windows 10/11, con .NET Framework 4.8, sin instalar Node, Python ni servicios.

## Descargar e instalar

Descarga **Instalar-Lexora.exe** desde la [última versión publicada](https://github.com/irongoldman/lexora/releases/latest). Es el único archivo necesario para instalar. Los ZIP de «Source code» de GitHub contienen el proyecto para desarrolladores, no el instalador.

En cada publicación se facilita el SHA-256 del instalador para comprobar la descarga. Los ejecutables todavía no tienen firma digital. Antes de actualizar, cierra Léxora desde **Salir** en la bandeja; las preferencias se conservan.

Puedes comunicar errores y proponer mejoras en [Issues](https://github.com/irongoldman/lexora/issues). Incluye tu versión de Windows, versión de Léxora y los pasos para reproducir el problema; evita adjuntar textos personales o confidenciales.

Software gratuito y de código abierto bajo [licencia MIT](LICENSE). Los [términos y el descargo](TERMS.txt) se muestran en el instalador junto con la licencia. Debes marcar la aceptación para habilitar **Instalar**; **No acepto y salir** cierra sin instalar ni actualizar. La desinstalación no exige aceptación.

El instalador único incluye el código fuente correspondiente (`SourceCode.zip`), licencia y términos, que se copian en `%LOCALAPPDATA%/SeleccionTraductor`. No depende de un repositorio público para acceder al código. Extrae el ZIP en una carpeta de trabajo y ejecuta `build.ps1` con PowerShell para compilarlo; requiere Windows y el compilador de .NET Framework indicado en el script. El ZIP no contiene archivos personales, binarios de entrega ni credenciales.

## Uso

1. Ejecuta el único archivo de entrega, `dist/Instalar-Lexora.exe`. Consulta la licencia y los términos, marca su aceptación y, si lo deseas, **Iniciar Léxora con Windows (en la bandeja)**; pulsa **Instalar**. Incluye aplicación, icono, desinstalador, documentos legales y código fuente. Después puedes abrir el acceso directo **Léxora**.
2. Elige el idioma de destino una vez. Se conserva en tu perfil de Windows al cerrar y volver a abrir la aplicación.
3. Selecciona texto en una página, un documento u otra aplicación. Pulsa el pequeño icono azul que aparece junto a la selección.
4. Como alternativa, mantén **Ctrl** pulsado y presiona **C dos veces** en menos de 850 ms. Suelta Ctrl para abrir la traducción. Un Ctrl+C simple continúa copiando normalmente.
5. El botón **Borrar** vacía original y traducción, cancela una traducción en curso y conserva el idioma de destino.
6. El botón derecho del icono flotante ofrece **Traducir con Léxora**. La misma opción aparece en los dos paneles de texto: traduce la selección o, si no hay selección, el contenido del panel sin sustituir el original.
7. En un documento editable, pulsa **Reemplazar en documento** después de traducir la selección. Léxora vuelve al documento, comprueba que sigue seleccionada la misma porción de texto y la sustituye por la traducción. Puedes deshacer en el editor con **Ctrl+Z**. La traducción queda en el portapapeles.

Al pulsar la X puedes elegir **Dejar en la bandeja**, **Cerrar aplicación** o **Cancelar**. En la bandeja sigue funcionando el icono flotante y Ctrl+C+C. Usa su menú para abrir Léxora, desactivar el icono flotante, cambiar **Iniciar con Windows (en la bandeja)** o salir por completo.

El inicio automático es opcional y está desactivado en una instalación nueva. Si lo activas, Léxora arrancará sin mostrar la ventana al iniciar sesión en Windows. Puedes abrirla desde la bandeja. La actualización conserva la elección existente y la desinstalación retira la entrada de inicio automático de Léxora.

Los menús de Léxora no modifican los menús propios de Chrome, Word u otras aplicaciones. Esta versión no incluye extensión del navegador.

## Traducción y privacidad

El origen se detecta automáticamente. El destino se guarda en `%LOCALAPPDATA%/SeleccionTraductor/language.txt`. La carpeta interna se conserva para poder actualizar la versión anterior sin perder preferencias. Se incluyen 35 idiomas de destino.

La selección se consulta localmente mediante las funciones de accesibilidad de Windows. El texto solo se envía a Google cuando pulsas el icono, utilizas Ctrl+C+C o pulsas Traducir. No se guarda un historial de los textos. Google recibe y procesa el contenido enviado; la aplicación no es un producto oficial de Google ni de DeepL.

Se usa un acceso web no documentado de Google, sin clave API. No tiene garantía de disponibilidad y puede devolver límites de peticiones o cambiar de formato. Ante un error, la ventana permite usar **Abrir en Google** en tu navegador, hasta 4.000 caracteres. La integración oficial de Google Cloud Translation no está implementada en esta versión.

## Límites

- Hasta 20.000 caracteres por traducción; los textos largos se dividen en fragmentos.
- Se necesita conexión a Internet.
- Funciona con texto seleccionable expuesto por accesibilidad o que la aplicación permita copiar con Ctrl+C. No incluye OCR de imágenes o PDF escaneados, ni evita restricciones de documentos protegidos.
- Tras un arrastre o doble clic, el icono se ofrece aunque el proveedor de accesibilidad anuncie soporte pero no devuelva la selección. Si no hay texto copiable, se muestra un mensaje al pulsarlo.
- El icono se oculta al hacer clic fuera, escribir, desplazar con la rueda o cambiar de aplicación.
- No requiere permisos de administrador. La captura desde aplicaciones ejecutadas como administrador puede estar restringida por Windows.
- El icono intenta respetar el foco de la aplicación original. Si necesita copiar para obtener el texto, intenta restaurar el portapapeles anterior. Ctrl+C+C deja el texto copiado como corresponde a Ctrl+C.
- Los ejecutables no están firmados digitalmente.
- Reemplazar requiere un editor compatible con la selección de Windows o con accesibilidad. Si no se puede verificar la selección, el botón permanece deshabilitado; puedes usar **Copiar resultado**. No funciona sobre páginas de lectura, PDF protegidos ni texto pegado manualmente en Léxora. El pegado es de texto plano: el formato final depende del editor. Word y LibreOffice necesitan validación manual.

## Construir y comprobar

```powershell
.\build.ps1
.\test.ps1
```

`test.ps1` ejecuta pruebas de fragmentación, respuestas, preferencias, registro de hooks y traducción real con Google. Usa únicamente texto de prueba y preferencias aisladas dentro de `test-output`.

Los flujos de interfaz usan respuestas de traducción simuladas e identificadas en la salida de las pruebas. La comprobación de conexión llama por separado al cliente real de Google. La aplicación normal siempre utiliza Google.

```powershell
.\test.ps1 -Interactive
.\test.ps1 -Interactive -Offline
.\test.ps1 -StartupOnly -Offline
.\test.ps1 -ReplaceOnly -Offline
```

La variante interactiva abre un documento temporal y utiliza entrada de ratón de Windows para seleccionar texto y pulsar el icono. Comprueba captura, traducción, menú y conservación del portapapeles. También simula las notificaciones físicas del hook Ctrl+C+C y entrega realmente Ctrl+C al editor de prueba. Necesita que no se use el ratón/teclado durante la ejecución. No reemplaza una prueba manual en navegadores y documentos reales.

`-Offline` omite la comprobación de red y permite verificar la interfaz sin nuevas peticiones a Google.

`-StartupOnly` comprueba el arranque oculto, la reapertura y la liberación de los hooks. Verifica activar/desactivar el inicio con una clave temporal aislada del registro que después elimina; no cambia el inicio real de Windows.

`-ReplaceOnly` abre un editor temporal para comprobar sustitución de la selección, conservación del resto, deshacer y cancelación ante selección modificada, solo lectura o documento cerrado. También comprueba que un editor que transforma a mayúsculas el pegado no produzca una confirmación falsa. Usa traducciones simuladas.

## Desinstalar

Utiliza la entrada **Léxora** de Aplicaciones instaladas de Windows o abre `%LOCALAPPDATA%/SeleccionTraductor/Desinstalar.exe`. También puedes abrir el instalador nuevo y pulsar **Desinstalar**, incluso si la copia instalada es antigua. Pide confirmación y conserva el idioma guardado. Si Léxora sigue abierta, ofrece finalizar sus procesos y continuar, con advertencia sobre los textos no guardados. Elegir **No** cancela antes de eliminar archivos o configuración. Solo cierra procesos de esa ruta de instalación y de tu sesión; otros programas con el mismo nombre no se cierran. Antes de actualizar, sigue siendo necesario cerrar Léxora.

`test-installer.ps1` comprueba este cierre con dos procesos temporales del mismo nombre en rutas diferentes. No instala ni desinstala la copia del usuario.

Ver [INSTALL.md](INSTALL.md) para instalación, actualización y desinstalación.

## Archivos del proyecto

- `App.cs`, `DocumentTarget.cs`, `Setup.cs`, `StartupSettings.cs` y `app.manifest`: aplicación, verificación del documento de origen, instalación e inicio opcional con Windows.
- `assets/`: icono y script para regenerarlo.
- `build.ps1`: genera únicamente `dist/Instalar-Lexora.exe`; los ejecutables intermedios se compilan en una carpeta temporal que después se elimina.
- `Tests.cs` y `test.ps1`: pruebas. La carpeta temporal `test-output/` se crea al ejecutarlas y se puede eliminar después.
- `INSTALL.md` y `VALIDACION.md`: instrucciones y registro de las comprobaciones realizadas.
