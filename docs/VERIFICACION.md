# Verificación local — 5 de noviembre de 2026

Versión actual 0.8.0. Windows 11 x64, build 26200. SDK .NET 10.0.402, runtime 10.0.13, WPF y PDFium 159.0.8138.0 sin V8/XFA. No se publicó en Store ni se modificó `firmar_certificado`.

## Cambios 0.8.0

Motor: regresiones de las cuatro alineaciones con márgenes, guardado/reapertura y duplicación/eliminación de imágenes en `artifacts/test-0.8.0-final.log`. Interfaz: vista previa de impresión, captura en inglés, cambios de paneles y ausencia de recuperación persistente en `artifacts/test-0.8.0-visual.log`. Las pruebas no imprimen en una impresora física ni firman documentos.

FirmaPDF: se confirmó una instalación portable y el contrato local `--open` leyendo sus fuentes, sin modificarlas. La transferencia se realiza solo por pulsación y tras guardar una copia elegida por el usuario. La activación real de FirmaPDF y la variante instalada mediante MSIX siguen pendientes de prueba de extremo a extremo. La interfaz ofrece la ficha de Store si no se detecta la instalación compatible.

Las secciones siguientes conservan evidencia histórica de versiones anteriores; las funciones de recuperación descritas en ellas se han retirado de la interfaz actual.

## Controles de estilo 0.7.1

Negrita y cursiva aparecen como botones B e I con estado activo, accesibles por teclado y con nombres de automatización. La suite WPF comprueba su activación y el render del panel derecho.

## Interfaz y reflujo 0.7.0

La suite local verifica reflujo de líneas y separación entre párrafos, selección por arrastre y acción contextual, navegación con la rueda al borde de página, acción sobre una miniatura concreta, render WPF a 1280 y 920 unidades lógicas y guardia nativa de red. Las capturas se inspeccionaron visualmente. Queda por probar manualmente la edición del contrato concreto y una variedad mayor de PDF.

## Corrección 0.6.2

«Editar párrafo» permite marcar en la página todo el bloque; la selección incluye los fragmentos horizontales que caben en el recuadro y los presenta en orden de lectura. El motor admite estilos mixtos si se elige una fuente incluida y comprueba guardado/reapertura sin alterar contenido ajeno. El contrato indicado por el usuario se inspeccionó solo con métricas de objetos, sin incorporar su texto ni el archivo al repositorio: sus líneas tienen varios fragmentos. La prueba sintética cubre ese caso; el resultado de edición manual en ese documento concreto queda por comprobar.

## Corrección 0.6.1

Cerrar un editor original sin cambios y pulsar Aplicar sin cambios no reemplazan el documento de sesión. La barra contextual flotante muestra fuente, tamaño, negrita, cursiva, color y Aplicar; se ha comprobado en una captura WPF y con una modificación real del tamaño y una variante negrita guardada. Pruebas de regresión en artifacts/test-0.6.1.log.

## Edición 0.6.0

Selector de color visual HSV, controles de tamaño, cuatro familias DejaVu adicionales incluidas con licencia y edición directa que toma el tamaño/posición/color de texto horizontal original y ubica el cursor en el punto pulsado. Edición de párrafos uniformes con reflujo, validación de colisión y conservación del contenido ajeno. Prueba PDF real guardado/reabierto e inspección independiente con pypdf de texto y contenido ajeno; prueba WPF de controles y capturas en artifacts/test-0.6.0-final.log. Pendiente validación manual con documentos variados y párrafos fragmentados complejos.

## Corrección 0.5.1

Se eliminó la casilla de sustitución. El texto original seleccionado mantiene por defecto su fuente y elegir una familia en el desplegable sustituye la fuente del objeto. Si la original no puede representar el texto, la transacción conserva el PDF y el aviso indica elegir una fuente. La prueba WPF introduce 12345, sustituye por Liberation Sans y comprueba el texto y la fuente del PDF. Se conserva la lectura de sesiones previas con el antiguo campo Substitute.

## Cambios y comprobaciones de 0.5.0

Doble clic permite editar un objeto de texto compatible en la página; la selección y las esquinas se redibujan tras soltar. Menú contextual con iconos, seis familias de fuentes incluidas y variantes reales de negrita/cursiva, cuadro estándar de impresión de Windows y menú Configuración. La prueba WPF comprueba la edición original y la reapertura con las seis fuentes. Suite completa: artifacts/test-0.5-final.log (sin intentos de API de red). El diálogo de impresión se compila pero no se enviaron trabajos reales a una impresora.

## Cambios y comprobaciones de 0.4.0

Texto nuevo directamente sobre la página con un TextBox WPF temporal: visible mientras se escribe, confirmado con Intro o al cambiar de foco, cancelable con Esc. La confirmación añade un objeto PDF real con Noto Sans, sin rectángulos de ocultación. El borrador se recupera con la sesión; el texto nuevo se orienta horizontalmente también con las cuatro rotaciones de página. La edición del texto original permanece en el panel contextual.

Movimiento de texto/imágenes y redimensionado por cuatro esquinas con vista previa PDFium sobre una copia desechable en memoria. El render transitorio puede generar apariencias en esa copia, que nunca se incorpora a la sesión. Se aplica una sola operación al soltar; Esc cancela sin cambiar el PDF. Esquinas proporcionales por defecto y proporción libre con Mayús. Se compara el bitmap de la vista previa con el original para comprobar que hay movimiento visible, y se comprueba que el PDF de la sesión sigue intacto antes de soltar.

