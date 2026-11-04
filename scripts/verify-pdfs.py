from pypdf import PdfReader
from pathlib import Path
import sys
p=Path(sys.argv[1])
original=PdfReader(p/'fixtures'/'externo.pdf'); edited=PdfReader(p/'externo-editado.pdf')
assert len(original.pages)==len(edited.pages)==2
assert 'Edicion real 456' in edited.pages[0].extract_text()
assert 'Texto original compatible 123' not in edited.pages[0].extract_text()
assert 'Contenido independiente' in edited.pages[0].extract_text()
assert original.pages[1].extract_text()==edited.pages[1].extract_text()
assert len(edited.pages[0]['/Contents'].get_object().get_data())>0
complex_before=PdfReader(p/'fixtures'/'estructuras.pdf'); complex_after=PdfReader(p/'estructuras-editado.pdf')
assert len(complex_after.outline)==len(complex_before.outline)==1
assert list(complex_after.attachments)==list(complex_before.attachments)
assert complex_after.attachments['nota.txt'][0]==complex_before.attachments['nota.txt'][0]
assert complex_after.metadata.title==complex_before.metadata.title
assert len(complex_after.pages[0]['/Annots'])==1
dest=complex_after.pages[0]['/Annots'][0].get_object()['/Dest'][0]
assert dest.idnum==complex_after.pages[1].indirect_reference.idnum
assert 'Edicion real 456' in complex_after.pages[0].extract_text()
print('PASS verificacion independiente pypdf: contenido, paginas, marcador, adjunto, metadatos y destino interno')
