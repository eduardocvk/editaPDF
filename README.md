# Editor PDF para Windows 11

Aplicación WPF independiente, C#/.NET 10 y PDFium. Vista previa **0.1.1**, licencia **AGPL-3.0-or-later**. No modifica ni utiliza FirmaPDF. No publicada en Microsoft Store.

## Descargar para Windows 11 (x64)

[Descargar Editor PDF 0.1.1](https://github.com/eduardocvk/editaPDF/releases/download/v0.1.1/EditorPDF-0.1.1-win-x64.zip) · [Versiones y notas](https://github.com/eduardocvk/editaPDF/releases)

Extrae **todo el ZIP** en una carpeta local y abre `EditorPDF.exe`. No lo ejecutes dentro del ZIP. No necesita instalador ni descargar .NET. La carpeta incluye `Source.zip` con el código y sus dependencias. Es una vista previa sin firma Authenticode; consulta los límites y las comprobaciones pendientes antes de usarla con documentos importantes.

## Ejecutar

Abre **`artifacts/app/EditorPDF.exe`**. Conserva toda esa carpeta: incluye el runtime .NET, PDFium, Noto Sans y las licencias. No requiere instalación de .NET, conexión ni descargas al iniciar. Compilación x64 sin firma Authenticode.

Las pestañas **Organizar** y **Editar** están encima del panel derecho. La aplicación abre PDF, une varios, inserta páginas seleccionadas o en blanco, elimina, duplica, gira, reordena mediante Subir/Bajar, extrae y divide por rangos. En Editar permite añadir texto, seleccionar objetos originales compatibles para modificar/mover/eliminar, insertar imágenes y moverlas por arrastre o posición, y cambiar su tamaño desde propiedades. Incluye búsqueda, zoom, preparación de impresión, deshacer/rehacer y recuperación local.

- **Rangos:** `1-3,5`. Para dividir en varios archivos: `1-3;4-6;7`. Insertar PDF pregunta las páginas del archivo de origen.
- **Guardar copia:** exige un nombre que todavía no exista. Nunca reemplaza el original ni otro archivo. Antes de publicar la copia comprueba reapertura, texto, geometría, rotación, número de anotaciones, presencia de marcadores, adjuntos, etiquetas, destinos con nombre y representación de cada página a 600 px.
- **Teclado:** Tab/Mayús+Tab, Alt+A para abrir, Ctrl+O, Ctrl+S o Ctrl+Mayús+S, Ctrl+F, Ctrl+P, Ctrl+Z/Ctrl+Y; RePág/AvPág cambia de página fuera de campos de texto. En un campo, Ctrl+Z conserva el deshacer propio del campo.
- La selección azul del editor es una capa WPF de selección; **no se guarda como rectángulo en el PDF**.

## Texto y límites de esta versión

La prueba de viabilidad se ejecutó **antes de implementar la interfaz**: abrir un PDF, modificar el objeto de texto existente con `FPDFText_SetText`, regenerar contenido, guardar y reabrir. Se comprobó la conservación de fuente y la ausencia del texto anterior y de objetos de ocultación. Después se verificó también con PDF externos de ReportLab/Helvetica y un lector independiente.

La unidad de edición es el **objeto PDF**, no el párrafo. El texto puede estar dividido en muchos objetos. Se conserva la fuente original cuando su mapa de caracteres permite el cambio. Si no, se rechaza la operación sin aplicarla; la sustitución por Noto Sans requiere marcar la opción y confirmar. No se reconstruyen párrafos, saltos de línea ni diseño como Word. No se edita texto dentro de XObjects/Form anidados, trazados, contornos o máscaras de texto. No se ofrece OCR, censura segura ni edición universal. Escrituras complejas y caracteres fuera del plano básico Unicode se rechazan en esta versión.

PDF firmados, cifrados/protegidos, formularios y documentos etiquetados se abren en **solo lectura**; no se reescriben como una copia que aparente conservar firmas válidas. No se valida criptográficamente una firma. Se pueden añadir PDF al final de un documento con marcadores, anotaciones, destinos con nombre y adjuntos, conservando los del documento principal. También se importan notas, marcas, sellos visuales independientes y enlaces web compatibles, sin abrir esos enlaces. Mover/eliminar páginas o insertarlas entre las existentes sigue limitado en documentos con referencias. Si el archivo que quieres añadir contiene marcadores, adjuntos, destinos con nombre, enlaces internos o comentarios enlazados que aún no se pueden transferir, se informa de ello antes de aplicar cambios; puedes abrirlo como documento principal y añadir los demás al final. Sí se ha probado editar texto en un documento con marcador, adjunto y enlace interno, conservando esas estructuras. No se garantiza conservar todas las extensiones del estándar en cualquier PDF.

Límite inicial: 256 MiB por PDF; historial en memoria de hasta 30 operaciones/128 MiB. La recuperación conserva la última sesión, no todo su historial de deshacer. Las operaciones pesadas y la comprobación al guardar son síncronas; pueden tardar en documentos grandes.

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

`src/EditorPdf.Core`: ABI nativa, operaciones, transacciones, historial y recuperación. `src/EditorPdf.App`: WPF y paginación. `tests`: regresiones, render WPF y guardia de red exclusiva de desarrollo. `vendor/include`: cabeceras de la **misma distribución binaria**. No hay wrapper PDF administrado ni dependencia comercial.

La [API de edición de PDFium](https://pdfium.googlesource.com/pdfium/+/refs/heads/chromium/8138/public/fpdf_edit.h), los binarios y los avisos se fijan en `vendor/dependencies.lock.json`; Build verifica SHA-256 de DLL y fuente. `vendor/args.gn` registra V8/XFA desactivados. La [distribución de binarios](https://github.com/bblanchon/pdfium-binaries) incluye su licencia MIT y las licencias de PDFium y sus componentes; se conservan todas. .NET/WPF: MIT y avisos incluidos. Noto Sans: SIL OFL 1.1. Véase `NOTICE.txt` y `LICENSE.txt`. Se elige la licencia FreeType, incluida en los avisos, para FreeType.

El repositorio público es [eduardocvk/editaPDF](https://github.com/eduardocvk/editaPDF). Los scripts no dependen de `firmar_certificado`. La descarga portable acompaña la compilación con `Source.zip`, scripts, dependencias fijadas y avisos; el código también está disponible en este repositorio.
