# Siguiente fase de edición

Incremento 0.4.0 implementado: texto nuevo sobre la página, movimiento con vista previa, esquinas de redimensión de imágenes, orden de pintado y menú contextual. Las páginas se eligen visualmente al insertar/extraer/dividir. Se mantienen transacciones, deshacer/rehacer y recuperación de borradores. Pendientes edición continua de texto original en el propio documento, selección múltiple, alineaciones y agrupación visual de líneas. El render de arrastre puede usar menor resolución temporal para mantener la respuesta; al soltar se restaura la calidad normal.

Primer incremento implementado en 0.3.0: modificar tamaño (1–512 pt) y color de relleno (#RRGGBB) sobre el objeto de texto original con PDFium. Conserva fuente, texto, matriz y transparencia; rechaza modos especiales. Incluye transacción, deshacer/rehacer, reapertura y controles WPF. Los borradores de estilo se mantienen por pestaña y se recuperan localmente. El tamaño nominal en puntos se combina con la transformación que ya tenga el objeto; no reconstruye párrafos ni ajusta el texto a una caja.

Orden de los próximos incrementos:

1. Selección múltiple y movimiento preciso de objetos, con alineación y guías. Reutilizar transacciones de `Session` y coordenadas PDF/pantalla ya verificadas.
2. Edición continua de un objeto compatible en el documento, confirmación al terminar y aviso de fuente incompatible. Mantener el panel como alternativa de teclado.
3. Agrupación visual de fragmentos y líneas, primero como ayuda de selección; comprobar fuentes, matrices y espaciado antes de permitir cambios de grupos.
4. Más funciones de texto e imagen solo después de un corpus de conservación: recorte, escala proporcional y orden de objetos. Mantener bloqueados objetos anidados o estructuras no transferibles hasta disponer de pruebas específicas.

Criterio de aceptación de cada incremento: guardar/reabrir, independencia del contenido no seleccionado, representación antes/después, conservación de fuente cuando proceda, deshacer/rehacer, teclado, escalado y ausencia de tráfico. El corpus debe incluir documentos de oficina, fuentes incrustadas, rotaciones y texto fragmentado, además de los PDF sintéticos actuales.

Se mantienen fuera OCR, censura segura y reconstrucción universal de documentos como Word. La retirada de firmas se hace en una copia distinta, con advertencia y sin modificar la firma del original; no es validación de confianza del certificado ni borrado forense.

En 0.5.0, doble clic en texto compatible abre la edición en la página. La fuente original se conserva por defecto; la sustitución se elige entre seis familias incluidas. Negrita y cursiva usan archivos de fuente propios cuando existen. La impresión usa el cuadro de Windows y pide confirmación antes de enviar a una impresora que puede estar en red.
