# Privacidad de Editor PDF 0.8.1

Los PDF se procesan en el equipo. No se implementan telemetría, analítica, procesamiento remoto, descargas de fuentes ni envío automático de errores. Runtime, motores y fuentes están incluidos. No se ejecuta JavaScript de los PDF.

No se guarda ni se restaura automáticamente una sesión entre ejecuciones. Los documentos y borradores abiertos permanecen en memoria. Guardar, extraer, dividir o enviar a FirmaPDF crea archivos nuevos en las ubicaciones elegidas por el usuario. La elección de idioma se conserva en `%LOCALAPPDATA%\EditorPDF\language.txt`; ese archivo no contiene datos de documentos.

Las recuperaciones de versiones anteriores no se abren ni se modifican automáticamente. Si existen, permanecen en la carpeta Recovery de la versión anterior hasta que el usuario las retire.

Los PDF originales no se sobrescriben. Las copias editables de PDF protegidos pierden el cifrado y las firmas con confirmación previa. No se guardan contraseñas.

Imprimir abre una única ventana de configuración y vista previa. Enumera las colas instaladas mediante el servicio local de impresión y consulta las capacidades de la impresora seleccionada. La vista previa se genera localmente usando el mismo paginador del trabajo. Una impresora de red puede enviar documentos por la red; se solicita confirmación antes de imprimir en un puerto que pueda ser remoto. El controlador y la cola pertenecen a Windows.

El botón FirmaPDF comprueba una ubicación de instalación local conocida. Si encuentra una instalación compatible, pide guardar una copia y la abre mediante `--open` en FirmaPDF. Se entrega exclusivamente esa ruta mediante argumentos locales, solo por acción explícita del usuario. Si no se detecta la instalación, abre la ficha fija `https://apps.microsoft.com/detail/9P5ZK5W4J9ZJ` sin documentos ni datos de sesión. El comportamiento posterior de FirmaPDF, del navegador y de Microsoft Store corresponde a esas aplicaciones.

La integración opcional del Explorador registra entradas para el usuario y no cambia el lector PDF predeterminado. No hay sockets ni servicios residentes de Editor PDF. Se rechazan rutas UNC, de red y con redirecciones antes de leer o guardar documentos. Otros programas, como clientes de sincronización, pueden actuar sobre las carpetas que elija el usuario.

Las pruebas ejercitan motor e interfaz bajo una guardia de APIs de red; esta evidencia se limita a los flujos probados. La captura adicional de red del sistema y la prueba interactiva con adaptadores desconectados siguen pendientes.
