# Editor PDF para Windows 11

Aplicación gratuita y de código abierto, WPF/.NET 10, PDFium e iText Community. Versión **1.1.0**, licencia **AGPL-3.0-or-later**. Windows 11 x64. No publicada en Microsoft Store.

[Descargar 1.0.0](https://github.com/eduardocvk/editaPDF/releases/download/v1.0.0/EditorPDF-1.0.0-win-x64.zip) · [Código y versiones](https://github.com/eduardocvk/editaPDF)

Extrae todo el ZIP y abre `EditorPDF.exe`. Incluye runtime, fuentes, motores, licencias y `Source.zip`. No necesita descargar .NET. La distribución portable no tiene firma Authenticode.

## Uso

- Abre documentos en pestañas. La rueda pasa a la página contigua cuando llegas al borde de la actual. Ctrl + rueda amplía o reduce alrededor del puntero.
- Ctrl y Mayús permiten seleccionar varias páginas. El botón derecho ofrece añadirlas a otro documento abierto; también puedes arrastrar el grupo a la pestaña de destino. Se copian al final sin retirar las páginas del origen.
- **Unir**, a la derecha de Imprimir, abre un compositor visual: elige un documento de origen, arrastra sus páginas a la lista del PDF nuevo, reordénalas y añade otros PDF con «Abrir otro PDF». Al crear el resultado se abre una pestaña independiente.
- Las acciones de organización están junto a las miniaturas: insertar, extraer, dividir, duplicar, girar, eliminar y reordenar. Insertar permite elegir páginas de los otros documentos abiertos o abrir otro PDF.
- Añade texto o imágenes desde el panel derecho. Selecciona varios objetos de una página con Ctrl + clic. Arrastra un objeto seleccionado para mover el grupo, duplica con Ctrl+D o el menú contextual y elimina con Supr. La duplicación conserva la fuente original cuando el texto es compatible. Aplica el texto desde el botón dentro de su cuadro. La barra contextual permite editar estilos y eliminar; las imágenes también permiten duplicar y cambiar su orden de pintado.
- Doble clic para editar texto original compatible. Arrastra desde una zona vacía para seleccionar varios fragmentos y pulsa el botón de edición junto al recuadro. Los párrafos pueden alinearse a izquierda, derecha, centro o justificarse dentro del ancho original.
- La búsqueda muestra el número de coincidencias y permite recorrerlas con flechas. Imprimir abre una única ventana con impresora, orientación, papel, copias, color y páginas. Puedes colocar 2, 4, 6, 9 o 16 páginas por hoja, con orden por filas o columnas y bordes opcionales. La vista previa se actualiza con esas opciones y solo muestra las páginas seleccionadas, en su orden. El trabajo utiliza el mismo paginador y los márgenes que informa la impresora, sin un segundo diálogo de opciones.
- Configuración permite elegir español o inglés. Solo se guarda la preferencia de idioma: no se guarda ni restaura automáticamente una sesión entre ejecuciones. Guarda una copia antes de cerrar si quieres conservar tus cambios.
- El botón FirmaPDF detecta la instalación portable compatible en su ubicación de usuario. Si está disponible, solicita guardar una copia con el nombre original más `_para_firmar.pdf` y la abre con `--open`. Si no, abre su ficha de Store. La detección y activación de la edición MSIX de FirmaPDF aún no están verificadas.

## Texto, seguridad y privacidad

Guardar exige un nombre nuevo: no reemplaza originales ni otros archivos. El resultado se reabre y se comprueba antes de publicarlo. Deshacer/rehacer conserva el historial en memoria mientras el documento está abierto.

La edición real depende de la estructura y las fuentes del PDF. Se conserva la fuente original cuando admite los caracteres. Los estilos mixtos de una selección pueden unificarse mediante una fuente incluida, con aviso. No se ofrece edición universal, OCR, censura segura ni reconstrucción de documentos como Word. Texto anidado en Form/XObjects, escrituras complejas y algunos modos especiales permanecen limitados. El reflujo rechaza desbordamientos y solapamientos con objetos no seleccionados.

Los PDF firmados o protegidos ofrecen crear una copia editable, con confirmación de la retirada de firmas, apariencias y cifrado. No se averiguan contraseñas ni se valida la confianza de certificados. XFA no se convierte.

El procesamiento es local, sin telemetría, descargas de recursos ni envío automático de errores. La impresión de red y la apertura de otra aplicación solo ocurren por acción del usuario. Consulta [Privacidad](docs/PRIVACIDAD.md) y [Verificación](docs/VERIFICACION.md). Las recuperaciones antiguas no se abren ni se borran automáticamente.

## Teclado

Ctrl+O abre; Ctrl+S guarda copia; Ctrl+P imprime; Ctrl+F busca; Ctrl+Z/Ctrl+Y deshace/rehace. Ctrl+Tab cambia de documento y Ctrl+W cierra su pestaña. Intro confirma la edición directa y Esc cancela el borrador o selección. Tab y Mayús+Tab recorren controles con nombres accesibles.

El botón **Reducir tamaño del PDF**, al pie del panel derecho, abre una barra de calidad con vista previa, comparación con el original, zoom y estimación del peso. Guarda una copia `_reducido.pdf`. Recomprime imágenes RGB/grises compatibles y puede reducir su resolución; conserva texto, OCR existente y estructuras. Las imágenes con transparencias, máscaras, espacios de color especiales o más de 50 megapíxeles se dejan intactas. La reducción es con pérdida visual y no todos los PDF permiten ahorrar espacio. Los originales firmados o protegidos requieren primero una copia editable.

## Compilar y verificar

`scripts/Build.ps1 -DotnetPath <dotnet.exe> -Offline` genera `artifacts/app`. `scripts/Test.ps1 -DotnetPath <dotnet.exe> -PythonPath <python.exe>` ejecuta regresiones, verificación independiente y WPF bajo guardia de red. `scripts/Package-Portable.ps1` crea el ZIP y SHA-256.

El manifiesto MSIX y `scripts/Package-Msix.ps1` requieren los datos reales de Partner Center y MakeAppx del Windows SDK. Faltan instalación/desinstalación MSIX, revisión de integración con el Explorador, WACK y certificación Store. El paquete declara full trust; la ausencia de una capacidad de Internet no constituye un cortafuegos.

## Licencias

AGPL-3.0-or-later, con código de la aplicación y dependencias en `Source.zip` y el repositorio. iText Community se distribuye bajo AGPL; PDFium, .NET, bibliotecas y diez familias de fuentes conservan sus avisos. Dependencias fijadas, fuentes de producción y procedencia en `vendor`, `licenses`, `NOTICE.txt` y archivos de bloqueo. El desarrollo no modifica el proyecto independiente FirmaPDF.
