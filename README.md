# Editor PDF para Windows 11

Aplicación WPF independiente, C#/.NET 10, PDFium e iText Community para las operaciones de estructura. Vista previa **0.5.0**, licencia **AGPL-3.0-or-later**. No modifica ni utiliza FirmaPDF. No publicada en Microsoft Store.

## Descargar para Windows 11 (x64)

[Descargar Editor PDF 0.5.0](https://github.com/eduardocvk/editaPDF/releases/download/v0.5.0/EditorPDF-0.5.0-win-x64.zip) · [Versiones y notas](https://github.com/eduardocvk/editaPDF/releases)

Extrae **todo el ZIP** en una carpeta local y abre `EditorPDF.exe`. No lo ejecutes dentro del ZIP. No necesita instalador ni descargar .NET. La carpeta incluye `Source.zip` con el código y sus dependencias. Es una vista previa sin firma Authenticode; consulta los límites y las comprobaciones pendientes antes de usarla con documentos importantes.

## Ejecutar

Abre **`artifacts/app/EditorPDF.exe`**. Conserva toda esa carpeta: incluye el runtime .NET, PDFium, Noto Sans y las licencias. No requiere instalación de .NET, conexión ni descargas al iniciar. Compilación x64 sin firma Authenticode.

**Abrir** crea pestañas de documentos independientes en la parte superior; **Unir / añadir** incorpora páginas al documento activo. Cada pestaña mantiene sus cambios, deshacer/rehacer, página, zoom y borradores. Las pestañas **Organizar** y **Editar** están encima del panel derecho y sus herramientas tienen iconos. La aplicación abre PDF, une varios, inserta páginas seleccionadas o en blanco, elimina, duplica, gira, reordena mediante Subir/Bajar o arrastrando las miniaturas, extrae y divide por rangos. En Editar permite añadir texto, seleccionar objetos originales compatibles para modificar/mover/eliminar, insertar imágenes y moverlas por arrastre o posición, y cambiar su tamaño desde propiedades. Incluye búsqueda, zoom, preparación de impresión, deshacer/rehacer y recuperación local.

- **Reordenar:** arrastra una miniatura y suéltala antes o después de otra. Una línea indica la inserción y la lista se desplaza al acercarse a sus extremos.
- **Páginas:** Extraer e Insertar PDF abren miniaturas con casillas y una vista previa. Elige Todas/Ninguna o páginas individuales. Al insertar, escoge antes/después de la página actual o al final. Dividir permite elegir visualmente las páginas y cuántas habrá por archivo.
- **Guardar copia:** exige un nombre que todavía no exista. Nunca reemplaza el original ni otro archivo. Antes de publicar la copia comprueba reapertura, texto, geometría, rotación, número de anotaciones, presencia de marcadores, adjuntos, etiquetas, destinos con nombre y representación de cada página a 600 px.
- **Teclado:** Tab/Mayús+Tab, Alt+A para abrir, Ctrl+O, Ctrl+S o Ctrl+Mayús+S, Ctrl+F, Ctrl+P, Ctrl+Z/Ctrl+Y; Ctrl+Tab/Ctrl+Mayús+Tab cambia de documento y Ctrl+W cierra la pestaña activa; RePág/AvPág cambia de página fuera de campos de texto. En un campo, Ctrl+Z conserva el deshacer propio del campo.
- La selección azul del editor es una capa WPF de selección; **no se guarda como rectángulo en el PDF**.

## Edición sobre la página

**Añadir texto** activa el cursor: pulsa en la página y escribe. El texto aparece mientras escribes; Intro o cambiar el foco lo aplica al PDF, Esc cancela el borrador. Se conserva un borrador local para recuperación. Esta versión añade una línea por objeto; la edición de texto original sigue disponible en Propiedades.

Arrastra texto o imágenes para ver el movimiento en tiempo real. Al soltar se aplica una sola operación de deshacer; Esc cancela. Las imágenes seleccionadas muestran cuatro esquinas: arrástralas para escalar proporcionalmente; Mayús permite proporción libre. A zoom alto la vista previa puede tener menor resolución mientras arrastras; al terminar vuelve la calidad completa.

Botón derecho sobre la página o la lista de objetos: añadir texto/imagen, propiedades, eliminar, deseleccionar, deshacer/rehacer y cambiar el orden (traer al frente, adelantar, atrasar o enviar al fondo). Es el orden de pintado de los objetos de la página; no crea capas opcionales OCG del estándar PDF.

## PDF firmados o protegidos

El original se abre en solo lectura y ofrece **Quitar firmas (copia)** o **Crear copia editable** en el panel derecho. Se crea otra pestaña y el original permanece intacto. La conversión elimina las firmas digitales, sus campos/sellos visibles y el cifrado o restricciones; la copia pierde el valor de autenticación del original. Requiere confirmación y el guardado siempre usa un nombre nuevo.

Si hace falta contraseña para abrir el PDF, se solicita en un campo oculto; una contraseña incorrecta no altera la sesión. No se averiguan contraseñas. La copia editable y su recuperación local quedan **sin cifrado**. No se guardan contraseñas. XFA sigue excluido y los límites de edición de formularios y etiquetas permanecen. Retirar firmas no ofrece borrado forense ni valida la confianza de los certificados.

## Explorador de Windows

En **Opciones → Activar menú del Explorador** registra para tu usuario, sin permisos de administrador:

- **Abrir con Editor PDF**: abre los PDF seleccionados en pestañas.
- **Enviar a → Unir PDF con Editor PDF**: abre una nueva pestaña con todos, en el orden entregado por Windows. Revisa el orden con las miniaturas antes de guardar.

En Windows 11 están en **Mostrar más opciones**. No cambia el lector PDF predeterminado. También registra la app en «Abrir con». Conserva la carpeta portable en su ubicación; si la mueves, vuelve a activar el menú desde la nueva carpeta. **Opciones → Quitar menú del Explorador** retira solo las entradas de Editor PDF. Alternativa de consola: `EditorPDF.exe --register-explorer` / `--unregister-explorer`. `--open` y `--merge` admiten rutas como argumentos.

La unión utiliza un acceso directo nativo en la carpeta Enviar a del usuario. Windows entrega los archivos como argumentos; no hay extensión cargada en Explorer, servidor COM propio ni archivo de intercambio con rutas. Si la app ya está abierta, la selección se entrega mediante mensajes locales de ventana. La entrada directa de unión y la integración moderna del menú principal, junto con su declaración MSIX, quedan para la fase de empaquetado.

El panel Editar permite cambiar **tamaño y color** del texto original compatible conservando su fuente y transparencia. El [desarrollo de la siguiente fase](docs/EDICION.md) recoge este incremento y los próximos criterios para selección múltiple y edición directa.

## Texto y límites de esta versión

La prueba de viabilidad se ejecutó **antes de implementar la interfaz**: abrir un PDF, modificar el objeto de texto existente con `FPDFText_SetText`, regenerar contenido, guardar y reabrir. Se comprobó la conservación de fuente y la ausencia del texto anterior y de objetos de ocultación. Después se verificó también con PDF externos de ReportLab/Helvetica y un lector independiente.

La unidad de edición es el **objeto PDF**, no el párrafo. El texto puede estar dividido en muchos objetos. Se conserva la fuente original cuando su mapa de caracteres permite el cambio. Si no, se rechaza la operación sin aplicarla; la sustitución por Noto Sans requiere marcar la opción y confirmar. No se reconstruyen párrafos, saltos de línea ni diseño como Word. No se edita texto dentro de XObjects/Form anidados, trazados, contornos o máscaras de texto. No se ofrece OCR, censura segura ni edición universal. Escrituras complejas y caracteres fuera del plano básico Unicode se rechazan en esta versión.

PDF firmados, cifrados/protegidos y formularios XFA no se modifican. No se valida criptográficamente una firma. La unión y reordenación admiten ahora anotaciones, enlaces internos, marcadores, destinos con nombre, adjuntos, formularios AcroForm y etiquetas de accesibilidad, conservando los casos verificados. Los nombres repetidos de destinos, adjuntos y campos se distinguen para no sobrescribir datos. La edición de objetos en PDF con formularios o etiquetas sigue limitada; algunas otras operaciones de páginas también tienen límites. No se garantiza conservar todas las extensiones del estándar en cualquier PDF. Antes de aceptar una unión o movimiento se compara el texto, la geometría, el número de anotaciones y la representación de cada página; un fallo revierte la operación de la sesión.

Límite inicial: 256 MiB por PDF; historial en memoria de hasta 30 operaciones/128 MiB. Hasta 32 documentos abiertos. La recuperación conserva las pestañas de la última sesión y los borradores del panel, no todo su historial de deshacer. Las operaciones pesadas y la comprobación al guardar son síncronas; pueden tardar en documentos grandes.

## Privacidad

Consulta [PRIVACIDAD.md](docs/PRIVACIDAD.md). No hay cliente HTTP, actualizador, analítica, SDK de errores ni ejecución de JavaScript PDF. El pequeño banner [FirmaPDF](https://apps.microsoft.com/detail/9P5ZK5W4J9ZJ) solo abre su ficha oficial en el navegador al pulsarlo. No añade documentos, rutas, consultas ni datos de sesión al enlace.

## Compilar y probar

Requiere Windows x64 y SDK **.NET 10.0.402**. En un equipo nuevo, el SDK puede restaurar los paquetes de runtime de Microsoft **durante el desarrollo**. La aplicación distribuida incluye las dependencias y no restaura ni descarga nada.

```powershell
.\scripts\Build.ps1
# O indicar un SDK instalado fuera del PATH:
.\scripts\Build.ps1 -DotnetPath 'C:\ruta\dotnet.exe'
# Con los paquetes de runtime ya presentes en la caché:
.\scripts\Build.ps1 -Offline
# Crear el ZIP portable y su comprobante SHA-256:
.\scripts\Package-Portable.ps1
```

Las pruebas adicionales necesitan Python con `reportlab` y `pypdf`, solo en desarrollo:

```powershell
.\scripts\Test.ps1 -DotnetPath dotnet -PythonPath python
```

Las pruebas generan documentos sintéticos, prueban el motor y los controles WPF, producen capturas y ejecutan ambos procesos bajo una guardia nativa que interrumpe antes de crear sockets o invocar las APIs de red vigiladas. Un control positivo demuestra que la guardia intercepta un intento de crear socket antes de conectar. No se envían documentos a servicios externos ni se imprimen páginas reales en las pruebas. Alcance y resultados: [VERIFICACION.md](docs/VERIFICACION.md).

## MSIX y distribución futura

Hay un manifiesto parametrizado para Windows 11 x64, sin capacidades de Internet, y `scripts/Package-Msix.ps1`. Requiere la identidad y el editor reales de Partner Center y MakeAppx del Windows SDK. El script genera un paquete sin firmar; no instala, firma ni publica. El proceso WPF es full trust: la ausencia de `internetClient` **no constituye un cortafuegos**.

Pendientes la identidad oficial, firma del paquete/ejecutable, instalación y desinstalación MSIX, validación WACK y certificación Store. Microsoft documenta el [empaquetado de WPF con MSIX](https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/dotnet/package-app). No se ha creado una cuenta, ficha ni envío a Store.

## Código y dependencias

`src/EditorPdf.Core`: ABI nativa, operaciones, transacciones, historial y recuperación. `src/EditorPdf.App`: WPF, documentos en pestañas, arrastre y paginación. `tests`: regresiones, render WPF y guardia de red exclusiva de desarrollo. `vendor/include`: cabeceras de la **misma distribución binaria**. PDFium mantiene el render y la edición de texto original. iText Community 9.8.0 se usa bajo AGPL para unir y mover páginas con sus referencias; no requiere licencia comercial en esta distribución AGPL.

La [API de edición de PDFium](https://pdfium.googlesource.com/pdfium/+/refs/heads/chromium/8138/public/fpdf_edit.h), los binarios y los avisos se fijan en `vendor/dependencies.lock.json`; Build verifica SHA-256 de DLL y fuente. `vendor/args.gn` registra V8/XFA desactivados. La [distribución de binarios](https://github.com/bblanchon/pdfium-binaries) incluye su licencia MIT y las licencias de PDFium y sus componentes; se conservan todas. .NET/WPF: MIT y avisos incluidos. Noto Sans: SIL OFL 1.1. Véase `NOTICE.txt` y `LICENSE.txt`. Se elige la licencia FreeType, incluida en los avisos, para FreeType.

iText, Commons y su adaptador Bouncy Castle tienen avisos en `licenses/itext` y `licenses/managed`, versiones fijadas en `packages.lock.json` y fuente de producción sin modificar en `vendor/sources`. Los PDF manipulados con iText conservan su Producer y la referencia de licencia en los metadatos. Bouncy Castle y dependencias de Microsoft conservan sus licencias incluidas. La consulta de vulnerabilidades de NuGet se realizó durante el desarrollo; no se ejecuta al abrir la aplicación.

El [logo](assets/logo.png), su [prompt de creación](assets/LOGO.md) y el icono ICO se incluyen en el proyecto.

El repositorio público es [eduardocvk/editaPDF](https://github.com/eduardocvk/editaPDF). Los scripts no dependen de `firmar_certificado`. La descarga portable acompaña la compilación con `Source.zip`, scripts, dependencias fijadas y avisos; el código también está disponible en este repositorio.

## Novedades 0.5.0

El recuadro de selección se conserva tras pulsar y soltar. Doble clic en texto compatible para editarlo sobre la página; el menú contextual incorpora iconos. En el panel derecho puedes elegir seis familias tipográficas incluidas y aplicar negrita o cursiva a texto nuevo o sustituido. Imprimir abre el cuadro estándar de Windows, con impresoras y rango de páginas; se confirma el envío a posibles impresoras de red. El menú Opciones se llama Configuración.
