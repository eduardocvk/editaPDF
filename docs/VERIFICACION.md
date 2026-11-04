# Verificación local — 4 de noviembre de 2026

Versión actual 0.2.0. Windows 11 x64, build 26200. SDK .NET 10.0.402, runtime 10.0.13, WPF y PDFium 159.0.8138.0 sin V8/XFA. No se publicó en Store ni se modificó `firmar_certificado`.

## Cambios y comprobaciones de 0.2.0

Se incorpora iText Community 9.8.0 para unir e importar páginas y reordenarlas conservando referencias; PDFium mantiene el render y la edición real de objetos de texto. Se revisaron su licencia AGPL, dependencias y compatibilidad con .NET 10; el análisis NuGet no informó vulnerabilidades conocidas en los paquetes restaurados. Se incluyen los avisos de dependencias y el código de producción de iText (con Commons y adaptador Bouncy Castle), con procedencia y SHA-256 en `vendor/sources/provenance.json`. No se utilizan servicios de validación, firma, descarga ni recursos remotos de estas bibliotecas.

Las regresiones y pypdf comprueban importación y movimiento de páginas con marcadores, enlaces internos, notas, apariencia de sellos, destinos con nombre, adjuntos, campos AcroForm y árbol de etiquetas. Los casos con nombres repetidos conservan destinos de enlaces y marcadores, valores de campos y contenido de ambos adjuntos. La inicialización de marcadores se realiza después de resolver las colisiones para conservar sus referencias. Se comparan texto, geometría, anotaciones y render de las páginas antes de aceptar los cambios. Los originales mantienen su SHA-256.

La interfaz abre documentos independientes en pestañas superiores. Se prueban historial por documento, cambio de página/zoom/búsqueda/borrador, recuperación completa de pestañas, cierre y aislamiento de selecciones incluso con objetos iguales en dos PDF. Los botones del panel derecho tienen iconos vectoriales. Logo e ICO con varios tamaños incluidos, junto con imágenes para MSIX; creación y prompt documentados en `assets/LOGO.md`.

La reorganización por miniaturas utiliza el arrastre nativo WPF, indicador de inserción y desplazamiento de la lista. La prueba de interfaz genera un evento real enrutado `Drop` sobre una miniatura y comprueba el nuevo orden, el guardado y la independencia del resto de pestañas. **El gesto físico de arrastrar con el ratón sigue pendiente**: el controlador de escritorio volvió a fallar al activar/capturar la ventana. El render WPF se revisa a 1280 y 920 unidades lógicas y densidades de 100 %, 150 % y 200 %.

Evidencia reproducible de esta versión: `artifacts/test-0.2.0.log` y el directorio `artifacts/tests-*` indicado al final del registro. Los flujos se ejecutan bajo la guardia nativa de red y el motor observa también eventos `System.Net.*`. Los límites de la evidencia de red, impresión y MSIX permanecen en la sección final.

Se mantienen bloqueados los PDF firmados, protegidos y XFA. AcroForm y etiquetas permiten ahora unión y movimiento, con límites en edición de objetos y otras operaciones. No se ha probado el archivo concreto de la fotografía ni un corpus exhaustivo. Las siguientes secciones conservan la evidencia histórica y las restricciones de sus versiones correspondientes.

## Cambios y comprobaciones de 0.1.1

Organizar y Editar son dos pestañas WPF sobre el panel derecho. El banner inferior tiene más espacio y el texto «¿Quieres firmar tus PDF? Prueba FirmaPDF», conservando la apertura explícita de su URL fija sin datos del documento. Se han comprobado selección de pestañas, ubicación, tamaño del banner, render a 1280 y 920 unidades lógicas y escalas de 100 %, 150 % y 200 %. Las capturas se inspeccionaron.

Se eliminó el bloqueo general al **añadir páginas al final** de un documento con anotaciones, marcadores o destinos con nombre. Se conservaron en las regresiones las páginas existentes, marcadores, destinos, adjuntos y enlaces internos del PDF principal, con deshacer/rehacer y guardado en copia. También se comprobó la importación de notas independientes, sellos visuales y enlaces web, preservando contenido, posición, propiedades, URI y apariencia. Cada página importada se compara con el origen en texto, geometría, anotaciones y render antes de aceptar la operación en la sesión.

PDFium puede ajustar el recuadro de una nota al generar su apariencia durante la visualización. El render ahora utiliza una copia en memoria aislada del documento editable, invalidada al modificarlo. La comprobación independiente confirma que ver una página a distintos tamaños y guardarla conserva los recuadros y apariencias originales.

La suite completa y la verificación independiente con pypdf pasaron en `artifacts/test-0.1.1-release.log`; datos sintéticos y capturas en `artifacts/tests-20261104-181750-741a39/`. Los flujos del motor y la interfaz volvieron a finalizar bajo la guardia de red sin intentos detectados.

**Límites que permanecen:** los PDF firmados, protegidos, con formularios o etiquetados siguen en solo lectura. Mover/eliminar páginas o insertarlas en medio sigue limitado cuando el documento tiene referencias. Los marcadores, destinos con nombre y adjuntos del archivo de origen, así como enlaces internos o comentarios enlazados, todavía no se transfieren: se explica el motivo y la alternativa de abrir ese archivo como documento principal. Las regresiones comprueban que esos rechazos dejan la sesión anterior intacta. No se ha probado el documento concreto de la fotografía aportada por el usuario; la reproducción utiliza PDF sintéticos con las estructuras que provocaban el mismo bloqueo.

## Evidencia inicial de 0.1.0

Prueba funcional de PDFium ejecutada antes de la interfaz: edición del objeto original, regeneración, guardado, reapertura, fuente conservada, texto anterior ausente del contenido de página y sin objetos de ocultación. Después se repitió con un PDF externo de ReportLab/Helvetica. No es una afirmación de censura segura o borrado forense.

