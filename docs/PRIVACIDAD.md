# Privacidad de Editor PDF 0.1.1

El motor y la interfaz procesan los PDF en el equipo. No se implementan conexiones, servicios remotos, telemetría, analítica, actualizaciones, descargas de fuentes ni envío automático de errores. No se ejecutan JavaScript, enlaces o acciones de los documentos. PDFium se incluye sin V8 ni XFA. La app no carga páginas web.

La compilación autónoma incluye runtime, motor y Noto Sans. El SDK de desarrollo y los scripts de adquisición de dependencias son herramientas separadas; no se ejecutan al usar la aplicación. Los scripts de compilación desactivan la telemetría del SDK.

## Datos locales

PDF originales: solo lectura. Exportaciones: archivos nuevos elegidos por el usuario; no reemplazan existentes. El guardado utiliza un temporal en la misma carpeta y un movimiento sin reemplazo, después de comprobar la copia.

La sesión se guarda automáticamente en `%LOCALAPPDATA%\EditorPDF\Recovery\session.json`. Contiene una copia del PDF, las rutas originales y el estado de cambios. No contiene contraseñas ni certificados. Es un archivo local, **sin cifrado adicional**, sujeto a los permisos del perfil Windows. Se conserva al cerrar para poder recuperar la sesión. El historial de deshacer permanece solo en memoria. Puedes eliminar la carpeta Recovery con la aplicación cerrada para borrar la recuperación; los originales no se alteran. No hay registro persistente de actividad, documentos ni búsquedas.

Se rechazan rutas UNC, unidades de red y rutas con enlaces/redirecciones antes de leer o guardar documentos. Usa una carpeta local que no esté sincronizada si quieres que otro programa, como un cliente de nube, tampoco sincronice tus exportaciones. La app no controla programas externos ni las políticas de diagnóstico del propio Windows.

## Impresión y FirmaPDF

La impresión solo se inicia por una acción explícita y se entrega al sistema de impresión de Windows. El selector enumera únicamente colas locales y acepta puertos USB, LPT, COM, FILE, XPSPort o PORTPROMPT (como Microsoft Print to PDF). No ofrece colas remotas ni puertos TCP/IP o WSD. El controlador, la cola y los diálogos de destino pertenecen a Windows; la app no controla su comportamiento interno. No se han impreso documentos durante las pruebas.

FirmaPDF no está integrada. El pequeño banner solo abre `https://apps.microsoft.com/detail/9P5ZK5W4J9ZJ` en el navegador del usuario al pulsarlo. La ficha oficial fue proporcionada por el usuario y comprobada en Microsoft Store. El enlace es fijo y no incluye parámetros de seguimiento, documentos, rutas, búsquedas ni datos de sesión. No se consulta la ficha al iniciar ni al abrir o editar documentos. La navegación posterior pertenece al navegador y a Microsoft Store.

## Comprobación de red

Se revisó el código y se ejecutaron los flujos del motor y los controles WPF bajo vigilancia de carga de DLL y puntos de interrupción en APIs de sockets, DNS, WinHTTP y WinINet. La guardia termina el proceso antes de ejecutar una API vigilada. Su control positivo detectó y bloqueó `WSASocketW`; los flujos probados terminaron sin intentos. Además, un EventListener registró cero eventos `System.Net.*` durante las pruebas del motor.

Esto comprueba los flujos ejercitados; no sustituye una auditoría exhaustiva del sistema, controladores o diálogos del shell. La traza de red del kernel con `netsh trace` no pudo iniciarse: Windows devolvió **error 5, requiere elevación**. No se cambiaron adaptadores ni reglas del cortafuegos. Está pendiente esa captura adicional en un equipo de pruebas y la ejecución interactiva con los adaptadores desconectados.
