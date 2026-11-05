# Privacidad de Editor PDF 0.6.1

El motor y la interfaz procesan los PDF en el equipo. No se implementan conexiones, servicios remotos, telemetría, analítica, actualizaciones, descargas de fuentes ni envío automático de errores. No se ejecutan JavaScript, enlaces o acciones de los documentos. PDFium se incluye sin V8 ni XFA. La app no carga páginas web.

La compilación autónoma incluye runtime, motor y las diez familias de fuentes incluidas. El SDK de desarrollo y los scripts de adquisición de dependencias son herramientas separadas; no se ejecutan al usar la aplicación. Los scripts de compilación desactivan la telemetría del SDK.

## Datos locales

El texto nuevo pendiente de confirmar también se conserva como borrador local de la pestaña. Durante un arrastre/redimensionado se usa una copia transitoria en memoria; no se guardan estados intermedios en el PDF ni se envía el contenido a ningún servicio. Cancelar descarta esa copia. El selector visual representa las páginas con el PDFium incluido y no descarga miniaturas.

PDF originales: solo lectura. Exportaciones: archivos nuevos elegidos por el usuario; no reemplazan existentes. El guardado utiliza un temporal en la misma carpeta y un movimiento sin reemplazo, después de comprobar la copia.

La sesión se guarda automáticamente en `%LOCALAPPDATA%\EditorPDF\Recovery`. `workspace.json` conserva las pestañas y su contexto; cada subcarpeta con identificador contiene una copia del PDF, las rutas originales y el estado de cambios. Los borradores del panel, búsqueda y posición también son locales. No se guardan contraseñas ni certificados. Los archivos no tienen cifrado adicional y dependen de los permisos del perfil Windows. Se conserva la sesión al cerrar la ventana para recuperarla; cerrar una pestaña retira su recuperación después de guardar el estado de las demás. El historial de deshacer permanece en memoria y es independiente por pestaña. Las sesiones antiguas de un solo documento se pueden recuperar. Puedes eliminar la carpeta Recovery con la aplicación cerrada; los originales no se alteran. No hay registro persistente de actividad ni envío de búsquedas.

iText Community se utiliza solo con flujos de memoria para las estructuras PDF. No se invocan sus funciones de firma, TSA, revocación ni recursos remotos. La generación del logo y la adquisición de bibliotecas fueron tareas de desarrollo; la app distribuida incluye todos sus archivos y no ejecuta generadores ni consultas de paquetes.

Se rechazan rutas UNC, unidades de red y rutas con enlaces/redirecciones antes de leer o guardar documentos. Usa una carpeta local que no esté sincronizada si quieres que otro programa, como un cliente de nube, tampoco sincronice tus exportaciones. La app no controla programas externos ni las políticas de diagnóstico del propio Windows.

## Impresión y FirmaPDF

La creación de una copia editable de un PDF protegido requiere confirmación. Se retira el cifrado y las firmas de la copia; su recuperación se guarda sin contraseña en el perfil local. La contraseña se introduce en un campo oculto y no se persiste. Los bytes de contraseña de la biblioteca se ponen a cero al finalizar; las cadenas gestionadas permanecen sujetas a la gestión de memoria de .NET. La copia no acredita la firma del original.

La integración opcional del Explorador registra únicamente claves del usuario actual y un acceso directo en su carpeta Enviar a; se puede retirar desde Opciones. No cambia la asociación predeterminada. El acceso directo contiene la ruta de la app y --merge; Windows entrega los archivos seleccionados como argumentos. Si hay una instancia abierta, usa WM_COPYDATA. No hay sockets, servicios residentes, servidores COM propios, archivos de intercambio con rutas ni conexiones remotas. Las rutas se usan para la apertura/unión solicitada y se aplican las comprobaciones de unidades locales.

La impresión solo se inicia por una acción explícita y se entrega al sistema de impresión de Windows. El diálogo estándar de Windows muestra las impresoras disponibles. Si se elige una cola cuyo puerto no es USB, LPT, COM, FILE, XPSPort o PORTPROMPT, la aplicación advierte que puede enviar el PDF por la red y pide confirmación antes de imprimir. El controlador, la cola y los diálogos de destino pertenecen a Windows; la app no controla su comportamiento interno. No se han impreso documentos durante las pruebas.

FirmaPDF no está integrada. El pequeño banner solo abre `https://apps.microsoft.com/detail/9P5ZK5W4J9ZJ` en el navegador del usuario al pulsarlo. La ficha oficial fue proporcionada por el usuario y comprobada en Microsoft Store. El enlace es fijo y no incluye parámetros de seguimiento, documentos, rutas, búsquedas ni datos de sesión. No se consulta la ficha al iniciar ni al abrir o editar documentos. La navegación posterior pertenece al navegador y a Microsoft Store.

## Comprobación de red

Se revisó el código y se ejecutaron los flujos del motor y los controles WPF bajo vigilancia de carga de DLL y puntos de interrupción en APIs de sockets, DNS, WinHTTP y WinINet. La guardia termina el proceso antes de ejecutar una API vigilada. Su control positivo detectó y bloqueó `WSASocketW`; los flujos probados terminaron sin intentos. Además, un EventListener registró cero eventos `System.Net.*` durante las pruebas del motor.

Esto comprueba los flujos ejercitados; no sustituye una auditoría exhaustiva del sistema, controladores o diálogos del shell. La traza de red del kernel con `netsh trace` no pudo iniciarse: Windows devolvió **error 5, requiere elevación**. No se cambiaron adaptadores ni reglas del cortafuegos. Está pendiente esa captura adicional en un equipo de pruebas y la ejecución interactiva con los adaptadores desconectados.
