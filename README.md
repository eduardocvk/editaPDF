# Editor PDF para Windows 11

Aplicación gratuita y de código abierto, WPF/.NET 10, PDFium e iText Community. Vista previa **0.8.0**, licencia **AGPL-3.0-or-later**. Windows 11 x64. No publicada en Microsoft Store.

[Descargar 0.8.0](https://github.com/eduardocvk/editaPDF/releases/download/v0.8.0/EditorPDF-0.8.0-win-x64.zip) · [Código y versiones](https://github.com/eduardocvk/editaPDF)

Extrae todo el ZIP y abre `EditorPDF.exe`. Incluye runtime, fuentes, motores, licencias y `Source.zip`. No necesita descargar .NET. La distribución portable no tiene firma Authenticode.

## Uso

- Abre documentos en pestañas. La rueda pasa a la página contigua cuando llegas al borde de la actual.
- Las acciones de organización están junto a las miniaturas: insertar, extraer, dividir, duplicar, girar, eliminar y reordenar. Insertar permite elegir páginas de los otros documentos abiertos o abrir otro PDF.
- Añade texto o imágenes desde el panel derecho. Aplica el texto desde el botón dentro de su cuadro. La barra contextual permite editar estilos y eliminar; las imágenes también permiten duplicar y cambiar su orden de pintado.
- Doble clic para editar texto original compatible. Arrastra desde una zona vacía para seleccionar varios fragmentos y pulsa el botón de edición junto al recuadro. Los párrafos pueden alinearse a izquierda, derecha, centro o justificarse dentro del ancho original.
- La búsqueda muestra el número de coincidencias y permite recorrerlas con flechas. Imprimir muestra una vista previa antes del diálogo de impresora de Windows.
- Configuración permite elegir español o inglés. Solo se guarda la preferencia de idioma: no se guarda ni restaura automáticamente una sesión entre ejecuciones. Guarda una copia antes de cerrar si quieres conservar tus cambios.
- El botón FirmaPDF detecta la instalación portable compatible en su ubicación de usuario. Si está disponible, solicita guardar una copia y la abre con `--open`. Si no, abre su ficha de Store. La detección y activación de la edición MSIX de FirmaPDF aún no están verificadas.

## Texto, seguridad y privacidad

Guardar exige un nombre nuevo: no reemplaza originales ni otros archivos. El resultado se reabre y se comprueba antes de publicarlo. Deshacer/rehacer conserva el historial en memoria mientras el documento está abierto.

La edición real depende de la estructura y las fuentes del PDF. Se conserva la fuente original cuando admite los caracteres. Los estilos mixtos de una selección pueden unificarse mediante una fuente incluida, con aviso. No se ofrece edición universal, OCR, censura segura ni reconstrucción de documentos como Word. Texto anidado en Form/XObjects, escrituras complejas y algunos modos especiales permanecen limitados. El reflujo rechaza desbordamientos y solapamientos con objetos no seleccionados.

Los PDF firmados o protegidos ofrecen crear una copia editable, con confirmación de la retirada de firmas, apariencias y cifrado. No se averiguan contraseñas ni se valida la confianza de certificados. XFA no se convierte.

El procesamiento es local, sin telemetría, descargas de recursos ni envío automático de errores. La impresión de red y la apertura de otra aplicación solo ocurren por acción del usuario. Consulta [Privacidad](docs/PRIVACIDAD.md) y [Verificación](docs/VERIFICACION.md). Las recuperaciones antiguas no se abren ni se borran automáticamente.

## Teclado

Ctrl+O abre; Ctrl+S guarda copia; Ctrl+P imprime; Ctrl+F busca; Ctrl+Z/Ctrl+Y deshace/rehace. Ctrl+Tab cambia de documento y Ctrl+W cierra su pestaña. Intro confirma la edición directa y Esc cancela el borrador o selección. Tab y Mayús+Tab recorren controles con nombres accesibles.

## Compilar y verificar

`scripts/Build.ps1 -DotnetPath <dotnet.exe> -Offline` genera `artifacts/app`. `scripts/Test.ps1 -DotnetPath <dotnet.exe> -PythonPath <python.exe>` ejecuta regresiones, verificación independiente y WPF bajo guardia de red. `scripts/Package-Portable.ps1` crea el ZIP y SHA-256.

El manifiesto MSIX y `scripts/Package-Msix.ps1` requieren los datos reales de Partner Center y MakeAppx del Windows SDK. Faltan instalación/desinstalación MSIX, revisión de integración con el Explorador, WACK y certificación Store. El paquete declara full trust; la ausencia de una capacidad de Internet no constituye un cortafuegos.

## Licencias

AGPL-3.0-or-later, con código de la aplicación y dependencias en `Source.zip` y el repositorio. iText Community se distribuye bajo AGPL; PDFium, .NET, bibliotecas y diez familias de fuentes conservan sus avisos. Dependencias fijadas, fuentes de producción y procedencia en `vendor`, `licenses`, `NOTICE.txt` y archivos de bloqueo. El desarrollo no modifica el proyecto independiente FirmaPDF.