Menú contextual en página y lista de objetos: insertar, propiedades, eliminar, deseleccionar, deshacer/rehacer y orden de pintado. Para persistir el cambio de orden, primero se consolidan los flujos de contenido de la página y se comprueba que eso conserva texto, objetos, geometría, anotaciones y render. Después se cambia el orden y se regeneran los objetos con PDFium. La regresión de imágenes solapadas y pypdf verifican el orden guardado; no son capas OCG.

Insertar, Extraer y Dividir usan un selector con miniaturas, casillas, vista previa, Todas/Ninguna, posición de inserción y número de páginas por archivo. El campo de rangos anterior queda oculto, conservado únicamente para compatibilidad del estado de sesión. Se prueban selección, extracción guardada e inserción de las páginas elegidas. Capturas WPF: `texto-en-pagina`, `movimiento-en-vivo`, `esquinas-redimensionar` y `selector-paginas`.

Se comprobó localmente la combinación exacta indicada por el usuario, en ambos órdenes, desde recuperación, después de editar y desde WPF. La copia unida de 76 páginas pasó guardado/reapertura, texto, geometría, anotaciones y render PDFium, además de verificación independiente con pypdf de texto y anotaciones de todas las páginas; originales intactos por SHA-256. El aviso original no se ha reproducido con los archivos actuales. Se conserva el rechazo si no se puede conservar una página y ahora se identifica página de origen y característica (tamaño/rotación/anotaciones, texto o apariencia); no se ha relajado ni eliminado la comprobación. Los documentos del usuario y sus copias de diagnóstico son locales, excluidos de Git y de la distribución.

La suite se registra en `artifacts/test-0.4.0.log`. La prueba de activación está dirigida exclusivamente a su ventana de prueba: se corrigió el envío no acotado que había abierto una pestaña sintética en la app del usuario; el usuario confirmó que la cerró. No se modificaron sus originales.

Se revisaron capturas WPF y el árbol accesible de la ventana de prueba. La automatización física no es evidencia fiable en este equipo: devolvió `coordinate input geometry is unavailable` y la captura incluía otra ventana superpuesta; se detuvo tras reintentar. Los gestos se verifican mediante los controles/modelo del lienzo y sus renders, no se afirma una sesión manual completa con el ratón.

## Cambios y comprobaciones de 0.3.0

Conversión explícita en memoria a otra pestaña editable, retirando cifrado, restricciones, campos de firma, certificación y DSS. No hay escritura en el original. La advertencia explica la retirada de firmas/sellos visibles y la recuperación sin cifrar. XFA se rechaza. La contraseña se pide con PasswordBox, no se guarda y una contraseña errónea conserva la sesión anterior. La validación de copia compara páginas, texto, geometría, render PDFium y presencia de estructuras. PDFium no dibuja widgets de firma con este render; retirar su apariencia es parte declarada de la operación.

Pruebas de restricción de propietario, contraseña de apertura correcta/incorrecta, firma sintética y firma CAdES con clave efímera y certificado de prueba autofirmado generados localmente. Se verifica integridad criptográfica de la firma de prueba antes y después de la conversión del PDF a copia: el SHA-256 y la firma del original siguen intactos. Esto no añade validación de confianza de certificados a la aplicación. La verificación independiente con pypdf comprueba la ausencia de cifrado/campos de firma y la conservación del texto, marcadores y adjuntos.

La integración del Explorador usa «Abrir con Editor PDF» y «Enviar a → Unir PDF con Editor PDF» en el menú clásico. Se verifica registro/desregistro bajo una raíz aislada, preservación de asociaciones ajenas, creación y lectura del acceso .lnk con destino/argumentos correctos, y entrega completa de dos rutas mediante WM_COPYDATA a una ventana de prueba. La unión abre otra pestaña sin alterar las existentes. El mecanismo Enviar a sigue la [integración documentada por Microsoft](https://devblogs.microsoft.com/oldnewthing/20170403-00/?p=95885). La pulsación física del menú y la entrega nativa del Shell no están verificadas de extremo a extremo: la prueba automatizada de Drop sobre el acceso directo quedó bloqueada y no forma parte de la suite que pasa. La entrada directa de unión, el menú principal moderno y MSIX permanecen pendientes. No se distribuye el servidor COM propio ensayado durante desarrollo, cuya activación devolvía 0x80040154.

Primer incremento de la siguiente fase: tamaño y color sobre el objeto de texto original, conservando fuente, contenido, matriz y alfa. Se prueba aplicación desde controles WPF, guardado/reapertura, deshacer/rehacer y rechazo de medidas inválidas sin perder cambios. pypdf comprueba operadores Tf y rg del contenido. La API experimental FPDFTextObj_SetFontSize pertenece al binario PDFium fijado y se ha ejercitado funcionalmente. Próximos incrementos en EDICION.md.

Registro de la suite: `artifacts/test-0.3.0.log`. Los flujos de motor e interfaz, incluyendo registro del acceso y activación local, se ejecutan bajo la guardia de red. Capturas WPF de las funciones nuevas en `estilo-copia-1280.png` dentro de la ejecución indicada por el registro. Siguen vigentes las comprobaciones pendientes de impresión, desconexión física, ratón, MSIX y corpus amplio que se detallan al final.

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
