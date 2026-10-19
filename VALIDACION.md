# Validación de Léxora 1.4.0

## Licencia y aceptación

- Licencia MIT, términos de uso y descargo incrustados en el instalador único. Se incluyen también los fuentes correspondientes en SourceCode.zip. No se ha publicado el proyecto en GitHub.
- Instalación bloqueada por defecto. La casilla de aceptación empieza desmarcada; marcarla habilita Instalar y desmarcarla vuelve a bloquearlo. La propia rutina de instalación comprueba la aceptación antes de tocar archivos o registro.
- «No acepto y salir» cierra la ventana. La desinstalación sigue accesible sin aceptar.
- 16 comprobaciones superadas en test-installer.ps1: controles legales, aceptación y rechazo, junto con la regresión de finalización de procesos. No se instala ni desinstala la copia real del usuario.
- Inspección visual del instalador: documentos en pestañas desplazables, aceptación desmarcada, rechazo y botón Instalar deshabilitado visibles. Recursos LICENSE, TERMS.txt y SourceCode.zip presentes; el ZIP contiene los archivos fuente y los scripts, y se compararon LICENSE, TERMS.txt, Setup.cs y build.ps1 con el proyecto actual.

## Historial de validación de 1.3.1

## Desinstalación con la aplicación abierta

- Causa confirmada en el código: el acceso exclusivo al ejecutable fallaba si la aplicación estaba abierta, sin ofrecer finalizarla.
- El desinstalador detecta los procesos de su ruta exacta y sesión actual; pide confirmación antes de finalizarlos y espera su salida antes de eliminar archivos. No cancela procesos únicamente por su nombre. Cancelar detiene el flujo antes de modificar archivos o registro.
- Nuevo botón Desinstalar en el instalador para poder retirar versiones antiguas sin instalar una actualización previamente.
- Pruebas aisladas con ejecutables temporales: detección por ruta, cancelación sin cierre, cierre confirmado, conservación del proceso homónimo de otra ruta, conservación de archivos y continuación sin pregunta cuando no hay procesos activos. Compilación correcta.
- No se ha ejecutado la desinstalación real de la copia del usuario. La advertencia informa de la pérdida de textos no guardados en Léxora al finalizar procesos.

## Historial de validación de 1.3.0

## Reemplazo en el documento

- Compilación del nuevo botón y de DocumentTarget, que conserva el destino y verifica ventana, foco, selección y texto antes de pegar.
- 28 comprobaciones superadas en un editor temporal con traducción simulada: reemplazo de la selección conservando prefijo y sufijo, verificación del texto insertado, portapapeles con la traducción, deshacer, selección cambiada, solo lectura, edición manual del original y documento cerrado. Un editor configurado para transformar el pegado a mayúsculas no produce una confirmación falsa.
- 18 comprobaciones generales sin red y 11 comprobaciones de arranque opcional/bandeja superadas. No se ha cambiado la instalación ni el inicio real de Windows.
- Regresión interactiva completa: 51 comprobaciones superadas, incluidos icono flotante, menú, Ctrl+C+C y restauración del portapapeles. La ejecución separada del terminal evitó interferencias de foco observadas durante las primeras pruebas.
- Inspección visual del botón en la ventana de 900 × 610: texto completo y controles visibles.
- Queda pendiente validar documentos reales de Word/LibreOffice y sus reglas de formato. No se garantiza compatibilidad con todos los editores. El reemplazo pega texto plano.

## Historial de validación de 1.2.0

## Instalador único e inicio opcional

- Compilación correcta. `dist` contiene únicamente `Instalar-Lexora.exe`, que incluye aplicación e icono y se copia como desinstalador al instalar.
- Once comprobaciones específicas superadas: comando de inicio entre comillas con --tray, activación/desactivación en clave temporal aislada, conservación de otras entradas, arranque sin mostrar ventana, bandeja visible, hooks activos, reapertura sin duplicarlos y salida completa.
- Otras 51 comprobaciones de interfaz superadas con `test.ps1 -Interactive -Offline`. Traducción simulada; no se hicieron nuevas peticiones a Google.
- Revisión independiente de solo lectura del arranque, hooks, registro de inicio y empaquetado sin fallos bloqueantes detectados.
- Verificados los recursos del instalador: aplicación con StartupSettings e icono incluidos. Vista previa de la ventana comprobada con la casilla de inicio presente y todos los controles dentro de sus límites, sin pulsar Instalar.
- No se ha modificado la entrada real de inicio de Windows ni instalado la actualización. Queda comprobar un inicio de sesión real con la opción activada.