La ejecución reproducible más reciente del conjunto completo está en `artifacts/test-final.log`; documentos y capturas en `artifacts/tests-20261104-165530-8eb312/`. Incluye:

- Añadir texto con fuente incluida; cambiar texto original conservando Helvetica y Noto Sans; sustitución explícita; rechazo de texto incompatible sin perder la sesión.
- Movimiento/eliminación de objetos, inserción y transformación de imagen; insertar, duplicar, eliminar, girar, reordenar en ambas direcciones, unir e importar/extraer páginas.
- Coordenadas entre representación y PDF en las cuatro rotaciones.
- Guardado y reapertura; comprobación de texto, geometría, anotaciones y render de todas las páginas de cada copia guardada. Intento de reemplazar un archivo rechazado. SHA-256 del original intacto.
- Deshacer/rehacer, fallo inyectado después de una modificación con reversión completa, apertura múltiple fallida conservando la sesión anterior y recuperación local de cambios.
- PDF protegido y con firma sintética identificados y bloqueados. La recuperación de esos documentos conserva los bytes originales; la firma sintética no es una firma válida ni una prueba criptográfica.
- Edición de un PDF con marcador, adjunto, metadatos y enlace interno. Comprobación independiente con pypdf de su conservación y de que el enlace sigue apuntando a la segunda página. Reorganización de estructuras sensibles rechazada.
- Controles WPF reales: aplicar texto original, deshacer, rehacer, guardar copia; render a 1280×780 y 920×560 unidades lógicas, con capturas a 100 %, 150 % y 200 % de densidad.
- Paginación de impresión creada y renderizada; filtro de puertos locales probado. No se envió ninguna página a una impresora.

Se inspeccionaron visualmente las capturas WPF y el PDF editado renderizado por **Poppler**, independiente de PDFium. La interfaz mantiene desplazamiento cuando la página o el panel no caben. Los paneles se pueden redimensionar. La escala de las capturas no sustituye una prueba con varios monitores físicos.

La última revisión del panel contextual (limpieza de propiedades al perder selección y nombre accesible de las miniaturas) se volvió a probar bajo la guardia de red: `artifacts/ui-final/`. Pasaron edición, deshacer/rehacer, guardado y preparación de impresión. Las capturas finales también se inspeccionaron.

Para la descarga de GitHub se comprobó también el banner FirmaPDF en `artifacts/ui-release/`: no abre enlaces automáticamente y una pulsación entrega exclusivamente `https://apps.microsoft.com/detail/9P5ZK5W4J9ZJ`, sin parámetros ni datos del documento. La apertura del navegador se sustituye por un receptor local únicamente en esa prueba; la ejecución real utiliza el navegador predeterminado. La página de Microsoft Store devolvió HTTP 200 y el título «FirmaPDF - Descargar e instalar en Windows». Los flujos WPF volvieron a pasar bajo la guardia nativa sin intentos de red de la app.

## Red y dependencias

La guardia nativa de desarrollo usa la API de depuración de Windows para observar las DLL cargadas e interrumpir antes de las APIs vigiladas de sockets, DNS, WinHTTP y WinINet. El control positivo cargó Winsock y fue bloqueado en `WSASocketW` **antes de conectar** (código 2 esperado; la palabra FAIL de ese proceso indica el bloqueo intencional del control). Los procesos del motor y los controles WPF finalizaron con código 0, sin cargar esas DLL de red ni intentar sus APIs. El motor también registró cero eventos `System.Net.*`.

El análisis de importaciones normales y retardadas del binario PDFium encontró únicamente KERNEL32, ADVAPI32, GDI32 y USER32. No importa WinSock, DNS, WinHTTP, WinINet, URLMon ni un runtime VC externo. Sus hashes coinciden con el archivo fijado; el SHA-256 del archivo descargado coincide con el digest publicado en la release. Avisos de PDFium, sus componentes, binarios MIT, .NET/WPF y Noto Sans incluidos.

La compilación final se realiza con `Build.ps1 -Offline`, usando únicamente la caché local de paquetes de runtime y una fuente NuGet local vacía. La aplicación publicada es autónoma. `Source.zip` acompaña al ejecutable con código, scripts, cabeceras, dependencias y licencias para reconstruir el proyecto con el SDK indicado.

## Pendiente y límites de la evidencia

- Interacción con la ventana mediante el controlador de escritorio: la captura/activación devolvió `failed to activate captured window`. La verificación disponible usa render real de WPF y ejecución de sus manejadores de controles; no se afirma haber completado una sesión manual con ratón/teclado del sistema.
- Ejecución interactiva con adaptadores desconectados y captura adicional de tráfico del kernel. `netsh trace` devolvió error 5: requiere elevación. No se cambiaron reglas de cortafuegos ni adaptadores. La guardia comprueba los flujos ejercitados, no todos los posibles caminos de Windows, shell o controladores.
- Impresora física/virtual y diálogo de destino del controlador; no se ha comprobado una impresión real.
- Instalación, desinstalación, firma, WACK y certificación de MSIX. No hay identidad Store inventada ni paquete publicado. El manifiesto es parametrizado y requiere datos oficiales.
- Corpus amplio de PDF de terceros, grandes documentos, formularios, etiquetas, fuentes especiales, escritura compleja y objetos anidados. Las funciones no compatibles se bloquean donde pueden identificarse; no se promete edición universal ni conservación exhaustiva de cualquier extensión PDF.

La aplicación conserva límites explícitos en lugar de eliminar estructuras silenciosamente. El detalle de uso y los límites funcionales están en el README; el almacenamiento y los límites del sistema de impresión están en PRIVACIDAD.md.