## Historial de validación de 1.1.1

## Actualización del icono

- Compilación correcta de aplicación, instalador y desinstalador con icono nativo propio.
- Verificados los nueve tamaños de imagen del ICO y extraído el icono de los tres ejecutables. Inspección visual de la imagen extraída de Lexora.exe.
- Recurso Lexora.ico del instalador comparado por SHA-256 con el original: idéntico.
- Creado y leído un acceso directo aislado en test-output: IconLocation apunta explícitamente a Lexora.ico.
- No se ha ejecutado la actualización de la instalación del usuario; queda verificar el aspecto del acceso directo tras ejecutar el nuevo instalador.

Las comprobaciones funcionales siguientes corresponden a la versión 1.1.0, anterior a este cambio de icono. No se repitieron los flujos de traducción para un cambio visual y de recursos.

## Ejecutado

- Compilación de aplicación e instalador con el compilador de .NET Framework incluido en Windows.
- Última ejecución: `test.ps1 -Interactive -Offline`, 51 comprobaciones superadas. El recuento incluye comprobaciones de entrada de ratón y teclado.
- Borrar ambos paneles, conservar idioma, cancelar una traducción en curso y evitar resultados tardíos.
- Cancelar cierre, permanencia en bandeja, reapertura y salida completa con retirada del icono.
- Selección con entrada real de ratón de Windows en un documento de otro proceso; detección, aparición del icono por encima del documento, foco conservado y pulsación.
- Apertura y activación del menú del botón derecho del icono. Menús en ambos paneles: traducción de la selección conservando el original.
- Copia real desde otro proceso, restauración del portapapeles de texto y rechazo de contenido previo cuando no hay selección.
- Ctrl+C+C: notificaciones del hook simuladas y entrega real de Ctrl+C al documento temporal.
- La última ejecución usó respuestas de traducción simuladas para los flujos de interfaz. La conexión real de Google se comprobó por separado durante esta sesión: `Hello world` → `Hola Mundo`. También se completó previamente un recorrido por el icono con traducción real.
- Desinstalador independiente compilado y abierto sin argumentos. Confirmación Sí/No detectada, respuesta No automática y salida correcta conservando la instalación. No se ejecutó la eliminación.
- Inspección visual de la ventana mediante `test-output/app.png`.
- Revisión estática independiente de instalador, popup no activable, hooks, accesibilidad y captura asíncrona.

## Pendiente

- Prueba manual en los navegadores, Word y lectores PDF concretos del usuario; la accesibilidad y los permisos varían.
- Atajo físico Ctrl+C+C y conservación del portapapeles con contenido enriquecido.
- Posición del icono en monitores con distintas escalas y escalado 125/150 %.
- Instalación y desinstalación reales. El instalador se ha compilado, pero no se ha ejecutado para modificar este equipo.

La prueba interactiva completa terminó correctamente. Durante el desarrollo se corrigieron el error de colección modificada al cerrar (liberación de ventanas en OnFormClosed), el estilo nativo de superposición del icono (WS_EX_TOPMOST explícito) y la oferta del icono cuando accesibilidad no devuelve selección. También se alineó el modo de pantalla del ejecutable de pruebas con el de la aplicación.

No se ha actualizado ni desinstalado la copia del usuario. El acceso web no oficial de Google puede imponer límites o cambiar; no se garantiza disponibilidad ilimitada.

## Limpieza del proyecto

Se retiraron las capturas, scripts auxiliares y resultados temporales de `test-output/`, la vista previa PNG del icono y los dos ejecutables duplicados con el nombre anterior. Las referencias a capturas anteriores describen comprobaciones históricas; esos archivos temporales ya no se conservan.

La compilación genera solo los tres ejecutables de entrega. El nombre interno del ejecutable instalado y el recurso incrustado se mantienen para conservar la compatibilidad con la instalación existente. El instalador y el desinstalador independientes comparten contenido deliberadamente: su nombre determina el modo de apertura.
